# 0125. 버그 수정: 아군 강제 공격 중 근처 적에게 대상을 뺏기는 문제

## 날짜
2026-07-16

## 요청
현재 아군 강제 공격일때 근처에 적이있으면 멀리있는 아군을 강제공격하지 않고 가까운 적을 먼저 공격해버리는데 이를 확인하고 해결해줘

## 원인
`UnitController.AttackFriendlyTarget()`은 `orderedTarget`(적 지정 추격 대상)을 `null`로 비우고 `friendlyTarget`(아군 강제공격 대상)만 채운 뒤 상태를 `UnitState.Attack`으로 바꾼다.

그런데 `AttackRange.GetPreferredTarget()`은 `orderedTarget`이 `null`이면 무조건 `GetClosestEnemy()`로 폴백해서 사거리 안의 "가장 가까운 적"을 반환한다. 아군 강제공격 중에는 `orderedTarget`이 항상 `null`이므로 이 폴백이 매번 발동한다.

`AttackRange.Update()`는 유닛 상태가 `Attack`이면 실행되므로, 이렇게 찾아낸 가까운 적을 그대로 `unitController.Attack()`으로 공격시켜버린다 — 이게 매 프레임 `FriendlyAttackTick()`이 하려던 "먼 아군 대상 추격/공격"을 덮어써서, 결과적으로 멀리 있는 지정 아군 대신 가까운 적을 공격하게 된다.

(0010에서 "지정 적을 무시하고 다른 적을 먼저 공격하는 문제"를 고쳤을 때는 `orderedTarget`(적 지정) 케이스만 다뤘고, `friendlyTarget`(아군 지정) 케이스는 `AttackRange`가 애초에 그 개념을 모르기 때문에 같은 문제가 남아있었다.)

## 답변 / 변경사항
1. **`UnitController.cs`**: `hasFriendlyOrder`를 외부에서 읽을 수 있도록 공개 프로퍼티 추가.
   ```csharp
   public bool HasFriendlyOrder => hasFriendlyOrder;
   ```
2. **`AttackRange.cs`**: `GetPreferredTarget()` 맨 앞에서 아군 강제공격 중인지 먼저 확인해서, 그렇다면 다른 적으로 폴백하지 않고 `null`을 반환(=어떤 적도 자동 교전하지 않음).
   ```csharp
   private GameObject GetPreferredTarget()
   {
       if (unitController.HasFriendlyOrder)
           return null; // 아군 강제 공격 중엔 다른 적을 무시한다 (FriendlyAttackTick이 전담 처리)

       EnemyController ordered = unitController.GetOrderedTarget();

       if (ordered != null)
           return enemiesInRange.Contains(ordered.gameObject) ? ordered.gameObject : null;

       return GetClosestEnemy();
   }
   ```

이렇게 하면 아군 강제공격 명령이 유지되는 동안은 `AttackRange`가 완전히 개입하지 않고, `FriendlyAttackTick()`이 매 프레임 지정된 아군 대상만 사거리 판정/추격/공격하게 되어 근처 적에게 대상을 뺏기지 않는다.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/AttackRange.cs`
