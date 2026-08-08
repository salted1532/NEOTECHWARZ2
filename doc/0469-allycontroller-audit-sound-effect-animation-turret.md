# 0469. AllyController(아군 OC) 관련 컴포넌트 전수 점검 - 사운드/이펙트/애니메이션/포탑

**날짜:** 2026-08-08

## 요청 내용
> AllyController라서 UnitController와 다르게 작동 안할만한게 뭐가 있는지 확인하고(unitEffect라던지
> 연결된것들) 있으면 수정해주고 EnemyController도 작동 안하는게 있는지 확인좀

`doc/0466`~`0468`에서 발견한 "AllyController를 빼먹은 컴포넌트" 패턴이 다른 곳에도 있는지 전체 점검.
`Assets/Scripts` 전체에서 `UnitController`/`EnemyUnitController`/`AllyController`를
`GetComponent`(`InParent`/`InChildren`)로 조회하는 모든 파일(17개)을 하나씩 확인.

## 발견 및 수정

### 실제 버그였던 것 (수정함)

| 파일 | 증상 | 원인 |
|---|---|---|
| `UnitAudio.cs` | **아군 OC 유닛의 모든 소리가 조용함**(공격음/스폰음/채취음/스킬음/선택·이동·공격 대사/사망음 전부) | `Awake()`의 `bank` 조회가 `unitController`/`enemyUnitController`만 확인 - `AllyController`는 조회 대상이 아니라 `bank`가 계속 `null`로 남아 모든 재생 메소드의 `if (bank != null)` 가드에 걸림 |
| `UnitEffects.cs` | 아군 OC 유닛이 이동해도 이동 트레일 이펙트가 안 나옴 | `Update()`의 `moving` 판정에 `allyController` 분기 없음 |
| `UnitAnimatorDriver.cs` | 아군 OC 유닛에 Animator가 있어도 IsMoving/Fire 파라미터가 갱신 안 됨(애니메이션 정지) | `Update()` 초입 가드와 판정 둘 다 `allyController` 없음 |
| `VehicleShake.cs` | 아군 OC 차량이 이동해도 엔진 흔들림 이펙트가 안 나옴 | `allyController` 없음 + `HoverBob`(doc/0466)과 동일한 중첩 프리팹 Awake 타이밍 문제 |
| `TurretController.cs` | 아군 OC 차량의 포탑이 대상을 감지해도 조준하지 않고 계속 정면만 봄 | `Start()`의 사거리 조회가 `unitController`/`enemyUnitController`만 확인 |

### 적용
- `UnitAudio.cs`: `allyController` 필드 추가. `bank` 조회에
  `rtsController.GetEnemyUnitData(allyController.GetAllyUnitID())` 분기 추가(아군 OC도 적 OC와
  같은 `EnemyUnitDataSO` 로스터를 재사용하므로 `enemyUnitController`와 동일한 조회 방식, doc/0447/0448).
- `UnitEffects.cs`: `allyController` 필드 추가, `moving` 판정에
  `allyController.IsCurrentlyMoving()` 분기 추가.
- `UnitAnimatorDriver.cs`: `allyController` 필드 추가, 널 가드와 `isMoving`/`isAttacking` 판정에
  분기 추가.
- `VehicleShake.cs`: `allyController` 필드 추가 + 조회 로직을 `Awake()`→`Start()`로 이동(doc/0466과
  동일한 이유 - 중첩 프리팹 재부모 타이밍).
- `TurretController.cs`: `unitController`/`enemyUnitController`에 이어 `AllyController` 분기 추가
  - `AllyController.GetAttackRange()`가 `EnemyAttackRange` 타입을 그대로 반환하므로 기존
    `enemyAttackRange` 필드를 그대로 재사용.

### 이미 정상이었던 것 (수정 안 함)
- `AttackRange.cs`: 플레이어 전용 컴포넌트(`UnitController`와 1:1) - 애초에 적/아군 OC가 안 씀. 정상.
- `EnemyAttackRange.cs`: `IAttackRangeUnit` 인터페이스로 `EnemyUnitController`/`AllyController`를
  이미 동등하게 취급하도록 설계돼 있음(doc/0452). 정상.
- `UserControl.cs`: 좌클릭 대상 판정에 `AllyController` 전용 레이캐스트 레이어/분기가 이미 있음. 정상.
- `UnitSpawner.cs`, `SkyLancerSkill.cs`, `ResourceNode.cs`: 플레이어(NTA) 전용 개념(생산 대기열/액티브
  스킬/자원 채취) - 아군 OC는 애초에 해당 없음. 정상.
- `ProjectileAttack.cs`, `FogRevealerAgent.cs`: 컨트롤러 타입을 아예 안 가리는 범용 컴포넌트(호출부가
  타입 무관하게 직접 호출) - 주석에 `AllyController` 언급이 없을 뿐 실제 분기 로직 자체가 없음. 정상.
- `CaptureSystem.cs`: 거점 포인트의 "범위 내 아군/적 유닛 수" 판정이 `AllyController`를 안 셈 -
  사용자에게 확인 결과 **의도된 동작**("아군OC는 점령에 기여 안해") - 수정하지 않음.

## EnemyController(EnemyUnitController) 점검 결과

이번 전수 점검에서 확인한 17개 파일 전부 `EnemyUnitController`는 이미 빠짐없이 처리되고 있었음 -
이번에 발견된 문제는 전부 "AllyController가 새로 추가되면서 기존 UnitController/EnemyUnitController
2종 체크 패턴에 편입이 안 된" 케이스였고, `EnemyUnitController` 자체가 빠진 곳은 없었음.

## 검증 (Play Mode)

- `Heavy Assault Tank (Ally)`를 스폰한 뒤 `VehicleShake`/`UnitAnimatorDriver`/`TurretController`를
  런타임으로 추가하고 한 프레임 대기 후 확인:
  - `UnitAudio`: `allyController!=null=True`, **`bank!=null=True`**(수정 전엔 항상 `False`)
  - `UnitEffects`: `allyController!=null=True`
  - `UnitAnimatorDriver`: `allyController!=null=True`
  - `VehicleShake`: `allyController!=null=True`
  - `TurretController`: `enemyAttackRange!=null=True`(수정 전엔 항상 `False` - 포탑이 못 쏨)
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- `git status`: 물 메쉬 애셋 노이즈 없음. `Cyborg Soldier `/`Railgunner`/`Striker`/`Brute Mech`/
  `Heavy Assault Tank`/`Ironhawk`/`Raven` 프리팹 변경은 이 세션이 만든 게 아니라 동시에 진행 중인
  다른 세션(`doc/0460`)의 작업 - 건드리지 않음.

## 변경된 파일

- `Assets/Scripts/Audio/UnitAudio.cs`
- `Assets/Scripts/Effects/UnitEffects.cs`
- `Assets/Scripts/Animation/UnitAnimatorDriver.cs`
- `Assets/Scripts/Animation/VehicleShake.cs`
- `Assets/Scripts/Unit/TurretController.cs`
