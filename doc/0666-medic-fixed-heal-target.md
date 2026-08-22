# 0666 - 메딕 우클릭 = 고정 치유 대상 지정 (구현 완료)

## 요청 흐름
1. "메딕이 우클릭으로 힐하는 유닛 지정하는거에서... 힐 범위 안에있으면 정지해서 힐하고 그러고 나서
   따라가기로 넘어가고, 범위 밖이면 그 유닛한테 가다가 범위 안에 들어가면 정지하고" (doc/0665)
2. "우클릭 했을때 해당 유닛이 체력이 다 차있으면 따라가기로 하고 아니면 힐하는거로 하면될거 같아
   일꾼 건물 수리같이"
3. "한 유닛을 고정 치유 대상으로 삼는 로직으로 하자" ← 이번 구현 확정 지점

doc/0665는 "분기 코드 없이 기존 HealRange 자동교전 + FollowTick 가드만으로 충분하다"는 제안이었으나,
사용자가 일꾼 수리처럼 **클릭한 유닛을 고정 대상으로 삼는 방식**을 명시적으로 선택해 이번에 그
방향으로 구현했다.

## 설계: 기존 "지정 공격 대상" 패턴을 그대로 미러링
전투 유닛은 이미 이 정확한 문제를 갖고 있었다 - `orderedTarget`(우클릭으로 지정한 추격 대상)이 있으면
`AttackRange`가 다른 적은 무시하고 그 대상만 최우선으로 고른다(`AttackRange.GetPreferredTarget()`).
메딕도 동일한 골격을 그대로 재사용:

| 전투 유닛 | 메딕 (신규) |
|---|---|
| `orderedTarget` (EnemyUnitController) | `orderedHealTarget` (UnitController) |
| `AttackUnitTarget()` | `Heal()` |
| `AttackOrderTick()` | `HealOrderTick()` |
| `AttackRange.GetPreferredTarget()` | `HealRange.GetPreferredTarget()` (신규) |
| `GetOrderedTarget()` | `GetOrderedHealTarget()` (신규) |

우클릭 디스패치 자체(`FollowUnit`)는 일꾼의 `MoveToBuilding`(체력 확인 후 `Repair`/`FollowBuilding`
분기)과 동일한 형태로 분기: 대상이 다쳤으면 `Heal()`, 아니면 기존 그대로 `FollowUnit` 계속 진행.

메딕만의 차이점: 일꾼 수리는 다 고치면 그냥 끝(대기)이지만, 메딕은 다 나으면 같은 대상을 계속
"따라가기"로 이어간다(`HealOrderTick()`이 `FollowUnit(healedTarget)`을 재호출) - 요청 1번의
"그러고 나서 따라가기로 넘어가고"를 그대로 만족.

## 변경 내역

### `Assets/Scripts/Unit/UnitController.cs`

**필드 추가** (healTarget 관련 필드 블록 아래):
```diff
+    // ===== 지정 치유 (우클릭으로 다친 아군을 직접 지정, doc/0666) =====
+    private UnitController orderedHealTarget;
+    private bool hasHealOrder;
```

**`CancelAttackOrder()`** - 다른 명령이 들어오면 지정 치유도 함께 취소:
```diff
         followBuildingTarget = null;
         hasFollowBuildingOrder = false;
+        orderedHealTarget = null;
+        hasHealOrder = false;
```

**`FollowUnit()`** - 대상이 다쳤으면 `Heal()`로 위임 (일꾼 `MoveToBuilding`과 동일한 분기 패턴):
```diff
     public void FollowUnit(UnitController target)
     {
         if (isConstructing || isRescueUnit) return;

+        if (healRangeDetector != null && IsDamagedUnit(target))
+        {
+            Heal(target);
+            return;
+        }
+
         CancelGatheringForNewCommand();
         CancelAttackOrder();
         ...
```

