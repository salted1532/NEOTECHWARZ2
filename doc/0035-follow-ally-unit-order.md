# 0035. 아군 유닛 우클릭 → 계속 따라다니기(Follow) 명령

**날짜:** 2026-07-09

## 요청 내용
> 아군 유닛을 우클릭 할 경우 해당 유닛을 계속 따라다니도록 코드 수정해줘. 그게 unitcontroller에서 move 상태도 동작하는게 아니라 idle상태로 동작해서 적유닛을 만나면 공격하도록 그리고 우클릭 했을때 자원이나, 적유닛을 우클릭 했을때와 같이 마커가 3번 깜박거리는것도 추가해줘

정리하면 3가지 요구사항:
1. 아군 유닛을 우클릭하면, 선택된 유닛들이 그 아군 유닛을 계속 따라다닌다(대상이 이동해도 계속 쫓아감).
2. 이 상태는 `UnitState.Move`가 아니라 `UnitState.Idle`로 동작해야 한다 — `AttackRange.Update()`가 `IsAttack() || IsIdle()`일 때만 자동 교전하므로, 따라가는 도중 적을 만나면 자동으로 공격하도록.
3. 우클릭 시 대상 유닛의 마커가 (자원/적 우클릭과 동일하게) 0.3초 간격으로 3번 깜빡인다.

## 조사 결과 (현재 코드 상태)
- `UnitController.cs`에 이미 비슷한 패턴이 존재: `AttackFriendlyTarget()`(아군 강제공격, A모드)이 `friendlyTarget`/`hasFriendlyOrder`를 두고 `FriendlyAttackTick()`에서 매 프레임 거리 체크 후 사거리 밖이면 무제한 추격, 사거리 안이면 공격 — 단 이건 `UnitState.Attack`으로 설정되고 "공격" 의도라 지금 요청("그냥 따라다니기, 적 만나면 알아서 교전")과는 다름.
- `AttackMoveTo()`가 "Idle 상태를 유지한 채 이동" 패턴의 선례: `UnitcurrentState = UnitState.Idle`로 둬야 `AttackRange`가 사거리 내 적을 자동으로 교전한다는 주석이 이미 달려 있음 (`UnitController.cs:365`).
- `FlashMarker()`는 이미 `UnitController`에 구현되어 있고(마커 컴포넌트 재사용, 0.3초 간격 3회 깜빡임) `AttackFriendlyTarget` 지정 시(A모드 좌클릭) 이미 사용 중. 새로 만들 필요 없이 그대로 재사용 가능.
- `UserControl.cs`의 `HandleRightClick()`에는 `clickedUnit`(아군 유닛 우클릭) 분기가 **비어있음** (`UserControl.cs:343-346`) — 지금은 아군 유닛을 우클릭해도 아무 일도 일어나지 않고, 그 아래 `clickedGround`가 함께 true가 되어 그냥 그 지점으로 이동 명령이 나가게 됨.

## 설계안

### 1. `UnitController.cs` — Follow 상태 추가

**필드 추가** (`markerFlashCount` 필드 아래):
```csharp
// 기존 코드 (markerFlashCount 선언부 다음)
    private Coroutine markerFlashRoutine; // 공격 대상 지정 피드백 깜빡임 (Enemy/ResourceNode와 동일한 패턴)
    [SerializeField] private float markerFlashInterval = 0.3f;
    [SerializeField] private int markerFlashCount = 3;
```
```csharp
// 변경 코드
    private Coroutine markerFlashRoutine; // 공격 대상 지정 피드백 깜빡임 (Enemy/ResourceNode와 동일한 패턴)
    [SerializeField] private float markerFlashInterval = 0.3f;
    [SerializeField] private int markerFlashCount = 3;

    // ===== 아군 유닛 우클릭 = 계속 따라다니기 (공격 명령 아님, Idle 상태 유지) =====
    // Attack 상태가 아니라 Idle로 유지해야 AttackRange가 사거리 내 적을 자동으로 교전한다 (AttackMoveTo와 동일한 이유).
    private UnitController followTarget;
    private bool hasFollowOrder;
```

**진입점 추가** (`AttackFriendlyTarget()` 아래, `FriendlyAttackTick()` 앞):
```csharp
    public void FollowUnit(UnitController target)
    {
        CancelGatheringForNewCommand();
        CancelAttackOrder();

        followTarget = target;
        hasFollowOrder = true;

        arrived = false;
        UnitcurrentState = UnitState.Idle; // Idle 유지 - AttackRange가 사거리 내 적을 자동으로 교전하게 함

        MoveAgentTo(target.transform.position);
    }

    // 따라다니기 명령을 매 프레임 갱신한다: 대상이 죽으면 그 자리에 멈추고, 교전 중(AttackRange가 정지시킨 상태)이면
    // 이동 명령을 덮어쓰지 않으며, 그 외에는 대상의 최신 위치로 계속 이동한다 (거리 제한 없음 - FriendlyAttackTick과 동일 패턴).
    private void FollowTick()
    {
        if (!hasFollowOrder)
            return;

        if (followTarget == null)
        {
            hasFollowOrder = false;

            arrived = true;
            if (!isAirUnit)
                navMeshAgent.ResetPath();
            else
                isMovingAirUnit = false;
            return;
        }

        if (attackRange != null && attackRange.HasEnemyInRange)
            return; // 교전 중이면 그대로 둔다 (AttackRange가 정지시킨 상태 유지)

        MoveAgentTo(followTarget.transform.position);
    }
```

