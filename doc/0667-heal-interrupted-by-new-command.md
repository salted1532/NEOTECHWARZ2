# 0667 - 치유 도중 다른 명령이 오면 즉시 중단

## 요청
> 힐 하는 도중 다른 명령이 내려오면 힐하는걸 중단했으면 좋겠어

## 원인
doc/0666에서 `orderedHealTarget`/`hasHealOrder`(지정 치유 "명령" 상태)는 `CancelAttackOrder()`가
다른 명령이 들어올 때 함께 정리하도록 했지만, 실제 치유 실행 상태(`isHealing`/`healTarget`/치유
빔)는 별개로 관리되고 있어서 정리 대상이 아니었다. 그 결과 치유 중 다른 명령(이동/정지/홀드 등,
전부 `CancelAttackOrder()`를 거침)이 들어와도 `isHealing`은 그대로 `true`로 남아 `HealTick()`이
매 프레임 계속 돌면서 이동 중에도 뒤에서 계속 치유가 진행됐다(빔도 안 꺼짐).

## 수정
`CancelAttackOrder()`(`UnitController.cs`, 새 명령이 들어올 때마다 공용으로 거치는 취소 지점 - 이동/
정지/홀드/따라가기/수리/건설이동/스킬이동 등 대부분의 명령이 이미 호출하고 있음)에 `StopHeal()`
호출 추가:
```diff
         orderedHealTarget = null;
         hasHealOrder = false;
+        StopHeal(); // 치유 도중 다른 명령이 들어오면 즉시 중단 (isHealing == false면 내부에서 그냥 무시)

         isRepairing = false;
```
`StopHeal()`은 `isHealing`이 이미 `false`면 아무 것도 안 하므로(no-op) 안전하게 매번 호출해도 된다.

## 결과
- 치유 중 새 이동/정지/홀드/다른 유닛 따라가기 등 명령이 들어오면 즉시 `isHealing = false` +
  치유 빔 정지, 새 명령이 방해받지 않고 바로 실행된다.
- 다른 다친 아군을 새로 지정 치유(`Heal()`)해도 같은 경로(`CancelAttackOrder()`)를 타므로 이전
  대상 치유가 먼저 깔끔히 끊기고 새 대상으로 전환된다.
- 컴파일 성공 (Errors 0, Warnings 49 - 전부 기존 경고, 이번 변경과 무관).
