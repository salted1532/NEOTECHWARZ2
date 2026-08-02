# 0388 - 감지 트리거 경계 미세 진동으로 인한 대상 놓침(멈칫거림) 수정

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> AttackRange 트리거 콜리전 범위 안에 들어가서 targetsInRange안에 들어갔을때 부터 멈쳐서 천천히
> 다가와 범위 밖을 나갔다가 들어왔다 하면서 멈칫거리는거 같고 어떤 루프에 빠진것 처럼 보여

## 조사 결과

- `EnemyAttackRange`(및 플레이어 쪽 `AttackRange`)는 매 프레임 "지금 이 순간 감지 트리거 콜라이더
  (`targetsInRange`) 안에 있는 대상 중 가장 가까운 것"을 새로 뽑아서 쓴다
  (`EnemyAttackRange.GetClosestTarget()` / `AttackRange.GetClosestEnemy()`). 지정 명령(플레이어의
  `orderedTarget`/`friendlyTarget`)과 달리 이 순수 자동교전 경로는 특정 대상을 "물고 있는" 상태가
  전혀 없고, 매 프레임 트리거 콜라이더 멤버십에서 처음부터 다시 판단한다.
- 도달 불가능한 대상(언덕 위 등)에게 다가가 [[0375]]/[[0386]] fallback으로 가장 가까운 지점에
  정착하면, 그 지점은 감지 콜라이더(`UnitRange` + 5, doc/0239) 경계에 우연히 가깝게 걸릴 수 있다.
  유닛이 그 자리에 멈춰 서는 과정의 아주 작은 위치 흔들림(스티어링)만으로도 대상이 콜라이더 경계를
  들락날락하면서 `OnTriggerEnter`/`OnTriggerExit`가 반복 발생한다:
  1. 나가는 순간 → `targetsInRange`에서 제거 → `GetClosestTarget()`이 `null` → 이번 프레임엔 아무
     것도 안 함(제자리에 멈춤, `Attack()`도 `ChaseTarget()`도 호출 안 됨).
  2. 다시 들어오는 순간 → 다시 추가 → 처음부터 다시 `ChaseTarget()` 호출.
  3. 이게 반복되면서 "멈췄다 살짝 움직이려다 다시 멈추는" 것처럼 보임 - 보고하신 "멈칫거리며
     루프에 빠진 것 같다"는 증상과 정확히 일치.
- [[0386]]에서 만든 목적지 캐시는 "같은 대상으로 다시 `ChaseTarget()`이 불렸을 때 재요청을 막는 것"만
  다루기 때문에, 애초에 대상이 사라져서 이번 프레임에 아무것도 호출되지 않는 이 문제는 막지 못한다.
- 고칠 부분: 한 번 교전(추격/공격)을 시작한 대상은, 트리거 경계를 살짝 벗어나는 정도로는 놓치지 않고
  계속 우선시하도록 "완충 구간(hysteresis)"을 둔다. 완전히 멀어졌을 때만(감지 반경보다 더 넉넉한
  거리) 진짜로 포기하고 다음 후보를 다시 찾는다. 트리거 자체(콜라이더 크기)는 안 건드리고, 대상
  선택 로직에서만 "이미 물고 있던 대상은 조금 더 봐준다"는 여유를 추가하는 방식.
- 플레이어 쪽 `AttackRange.GetPreferredTarget()`/`GetTrackingTarget()`의 `GetClosestEnemy()` 폴백도
  (명시 지정 명령이 없는 순수 자동교전일 때) 완전히 같은 구조라 동일하게 영향받는다 - 두 파일 다 같이
  고친다(0375/0386/0387과 동일한 이유로 항상 짝을 맞춰온 부분).

## 코드 변경 (제안)

### `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`

`private const float DetectionRangeMargin = 5f;` 아래에 필드/헬퍼 추가, `Update()`에서
`GetClosestTarget()` 대신 새 헬퍼 사용:

```csharp
    // 한 번 교전(추격/공격)을 시작한 대상은, 이 여유 거리(감지 반경 + 추가 완충) 밖으로 완전히
    // 벗어나기 전까지는 계속 우선시한다. 도달 불가능한 대상 근처에 정착할 때 유닛 자신의 미세한 위치
    // 흔들림만으로 감지 트리거 콜라이더 경계를 들락날락하면서, 매 프레임 "지금 이 순간의 최근접
    // 대상"만 다시 뽑아 추격/공격이 계속 처음부터 다시 시작되는 것처럼 멈칫거리는 문제가 있었다
    // (doc/0388).
    private const float EngagedTargetLoseSightMargin = 3f;
    private GameObject engagedTarget;

    private GameObject GetEngagedOrClosestTarget()
    {
        if (engagedTarget != null && CanEngage(engagedTarget))
        {
            float loseSightRange = UnitRange + DetectionRangeMargin + EngagedTargetLoseSightMargin;
            float sqrDist = (transform.position - engagedTarget.transform.position).sqrMagnitude;
            if (sqrDist <= loseSightRange * loseSightRange)
                return engagedTarget;
        }

        return engagedTarget = GetClosestTarget();
    }
```

`Update()`(99~120번째 줄) 기존 코드:
```csharp
    private void Update()
    {
        targetsInRange.RemoveAll(target => target == null);

        GameObject target = GetClosestTarget();
        if (target == null)
            return;
```

변경 코드:
```csharp
    private void Update()
    {
        targetsInRange.RemoveAll(target => target == null);

        GameObject target = GetEngagedOrClosestTarget();
        if (target == null)
            return;
```

`GetTrackingTarget()`(125번째 줄)도 포탑이 같은 대상을 계속 조준하도록 동일하게 교체:
```csharp
    public GameObject GetTrackingTarget() => GetEngagedOrClosestTarget();
```

### `Assets/Scripts/Unit/AttackRange.cs`

`private const float DetectionRangeMargin = 5f;` 아래에 동일한 필드/헬퍼 추가:

```csharp
    // EnemyAttackRange와 동일한 완충 구간(doc/0388) - 순수 자동교전(명시 지정 명령 없음) 중에만
    // 적용된다. orderedTarget/friendlyTarget이 있는 명시 명령 경로는 이미 자체 로직(doc/0384)이 있음.
    private const float EngagedTargetLoseSightMargin = 3f;
    private GameObject engagedEnemy;

    private GameObject GetEngagedOrClosestEnemy()
    {
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

`GetPreferredTarget()`(116~127번째 줄)과 `GetTrackingTarget()`(101~111번째 줄)에서 명시 지정 대상이
없을 때의 `GetClosestEnemy()` 호출을 `GetEngagedOrClosestEnemy()`로 교체 (두 곳 다).

## 열린 질문

- 완충 거리(`EngagedTargetLoseSightMargin` = 3m)는 임의값 - 유닛의 스티어링 흔들림 폭보다 넉넉하면
  충분하므로 일단 작게 잡음. 그래도 멈칫거리면 늘리면 됨.
- 대상이 정말로 멀리 달아나면(감지 반경 + 3m보다 더 멀어지면) 여전히 정상적으로 놓치고 새 대상을
  찾는다 - "한 번 물면 무한정 끝까지 쫓아간다"로 바뀌는 게 아님(그건 [[0384]]의 명시 지정 공격
  전용 동작).

## 영향받는 파일 (예정)

- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`
- `Assets/Scripts/Unit/AttackRange.cs`
