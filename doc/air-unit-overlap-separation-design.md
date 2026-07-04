# 공중 유닛(AirUnit) 겹침 해소 설계 — 이동 중엔 통과, 정지/공격 중엔 분리

작성일: 2026-07-04
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 요청 사항

공중 유닛 프리팹들은 서로의 몸체 콜라이더가 `Is Trigger = true`라서 물리적으로
부딪히지 않고 겹쳐서 지나다닐 수 있다.

- 이동 중에 겹치는 것 → **유지하고 싶음** (편대 이동 시 서로 통과해야 하므로)
- 정지했거나 공격 중일 때 겹쳐있는 것 → **풀리길(밀려나길) 원함**

## 2. 현재 구조 확인

`UnitController.cs`는 공중 유닛을 물리엔진이 아니라 **직접 좌표 보간**으로 움직인다.

```csharp
// Update()
if (isAirUnit && isMovingAirUnit)
{
    transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);
    ...
    if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
    {
        isMovingAirUnit = false;   // 도착하면 이동 플래그 OFF
        UnitcurrentState = UnitState.Idle;
    }
}
```

`StopUnit()`, `Attack()`, `HoldUnit()` 등 "멈춰야 하는" 진입점들은 전부
`isMovingAirUnit = false`로 떨어뜨린다. 즉 **"이동 중이냐(`isMovingAirUnit == true`)
vs 정지/공격 중이냐(`isMovingAirUnit == false`, `UnitState.Idle`/`Attack`)"는
이미 명확히 구분되는 상태값**이 존재한다. 이 값을 그대로 겹침 해소 스위치로 쓰면 된다.

문제는 콜라이더/리지드바디 쪽이다. 실제 유닛 프리팹(`Firehawk.prefab`,
`Guardian Drone.prefab` 등)을 보면:

```
CapsuleCollider (몸체)
  m_IsTrigger: 1
Rigidbody
  m_UseGravity: 0
  m_IsKinematic: 1   # ← Kinematic
```

**`Is Trigger`를 상태에 따라 껐다 켰다 하는 것만으로는 해결되지 않는다.**
PhysX는 두 콜라이더가 모두 "Kinematic Rigidbody + non-trigger"인 경우
서로 겹쳐도 밀어내는 힘(depenetration)을 계산하지 않는다. Kinematic
Rigidbody는 오직 스크립트가 옮기는 대로만 움직이고, 충돌 반응으로는
절대 움직이지 않기 때문이다 (Kinematic vs Kinematic, Kinematic vs
Kinematic 조합 모두 해당). 즉 두 공중 유닛이 전부 이 구조라면 `isTrigger`만
꺼서는 겹친 채로 얼어붙을 뿐 절대 떨어지지 않는다.

→ 결론: **물리엔진에 맡기지 말고, 지금처럼 스크립트가 직접 위치를 보정하는
"수동 분리(separation) 로직"을 추가하는 것이 이 프로젝트 구조와 맞다.**
(어차피 지상 유닛도 NavMeshAgent 상태머신으로 직접 제어하는 스타일이라,
공중 유닛만 물리엔진에 위임하면 오히려 스타일이 어긋난다.)

## 3. 해결 방향 (권장): 상태 기반 수동 분리

`RTSUnitController.UnitList`(모든 유닛 리스트, `Start()`에서 이미
`rtsController.UnitList.Add(this)`로 등록됨)를 그대로 활용해서, **이동 중이
아닌 공중 유닛끼리만** 서로 밀어내는 보정을 매 프레임 아주 조금씩 적용한다.

```csharp
[SerializeField] private float airSeparationRadius = 1.2f; // 두 콜라이더 반경 합 정도
[SerializeField] private float airSeparationSpeed = 4f;    // 밀려나는 속도(초당)

private void SeparateFromOverlappingAirUnits()
{
    if (!isAirUnit || isMovingAirUnit)
        return; // 이동 중엔 서로 통과 허용 (기존 요구사항 유지)

    Vector3 push = Vector3.zero;

    foreach (UnitController other in rtsController.UnitList)
    {
        if (other == this || other == null || !other.isAirUnit)
            continue;
        if (other.isMovingAirUnit)
            continue; // 상대가 지나가는 중이면 무시 (통과시켜줌)

        Vector3 diff = transform.position - other.transform.position;
        diff.y = 0f; // 수평으로만 분리 (고도 유지)
        float dist = diff.magnitude;

        if (dist > 0.001f && dist < airSeparationRadius)
        {
            float overlap = airSeparationRadius - dist;
            push += diff.normalized * overlap;
        }
    }

    if (push.sqrMagnitude > 0.0001f)
    {
        Vector3 step = push.normalized * Mathf.Min(push.magnitude, airSeparationSpeed * Time.deltaTime);
        transform.position += step;
    }
}
```

`Update()` 끝부분에 한 줄만 추가한다.

