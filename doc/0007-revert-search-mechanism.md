# 0007. 재탐색(search) 메커니즘 되돌리기

## 날짜
2026-07-07

## 요청
아니다 이번 수정사항은 다시 되돌려줘

## 답변 / 변경사항
0006에서 추가한 `lostTarget`/`searchRadius` 기반 재탐색 메커니즘을 전부 제거하고, 0005 시점의 단순한 "추격 → 시야 이탈 시 마지막 위치로 공격-이동(Idle, 도중 다른 적과 교전 허용)" 방식으로 되돌림.

- `UnitController.cs`의 필드 선언(`searchRadius`, `lostTarget`), `Update()`의 도착 가드 조건, `CancelAttackOrder()`, `AttackUnitTarget()`, `AttackMoveTo()`, `AttackOrderTick()`을 모두 되돌림.
- `lostTarget`/`searchRadius` 참조가 코드베이스에 전혀 남아있지 않음을 grep으로 확인.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs`
