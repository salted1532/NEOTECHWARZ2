# InfantryIdleLookAround

`Assets/Scripts/Animation/InfantryIdleLookAround.cs`

## 개요

보병 유닛이 가만히 서 있을 때 랜덤한 방향으로 몸을 돌려 주변을 경계하는 느낌을 주는 연출용 컴포넌트. 유닛 루트(`UnitController`/`EnemyUnitController`/`AllyController`가 붙은 오브젝트)에 직접 부착하며, 이동 중에는 NavMeshAgent가, 공격 중에는 각 컨트롤러의 회전 로직이 이미 회전을 담당하므로 그 두 경우를 제외한 idle 상태에서만 개입한다. 랜덤 `idleWaitMin`~`idleWaitMax`초(기본 5~15초, 매번 다시 뽑아 유닛마다 한꺼번에 돌지 않게 함)마다 랜덤 Y축 방향으로 회전 트윈을 재생하고, 이동/공격이 재개되면 즉시 트윈을 끊는다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `idleWaitMin` / `idleWaitMax` | 다음 방향 전환까지 대기하는 시간 범위 |
| `turnDuration` / `turnEase` | 회전 트윈 소요 시간과 이징 |
| `unitController` / `enemyUnitController` / `allyController` | 붙어있는 컨트롤러 종류 판별용(셋 중 하나만 세팅됨) — 아군 OC 보병도 `EnemyController`와 동일 판정(doc/0468) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 컨트롤러 3종 캐싱, 첫 대기 시간 랜덤 결정 |
| `Update()` | idle이 아니면 진행 중인 트윈을 끊고 타이머 리셋. idle이면 타이머를 누적하다 대기시간을 넘기면 랜덤 Y축 회전 트윈 재생 |
| `IsIdle()` (private) | 세 컨트롤러 중 붙어있는 것의 `IsCurrentlyMoving()`/`IsAttack()`이 모두 false인지 확인 |
| `OnDestroy()` | 트윈 정리 |

## 연관 컴포넌트

- **UnitController / EnemyUnitController / AllyController**: idle 상태 판정 대상 — 이 중 어느 것이 붙어있는지에 따라 아군/적/아군 OC 유닛 모두 지원
- **VehicleIdleAnimation**: 차량 유닛용 대응 컴포넌트(포탑 방황 + 엔진 떨림 포함), 이 스크립트는 그 보병 버전