```csharp
void Update()
{
    if (isAirUnit && isMovingAirUnit) { ... }
    if (!isAirUnit) { ... }

    GatherTick();
    PatrolTick();

    if (isAirUnit)
        SeparateFromOverlappingAirUnits(); // ← 추가
}
```

### 왜 이 방식이 맞는가

- **상태 판정이 이미 있다**: `isMovingAirUnit`이 곧 "지금 서로 통과해도 되는가"의
  기준이라서 별도 상태를 새로 만들 필요가 없다. `StopUnit()`, `Attack()`,
  `HoldUnit()`, 도착 처리(Update 내부) 전부 이미 이 값을 false로 내려주고 있으므로
  "정지"와 "공격" 두 케이스를 자동으로 커버한다.
- **콜라이더/리지드바디를 건드리지 않는다**: `AttackRange`용 트리거 콜라이더는
  그대로 항상 트리거로 남기고, 몸체 콜라이더도 물리 설정 변경 없이 그대로 둔다.
  (물리 이벤트가 필요 없는 순수 위치 보정이라 Kinematic 여부와 무관하게 동작한다.)
- **y(고도)는 절대 건드리지 않는다** (`diff.y = 0`) → 고도 유지 로직과 충돌 안 함.
- **한쪽만 계산해도 결과가 자연스럽다**: 이동 중이 아닌 유닛끼리는 서로가 서로를
  밀어내므로(A가 B를 미는 동안 B도 A를 미는 중), 매 프레임 아주 작은 스텝만
  적용해도 몇 프레임 안에 자연스럽게 벌어진다.
- 유닛 수가 아주 많아지면 `RTSUnitController.UnitList` 전체 순회가 O(n²)이 되는데,
  RTS 특성상 이동 중이 아닌 유닛만 순회하고 공중 유닛 수 자체가 지상 유닛보다
  적은 편이라 실전 규모에서는 문제 없을 가능성이 높다. 나중에 유닛 수가 매우
  많아지면 공간 분할(그리드) 최적화를 고려하면 된다.

## 4. 대안으로 검토했지만 권장하지 않는 방법

### 4-1. `isTrigger`를 상태에 따라 토글 + Rigidbody를 Non-Kinematic으로 변경

정지/공격 시 `bodyCollider.isTrigger = false`로 바꾸고 Rigidbody를 물리 시뮬레이션에
맡기면 PhysX가 자동으로 밀어내 줄 것처럼 보이지만:

- 두 유닛이 모두 Kinematic이면 애초에 밀어내는 힘 자체가 계산되지 않는다
  (2절 참고). 이걸 쓰려면 **Rigidbody를 Non-Kinematic으로 바꿔야 하는데**, 그러면
  지금처럼 `transform.position`을 직접 대입하는 이동 방식과 충돌한다
  (물리 스텝과 트랜스폼 강제 대입이 서로 덮어써서 튐/떨림이 생기기 쉬움).
- 힘 기반 분리라서 결과가 결정적이지 않고(밀려나는 속도/방향이 매 실행마다
  살짝 달라짐), 분리 후에도 관성으로 미끄러지는 걸 막으려면 Drag/Damping
  튜닝이 추가로 필요하다.
- RTS류 게임에서 유닛 위치는 보통 예측 가능해야 하는데, 물리 엔진에 맡기면
  디버깅이 어려워진다.

→ 즉시 동작은 하지만 지금 프로젝트의 "상태머신이 직접 위치를 제어" 스타일과
안 맞고, 튜닝 비용이 더 크다. 3절 방식보다 이점이 없어서 권장하지 않는다.

### 4-2. `NavMeshObstacle`로 자리 차지

지상 유닛(`NavMeshAgent`)끼리는 정지한 유닛에 `NavMeshObstacle`을 붙여서
"자리를 비켜가게" 만드는 방법이 일반적이다. 하지만 공중 유닛은애초에
NavMesh를 전혀 사용하지 않고 `Vector3.MoveTowards`로 직접 좌표를 움직이므로
`NavMeshObstacle`은 공중 유닛의 이동 경로에 아무 영향을 주지 못한다. 공중
유닛에는 적용 불가.

## 5. 적용 대상

`isAirUnit = true`로 설정된 프리팹 전부에 자동 적용됨 (별도 프리팹 수정 불필요,
`UnitController.cs` 코드 변경만으로 충분):

- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`
- `Assets/prefabs/Test/TestAirUnit.prefab`
- (그 외 `isAirUnit` 체크된 프리팹)

## 6. 튜닝 포인트

- `airSeparationRadius`: 실제 몸체 `CapsuleCollider.radius`의 합보다 살짝 크게
  잡아야 "겹친 것처럼 보이는" 상태를 확실히 풀 수 있다 (예: 반경 0.5 유닛이면
  1.2~1.5 정도부터 시작해서 눈으로 보면서 조정).
- `airSeparationSpeed`: 너무 크면 정지 직후 유닛이 튕기듯 밀려나는 느낌이
  들 수 있으니, 부드럽게 벌어지는 느낌이 나는 선에서 조정.