**신규 `Heal()` / `IsDamagedUnit()` / `GetOrderedHealTarget()` / `HealOrderTick()`** -
`AttackUnitTarget`/`GetOrderedTarget`/`AttackOrderTick`의 거울상:
```csharp
public void Heal(UnitController target)
{
    if (isConstructing || isRescueUnit || target == null) return;

    CancelGatheringForNewCommand();
    CancelAttackOrder();

    orderedHealTarget = target;
    hasHealOrder = true;

    arrived = false;
    UnitcurrentState = UnitState.Idle;

    MoveAgentTo(target.transform.position, target.isAirUnit);
}

private void HealOrderTick()
{
    if (!hasHealOrder) return;

    HealthManager targetHealth = orderedHealTarget != null ? orderedHealTarget.GetHealthManager() : null;
    if (targetHealth == null || targetHealth.IsDead())
    {
        // 대상 파괴 - 정리하고 그 자리에 정지
        hasHealOrder = false; orderedHealTarget = null;
        arrived = true;
        if (!isAirUnit) navMeshAgent.ResetPath(); else isMovingAirUnit = false;
        return;
    }

    if (targetHealth.GetHealth() >= targetHealth.GetMaxHealth())
    {
        // 다 나음 - 같은 대상을 계속 따라가기로 전환
        UnitController healedTarget = orderedHealTarget;
        hasHealOrder = false; orderedHealTarget = null;
        FollowUnit(healedTarget);
        return;
    }

    if (isHealing) return; // 치유 중이면 그대로 둔다

    float healUnitRange = healRangeDetector != null ? healRangeDetector.UnitRange : 0f;
    float sqrDistance = (transform.position - orderedHealTarget.transform.position).sqrMagnitude;
    if (sqrDistance <= healUnitRange * healUnitRange)
        return; // 사거리 안 - HealRange.Update()가 BeginHeal 호출

    if (UpdateUnreachableChase(orderedHealTarget.transform.position, orderedHealTarget.isAirUnit, false))
    {
        hasHealOrder = false; orderedHealTarget = null;
        HaltInPlace();
    }
}
```

**`Update()`** - 새 틱 호출 추가:
```diff
         RepairTick();
+        HealOrderTick();
         HealTick();
```

**`FollowTick()`** - doc/0665에서 제안했던 가드도 함께 반영 (다른 다친 아군을 자동교전 중일 때도
따라가기가 덮어쓰지 않도록):
```diff
         if (attackRange != null && attackRange.HasEnemyInRange)
             return;

+        if (isHealing)
+            return; // 치유 중이면 그대로 둔다 (doc/0662와 동일 패턴, doc/0665)
+
         float stopDistance;
```

### `Assets/Scripts/Unit/HealRange.cs`

**`Update()`** - `GetClosestDamagedAlly()` 직접 호출 대신 `GetPreferredTarget()` 경유:
```diff
-        GameObject target = GetClosestDamagedAlly();
+        GameObject target = GetPreferredTarget();
```

**신규 `GetPreferredTarget()`** - `AttackRange.GetPreferredTarget()`의 거울상:
```csharp
private GameObject GetPreferredTarget()
{
    UnitController ordered = unitController.GetOrderedHealTarget();
    if (ordered != null)
        return targetsInRange.Contains(ordered.gameObject) ? ordered.gameObject : null;

    return GetClosestDamagedAlly();
}
```

## 동작
- 만피 아군 우클릭 → `IsDamagedUnit` false → 기존 그대로 `FollowUnit` (따라가기만).
- 다친 아군 우클릭 → `Heal(target)` → 사거리 밖이면 `HealOrderTick`이 계속 접근(다른 다친 아군은
  `HealRange.GetPreferredTarget()`이 무시), 사거리 안이면 `HealRange`가 `BeginHeal` 호출 → 정지 + 치유.
- 다 나으면 → `HealOrderTick`이 같은 대상으로 `FollowUnit` 재호출 → 계속 따라다님.
- 대상이 죽으면 → 지정 해제, 그 자리에 정지(Idle).
- 지정 치유 중에도 다른 다친 아군을 자동으로 가로채 치유하지 않는다(`GetPreferredTarget`이 지정
  대상만 반환) - 일꾼이 특정 건물을 수리하는 동안 다른 건물로 안 새는 것과 동일.

## 결과
- 컴파일 성공 (`npx uloop-cli compile`): Errors 0, Warnings 49(전부 기존 `FindFirstObjectByType`
  obsolete 경고 등 이번 변경과 무관한 기존 경고).
