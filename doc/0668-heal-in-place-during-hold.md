# 0668 - 홀드 중에도 사거리 안이면 치유(쫓아가진 않음)

## 요청
> 홀드시 그 자리에 그대로 있는거긴 한대 힐 범위 안에 있으면 힐은 해야해 (힐을 하러 따라가는건 안함)

## 원인
`HoldUnit()`은 `UnitcurrentState = UnitState.Attack`으로 "제자리 대기, 사거리 안이면 교전, 쫓아가진
않음"을 표현한다 - `AttackRange.Update()`는 이 상태를 `IsAttackOrderState()`로 인식해서 정확히 그렇게
동작한다(`IsAttackOrderState() || IsIdle()` 게이트, 사거리 안이면 공격, 사거리 밖 추격은 `IsIdle()`
일 때만). 반면 `HealRange.Update()`는 게이트가 `!unitController.IsIdle()) return;` 하나뿐이라
홀드 중(Idle이 아님)엔 아예 통째로 멈춰서, 사거리 안에 다친 아군이 있어도 치유하지 않았다.

## 수정
`HealRange.cs`의 `Update()`를 `AttackRange.Update()`와 동일한 게이트 구조로 변경:
```diff
-        if (unitController.IsConstructing() || unitController.HasPendingSkillOrder || !unitController.IsIdle())
+        if (unitController.IsConstructing() || unitController.HasPendingSkillOrder)
+            return;
+
+        if (!unitController.IsAttackOrderState() && !unitController.IsIdle())
             return;
...
         if (sqrDistance <= UnitRange * UnitRange)
             unitController.BeginHeal(target);
-        else
-            unitController.ChaseTarget(target.transform.position);
+        else if (unitController.IsIdle())
+            unitController.ChaseTarget(target.transform.position); // 홀드 중엔 쫓아가지 않는다
```
`IsAttackOrderState()`는 이미 `UnitController`에 공개되어 있는 접근자(`UnitcurrentState ==
UnitState.Attack`)라 추가 코드 없이 그대로 재사용.

## 결과
- 홀드 중(제자리 정지) 사거리 안에 다친 아군이 들어오면 정지한 채로 치유(기존 `BeginHeal`이
  `isStopped = true`를 다시 걸어도 이미 정지 상태라 변화 없음).
- 홀드 중 사거리 밖 다친 아군은 무시(쫓아가지 않음) - 요청대로.
- 일반 Follow/Idle/지정 치유 등 기존 케이스는 변경 없음(`IsIdle()`이 여전히 참이므로 그대로 추격
  포함 전체 동작).
- 컴파일 성공 (Errors 0, Warnings 49 - 전부 기존 경고).
