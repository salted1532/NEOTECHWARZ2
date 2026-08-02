# BuildingAudio

`Assets/Scripts/Audio/BuildingAudio.cs`

## 개요

완공된 건물(`BuildingController`)과 건설 중인 파운데이션(`BaseStructure`) 양쪽에 공용으로 부착하는 사운드 전담 컴포넌트. `UnitAudio`와 동일한 패턴(doc/0255) — `BaseStructure`는 `Initialize()`에서 뒤늦게 `buildingID`를 받으므로, 재생 시점마다 사운드 뱅크를 다시 조회한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `buildingController`, `baseStructure`, `healthManager` | 같은 오브젝트의 컴포넌트 캐시(완공 건물이면 전자, 건설 중이면 후자) |
| `rtsController` | `BuildingData` 조회용 `RTSUnitController` 참조 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 컴포넌트 캐싱. `Start()`가 아니라 `Awake()`에서 하는 이유(doc/0267): `PlacementSystem.StartConstruction()`이 Instantiate 직후 같은 프레임에 `BaseStructure.Initialize() → PlayConstructLoop()`를 바로 호출하기 때문 |
| `OnEnable()` / `OnDisable()` | `HealthManager.OnDeath`/`OnDamaged` 구독/해제 |
| `GetBank()` (private) | `buildingController`/`baseStructure` 중 존재하는 쪽의 `buildingID`로 `BuildingSoundBankSO` 조회 — 재생마다 새로 조회(건설 중 뒤늦게 ID가 채워지는 경우 대응) |
| `PlayConstructLoop()` | `BaseStructure.Initialize()`에서 `ConstructionEffects.StartLoop()`와 나란히 호출 — `constructLoopSFX`(3D) 재생 |
| `PlayConstructComplete()` | `BaseStructure.CompleteConstruction()`에서 `ConstructionEffects.StopLoopAndPlayComplete()`와 나란히 호출 — `constructCompleteSFX`(3D) 재생 |
| `PlaySelectVoice()` | `RTSUnitController.SelectBuilding()`에서 호출("건물 음성") — `selectVoice`(2D) 재생 |
| `HandleDestroyed()` (private) | `OnDeath` 콜백(전투로 체력이 0이 됐을 때만 발생, `BuildingEffects.HandleDestroyed`와 동일 조건) — `destroySFX`(3D) 재생 |
| `HandleDamaged(amount, attackerPosition, attackType, isEnemyAttacker)` (private) | `OnDamaged` 콜백 — 화면 밖에서 공격받았고 `isEnemyAttacker`가 true일 때만 `PlayBuildingUnderAttackWarning()` 호출(doc/0292). 이 호출이 쿨다운을 통과해서 **실제로 새로 재생됐을 때만**(`bool` 반환값 확인) `MinimapAlertController.Instance.SpawnAttackedPointer(transform.position)`도 함께 호출(doc/0362) |

## 연관 컴포넌트

- **SoundManager**: 실제 재생 담당
- **BuildingSoundBankSO**: 실제 클립 데이터 소스
- **HealthManager**: `OnDeath`/`OnDamaged` 이벤트 구독
- **BaseStructure / ConstructionEffects**: 건설 진행/완료 훅 지점에서 나란히 호출
- **BuildingEffects**: 파괴 시 나란히 동작하는 시각효과 컴포넌트
