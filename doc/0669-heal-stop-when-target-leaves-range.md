# 0669 - 치유 대상이 사거리를 벗어나면 치유 중단

## 요청
> 힐 범위를 넘어가면 힐 중단되도록도 해줘 힐받는 유닛이 멀어져도 계속 힐을 하네

## 원인
`HealRange.Update()`의 대상 선택은 감지 콜라이더(`UnitRange + margin`, 5f 여유) 기준 `targetsInRange`
멤버십으로 이뤄지는데, 실제 치유 사거리 판정은 그보다 좁은 `UnitRange`다. 대상이 `UnitRange`는
벗어났지만 `UnitRange + margin` 안에는 아직 있으면:
```csharp
if (sqrDistance <= UnitRange * UnitRange)
    unitController.BeginHeal(target);
else if (unitController.IsIdle())
    unitController.ChaseTarget(target.transform.position); // ← StopHeal() 호출이 없었음
```
`else` 분기가 추격만 시작하고 `StopHeal()`을 부르지 않아서, 이미 켜져있던 `isHealing`이 그대로
`true`로 남아 `HealTick()`이 매 프레임 계속 돌았다 - 대상이 사거리 밖으로 멀어지며 이동해도(추격
중에도) 치유가 끊기지 않고 계속 이어진 것.

## 수정
사거리를 벗어난 `else` 분기에서 추격 여부와 무관하게 먼저 `StopHeal()`을 호출:
```diff
         if (sqrDistance <= UnitRange * UnitRange)
-            unitController.BeginHeal(target);
-        else if (unitController.IsIdle())
-            unitController.ChaseTarget(target.transform.position);
+        {
+            unitController.BeginHeal(target);
+        }
+        else
+        {
+            unitController.StopHeal(); // 사거리를 벗어나면 치유부터 끊는다
+            if (unitController.IsIdle())
+                unitController.ChaseTarget(target.transform.position); // 홀드 중엔 쫓아가지 않는다(doc/0668)
+        }
```

## 결과
- 치유 중 대상이 실제 사거리(`UnitRange`)를 벗어나면 즉시 치유 중단(빔 정지) - 감지 콜라이더 안에
  남아있어도 마찬가지.
- 지정 치유(`Heal()`, doc/0666) 중엔 그 다음 `HealOrderTick`이 계속 접근(사거리 안으로 재진입하면
  다시 `BeginHeal`), 홀드 중(doc/0668)엔 추격 없이 그냥 중단된 채로 대기.
- 컴파일 성공 (Errors 0, Warnings 49 - 전부 기존 경고, 이번 변경과 무관).
