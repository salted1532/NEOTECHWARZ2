# 0636 - 적 방어/보충 유닛 재배치 이동 중 교전 안 하는 버그 수정

## 요청
방어벙력 유닛이 죽어서 새로 생성돼 원래 자리로 배치하러 갈 때, 이동이 우선이라 중간에 플레이어
유닛/건물을 마주쳐도 멈춰서 전투를 안 함. 마주치면 그대로 전투로 넘어가도록 수정.

## 원인
`EnemyAIDirector.cs`가 새로 생산된 유닛을 목적지로 보낼 때 `EnemyUnitController.MoveTo()`를 쓰는데,
`MoveTo()`는 `currentState`를 `EnemyState.Move`로 둔다(`EnemyUnitController.cs:344`).
`EnemyAttackRange.Update()`의 자동 추격 분기(`ChaseTarget`, `EnemyAttackRange.cs:160-169`)는
`enemyUnit.IsIdle()`일 때만 동작하므로, `Move` 상태에서는 감지 범위 안에 적이 있어도 다가가서 싸우지
않고 그냥 목적지로 직진한다. 실제 공격 사거리(더 좁은 `UnitRange`)까지 우연히 딱 붙는 경우에만
`IsAttack()`이 true가 돼 공격이 발동하는데(doc/0584 참고 주석), 그 전에는 그냥 스쳐 지나간다.

`AttackMoveTo()`(플레이어의 "A + 클릭"과 동일)는 `currentState`를 `Idle`로 둬서 이동 중 감지된 상대를
자동으로 추격/교전하고, 끝나면 원래 목적지로 이동을 재개한다(`AttackMoveTick`) — 요청한 동작과 정확히
일치.

## 변경
`Assets/Scripts/System/EnemyAIDirector.cs`

```diff
                     if (front.targetSlot != null) // 방어 슬롯용 - 원래 있던 위치로 이동(doc/0582)
                     {
                         front.targetSlot.current = unit;
                         front.targetSlot.respawned = true;
                         front.targetSlot.pendingProduction = false;
-                        unit.MoveTo(front.targetSlot.position);
+                        unit.AttackMoveTo(front.targetSlot.position);
                     }
                     else
                     {
                         front.destinationPool.Add(unit);
-                        unit.MoveTo(LaneRallyPosition(rt)); // 생산되자마자 그 레인의 집결지로 - 웨이브/별동대 공통(doc/0545, doc/0610)
+                        unit.AttackMoveTo(LaneRallyPosition(rt)); // 생산되자마자 그 레인의 집결지로 - 웨이브/별동대 공통(doc/0545, doc/0610)
                     }
```

방어 슬롯 재배치(446행)와 웨이브/별동대 보충 생산분의 레인 집결 이동(451행) 둘 다 `AttackMoveTo`로
교체 — 사용자 확인. 생산되자마자 이동하는 유닛이 집결지로 가는 길에 플레이어 유닛/건물과 마주쳐도
자동 교전 후 이동을 재개한다.

다른 `MoveTo` 호출(657행 별동대 재사용 이동, 그 외 이미 `AttackMoveTo`를 쓰는 곳들)은 이번 요청 범위
밖이라 변경하지 않음.

## 상태
완료.
