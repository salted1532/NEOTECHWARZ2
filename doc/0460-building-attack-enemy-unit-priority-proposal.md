# 0460. 건물 공격 중 사거리 내 적 유닛 우선순위 제안

**날짜:** 2026-08-08

## 요청 내용
> 현재 유닛의 공격 메커니즘에서 건물을 공격하는 와중이면 적유닛이 나타나서 공격해서 건물만
> 공격하거든 공격의 우선순위를 줬으면 좋겠어 건물을 때리다가도 AttackRange 콜리전 트리거 안에
> 유닛이 들어오면 가까운 유닛부터 공격하도록

### 후속 확인
> 강제 공격에 경우는 다 무시하고 지정한 대상을 공격하는게 맞는데 땅 공격이나 자동공격일 경우는
> 건물을 때리다가도 적 유닛이 나타나면 적유닛을 우선적으로 공격하도록 해줘

즉 **강제공격(우클릭 지정 공격, A모드 아군/건물 강제공격)은 현재 동작(지정 대상만 끝까지 공격,
`doc/0125`) 그대로 유지**하고, **땅 공격/자동공격(패시브 대기 중 자동교전 + 공격-이동)** 경로에서만
"건물을 자동으로 물고 있다가도 적 유닛이 나타나면 적 유닛 우선"으로 바꾼다. 아래는 이 범위로
수정한 조사/제안.

## 조사

`AttackRange`의 `enemiesInRange` 목록은 태그가 `"Enemy"`인 오브젝트를 전부 담는다(유닛/건물 구분
없음) - 실제로 적 건물(`EnemyBuildingController`)도 태그가 `Enemy`라서 이 목록에 들어온다. 그래서
"자동공격"(Idle 상태 패시브 대기)이나 "공격-이동"(`AttackMoveTo`) 중에도 `AttackRange`가 사거리 내
가장 가까운 대상으로 건물을 골라 `UnitController.Attack()`을 호출하는 게 이미 가능하다(강제공격
경로인 `FriendlyAttackTick`을 전혀 거치지 않음) - `CanEngage()`가 `EnemyUnitController`가 없는
대상(건물)은 항상 "지상"으로 취급해서 지상 공격 가능 유닛이면 통과시키기 때문.

문제는 대상을 고르는 `AttackRange.GetEngagedOrClosestEnemy()`의 "이미 물고 있던 대상 우선"
로직(`doc/0388`, 트리거 경계 깜빡임 방지용):

```csharp
private GameObject GetEngagedOrClosestEnemy()
{
    if (engagedEnemy != null && CanEngage(engagedEnemy))
    {
        float loseSightRange = UnitRange + DetectionRangeMargin + EngagedTargetLoseSightMargin;
        float sqrDist = (transform.position - engagedEnemy.transform.position).sqrMagnitude;
        if (sqrDist <= loseSightRange * loseSightRange)
            return engagedEnemy; // 건물이어도 무조건 여기서 계속 반환됨
    }

    return engagedEnemy = GetClosestEnemy();
}
```

건물은 정지해 있고 유닛이 바로 옆에 붙어서 때리는 중이라 `loseSightRange` 안을 벗어날 일이 없다 -
즉 한 번 건물을 `engagedEnemy`로 물면, 이후 적 유닛이 사거리 안에 들어와도 이 sticky 로직이
계속 건물을 우선 반환해서 절대 안 바뀐다. 이게 사용자가 본 증상의 실제 원인(자동공격/공격-이동
둘 다 이 함수 하나를 공유).

## 제안하는 변경 (`AttackRange.cs`만 수정)

`GetEngagedOrClosestEnemy()`에서, 사거리 트리거 안에 실제 적 **유닛**(`EnemyUnitController` 보유,
건물 제외)이 하나라도 있으면 - 현재 무엇을 물고 있었든(건물이든 다른 유닛이든) - 그 적 유닛을
최우선으로 선택하도록 앞단에 추가한다. 적 유닛이 하나도 없을 때만 기존 sticky 로직(건물 포함)이
그대로 동작한다.

```csharp
private GameObject GetEngagedOrClosestEnemy()
{
    // 사거리 내 실제 적 유닛(건물 제외)이 있으면 항상 최우선 - 건물을 자동공격/공격-이동으로 물고
    // 있던 중이었어도 즉시 교체한다(doc/0460). 강제공격(FriendlyAttackTick) 경로는 이 함수를 아예
    // 거치지 않으므로 영향 없음(doc/0125 동작 그대로 유지).
    GameObject priorityEnemyUnit = GetClosestEnemy(requireUnit: true);
    if (priorityEnemyUnit != null)
        return engagedEnemy = priorityEnemyUnit;

    if (engagedEnemy != null && CanEngage(engagedEnemy))
    {
        float loseSightRange = UnitRange + DetectionRangeMargin + EngagedTargetLoseSightMargin;
        float sqrDist = (transform.position - engagedEnemy.transform.position).sqrMagnitude;
        if (sqrDist <= loseSightRange * loseSightRange)
            return engagedEnemy;
    }

    return engagedEnemy = GetClosestEnemy();
}
```

기존 `GetClosestEnemy()`에 `bool requireUnit = false` 매개변수를 추가해서 `true`일 때는
`enemy.TryGetComponent<EnemyUnitController>(out _)`가 없는 대상(건물 등)을 후보에서 제외하도록
필터 한 줄만 더한다 - 나머지 최근접 탐색 로직은 그대로 재사용.

**부가 효과**: 적 유닛이 여러 마리 동시에 사거리 안에 있으면 매 프레임 그중 가장 가까운 유닛으로
재계산된다(유닛끼리는 sticky 로직을 안 거침). `doc/0388`이 막았던 깜빡임은 "사거리 경계에서 유닛
하나가 들락날락하는" 케이스인데, 이 경우엔 적 유닛이 사거리를 완전히 벗어나는 순간(=
`enemiesInRange`에서 빠짐) `priorityEnemyUnit`이 `null`이 되고 기존 sticky 폴백(`engagedEnemy` +
`loseSightRange`)이 그대로 이어받으므로 회귀 없음. 여러 적 유닛이 동시에 존재할 때 매 프레임
"가장 가까운 쪽"으로 조준이 옮겨가는 것은 사용자가 요청한 "가까운 유닛부터 공격"과 부합.

## 영향 범위
- **변경**: 자동교전(Idle 패시브 대기) / 공격-이동(`AttackMoveTo`) 중 사거리 내 건물을 물고 있다가
  적 유닛이 나타나면 그 유닛을 우선 공격.
- **변경 없음**: 강제공격 전체(`AttackFriendlyTarget` 기반 - 아군 강제공격, 적 건물 강제공격 클릭,
  아군 OC 강제공격) - `FriendlyAttackTick`은 `AttackRange`를 거치지 않으므로 `doc/0125` 동작 그대로.
- **변경 없음**: 적 유닛 지정 추격(`orderedTarget`, 우클릭/A모드로 적 유닛 지정) - 이미 지정된 대상만
  본다.

## 변경 예정 파일
- `Assets/Scripts/Unit/AttackRange.cs`

## 구현 결과

제안한 그대로 적용:
- `GetEngagedOrClosestEnemy()` 앞단에 `GetClosestEnemy(requireUnit: true)` 우선 확인 추가.
- `GetClosestEnemy()`에 `bool requireUnit = false` 매개변수 추가, `true`일 때 `EnemyUnitController`가
  없는 대상(건물 등)을 후보에서 제외.

## 검증
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일, 이번 변경과 무관).

## 변경된 파일
- `Assets/Scripts/Unit/AttackRange.cs`
