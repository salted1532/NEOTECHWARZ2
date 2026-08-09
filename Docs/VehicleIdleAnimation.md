# VehicleIdleAnimation

`Assets/Scripts/Animation/VehicleIdleAnimation.cs`

## 개요

지상 차량 유닛의 비주얼(메쉬) 자식 오브젝트에 부착한다(`VehicleShake`와 같은 오브젝트) — 루트는 `UnitController`/`NavMeshAgent`가 이동 중 매 프레임 좌표를 갱신하므로 피한다. 가만히 있을 때(`IsCurrentlyMoving()==false && IsAttack()==false`)만 두 가지를 재생한다: (1) 엔진 떨림 — 아주 미세한 `DOShakePosition`을 계속 이어붙여 덜덜거리는 느낌(`VehicleShake`와 동일한 체이닝 패턴, 진폭만 훨씬 작음). (2) 포탑 방황 — 랜덤 대기(기본 5~15초)마다 포탑을 랜덤 절대 각도로 돌렸다가 5~10초 대기 후 같은 duration으로 천천히 원위치 복귀(`TurretController`의 기본 회전속도는 360도 범위에서는 너무 빠르게 홱 도는 것처럼 보여서 쓰지 않음). 회전 중에는 `TurretController`를 잠시 꺼서 같은 트랜스폼을 두 스크립트가 동시에 건드리지 않게 하고, 대기/회전 중 실제 조준 대상이 잡히면 즉시 `TurretController`에 제어권을 돌려준다. 보병용 대응 컴포넌트는 `InfantryIdleLookAround`.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `turretController` | 포탑 방황 대상 (없으면 자동 탐색, 포탑 없는 차량이면 자동으로 스킵) |
| `idleWaitMin` / `idleWaitMax` | 다음 방황까지 대기 시간 범위 |
| `turretWanderDuration` | 방황/복귀 둘 다 이 시간으로 회전(같은 속도로 돌아오게) |
| `turretHoldMin` / `turretHoldMax` | 방황한 각도에서 머무는 시간 범위 |
| `idleShakeStrength` / `idleShakeVibrato` / `idleShakeCycleDuration` | 엔진 떨림 강도/진동수/사이클 시간 |
| `manualIdle` | `UnitController`/`EnemyUnitController`/`AllyController`가 없을 때만 사용되는 수동 idle 제어 플래그 |
| `unitController` / `enemyUnitController` / `allyController` | idle 판정용 컨트롤러(doc/0468) |
| `attackRange` / `enemyAttackRange` | 조준 대상 존재 여부 확인용(`AllyController.GetAttackRange()`도 `EnemyAttackRange` 타입 재사용, doc/0448) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 컨트롤러 3종/`TurretController`(못 찾으면 루트에서 탐색)/공격 사거리 캐싱. `Awake()`가 아니라 `Start()`인 이유는 `HoverBob`과 마찬가지로 상위 프리팹에 중첩된 프리팹 인스턴스(차량 메쉬)로 붙는 경우가 많아 `Awake` 시점엔 아직 재부모(reparent)되기 전이라 `GetComponentInParent`/`transform.root`가 루트 컨트롤러를 못 찾는 문제가 있었기 때문(doc/0466/0468) |
| `Update()` | `VehicleShake`/`HoverBob`과 동일한 폴링 토글 패턴(doc/0105) — idle로 전환되면 떨림 재생 + 방황 코루틴 시작, idle이 풀리면 전부 정지하고 `TurretController`를 재활성화 |
| `IsIdle()` (private) | 세 컨트롤러 중 붙어있는 것의 이동/공격 여부로 판정, 없으면 `manualIdle` 값 사용 |
| `SetManualIdle(idle)` | 컨트롤러가 없는 오브젝트(쇼케이스, 데모 씬 등)에서 외부에서 idle 애니메이션을 직접 켜고 끌 때 사용 |
| `HasTrackingTarget()` (private) | 조준 대상이 잡혀 있는지 확인 |
| `IdleTurretWanderRoutine()` (private) | 랜덤 대기 → (대상 없으면) `TurretController` 비활성화 후 랜덤 각도로 회전 → 랜덤 시간 대기 → 원위치 복귀 → `TurretController` 재활성화. 각 단계마다 대상이 잡히면 즉시 중단하고 제어권 반환 |
| `PlayIdleShakeCycle()` (private) | `basePosition` 기준으로 매 사이클 새로 시작해 반복해도 누적 오차 없이 흔들림(`VehicleShake.PlayShakeCycle`과 동일한 체이닝 패턴) |
| `StopIdleShake()` (private) | 떨림 트윈 정지 후 기본 위치로 복귀 |
| `OnDestroy()` | 떨림/방황 트윈 정리 |

## 연관 컴포넌트

- **TurretController**: 포탑 방황 중 제어권을 넘겨받고(`enabled = false`) 다시 돌려줌
- **UnitController / EnemyUnitController / AllyController**: idle 상태 판정, 조준 사거리(`AttackRange`/`EnemyAttackRange`) 제공
- **VehicleShake**: 엔진 떨림 트윈 체이닝 패턴을 공유하는 별도 컴포넌트
- **InfantryIdleLookAround**: 보병용 대응 컴포넌트(단순 방향 전환만 수행)
