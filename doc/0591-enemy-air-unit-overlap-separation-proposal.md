# 0591. 적(스포어 브루드) 공중 유닛끼리 서로 겹치는 문제 - 원인 및 수정 제안

- 날짜: 2026-08-16

## 요청 내용

- "적 스포어 공중유닛이 서로 겹치는걸 막아줘"

## 조사 내용

공중 유닛(`isAirUnit`)은 `NavMeshAgent`를 쓰지 않고 `EnemyUnitController.Update()`에서
`Vector3.MoveTowards(pos, horizontalTarget, ...)`로 `targetPosition` 한 점을 향해 직접
움직인다(지상 유닛의 NavMeshAgent가 제공하는 반경 기반 충돌 회피가 없음). 여러 공중 유닛이 같은
목적지로 이동(예: 별동대가 같은 `attackMoveDestination`으로 이동, 혹은 같은 대상을 추격)하면 전부
같은 좌표로 수렴해 그대로 겹쳐버린다 - 지상 유닛과 달리 "밀어내는" 로직 자체가 없었음.

플레이어 유닛(`UnitController.cs`)에는 이미 정확히 이 문제를 해결한 코드가 있다
(`SeparateFromOverlappingAirUnits()`, `airUnitRadius`/`airSeparationSpeed` 필드) - 이동 중이 아닌
공중 유닛끼리만 서로의 반경 합보다 가까우면 그만큼 수평으로 밀어낸다. 다만 이 구현은
`rtsController.UnitList`(플레이어 유닛 전역 캐시)를 순회하는데, `EnemyUnitController`엔 그런 전역
캐시가 없다(`EnemyAIDirector`의 `FindNearbyEnemyUnits()`가 대신 `Physics.OverlapSphere`로 주변
유닛을 즉석에서 찾는 것과 동일한 이유) - 그래서 여기도 `Physics.OverlapSphere` 방식으로 주변 공중
유닛을 찾는 것으로 구현한다.

## 수정 제안

`UnitController.SeparateFromOverlappingAirUnits()`와 동일한 로직을, 유닛 목록 출처만
`Physics.OverlapSphereNonAlloc`(매 프레임 호출이라 GC 할당을 피하기 위해 NonAlloc 사용)으로 바꿔
`EnemyUnitController.cs`에 추가한다.

### EnemyUnitController.cs

`isAirUnit` 필드 근처에 필드 추가:
```csharp
    [SerializeField] private float airCruiseAltitude = 5f;
    [SerializeField] private LayerMask airGroundLayer; // 공중 유닛이 발밑 지면 높이를 재는 레이어 (UnitController와 동일한 용도)

    [Header("공중 유닛 분리 (겹침 방지 - UnitController.SeparateFromOverlappingAirUnits와 동일한 패턴, doc/0591)")]
    [SerializeField] private float airUnitRadius = 0.6f;    // 기본값 0.6 = UnitController와 동일
    [SerializeField] private float airSeparationSpeed = 4f; // 밀려나는 속도(초당)
```

`Update()` 끝(`AttackMoveTick(); UpdateFogVisibility();` 다음)에 호출 추가:
```csharp
        AttackMoveTick();
        UpdateFogVisibility();

        if (isAirUnit)
            SeparateFromOverlappingAirUnits();
```

새 메서드 추가 (UnitController 판박이, 대상 탐색만 OverlapSphere로 교체):
```csharp
    // 이동 중이 아닌 공중 유닛끼리만 서로 겹친 만큼 수평으로 밀어낸다
    // (UnitController.SeparateFromOverlappingAirUnits와 동일한 패턴, doc/0591). EnemyUnitController엔
    // UnitList 같은 전역 캐시가 없어 Physics.OverlapSphere로 주변을 직접 찾는다
    // (EnemyAIDirector.FindNearbyEnemyUnits와 동일한 기법) - 매 프레임 호출이라 NonAlloc으로 GC 할당을 피한다.
    private const float AirSeparationQueryRadius = 5f; // 기본 airUnitRadius(0.6) 두 개 합보다 훨씬 넉넉한 탐색 반경
    private static readonly Collider[] airSeparationHits = new Collider[16];

    private void SeparateFromOverlappingAirUnits()
    {
        if (isMovingAirUnit)
            return;

        Vector3 push = Vector3.zero;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, AirSeparationQueryRadius, airSeparationHits);

        for (int i = 0; i < hitCount; i++)
        {
            if (!airSeparationHits[i].TryGetComponent(out EnemyUnitController other) ||
                other == this || !other.isAirUnit || other.isMovingAirUnit)
                continue;

            Vector3 diff = transform.position - other.transform.position;
            diff.y = 0f; // 고도는 건드리지 않고 수평으로만 분리
            float dist = diff.magnitude;

            float requiredDist = airUnitRadius + other.airUnitRadius;
            if (dist < requiredDist)
            {
                float overlap = requiredDist - dist;
                Vector3 pushDir = dist > 0.001f ? diff.normalized : StackedNudgeDirection();
                push += pushDir * overlap;
            }
        }

        if (push.sqrMagnitude > 0.0001f)
        {
            Vector3 step = push.normalized * Mathf.Min(push.magnitude, airSeparationSpeed * Time.deltaTime);
            transform.position += step;
        }
    }

    // UnitController.StackedNudgeDirection과 동일 - 완전히 같은 좌표(dist≈0)로 겹쳤을 때 diff가 0벡터라
    // 미는 방향을 못 정하는 경우, 유닛 고유의(항상 같은) 방향으로라도 밀어서 겹침을 깬다.
    private Vector3 StackedNudgeDirection()
    {
        return Quaternion.Euler(0f, GetHashCode() % 360, 0f) * Vector3.forward;
    }
```

`Collider`는 `UnityEngine` 네임스페이스라 이미 `using UnityEngine;`이 있는 이 파일에 추가 using 불필요.

## 예상 영향

- Skitterwing 등 Spore Brood 공중 유닛이 이동 중이 아닐 때(도착/교전 등으로 정지했을 때) 서로 겹쳐
  있으면 자동으로 밀려나 벌어진다 - 정확한 동작은 플레이어 유닛과 동일.
- 사용자 확인 후 아군 OC 공중 유닛(Raven/Ironhawk, `AllyController.cs`)에도 동일하게 적용함 -
  `EnemyUnitController.cs`와 완전히 동일한 코드(타입만 `AllyController`로 교체).

## 변경 예정 파일

- Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs
- Assets/Scripts/FogOfWar/Ally/AllyController.cs

## 결과
- 사용자 확인 후 위 두 파일에 동일하게 적용함.
