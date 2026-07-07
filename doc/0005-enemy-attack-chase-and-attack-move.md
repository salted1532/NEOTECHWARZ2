# 0005. 적 유닛 공격 선택(추격+공격) / A키 적용 / 지점 지정 공격-이동

## 날짜
2026-07-07

## 요청
공격유닛선택 -> 적 우클릭 -> 적 마커 똑같이 0.3초 간격으로 깜박이고 -> 공격유닛은 해당 적유닛을 추격해서 공격 -> 적이 시야에서 사라지면 사라진 마지막 위치로 공격 이동하는 매커니즘으로 코드 수정해줘
그리고 A키로 공격 명령도 똑같이 적용
그리고 공격 명령으로 어느 지점을 지정해서 적유닛과 전투후 다시 지정한 위치로 공격 명령 수행하도록 수정해줘

## 답변 / 변경사항
**`UnitController.cs`**
- `orderedTarget`(추격 대상 `EnemyController`), `attackMoveDestination`(공격-이동 목적지/마지막 확인 위치), `chaseLoseSightRange`(시야 이탈 판정 거리, 기본 20) 필드 추가.
- `AttackUnitTarget(EnemyController target)` — 특정 적을 추격 공격 (우클릭 적 클릭 / A+적 클릭).
- `AttackMoveTo(Vector3 destination)` — 특정 지점으로 공격-이동 (A+땅 클릭).
- `AttackOrderTick()` (매 프레임): 추격 대상이 살아있고 사거리 안이면 계속 최신 위치를 쫓고, 대상이 죽거나 `chaseLoseSightRange`를 벗어나면 마지막 위치로 공격-이동 전환. 교전이 끝났는데 정지된 채 남아있으면 원래 목적지로 이동 재개(전투 후 지정 지점 복귀).
- `MoveTo/StopUnit/HoldUnit/PatrolUnit`에 `CancelAttackOrder()` 추가 — 다른 명령이 오면 추격/공격-이동 상태 확실히 취소.

**`AttackRange.cs`**
- `HasEnemyInRange` 프로퍼티 추가.
- `GetPreferredTarget()`으로 지정 추격 대상이 사거리 트리거 안에 있으면 그것을 우선 공격(포커스 파이어), 없으면 기존처럼 가장 가까운 적을 공격.

**`EnemyController.cs`**
- `FlashMarker()` 추가 — `ResourceNode`와 동일하게 0.3초 간격 3번 깜빡인 뒤 선택 상태 복원.

**`RTSUnitController.cs`**
- `AttackSelectedUnits(Vector3)` → `AttackSelectedUnits(EnemyController)`로 시그니처 변경.
- `AttackGroundSelectedUnits`는 새 `AttackMoveTo`를 호출하도록 변경.

**`UserControl.cs`**
- 좌클릭: 적 클릭 처리를 땅 클릭보다 먼저 검사(적이 지면 위에 있어 두 레이캐스트가 동시에 맞기 때문). A 모드 중 적 클릭 시 `AttackSelectedUnits` + `FlashMarker` 호출 후 공격 포인터 표시, 아니면 기존처럼 선택.
- 우클릭: 적 클릭 처리를 땅 클릭보다 먼저 검사하도록 추가, `AttackSelectedUnits` + `FlashMarker` 호출.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/AttackRange.cs`
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
