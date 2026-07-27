# 0253 - 적 유닛이 공격 애니메이션(Fire)에 안 들어감 (Railgunner 한정 아님, 전체 적 유닛 버그) (적용 완료)

## 요청

> Railgunner가 공격모션으로 안들어가는거 같은데 확인좀

## 조사 내용 - 원인: EnemyUnitController.IsAttack()이 항상 false

공격 애니메이션은 `UnitAnimatorDriver`가 매 프레임 갱신한다:

```csharp
// UnitAnimatorDriver.cs:22-34
private void Update()
{
    if (animator == null || (unitController == null && enemyUnitController == null))
        return;

    bool isMoving = unitController != null ? unitController.IsCurrentlyMoving() : enemyUnitController.IsCurrentlyMoving();
    bool isAttacking = unitController != null ? unitController.IsAttack() : enemyUnitController.IsAttack();

    animator.SetBool(IsMovingParam, isMoving);
    animator.SetBool(FireParam, isAttacking);   // ← 이게 계속 false라 Fire 상태로 안 들어감
}
```

`EnemyUnitController.IsAttack()`:
```csharp
// EnemyUnitController.cs:421
public bool IsAttack() => currentState == EnemyState.Attack;
```

그런데 `EnemyUnitController.cs` 전체를 찾아봐도 `currentState`에 `EnemyState.Attack`을 대입하는 코드가
**단 한 줄도 없다.** `MoveTo`/`AttackMoveTo`/`ChaseTarget`/`Update()`의 도착 처리 전부 `Idle` 또는
`Move`로만 설정하고, 실제 공격을 실행하는 `Attack()` 메서드도 `currentState`를 전혀 건드리지 않는다.
즉 `currentState`가 `Idle`/`Move` 사이에서만 바뀌고 `Attack`이 될 일이 원천적으로 없어서, `IsAttack()`은
적 유닛이 실제로 싸우고 있어도 언제나 `false`를 반환한다 - Railgunner뿐 아니라 **모든 적 유닛
(EnemyUnitController 기반)** 이 겪는 문제.

**참고 - 왜 이렇게 됐는지 (doc/0236)**: 원래는 `Attack()`이 `currentState = EnemyState.Attack;`을
설정했었는데, 이 상태를 다시 `Idle`로 되돌리는 코드가 "가만히 있다가 자동 교전" 시나리오에는 없어서
한 번 공격하면 `currentState`가 `Attack`에 영구히 멈춰버리는 버그가 있었다(상대가 사거리 밖으로
물러나도 추적을 재개 못 함). doc/0236에서 그 줄을 통째로 제거해서 그 버그는 고쳤는데, 그 부작용으로
`IsAttack()`이 영영 `true`가 될 수 없게 되어 애니메이션이 깨진 것으로 보인다(이 두 버그가 별도로
보고돼서 그 당시엔 애니메이션 쪽 영향을 놓쳤을 가능성이 높음).

`EnemyAttackRange.Update()`의 게이팅 조건도 이 사실을 은연중에 뒷받침한다:
```csharp
// EnemyAttackRange.cs:94
if (enemyUnit.IsAttack() || enemyUnit.IsIdle())
```
`IsAttack()`이 항상 `false`이니 이 조건은 사실상 `IsIdle()`에만 의존해서 동작 중이었다(실제 교전/데미지
적용 자체는 `currentState`와 무관하게 잘 작동해왔음 - 애니메이션만 깨진 상태).

## 제안하는 수정 - currentState를 건드리지 않고, "지금 사거리 안에 대상이 있는가"로 직접 판정

`currentState`에 다시 `Attack`을 대입하는 방식은 doc/0236의 버그를 그대로 재현하므로 쓰지 않는다.
대신 `EnemyAttackRange`가 이미 매 프레임 계산하는 "가장 가까운 유효 대상과의 거리"를 그대로 물어보는
방식으로 `IsAttack()`을 다시 정의한다 - 상태머신을 전혀 건드리지 않아 doc/0236에서 고친 추적 로직에
영향이 없다.

**`Assets/Scripts/Enemy/EnemyAttackRange.cs`**
```csharp
// 추가하는 코드 (HasTargetInRange 프로퍼티 바로 아래)
    // 지금 이 순간 실제 공격 사거리(UnitRange) 안에 유효한 대상이 있는지. HasTargetInRange는 더 넓은
    // 감지 콜라이더(UnitRange+margin) 기준이라 "지금 공격 중인가"를 나타내기엔 부적합해서 따로 둔다
    // (doc/0253, UnitAnimatorDriver의 Fire 파라미터 판정용).
    public bool HasTargetInAttackRange
    {
        get
        {
            GameObject target = GetClosestTarget();
            if (target == null)
                return false;

            return Vector3.Distance(transform.position, target.transform.position) <= UnitRange;
        }
    }
```

**`Assets/Scripts/Enemy/EnemyUnitController.cs`**
```csharp
// 기존 코드
    public bool IsAttack() => currentState == EnemyState.Attack;
```
```csharp
// 변경 코드
    // currentState는 Idle/Move만 쓰고 Attack은 쓰지 않는다(doc/0236 - Attack에 멈추면 교전 종료 후
    // 추적 재개가 안 됨). 대신 "지금 실제 사거리 안에 대상이 있어서 공격 중인가"를 EnemyAttackRange에
    // 직접 물어본다 - UnitAnimatorDriver가 Fire 애니메이션을 켤 때 이 값을 쓴다(doc/0253).
    public bool IsAttack() => attackRange != null && attackRange.HasTargetInAttackRange;
```

### 기존 호출부에 영향 없음 확인

`IsAttack()` 호출부는 딱 두 곳뿐이었다:
1. `UnitAnimatorDriver.Update()` - 이번에 고치려는 대상, 이제 정상적으로 true/false를 받음.
2. `EnemyAttackRange.Update()` 자기 자신의 게이팅 조건(`IsAttack() || IsIdle()`) - 새 `IsAttack()`은
   그 직후에 나오는 `distance <= UnitRange` 체크와 동일한 대상/거리 기준이라 사실상 같은 값이 되므로,
   기존 동작(사거리 안이면 공격, 밖이고 Idle이면 추적)이 그대로 유지된다. `currentState`를 전혀 안 건드리므로
   doc/0236에서 고친 "추적 재개" 로직에도 영향 없음.

## 변경된 파일

- `Assets/Scripts/Enemy/EnemyAttackRange.cs`
- `Assets/Scripts/Enemy/EnemyUnitController.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함(설계와 구현 간 차이 없음).
