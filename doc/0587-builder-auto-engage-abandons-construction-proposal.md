# 0587. 건설 중인 일꾼이 근처 적을 자동교전으로 쫓아가버리는 문제 - 제안

**날짜:** 2026-08-16

## 요청 내용

> 건물 건설중인 일꾼이 근처에 적 유닛이 나타나면 가서 공격하러 가버리는데 이거좀 고쳐줘 공격 못하게

## 조사 내용

건설 중인 일꾼은 `UnitController.BeginConstruction()`에서 `isConstructing = true`로 표시되지만,
유닛 상태(`UnitcurrentState`) 자체는 도착 시점에 `UnitState.Idle`로 남아있다
(`UnitController.cs:1121`, `BuildTick()`) - 건설 전용 상태가 따로 없고 "건설 중 = Idle"로 취급된다.

이동/공격 등 플레이어가 직접 내리는 명령 진입점 13곳은 전부
`if (isConstructing || isRescueUnit) return;`(doc/0458)로 막혀있어서 건설 중엔 새 명령을 받지 않는다.
하지만 `AttackRange.cs`의 자동교전 루프(`Update()`)는 이 가드를 거치지 않는 별도 경로다 - 매 프레임
`unitController.IsAttackOrderState() || unitController.IsIdle()`만 확인하는데, 건설 중엔 `IsIdle()`이
`true`이므로 감지 범위(트리거 콜라이더) 안에 적이 들어오면 그대로 `ChaseTarget()`을 호출해 일꾼이
건설 현장을 벗어나 적을 쫓아가 버린다. doc/0458이 막으려던 것과 같은 문제인데, 자동교전 경로 한 곳만
그 가드에서 빠져 있었다.

## 변경 계획

`AttackRange.cs`의 `Update()` 맨 앞에 건설 중 가드를 추가한다 - 기존 13곳과 동일한 판단 기준
(`IsConstructing()`)을 재사용.

```diff
     private void Update()
     {
         enemiesInRange.RemoveAll(enemy => enemy == null); // 이미 죽어서 destroy된 대상 정리

+        if (unitController.IsConstructing())
+            return; // 건설 중인 일꾼은 자동교전(추격/공격)으로 현장을 이탈하지 않는다 - 명시적 명령
+            // 13곳(doc/0458)은 이미 isConstructing으로 막혀있었는데 자동교전 루프만 빠져있었다(doc/0587).
+
         if (unitController.HasPendingSkillOrder)
             return; // 스킬 사용 명령이 우선 - 사거리 밖 적 자동교전으로 이동을 가로채지 않는다 (doc/0383)
```

## 변경 예정 파일
- `Assets/Scripts/Unit/AttackRange.cs`

---

## 적용 (사용자 승인 후)

제안대로 `Assets/Scripts/Unit/AttackRange.cs`에 위 diff 그대로 적용함. `npx uloop-cli compile`
성공 확인(Error 0개, Warning은 기존에 있던 무관한 것들뿐).

## 변경된 파일
- `Assets/Scripts/Unit/AttackRange.cs`
