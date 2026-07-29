# LaserBeamAttack

`Assets/Scripts/Unit/LaserBeamAttack.cs`

## 개요

레이저 공격 유닛에 붙이는 옵셔널 컴포넌트(`UnitEffects`와 동일하게 없으면 그냥 무시됨, doc/0218). `UnitController`/`EnemyUnitController`의 `Attack()`이 데미지를 적용한 직후 `Fire(target)`를 호출해서 `firePoint`와 대상을 잇는 빔을 `beamDuration`초간 재생한다.

`Attack_Laser_Blue_3D` 프리팹의 `LaserMachine` 컴포넌트는 재활성화할 때마다 `LineRenderer` 자식을 새로 쌓기만 하고, 조준 방향도 raycast+`transform.forward` 기반이라 공격자가 회전하면 조준 방향도 같이 돌아가 버려서(doc/0218) 이 용도(정확히 두 지점을 잇는 재사용 빔)에 안 맞는다. 그래서 프리팹에 직접 붙여둔 `LineRenderer`를 이 스크립트가 매 프레임 월드 좌표로만 갱신한다 — 로컬 회전을 전혀 참조하지 않으므로 공격자가 회전 중이어도 빔은 항상 `firePoint`와 대상의 실제 위치를 그대로 연결한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `laserBeamPrefab` | 빔 비주얼 프리팹(`Attack_Laser_Blue_3D`) |
| `firePoint` | 발사 위치 Transform |
| `beamDuration`(0.2초) | 빔이 연결된 채로 유지되는 시간 |
| `beamLine`, `beamInstance`, `activeBeam` (private) | 캐싱된 `LineRenderer`/인스턴스/재생 중인 코루틴 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 풀링 — 공격마다 Instantiate/Destroy 하지 않고 시작할 때 한 번만 `firePoint` 밑에 만들어두고 꺼둠 |
| `Fire(target)` | `UnitController.Attack()`에서 데미지 적용 직후 호출(`UnitEffects.PlayAttack()`과 같은 훅 지점) — 이미 실행 중인 코루틴이 있으면 정지 후 재시작 |
| `BeamRoutine(target)` (private) | `beamDuration` 동안 매 프레임 `firePoint`~대상(콜라이더가 있으면 `ClosestPoint`, 없으면 Transform 위치)을 `LineRenderer`로 연결, 대상이 중간에 파괴되면 즉시 종료 |

## 연관 컴포넌트

- **UnitController / EnemyUnitController**: `Attack()`에서 데미지 적용 직후 `Fire(enemy.transform)` 호출
- **ProjectileAttack**: 같은 자리(`Attack()`의 데미지 적용 훅)에서 동작하는 또 다른 공격 전달 방식이지만, 이쪽은 데미지가 이미 적용된 뒤의 순수 시각효과라는 점이 다름(투사체는 명중 시점에 데미지 적용)
