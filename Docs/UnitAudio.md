# UnitAudio

`Assets/Scripts/Audio/UnitAudio.cs`

## 개요

유닛 프리팹에 `UnitController`(아군) 또는 `EnemyUnitController`(적)/`HealthManager`와 같이 부착하는 사운드 전담 컴포넌트. `UnitEffects`와 완전히 동일한 자리, 동일한 패턴(doc/0255) — 재생할 클립은 전부 `RTSUnitController`를 거쳐 조회한 `UnitData.soundBank`(`UnitSoundBankSO`)에서 가져온다. 아군/적 컨트롤러 둘 다 null 체크해서 프리팹에 공용으로 하나만 붙이면 된다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `unitController`, `enemyUnitController`, `healthManager` | 같은 오브젝트의 컨트롤러/체력 컴포넌트 캐시 |
| `rtsController` | `UnitData` 조회용 `RTSUnitController` 참조 |
| `bank` | 이 유닛 "종류"의 `UnitSoundBankSO` — `Awake()`에서 한 번 조회 후 캐싱 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 컴포넌트 캐싱 후 `bank` 조회. `Start()`가 아니라 `Awake()`에서 하는 이유(doc/0267): `UnitSpawner.Spawn()`이 `Instantiate()` 직후 같은 프레임에 `PlaySpawnSound()`를 호출하는데 `Start()`는 다음 프레임에나 실행되기 때문 |
| `OnEnable()` / `OnDisable()` | `HealthManager.OnDeath`/`OnDamaged` 구독/해제 |
| `PlayAttackSFX()` | `UnitController.Attack()`에서 데미지 적용 직후 직접 호출(`UnitEffects.PlayAttack()`과 나란히) — `bank.attackSFX` 3D 재생 |
| `PlaySpawnSound()` | `UnitSpawner.Spawn()`에서 Instantiate 직후 호출 — `bank.spawnSFX`(3D, 이륙음/엔진음 등) + `bank.spawnVoice`(2D) 재생 |
| `PlayGatherSFX()` | `UnitController.GatherTick()`이 채취를 시작하는 순간 호출(워커 전용, 다른 유닛은 `bank.gatherSFX`가 비어있어 무음) |
| `PlaySkillSFX()` | 고급유닛 액티브 스킬 사용 시 호출(doc/0228 `IUnitSkill` 구현체 쪽에서 호출) |
| `PlaySelectVoice()` / `PlayOrderVoice()` / `PlayAttackOrderVoice()` | `RTSUnitController`의 선택/이동·순찰/공격명령 진입점에서 "대표 유닛 1마리"만 호출 — 다수 선택 시 대사가 안 겹치도록(doc/0262~0264). `PlayOrderVoice()`는 구 `PlayMoveVoice`(doc/0289 — 순찰까지 범위 확대) |
| `PlaySelectSFX()` | 선택 대사와 별개로 같이 나는 확인음 — `SoundManager.PlaySelectSFX`(전용 단일 채널) 재생, 2D(doc/0278, 0285) |
| `PlayOrderSFX()` | 이동/공격/순찰/정지/홀드/따라가기/채취/자원반환/건물이동 등 명령 시 확인음 — `SoundManager.PlayOrderSFX`(전용 단일 채널) 재생, 2D(doc/0279, 0285). `RTSUnitController.StopSelectedUnits`/`HoldSelectedUnits`에서는 호출되지 않음(doc/0289) |
| `PlayBuildCompleteVoice()` / `PlayBuildFailVoice()` | 워커 전용 — `BaseStructure`(건설 완료)/`PlacementSystem`(건설 실패)이 담당 일꾼을 통해 호출 |
| `HandleDeath()` (private) | `OnDeath` 콜백 — `bank.deathSFX`(3D) + `bank.deathVoice`(2D) 재생 |
| `HandleDamaged(amount, attackerPosition, attackType, isEnemyAttacker)` (private) | `OnDamaged` 콜백 — 화면 밖에서 공격받았고 `isEnemyAttacker`가 true(적에게 공격받음, 아군사격 아님)일 때만 `PlayUnitUnderAttackWarning()` 호출(doc/0292) |

## 연관 컴포넌트

- **SoundManager**: 실제 재생을 담당하는 싱글턴, 이 컴포넌트는 어떤 클립을 언제 재생할지만 결정
- **UnitSoundBankSO**: 실제 클립 데이터 소스
- **HealthManager**: `OnDeath`/`OnDamaged` 이벤트 구독
- **RTSUnitController**: 선택/명령 진입점에서 대표 유닛의 `UnitAudio`를 통해 재생 트리거
- **UnitEffects**: 동일한 훅 지점(`Attack()`, `OnDeath`)에서 나란히 동작하는 시각효과 컴포넌트