**`CancelAttackOrder()`에 follow 취소 추가** (다른 이동/정지/순찰 명령이 들어오면 따라다니기도 취소되도록):
```csharp
// 기존 코드
    private void CancelAttackOrder()
    {
        orderedTarget = null;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        hasFriendlyOrder = false;
        attackMoveDestination = null;
    }
```
```csharp
// 변경 코드
    private void CancelAttackOrder()
    {
        orderedTarget = null;
        hasEngagedOrderedTarget = false;
        friendlyTarget = null;
        hasFriendlyOrder = false;
        attackMoveDestination = null;
        followTarget = null;
        hasFollowOrder = false;
    }
```

`CancelAttackOrder()`를 거치지 않고 직접 필드를 세팅하는 `AttackUnitTarget()` / `AttackMoveTo()` / `AttackFriendlyTarget()` 세 곳에도 각각 `followTarget = null; hasFollowOrder = false;`를 추가해서, 공격 명령이 새로 들어오면 따라다니기가 확실히 해제되게 함.

**`Update()`에 `FollowTick()` 호출 추가**:
```csharp
// 기존 코드
        GatherTick();
        PatrolTick();
        AttackOrderTick();
        FriendlyAttackTick();
```
```csharp
// 변경 코드
        GatherTick();
        PatrolTick();
        AttackOrderTick();
        FriendlyAttackTick();
        FollowTick();
```

**지상/공중 "도착" 판정에 `followTarget == null` 조건 추가** (orderedTarget/friendlyTarget과 동일하게 취급 — 따라다니는 도중에 임의로 Idle 재전환/정지되지 않도록):
```csharp
// 기존 코드 (공중 유닛 도착 처리)
                if (orderedTarget == null && friendlyTarget == null)
                {
                    UnitcurrentState = UnitState.Idle;
                    attackMoveDestination = null;
                }
```
```csharp
// 변경 코드
                if (orderedTarget == null && friendlyTarget == null && followTarget == null)
                {
                    UnitcurrentState = UnitState.Idle;
                    attackMoveDestination = null;
                }
```
```csharp
// 기존 코드 (지상 유닛 도착 처리)
            if (!arrived &&
                orderedTarget == null &&
                friendlyTarget == null &&
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arriveDistance)
```
```csharp
// 변경 코드
            if (!arrived &&
                orderedTarget == null &&
                friendlyTarget == null &&
                followTarget == null &&
                !navMeshAgent.pathPending &&
                navMeshAgent.remainingDistance <= arriveDistance)
```

### 2. `RTSUnitController.cs` — 선택 유닛 일괄 명령

`AttackFriendlySelectedUnits()` 아래에 동일한 패턴으로 추가:
```csharp
    public void FollowSelectedUnits(UnitController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            if (selectedUnitList[i] == target)
                continue; // 자기 자신은 자기 자신을 따라다닐 수 없으므로 건너뜀 (AttackFriendlySelectedUnits와 동일 관례)

            selectedUnitList[i].FollowUnit(target);
        }
    }
```

### 3. `UserControl.cs` — 우클릭 입력 연결

`HandleRightClick()`의 비어있는 `clickedUnit` 블록을 채우고, 적 우클릭 분기보다 먼저(또는 나란히) 처리해서 아래 `clickedGround` 이동 명령으로 새지 않도록 `return`:
```csharp
// 기존 코드
        if (clickedUnit)
        {

        }

        // 1. 적 우클릭 = 공격 명령 (추격 후 공격)
```
```csharp
// 변경 코드
        // 0. 아군 유닛 우클릭 = 계속 따라다니기 (Idle 상태 유지 - 적 만나면 AttackRange가 자동 교전)
        // 아군도 지면 위에 서 있어 clickedGround가 함께 true가 되므로, 땅 클릭보다 먼저 처리하고 여기서 return 한다.
        if (clickedUnit && rtsUnitController.IsUnitSelect())
        {
            UnitController unit = unitHit.transform.GetComponent<UnitController>();

            if (unit != null)
            {
                rtsUnitController.FollowSelectedUnits(unit);
                unit.FlashMarker(); // 어느 아군을 따라갈지 마커 깜빡임으로 표시

                movePointer.transform.position = unit.transform.position;
                movePointer.SetActive(true);

                return;
            }
        }

        // 1. 적 우클릭 = 공격 명령 (추격 후 공격)
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **대상이 죽으면**: 그 자리에 멈추고 Idle 유지 (별도로 다시 "따라가기 재개" 등은 하지 않음).
- **거리 제한 없음**: `FriendlyAttackTick`과 동일하게, 얼마나 멀어지든 시야 이탈 개념 없이 계속 따라감 (요청이 "계속 따라다니도록"이므로).
- **자기 자신 우클릭**: 선택된 유닛들 중 우클릭 대상 본인만 자기 자신 팔로우를 건너뛰고, 나머지 선택 유닛들은 정상적으로 그 유닛을 따라감 (`AttackFriendlySelectedUnits`와 동일 관례).
- **건물은 대상 아님**: 요청이 "아군 유닛"이라 건물 우클릭(랠리 포인트 등 기존 동작)은 그대로 둠.
- **마커 깜빡임**: 새로 구현하지 않고 기존 `FlashMarker()`(0.3초 간격 3회)를 그대로 재사용.

## 변경 예정 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
