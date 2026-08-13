# 0568 - 공격-이동 중 공격 불가능한(도메인 불일치) 대상 때문에 영구 정지

## 날짜
2026-08-13

## 질문/요청 내용
"점령지 별동대가 플레이어유닛과 조우하여 전투가 벌어지고나서 전투가 끝나면 점령지로 이동하나? 현재
발견된거에선 공중유닛이라서 지상만 공격할수 있는 립팽이 그냥 멈춰버렸거든 만약 그게 문제라면 공중유닛은
무시하고 그냥 목적지로 설정된 점령지로가서 점령하는 로직으로 가야할거 같아"

→ (질문 1) 정상 케이스에서는 그렇다. 별동대는 `EnemyAIDirector.RaidRoutine()`이
`unit.AttackMoveTo(target.transform.position)`를 호출해 목적지를 지정하는데, 이 "공격-이동"은 유닛
자신의 `EnemyUnitController.AttackMoveTick()`이 매 프레임 감시하다가 교전이 끝나면(사거리 감지
`EnemyAttackRange.HasTargetInRange`가 false가 되면) 자동으로 원래 목적지(점령지)로 이동을 재개하도록
이미 설계돼 있다.

→ (질문 2, 실제 원인) 사용자가 짚은 대로 공중 유닛이 문제 맞음. `HasTargetInRange`가 감지 범위 안의
아무 태그 대상이나 있으면 true를 반환할 뿐, 이 유닛이 그 대상을 실제로 공격할 수 있는 도메인(지상/공중)인지
확인하지 않는다. 그래서 canAttackAir=false인 지상 전용 유닛(Ripfang 등)의 감지 범위 안에 공중 유닛이
계속 머물러 있으면, 실제로는 전투가 전혀 벌어지지 않는데도 `HasTargetInRange`가 계속 true로 잡혀
"아직 교전 중"으로 오판 → `AttackMoveTick()`이 목적지 재개를 영원히 보류 → 유닛이 (다른 이유로 한 번
정지하고 나면, 예: doc/0559에 이미 기록된 여러 유닛 몰림으로 인한 일시 정지) 다시는 움직이지 않고 멈춘
채로 남는다.

## 원인 확인
`Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`:

```csharp
// 지정 공격 명령이 없는 이 컨트롤러에서는 AttackMoveTick이 "교전 중이라 정지된 것인지" 판단할 때 조회한다.
public bool HasTargetInRange
{
    get
    {
        foreach (GameObject target in targetsInRange)
        {
            // 도달 불가로 이미 포기한 대상은 "교전 중"으로 치지 않는다 - ...
            if (target != null && target != unreachableTarget)
                return true;
        }

        return false;
    }
}
```

`targetsInRange`는 `OnTriggerEnter`에서 태그만 확인하고 넣은 것(도메인 무관, `IsValidTarget`은
태그만 검사). 반면 같은 파일의 `GetClosestTarget()`은 실제로 공격/추격할 대상을 고를 때
`CanEngage(target)`(도메인 필터: `enemyUnit.CanAttackDomain(targetIsAir)`)로 이미 걸러내고 있다 -
`HasTargetInRange`만 이 필터를 안 거친다는 게 불일치.

`EnemyUnitController.AttackMoveTick()`이 이 값을 이렇게 쓴다:
```csharp
private void AttackMoveTick()
{
    if (attackMoveDestination == null)
        return;

    if (attackRange != null && attackRange.HasTargetInRange)
        return; // 교전 중이면 그대로 둔다

    bool groundStopped = !isAirUnit && navMeshAgent.isStopped;
    bool airStopped = isAirUnit && !isMovingAirUnit;

    if (groundStopped || airStopped)
    {
        arrived = false;
        currentState = EnemyState.Idle;
        MoveAgentTo(attackMoveDestination.Value);
    }
}
```
`HasTargetInRange`가 도메인 불일치 대상 때문에 계속 true면, 유닛이 어떤 이유로든(회피/혼잡 등으로)
한 번 멈추고 나면 다시는 `MoveAgentTo`가 재호출되지 않아 영구 정지한다.

`AllyAttackRange`(아군 OC)는 `EnemyAttackRange`를 그대로 상속하고 `HasTargetInRange`를 오버라이드하지
않으므로 동일한 문제를 아군 OC 유닛도 그대로 갖고 있다.

## 설계안
`HasTargetInRange`도 `GetClosestTarget()`과 동일하게 `CanEngage()` 필터를 거치도록 고친다 - "실제로
공격할 수 있는 대상이 사거리 안에 있는가"로 의미를 통일한다.

```csharp
public bool HasTargetInRange
{
    get
    {
        foreach (GameObject target in targetsInRange)
        {
            if (target != null && target != unreachableTarget && CanEngage(target))
                return true;
        }

        return false;
    }
}
```

이렇게 하면 지상 전용 유닛은 공중 유닛을 "교전 중" 판단에서 아예 무시하고, 계속 원래 목적지(점령지 등)로
이동을 재개할 수 있다. `CanEngage()`는 이미 `private`로 같은 클래스 안에 있어 그대로 재사용 가능.

## 영향받는 파일
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` - `HasTargetInRange` getter 수정(적/아군 OC
  공통 - `AllyAttackRange`가 상속).

`EnemyUnitController.AttackMoveTick()`/`HandleAttacked()`, `AllyController`의 동일 코드는 수정 없음 -
`HasTargetInRange`의 의미만 정확해지면 그쪽은 이미 올바르게 동작한다.

## 확인 결과
사용자에게 물어본 결과 "바로 수정" 선택 - 위 설계안대로 그대로 적용.

## 변경 상세
`Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`의 `HasTargetInRange` getter 조건에
`&& CanEngage(target)`를 추가(위 설계안 코드 그대로). `AllyAttackRange`가 이 클래스를 상속하므로 별도
수정 없이 아군 OC에도 함께 적용됨.

## 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 40`(기존 베이스라인과 동일 - 새 경고 없음).
