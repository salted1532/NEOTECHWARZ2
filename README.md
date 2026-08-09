# NEOTECHWARZ2.0

Unity로 제작 중인 스타크래프트 스타일의 RTS(실시간 전략) 게임입니다. 이전 프로젝트 **네오테크워즈**의 문제점과 미구현 기능을 보완하고, UI/그래픽을 개선한 후속작으로 개발하고 있습니다.

## 기술 스택

| 구분 | 내용 |
|------|------|
| 엔진 | Unity 6000.4.8f1 |
| 렌더 파이프라인 | Universal Render Pipeline (URP) 17.4.0 |
| 입력 | Unity Input System 1.19.0 |
| 길찾기 | AI Navigation (NavMesh) 2.0.12 |
| UI | UGUI, TextMesh Pro |
| 그래픽 | URP Volume 포스트프로세싱(Bloom/Color Adjustments, Tonemapping은 현재 None), Screen Space Ambient Occlusion(SSAO), 오버레이 카메라 기반 레이어 분리(프리뷰/포인터 제외) |
| 애니메이션/트윈 | DOTween (Demigiant, `Assets/Plugins/Demigiant`) — 이펙트/모션 트위닝(호버링, 셰이크 등) |

## 프로젝트 구조

```
Assets/
├─ Scripts/
│  ├─ Animation/        # 공중유닛/리프트 건물 호버링(HoverBob), 지상 차량 이동 셰이크(VehicleShake), 지속 회전(AutoRotate), 보병 대기 중 둘러보기(InfantryIdleLookAround), 차량 대기 애니메이션(VehicleIdleAnimation), 유닛 애니메이터 파라미터 갱신(UnitAnimatorDriver), 아이템 부유(ItemHover) - DOTween 기반
│  ├─ Audio/            # 사운드 전담 싱글턴(SoundManager), 유닛/건물 사운드 재생 컴포넌트(UnitAudio/BuildingAudio), 랜덤 재생 클립 묶음(SoundClipSet)
│  ├─ Building/        # 건물 컨트롤러, 건설 중 건물 기반(BaseStructure), 연구 대기열(ResearchQueue)
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라/미니맵 이동·조작(명령까지 확장), 지형 티어별 줌 범위 보정, 미니맵 시야 사각형 표시, 공격받은 위치 미니맵 마커(MinimapAlertController), 미니맵 위 미션 목표 오브젝트 마커(MinimapObjectiveMarker/MinimapObjectiveOverlay)
│  ├─ CaptureSystem/    # 거점 점령(밀당식 Ally↔Neutral↔Enemy 순환, 완전 점령 되돌리기/재점령 2배속/방치 시 감쇠) + 다각형 영토 판정(TerritoryZone/TerritoryManager)
│  ├─ Effects/          # 공격/이동/피격/사망/건물 이착륙/건설 이펙트 재생 시스템(EffectPlayer 등), 스킬 범위 표시(RadiusIndicator), 안개에 가려진 위치면 스폰 자체를 건너뜀
│  ├─ FogOfWar/         # 전장의 안개(csFogWar) 연동 어댑터 — 유닛/건물 시야 소스 등록(FogRevealerAgent), 점령 영토 강제 시야 확보(TerritoryFogReveal), 안개 상태 조회 공용 헬퍼(FogVisibility)
│  │  ├─ Enemy/         # 적(외계종족) 유닛/건물 컨트롤러(EnemyUnitController/EnemyBuildingController/EnemyAttackRange) — 마커·미니맵 아이콘·체력 데이터 + 기초 AI(자동 교전/이동/공격-이동), 안개에 가려지면 미니맵 마커 숨김·선택 자동 해제
│  │  └─ Ally/          # 아군 OC(플레이어에게 적대적이지 않은 구조 유닛) 컨트롤러(AllyController/AllyBuildingController/AllyAttackRange) — Enemy 쪽 로직을 상속 없이 복제해 피아식별만 반대로 유지(doc/0452)
│  ├─ Localization/     # 언어별(en/ko) JSON 텍스트 조회 싱글턴(LocalizationManager), 정적 UI 라벨 자동 번역 컴포넌트(LocalizedText)
│  ├─ Resource/         # 자원 노드 및 자원 관리 (`ResourceController.cs`는 미사용 빈 스텁)
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO) — NTA(UnitDataSO/BuildingDataSO) + OC/스포어 브루드(EnemyUnitDataSO/EnemyBuildingDataSO, 같은 UnitData/BuildingData 구조 재사용)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템, 캠페인 스테이지별 목표 스크립트(Stage0~5Objectives/StageManager/ObjectiveTextUtil/MissionItem), 연구 보너스 전역 관리(UpgradeManager)
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러, 툴팁, 메인 메뉴(MainMenuController/MainMenuFlyby/SceneMenuController), 미션 선택 화면(MissionSelectManager), 승리 패널(VictoryPanelController), 부대지정 UI(ControlGroupPanel)
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리, 포탑(TurretController), 투사체(Projectile), 지속 피해(DamageOverTimeEffect), 은신 비주얼(StealthVisual)
│  │  └─ Skills/         # 고급유닛 액티브 스킬(SharpshooterSkill/SkyLancerSkill/GuardianDroneSkill)
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  ├─ Upgrade/          # 연구소 업그레이드로 얻는 전역 공격/방어 보너스 관리(UpgradeManager)
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리, 마우스 커서 상태 전환
├─ Scenes/
│  ├─ MainScene/        # 메인 메뉴(Play/Option/Exit), 언어 선택(EN/KR) 버튼
│  ├─ Missions/         # MissionSelect(미션 선택 화면) + Mission0~5(캠페인 스테이지 0~5 본편 씬)
│  ├─ SampleScene, TestScene  # 초기 프로토타입/기능 확인용 씬(캠페인 씬으로 대체되어 현재는 미사용에 가까움)
├─ prefabs/             # 유닛/건물 프리팹 (`NTA/`, 유닛은 전부·건물은 대부분 기본 프리미티브 메시 사용 — 병영 건물에만 실제 모델 1개 적용 시작), 맵 프리팹(`Maps/Mission0~5`, YuME 타일맵 기반 — 캠페인 스테이지 0~5 씬이 각각 대응하는 프리팹을 사용)
├─ AssetFolder/         # 3rd-party 에셋 — 모델링/스카이박스(Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox, TZ_Futuristic Panel Textures, LowPolyWater_Pack, 임포트+URP 머티리얼 변환 완료했지만 게임플레이 프리팹엔 대부분 미적용 — 병영 건물에 실제 모델 1개 적용 시작) + 전장의 안개 플러그인(`AOSFogWar`/csFogWar, 실제로 적용되어 작동 중)
├─ Material, Shader/    # 머티리얼 및 커스텀 셰이더
└─ Settings/            # URP 렌더 파이프라인 설정 + 포스트프로세싱 Volume Profile(Bloom/Color Adjustments/SSAO)

doc/                     # 세션별 작업 로그 + 코드 변경 전/후 diff + 기능 설계 노트, 전부 0001~ 번호로 통합
Docs/                    # 스크립트별 코드 문서(역할/필드/메소드) — 세션 로그는 doc/로 이동됨
```

> 전장의 안개(Fog of War)는 3rd-party 플러그인 `csFogWar`(`AssetFolder/AOSFogWar/`) 기반으로 구현 완료되어 유닛/건물 9+6종 전체와 점령 영토에 연동돼 있습니다 — 최초 설계는 [`doc/0069`](doc/0069-fog-of-war-design.md), 실제 구현 시작은 [`doc/0166`](doc/0166-fogofwar-folder-and-eye-script-design.md), 이후 버그수정까지 `doc/0166`~`0197` 참고.

### 핵심 스크립트

각 스크립트의 상세 문서(필드, 메소드별 동작 방식)는 [`Docs/`](Docs) 폴더에 스크립트 이름과 동일한 파일명으로 정리되어 있습니다.

| 스크립트 | 역할 | 문서 |
|---|---|---|
| `RTSUnitController` | 유닛/건물 선택 상태, 전체 목록, UI 갱신, 생산·건설 자원 검증을 총괄하는 중앙 허브 | [doc](Docs/RTSUnitController.md) |
| `UserControl` | 마우스/키보드 입력을 해석해 선택·명령을 `RTSUnitController`에 전달, 상태별(기본/선택/이동/공격) 마우스 커서 아이콘 전환, ESC로 대기 명령 취소 | [doc](Docs/UserControl.md) |
| `UnitController` | 유닛의 이동/전투/순찰/자원 채취 상태머신 (지상+공중 유닛 공통) | [doc](Docs/UnitController.md) |
| `AttackRange` | 사거리 내 적 감지 및 자동 공격/추격 | [doc](Docs/AttackRange.md) |
| `TurretController` | 차량형 유닛의 포탑 오브젝트가 몸체와 별개로 `AttackRange` 감지 대상을 향해 조준(이동 중에도 계속 조준), DOTween 반동 연출 | [doc](Docs/TurretController.md) |
| `UnitAnimatorDriver` | 유닛의 이동/공격 상태를 Animator 파라미터(IsMoving/Fire)에 반영, Animator 없는 유닛은 조용히 무시 | [doc](Docs/UnitAnimatorDriver.md) |
| `Projectile` | 투사체 인스턴스 자신이 이동/명중을 처리(발사자 사망 후에도 끊기지 않도록 소유권 이전, doc/0319) | [doc](Docs/Projectile.md) |
| `DamageOverTimeEffect` | 대상에 붙어 일정 시간 주기적으로 데미지를 주는 범용 DoT 컴포넌트, 재요청 시 스택 대신 지속시간만 갱신 | [doc](Docs/DamageOverTimeEffect.md) |
| `StealthVisual` | 살아있는 유닛의 머티리얼을 일시적으로 반투명 흰색으로 바꿨다가 원본으로 복원하는 은신 비주얼 컴포넌트 | [doc](Docs/StealthVisual.md) |
| `RadiusIndicator` | `LineRenderer` 기반으로 원형 범위를 잠깐 바닥에 그려 보여주는 범용 스킬 이펙트(텍스처/머티리얼 불필요) | [doc](Docs/RadiusIndicator.md) |
| `SharpshooterSkill` | 저격수 액티브 스킬 2종(저격 즉시데미지 / 은신) — `IUnitSkill` 구현, 특성(trait) 시스템에 연결 | [doc](Docs/SharpshooterSkill.md) |
| `SkyLancerSkill` | 스카이랜서 스킬 2종(공중 강화 패시브 화염 도트 / 지상 폭격 범위 데미지) | [doc](Docs/SkyLancerSkill.md) |
| `GuardianDroneSkill` | 가디언 드론 스킬 2종(집중 포화 3연발 투사체 / 쉴드 전개 임시 최대체력) | [doc](Docs/GuardianDroneSkill.md) |
| `BuildingController` | 건물 선택, 랠리 포인트, 생산 위임, 파괴 시 대기열 환불/인구수 반환 | [doc](Docs/BuildingController.md) |
| `BaseStructure` | 건설 중인 건물 기반 — 담당 일꾼이 붙어있을 때만 건설 진행(체력 상승), 완공 시 실제 건물 스폰, 취소/파괴 시 환불 | [doc](Docs/BaseStructure.md) |
| `UnitSpawner` | 건물의 유닛 생산 대기열(FIFO) 관리 및 스폰, 대기열 취소 시 환불용 유닛ID 반환 | [doc](Docs/UnitSpawner.md) |
| `ResearchQueue` | 연구소(Lab) 부착, 공격력/방어력 연구 대기열(레벨 1~3) 관리 — `UnitSpawner`와 동일한 FIFO 타이머 구조 | [doc](Docs/ResearchQueue.md) |
| `UpgradeManager` | 연구로 얻은 전역 공격력/방어력 보너스 저장, `RTSUnitController`의 `AddGlobalBonus` 경로로만 값이 오감 | [doc](Docs/UpgradeManager.md) |
| `PlacementSystem` | 그리드 기반 건물 배치, 배치 가능 여부 판정, 프리팹 높이 기반 자동 지면 정렬 | [doc](Docs/PlacementSystem.md) |
| `GridData` | 그리드 셀 점유 정보 관리 (순수 데이터 클래스) | [doc](Docs/GridData.md) |
| `PreviewSystem` | 배치 프리뷰(고스트 오브젝트) 및 셀 커서 표시 | [doc](Docs/PreviewSystem.md) |
| `InputManager` | 건물 배치 전용 입력 처리 (클릭/ESC/마우스 좌표) | [doc](Docs/InputManager.md) |
| `ResourceManager` | 팀의 광물/가스/인구수 중앙 관리, 인구수 한도 상한(200) | [doc](Docs/ResourceManager.md) |
| `ResourceNode` | 자원 채취 지점, 대기열(줄서기) 및 고갈 처리 | [doc](Docs/ResourceNode.md) |
| `HealthManager` | 체력/데미지/치유/사망 처리 공용 컴포넌트, 절대값 지정(SetHealth/SetMaxHealth), 데미지 이벤트에 공격자 진영 정보(isEnemyAttacker) 포함 | [doc](Docs/HealthManager.md) |
| `UnitDataSO` | 유닛 스탯 데이터베이스 — 체력/공격력/사거리/공격속도/아이콘/장갑타입/크기타입까지 스폰되는 유닛이 자기 `unitID`로 직접 조회해 스스로 적용(`UnitController.ApplyUnitData`, `doc/0205`), `tier`로 생산 가능 건물 자동 분류(`doc/0200`), 공격 전달 방식(Hitscan/Projectile) 선택 | [doc](Docs/UnitDataSO.md) |
| `BuildingDataSO` | 건물 스펙(비용/크기/생산시간/인구수 제공량 등) 데이터베이스 | [doc](Docs/BuildingDataSO.md) |
| `EnemyUnitDataSO` | 적(OC 등) 진영 유닛 데이터베이스 — `UnitDataSO`와 동일한 `UnitData` 구조 재사용, 진영별로 SO 에셋만 분리 | [doc](Docs/EnemyUnitDataSO.md) |
| `EnemyBuildingDataSO` | 적(OC 등) 진영 건물 데이터베이스 — `BuildingDataSO`와 동일한 `BuildingData` 구조 재사용 | [doc](Docs/EnemyBuildingDataSO.md) |
| `DamageMultiplierTableSO` | 공격 방식(소총/폭발/레이저/화염) × 대상 크기(소형/중형/대형) 데미지 배율표 — 코드가 아니라 별도 에셋으로 분리해 인스펙터에서 밸런스 조정 가능 | [doc](doc/0201-armor-size-damage-multiplier-system.md) |
| `DamageTypes` | 전투 공용 열거형 모음(ArmorType/SizeType/AttackDeliveryType) | [doc](Docs/DamageTypes.md) |
| `LaserBeamAttack` | 레이저 공격 유닛 전용 옵셔널 컴포넌트 — firePoint~대상을 매 프레임 월드 좌표로 잇는 재사용 빔(순수 시각효과, 데미지는 이미 적용된 뒤) | [doc](Docs/LaserBeamAttack.md) |
| `ProjectileAttack` | 투사체 공격 유닛 전용 옵셔널 컴포넌트 — 발사마다 투사체를 생성해 대상을 추적, 명중 시점에 데미지 적용, 여러 firePoints 동시발사 지원 | [doc](Docs/ProjectileAttack.md) |
| `CameraControl` | RTS 시점 카메라 이동/줌 — 지형 티어(`Layer1`/`Layer2` 태그) 감지로 언덕마다 줌 범위·현재 높이 자동 보정 | [doc](Docs/CameraControl.md) |
| `MinimapController` | 미니맵 표시, 클릭/드래그 시 카메라 이동, 대기 중인 명령이 있으면 미니맵 클릭으로 확정, 우클릭 시 선택된 유닛/건물에 명령(이동/랠리) | [doc](Docs/MinimapController.md) |
| `MinimapViewIndicator` | 메인 카메라 시야를 미니맵 위에 반투명 사각형으로 표시, 줌/회전에 따라 매 프레임 크기·위치 자동 갱신(미니맵 밖으로 안 나가게 클리핑) | [doc](Docs/MinimapViewIndicator.md) |
| `MinimapAlertController` | 아군 유닛/건물이 적에게 공격받아 경고음이 실제로 재생되는 순간(10초 쿨다운 통과 시)에만, 공격받은 위치 Y=40에 3D 마커(`Attacked_MiniMapPointer`)를 스폰하고 3초 뒤 자동 파괴 | [doc](Docs/MinimapAlertController.md) |
| `FogVisibility` | 월드 좌표가 지금 안개에 가려져 있는지(Revealed/PreviouslyRevealed면 보임) 조회하는 공용 정적 헬퍼 — 미니맵 마커/체력바/점령 타이머/이펙트 스폰이 전부 이걸 통해 안개 속에서 숨겨짐 | [doc](Docs/FogVisibility.md) |
| `EnemyUnitController` | 적 유닛 컨트롤러(구 `EnemyController`, doc/0231에서 개명) — 선택/마커/스탯뿐 아니라 자동 교전·이동·공격-이동까지 담당하는 단순 AI, 미니맵 마커/체력바가 안개 속에서 자동으로 숨겨지고 안개에 가려지면 선택도 자동 해제됨 | [doc](Docs/EnemyUnitController.md) |
| `EnemyBuildingController` | 적 건물 "껍데기" — 체력/선택/미니맵 마커만 있고 실제 생산 큐는 없음(캠페인 전용 배치형), 안개에 가려지면 미니맵 마커가 숨겨지고 선택도 자동 해제 | [doc](Docs/EnemyBuildingController.md) |
| `EnemyAttackRange` | 적 유닛의 자식 트리거 콜라이더 부착, 사거리 내 상대(플레이어) 자동 감지·공격·추격 — `AllyAttackRange`가 태그만 바꿔 상속 | [doc](Docs/EnemyAttackRange.md) |
| `AllyController` | 아군 OC(구조된 유닛 등) 컨트롤러 — `EnemyUnitController` 로직을 상속 없이 복제, 피아식별 방향만 반대(doc/0452) | [doc](Docs/AllyController.md) |
| `AllyBuildingController` | 아군 OC 건물 컨트롤러 — 껍데기라 AI가 없어 `EnemyBuildingController`를 이름만 다르게 그대로 상속 | [doc](Docs/AllyBuildingController.md) |
| `AllyAttackRange` | 아군 OC 유닛의 자식 오브젝트, 사거리 내 "적대 세력"(외계종족) 자동 감지/교전 — `EnemyAttackRange` 상속, 대상 태그만 교체 | [doc](Docs/AllyAttackRange.md) |
| `CaptureSystem` | 거점 점령 — 트리거 범위 내 아군/적 유닛 수에 따라 부호 있는 점령치를 밀당, 양쪽 다 있으면 교착, 완전 점령을 되돌리려면 1배속으로 30초+30초, 한 번도 완전 점령된 적 없는 상태에서 반대 진영 진행치를 지우는 중이면 2배속, 방치 시 완전 점령이면 원래 소유자 쪽으로 회복·중립 진행중이면 0으로 감쇠(3초 후 바 자동 숨김), 점령 타이머가 안개에 가려진 위치면 숨겨짐 | [doc](Docs/CaptureSystem.md) |
| `TerritoryZone` | 인스펙터에서 핀(꼭짓점) 개수만 늘리면 자동 생성되는 다각형 영토 범위(오목 다각형도 판정 가능), 소유자에 따라 외곽선 색이 흰색/초록/빨강으로 자동 전환 | [doc](doc/0133-territoryzone-implementation.md) |
| `TerritoryManager` | 씬의 모든 `TerritoryZone`을 등록해 특정 좌표가 아군 영토 안인지 한 번에 질의(여러 영토가 겹치면 합집합) | [doc](doc/0141-territory-restriction-implementation-design.md) |
| `FogRevealerAgent` | 유닛/건물에 부착해 `csFogWar`에 자신을 시야 소스로 등록/해제하는 어댑터(기존 컨트롤러는 건드리지 않음) | [doc](doc/0166-fogofwar-folder-and-eye-script-design.md) |
| `TerritoryFogReveal` | 아군이 점령한 `TerritoryZone` 내부를 시야 소스 없이도 항상 밝게 강제 반영 | [doc](doc/0166-fogofwar-folder-and-eye-script-design.md) |
| `LocalizationManager` | 현재 언어(PlayerPrefs)에 맞는 `Resources/Localization` JSON을 읽어 텍스트 조회를 제공하는 싱글턴(doc/0481) | [doc](Docs/LocalizationManager.md) |
| `LocalizedText` | 스크립트로 갱신되지 않는 정적 UI 라벨을 언어 변경 이벤트에 맞춰 자동 재표시, TMP/레거시 Text 둘 다 지원 | [doc](Docs/LocalizedText.md) |
| `UIController` | 커맨드 패널, 생산 대기열, 자원 표시 UI 총괄, 버튼별 키보드 단축키 데이터 보유 | [doc](Docs/UIController.md) |
| `ProductionSlot` | 커맨드/생산 대기열의 버튼 슬롯 하나, 자기 단축키 자동 감지 + 눌림 효과 재현 | [doc](Docs/ProductionSlot.md) |
| `TooltipUI` | 버튼/스탯 호버 시 툴팁 표시 | [doc](Docs/TooltipUI.md) |
| `TooltipContentFitter` | 툴팁 배경 크기를 실제 표시 중인 제목/설명 텍스트 분량에 맞춰 매번 다시 계산 | [doc](Docs/TooltipContentFitter.md) |
| `ControlGroupPanel` | 부대(컨트롤 그룹) 선택 버튼을 그룹 생성/전멸에 맞춰 자동 생성·파괴, `HorizontalLayoutGroup`으로 정렬 | [doc](Docs/ControlGroupPanel.md) |
| `HealthBarBillboard` | 체력바 UI가 카메라의 X(피치) 각도만 따라 회전(Y/Z 고정)하도록 하는 빌보드 컴포넌트 | [doc](Docs/HealthBarBillboard.md) |
| `EffectPlayer` | 이펙트 프리팹(파티클/사운드) 스폰·자동 파괴 공용 정적 헬퍼 — 단발/다중지점/지속형 재생 지원 | [doc](doc/0105-effect-system-integration-design.md) |
| `HitEffectSet` | 공격 타입(총기/폭발/레이저/화염)별 피격 이펙트 프리팹 묶음(직렬화 클래스) | [doc](doc/0108-hit-effect-attack-type-variants.md) |
| `UnitEffects` | 유닛의 공격(총구)/이동(트레일)/피격/사망 이펙트 재생 전담 컴포넌트 | [doc](doc/0105-effect-system-integration-design.md) |
| `BuildingEffects` | 건물의 이착륙/피격/파괴 이펙트 재생 전담 컴포넌트 | [doc](doc/0116-building-destroy-effect.md) |
| `ConstructionEffects` | `BaseStructure`의 건설 중 지속/완공/피격/파괴 이펙트 재생 전담 컴포넌트 | [doc](doc/0117-construction-destroy-effect.md) |
| `TrailRotationFollower` | 지속형 이펙트가 부착 지점을 부모-자식으로 즉시 따라가지 않고, 위치는 매 프레임 추적하되 회전만 Slerp로 서서히 따라가게 하는 컴포넌트(급회전 중 축소 포함) | [doc](doc/0118-move-trail-smooth-rotation-follow-design.md) |
| `HoverBob` | 공중 유닛/리프트 중인 건물의 비주얼 자식 오브젝트를 DOTween으로 둥실거리게 하는 컴포넌트 | [doc](doc/0119-dotween-hover-bob-design.md) |
| `VehicleShake` | 지상 차량 유닛이 이동 중일 때 DOTween으로 흔들림을 재현하는 컴포넌트 | [doc](doc/0120-vehicle-shake-and-animation-folder.md) |
| `AutoRotate` | 레이더 접시/터렛 헤드 등을 DOTween으로 조건 없이 지속 회전시키는 컴포넌트 | [doc](doc/0147-autorotate-dotween-script.md) |
| `InfantryIdleLookAround` | 보병 유닛이 대기 상태일 때 랜덤 주기로 몸을 돌려 주변을 경계하는 연출(이동/공격 중에는 개입 안 함) | [doc](Docs/InfantryIdleLookAround.md) |
| `VehicleIdleAnimation` | 차량 유닛 대기 시 엔진 떨림 + 포탑 방황 연출, 실제 조준 대상이 잡히면 즉시 `TurretController`에 제어권 반환 | [doc](Docs/VehicleIdleAnimation.md) |
| `ItemHover` | 조건 판정 없이 붙이기만 하면 항상 둥실거리고 회전하는 미션 아이템(유물 등) 전용 장식 컴포넌트 | [doc](Docs/ItemHover.md) |
| `SoundManager` | 사운드 전담 싱글턴 — BGM/SFX/Voice 볼륨·뮤트 관리, AudioSource 풀링, 동시재생 스팸 방지, 명령확인음 전용 단일채널 | [doc](Docs/SoundManager.md) |
| `SoundClipSet` | 랜덤 재생용 오디오 클립 묶음 직렬화 클래스 (모든 사운드 뱅크의 슬롯 타입) | [doc](Docs/SoundClipSet.md) |
| `UnitAudio` | 유닛의 SFX(공격/생성/사망/스킬/채취)·Voice(선택/명령/생성/사망) 재생 전담 컴포넌트 | [doc](Docs/UnitAudio.md) |
| `BuildingAudio` | 건물/BaseStructure의 SFX(건설/파괴)·Voice(선택) 재생 전담 컴포넌트 | [doc](Docs/BuildingAudio.md) |
| `UnitSoundBankSO` | 유닛 종류별 사운드 뱅크 에셋 | [doc](Docs/UnitSoundBankSO.md) |
| `BuildingSoundBankSO` | 건물 종류별 사운드 뱅크 에셋 | [doc](Docs/BuildingSoundBankSO.md) |
| `GlobalVoiceBankSO` | 유닛/건물에 안 묶이는 전역 나레이션(자원/인구 부족, 피격 경고, 업그레이드 완료) 에셋 | [doc](Docs/GlobalVoiceBankSO.md) |
| `SoundSettingsPanel` | 볼륨 슬라이더/뮤트 토글 UI 로직 (SoundManager API 연결, 실제 Canvas 배치는 미완료) | [doc](Docs/SoundSettingsPanel.md) |
| `MainMenuController` | 메인 메뉴(MainScene) Play/Option/Exit 버튼 연결, 버튼 호버 시 커서 전환 | [doc](Docs/MainMenuController.md) |
| `MainMenuFlyby` | 메인화면 배경 장식(우주선 등) — 대각선 비행 후 시작점 텔레포트, 랜덤 대기 반복 | [doc](Docs/MainMenuFlyby.md) |
| `SceneMenuController` | 게임플레이 씬의 옵션 패널 표시/숨김, "메인화면으로"·스테이지 이동 처리 | [doc](Docs/SceneMenuController.md) |
| `MissionSelectManager` | 미션 선택 씬의 스테이지 버튼 연결(인스펙터 리스트), 호버 툴팁, 해금 상태는 PlayerPrefs로 관리 | [doc](Docs/MissionSelectManager.md) |
| `MissionItem` | 유물/연구 데이터베이스 등 미션 오브젝트에 부착 — 선택 시 Info Panel 표시(이름/설명, 로컬라이제이션 지원), 트리거 접촉 판정(비콘 반납용) | [doc](Docs/MissionItem.md) |
| `ObjectiveTextUtil` | 스테이지 목표 체크리스트 텍스트 표시 공용 헬퍼 — 완료 시 취소선, 생존형 목표는 실패 확정 후 고정 | [doc](Docs/ObjectiveTextUtil.md) |
| `StageManager` | 스테이지 승리/패배 "결과"만 담당하는 최소 골격 싱글턴 — 판정은 각 `Stage0~5Objectives`가 직접 하고 결과만 보고 | [doc](Docs/StageManager.md) |
| `Stage0Objectives` ~ `Stage5Objectives` | 스테이지별 목표 체크리스트 — 거점 점령/생산·건설, 적 전초기지 파괴, 유물·연구데이터 확보·운반, 생존자 구조, 에너지 코어 파괴 등 목표 성격에 맞는 감지 방식(매 프레임 폴링/이벤트 기반/생존형) | [doc](Docs/Stage0Objectives.md) 외 |
| `MinimapObjectiveMarker` | 미션 목표 오브젝트에 붙이면 미니맵에 아이콘 표시, 오브젝트 비활성화/파괴 시 자동으로 사라짐(doc/0349) | [doc](Docs/MinimapObjectiveMarker.md) |
| `MinimapObjectiveOverlay` | 씬의 `MinimapObjectiveMarker`들을 미니맵 위에 아이콘으로 렌더링하는 싱글턴 | [doc](Docs/MinimapObjectiveOverlay.md) |
| `VictoryPanelController` | `StageManager.OnVictory` 구독, 승리 패널 표시 + "메인화면/다음 스테이지/계속하기" 버튼 처리 | [doc](Docs/VictoryPanelController.md) |

> 문서 칸이 `doc/NNNN-...` 형식인 스크립트(`DamageMultiplierTableSO`, `TerritoryZone`~`TerritoryFogReveal`, `EffectPlayer`~`AutoRotate` 등)는 아직 `Docs/` 폴더에 필드/메소드 상세 문서가 없어 관련 `doc/` 세션 로그로 대신 링크했습니다.

### 유닛/건물 수치 문서

- [`Docs/UnitAndBuildingStats.md`](Docs/UnitAndBuildingStats.md) — 유닛 9종 + 건물 6종의 현재 스탯을 정해진 양식(유닛명/ID/생산티어/공격범위/공격방식/장갑/크기/가격&인구수/생산시간/체력/공격력/사거리/공격속도/단축키)으로 정리한 최신 레퍼런스. `UnitDataSO`/`BuildingDataSO`에 적힌 실제 값 기준.
- [`Docs/UnitBalanceReference.md`](Docs/UnitBalanceReference.md) — 어떤 값이 실제로 게임에 반영되는지(SO vs 프리팹) 조사한 감사 기록, 설계 스펙과 실측값이 어긋났던 부분들의 이력.
- [`Docs/EnemyUnitAndBuildingStats.md`](Docs/EnemyUnitAndBuildingStats.md) — 적 진영(OC) 유닛/건물 수치를 아군과 동일한 양식으로 정리, `EnemyUnitDataSO`/`EnemyBuildingDataSO` 기준.
- [`Docs/SporeBrood.md`](Docs/SporeBrood.md) — 외계 종족 "스포어 브루드" 컨셉 + 유닛 3종/건물 3종 수치(설계 제안 [`doc/0441`](doc/0441-alien-monster-faction-design-proposal.md)의 구현 상태 확인 포함).
- [`Docs/ResourceSystem.md`](Docs/ResourceSystem.md) — 자원 "아이로나이트 광석(Ore)"/"페트로나이트(Gas)" 컨셉·명칭과 채취/저장 시스템 개요.
- [`Docs/Campaign.md`](Docs/Campaign.md) — 캠페인 세계관/스토리 설정, 스테이지 0~5 시놉시스.

## 주요 기능

- **유닛 시스템**: 단일/다수 선택(드래그, 쉬프트 클릭, 유닛 선택 중 Shift+드래그로 기존 선택에 추가), 이동, 공격, 정지, 홀드, 순찰, NavMesh를 사용하지 않는 공중 유닛 이동(발밑 지형을 매 프레임 재측정해 언덕 능선을 실제로 벗어나는 시점에 맞춰 고도가 자연스럽게 오르내리는 지형 추적 비행), 일꾼 자원 채취(반납은 가장 가까운 메인기지로만)
- **아군 유닛 계속 추적("Follow")**: 아군 유닛 우클릭 시 그 유닛을 계속 따라다니며(Idle 상태 유지) 도중에 만나는 적은 `AttackRange`가 자동으로 교전 — 대상과의 거리가 두 유닛의 실제 크기(반경) 합만큼 가까워지면 정지, 서로 밀거나 겹치지 않도록 지상/공중 모두 유닛 크기 비례로 정지 거리 계산
- **부대 지정(컨트롤 그룹)**: `Ctrl+숫자`(1~9,0)로 선택 저장(덮어쓰기), `Shift+숫자`로 겹치지 않는 대상만 병합 추가(유닛 하나가 여러 부대에 동시 소속 가능), 숫자만 누르면 저장된 부대를 선택 — 대기 중이던 공격/이동/순찰(A/M/P) 명령 모드는 부대 재선택 시 자동 취소
- **건물 시스템**: 그리드 기반 배치(셀 크기 2), 생산 대기열, 렐리 포인트 지정, 게임 시작 시 `startPoint` 위치에 메인기지 자동 생성(그리드 등록 포함)
- **건설 진행 시스템**: 건물 배치 클릭 시 즉시 완공되지 않고, 일꾼이 현장으로 이동해 `BaseStructure`(건물 기반, 실제 건물 크기(2x2/3x3)에 맞춰 자동 스케일)를 만들고 붙어서 건설 — 담당 일꾼이 없으면(사망 등) 건설이 자동 일시정지, 다른 일꾼을 우클릭으로 투입하면 재개. 건설 중엔 담당 일꾼이 다른 명령을 받지 못하며, 완공 전 취소하거나 파괴되면 건물 가격 전액 환불. 건설 중 입은 피해는 완공된 건물의 체력에 그대로 이어짐. 일꾼이 건설 현장에 도착하기 전(이동 중)에 다른 명령으로 취소되거나 그 도중에 사망해도 건물 가격 전액 환불
- **건물 이동(리프트)**: 건물을 공중으로 띄워(그리드 점유 해제) 공중유닛처럼 우클릭으로 자유 이동시키거나, 착륙 위치를 지정해 그 자리로 날아가 착륙(그리드 재등록) — 이동 중에도 공중 유닛과 동일하게 발밑 지형을 따라가는 지형 추적 비행 적용, 이륙/이동/착륙 전 구간에서 메쉬 피벗 오프셋까지 반영해 고도 기준이 일관됨. 공중에 뜬 동안은 생산/커맨드가 전부 잠기고 Land/Move 버튼만 노출, 생산 대기열이 남아있으면 이륙 자체가 차단됨. 메인기지 건설 시 자원(광물/가스)과 최소 거리(기본 7칸, 인스펙터 조정 가능) 이격 규칙 적용(다른 건물엔 미적용)
- **자원 시스템**: `ResourceManager` 기반 광물/가스/인구수 관리(한도 200), 건물 건설·유닛 생산 시 실제 자원/인구수 소모, 대기열 취소·생산 건물 파괴·건설 취소/파괴·건설 이동 중 취소·건설 이동 중 일꾼 사망 시 가격만큼 환불, 유닛 사망 시 인구수 반환, 자원 노드 대기열(줄서기)
- **전투**: 사거리 기반 자동 교전, 공격력/방어력 스탯, 적 강제 지정, 아군 강제 공격(오인사격, 완공 건물 + 건설 중인 `BaseStructure` 포함), 공격 전달 방식(Hitscan/Projectile)을 유닛별로 선택 가능(`UnitDataSO.attackDelivery`) — Projectile은 `ProjectileAttack`이 투사체를 발사해 대상에 명중해야 데미지가 들어감, 여러 firePoints 지정 시 동시 발사(다연장)도 지원
- **데미지 배율 시스템**: 유닛을 장갑 타입(경장갑/중장갑)과 크기 타입(소형/중형/대형)으로 분류하고, 공격 방식(소총/폭발/레이저/화염)별로 대상 크기에 따른 데미지 배율을 적용 — 배율표는 코드가 아니라 `DamageMultiplierTableSO` 에셋으로 분리해 인스펙터에서 조정 가능. 일부 유닛은 특정 장갑 타입 상대로 고유 추가 데미지(%)도 보유(예: 저격수 vs 중장갑, 스카이랜서 vs 경장갑). 최종 데미지는 `공격력 × 크기배율 × 고유보너스배율 → 반올림 → 고정방어력 감산 → 최소 1 보장` 순서로 계산
- **유닛 생산(자동 분류 + 자가 동기화)**: `UnitDataSO`에 유닛 항목을 추가하고 `tier`(0=본진/1=병영/2=공장/3=우주공항) 값만 지정하면 코드 수정 없이 해당 건물의 생산 패널에 자동으로 나타남. 스폰된 유닛은 자기 `unitID`로 `UnitDataSO`를 스스로 조회해 체력/공격력/사거리/공격속도/아이콘/장갑·크기 타입을 반영(`UnitController.ApplyUnitData`) — 생산 큐를 거쳤든 씬에 직접 배치했든 항상 적용됨
- **점령/영토 시스템**: 거점(`CaptureSystem`)은 트리거 범위 내 아군/적 유닛 수에 따라 점령치를 밀당하며 Ally↔Neutral↔Enemy 3단계로 순환 전환(항상 Neutral을 거침), 양쪽이 동시에 있으면 교착. `TerritoryZone`은 인스펙터에서 핀 개수만 조절하면 자동 생성/정리되는 다각형 영토(오목 다각형 포함)로 소유자별 외곽선 색이 자동 전환되고, `TerritoryManager`가 전체 영토를 등록해 좌표 질의를 제공. 건물 배치(칸 전부가 아군 영토 안이어야 함), 자원 채취(영토 밖 노드 채취 불가, 채취 중 영토 상실 시 즉시 중단), 유닛 생산(영토 밖이면 대기열 유지한 채 타이머 정지), 건설 진행(영토 밖이면 담당 일꾼 유무와 별개로 일시정지)이 전부 영토 여부에 실제로 게이팅됨
- **전장의 안개(Fog of War)**: 3rd-party 플러그인 `csFogWar` 기반, `FogRevealerAgent`를 유닛/건물에 부착해 시야 소스로 등록(기존 컨트롤러는 안 건드림), 아군이 점령한 `TerritoryZone`은 `TerritoryFogReveal`이 시야 소스 없이도 항상 밝게 강제 반영
- **체력바 UI**: 유닛/건물 공용 `HealthManager`에 `Slider` 연결 시 체력 변화에 맞춰 자동 갱신, 만피 상태에선 자동으로 숨겨지고 피해를 입는 즉시 표시(회복해서 만피로 돌아가면 다시 숨김), `HealthBarBillboard`로 카메라의 X(피치) 각도만 따라 회전(Y/Z 고정)해 유닛이 돌아도 체력바 자체는 방향을 유지
- **키보드 단축키**: 선택 상태(유닛/일꾼/건설모드/생산 패널/공중 건물)별 버튼에 단축키 배정 — 버튼이 자기 단축키를 직접 감지해 클릭과 동일하게 동작 + 눌림 시각 효과, 현재 패널에 없는 버튼의 단축키는 자동으로 비활성
- **명령 취소**: 공격/이동/순찰/랠리/건물이동 등 대기 중인 명령 모드를 ESC로 즉시 취소(포인터 마커도 함께 사라짐)
- **마우스 커서**: 기본 화살표 외에 선택 가능 대상(유닛/적/건물/광물/가스) 호버 시 선택 커서, 공격/이동 대기 상태(A/M/P/랠리/건물이동)에서 각각 공격/이동 커서로 전환(`UserControl`), UI 위에서는 항상 OS 기본 커서로 복귀
- **이펙트 시스템**: `EffectPlayer` 공용 헬퍼로 공격(총구)/이동(트레일)/피격(공격 타입별 4종: 총기·폭발·레이저·화염)/사망/건물 이착륙/건설 진행·완공·파괴 이펙트를 재생 — 유닛/건물 프리팹에 붙는 `UnitEffects`/`BuildingEffects`/`ConstructionEffects`가 각각 전담, 스폰 위치는 `List<Transform>`으로 다중 지점 지정 가능(비워두면 오브젝트 자신 위치 하나로 폴백), 피격 이펙트는 콜라이더 표면의 공격자 쪽 지점에서 방향까지 계산해 재생
- **모션 연출**: 이동 트레일은 `TrailRotationFollower`로 위치는 매 프레임 추적하되 회전만 Slerp로 서서히 따라가 급회전 시 부자연스럽게 홱 도는 문제 방지(급회전 중엔 크기/방출량도 축소), 공중 유닛/리프트 중인 건물은 `HoverBob`으로 DOTween 기반 부유(호버링) 애니메이션, 지상 차량 유닛은 이동 중 `VehicleShake`로 DOTween 기반 흔들림 연출 — 둘 다 루트가 아닌 비주얼 자식 오브젝트에 부착해 이동 로직(루트 트랜스폼 직접 갱신)과 충돌하지 않음
- **UI**: 패널 기반 커맨드 UI, Info Panel(공격 아이콘 호버 시 공격타입/공격력(투사체 다연장 유닛은 xN 배수 표기), 방어 아이콘 호버 시 방어력/장갑타입/유닛 크기), Squad Panel(최대 60마리 페이지네이션), 생산 대기열 UI, 미니맵
- **사운드**: `SoundManager` 싱글턴이 BGM(랜덤 무한 반복)/SFX/Voice 4개 카테고리 볼륨·뮤트를 관리, 유닛/건물 종류별 `SoundBank` 에셋(`UnitSoundBankSO`/`BuildingSoundBankSO`)으로 코드 수정 없이 사운드 추가, 명령·선택 확인음은 전용 단일 채널로 재생 중이면 새 요청을 버림, 동일 사운드가 짧은 시간에 몰리면 최소 재생 간격/동시 재생 개수 제한으로 스팸 방지, "적에게 공격받음" 경고음은 아군사격에는 울리지 않음(doc/0292) — 볼륨 슬라이더/뮤트 토글 UI(`SoundSettingsPanel`)는 로직만 있고 실제 Canvas 배치는 아직 안 됨(그동안 `SoundManager` 인스펙터에서 직접 조절)
- **그래픽/비주얼**: URP Volume 포스트프로세싱(Bloom, Color Adjustments) + SSAO 적용, 빌드 프리뷰/셀 커서/이동·공격 명령 포인터는 전용 레이어 + 오버레이 카메라로 포스트프로세싱 미적용 처리, 3rd-party 유닛/건물 모델링 에셋(Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox) 임포트 및 Built-in → URP 머티리얼 변환 완료(게임플레이 프리팹 적용은 병영 건물 1개만 시작, 나머지는 로드맵)

> 스크립트별 상세 동작 방식은 위 표의 [`Docs/`](Docs) 링크를 참고하세요. 요청 단위의 작업 로그(요청 내용, 코드 변경 전/후, 기능 설계 노트 포함)는 [`doc/`](doc) 폴더에 `0001-`부터 번호순으로 정리되어 있습니다.

## 시작하기

1. [Unity Hub](https://unity.com/download)에서 **Unity 6000.4.8f1** 버전을 설치합니다.
2. 저장소를 클론한 뒤 Unity Hub에서 프로젝트 폴더를 엽니다.
3. `Assets/Scenes/MainScene`(Play/Option/Exit 메인 메뉴)에서 시작해 Play → 미션 선택 화면(`Assets/Scenes/Missions/MissionSelect`)에서 스테이지(0~5)를 골라 플레이합니다. 각 미션 씬을 바로 열어(`Assets/Scenes/Missions/Mission0`~`Mission5`) 개별 실행할 수도 있습니다.

## 구현 완료 기능

### 선택 / 피드백
- [x] 유닛 선택 / 다중 선택(드래그, 쉬프트 클릭, 유닛 선택 중 Shift+드래그로 기존 선택에 추가)
- [x] 적 유닛 선택, 적 건물 선택, 중립 자원 노드 선택
- [x] 자원 채취지 노란색 선택 피드백
- [x] 자원 노드 고갈 시 자동 삭제(다 캐면 파괴)
- [x] 적 유닛/건물 공격 지정 시 빨간색 마커 깜빡임 피드백
- [x] 적 유닛/적 건물 선택 → 아군 유닛의 공격 대상 지정을 강제(A 모드)
- [x] 아군 유닛/건물에 대한 강제 공격("오인사격", A 모드에서 아군 좌클릭) — 원래 TODO 목록엔 없었지만 이미 구현되어 있어 이번에 정리에 포함, 이후 건설 중인 `BaseStructure`도 대상에 포함되도록 확장
- [x] 부대 지정(컨트롤 그룹) — `Ctrl+숫자`(1~9,0) 저장, `Shift+숫자` 병합 추가, 숫자만 눌러 선택 복원(죽거나 파괴된 대상은 자동 제외)

### 유닛
- [x] 이동, 정지, 홀드, 순찰 (지상/공중 공통)
- [x] 공중 유닛 이동 — NavMesh 대신 직접 좌표 보간이라 지형 제약을 받지 않음, 공중 유닛끼리 겹침을 유닛 크기(반경) 비례로 자동 분리, 매 프레임 발밑 지형을 재측정해 언덕 능선을 실제로 벗어나는 순간에 맞춰 고도가 자연스럽게 변하는 지형 추적 비행
- [x] 공격 — 사거리 내 적 감지 후 메소드로 데미지 적용 (`AttackRange` + `UnitController.Attack`)
- [x] 공격력 / 방어력 필드(`UnitController`, `EnemyUnitController`) + Info Panel 호버 시 공격 아이콘 = "Attack Type : N / Attack Damage : N(xN)", 방어 아이콘 = "Armor : N / Armor Type : N / Size : N" 툴팁(doc/0293)
- [x] 공격 전달 방식 선택(Hitscan/Projectile) — `UnitDataSO.attackDelivery`로 유닛별 지정, Projectile은 `ProjectileAttack`이 투사체를 발사해 명중 시점에 데미지 적용(그 전엔 데미지 없음), firePoints를 여러 개 지정하면 다연장(동시발사)도 지원(doc/0290, 0291)
- [x] 일꾼 자원 채취 (아래 "일꾼 채취 로직" 참고), 반납 대상은 가장 가까운 메인기지로 한정(다른 건물엔 반납하지 않음)
- [x] 아군 유닛 우클릭 시 계속 따라다니기("Follow") — Idle 상태 유지로 도중에 만나는 적은 자동 교전, 대상과의 거리가 두 유닛 반경 합만큼 가까워지면 정지(지상/공중 모두 유닛 크기 비례로 밀어붙이거나 겹치지 않게 계산)
- [x] 일꾼의 "건물 건설" 동작 — 일꾼이 현장으로 이동해 `BaseStructure`(건설 중 건물 기반)에 붙어서 건설 진행(아래 "건설 진행 시스템" 참고), 건설 중엔 다른 명령 불가. 현장 도착 전 다른 명령으로 취소되거나 이동 중 사망해도 건물 가격 전액 환불
- [x] 유닛 사망 시 자신이 차지하던 인구수를 현재 인구수에서 자동 반환
- [x] 생산 가능 건물 자동 분류 — `UnitDataSO`에 `tier`(0=본진/1=병영/2=공장/3=우주공항) 값만 지정하면 코드 수정 없이 해당 건물 생산 패널에 자동으로 나타남
- [x] 유닛 스탯 자가 동기화 — 유닛이 스폰 시 `Start()`에서 자기 `unitID`로 `UnitDataSO`를 직접 조회해 체력/공격력/사거리/공격속도/아이콘/장갑·크기 타입을 스스로 적용, 생산 큐를 거쳤든 씬에 직접 배치됐든 항상 적용됨
- [x] 신규 유닛 2종 추가 — Sharpshooter(저격수, 병영), SkyLancer(스카이랜서, 공장), 둘 다 특정 장갑 타입 상대 고유 추가 데미지 보유
- [x] 유닛 이동속도 전체 1.5배 증가
- [x] 카메라 줌 아웃 범위 증가
- [x] 건물 클릭 히트박스 조정 — 실제로 클릭되길 기대하는 위치에서 더 확실하게 선택되도록 콜라이더/판정 보정
- [x] 도달/추격 로직 정비(도달 가능/불가 2모드 통일) — 목적지가 도달 가능하면 매 프레임 실시간으로 추적(이동→재탐색→경로갱신 무한반복), 도달 불가능(경사로 없는 언덕 등)하면 가장 가까운 위치로 이동 후 도착 시점에만 재탐색해서 도달 가능 여부를 다시 확인 — 강제공격/이동(따라가기)/적 강제공격 전부 동일한 두 모드로 통일, 도중에 목적지가 도달 가능한 위치로 바뀌면 자동으로 실시간 추적 모드로 전환됨
- [x] 올라갈 수 없는 언덕(경사로가 연결되지 않아 NavMesh가 길을 못 찾는 경우) 이동/추격 예외처리 — 도달 불가로 판정되면 갈 수 있는 가장 가까운 위치까지만 이동
- [x] 올라갈 수 없거나 도달할 수 없는 대상에 대한 공격 명령 처리 — 최대한 가까운 곳까지 이동 후 사거리 안에 들면 공격, 도저히 도달할 수 없고 사거리 안에도 들지 않으면 공격 명령 자동 취소
- [x] 적 유닛(`EnemyAttackRange`)도 동일한 도달 가능/불가 추격 로직 적용 — 사거리 내 진입 시 추격, 도달 불가 대상은 가장 가까운 위치로 이동, 이동 중 사거리를 들락날락하는 대상 재탐색으로 인한 멈칫거림도 함께 해결(`engagedTarget` 우선 유지, doc/0388)
- [x] 고급유닛 스킬 사용이 공격 명령보다 우선순위 — 전투 중이더라도 스킬 사용 명령(지정 유닛/위치)을 내리면 사거리 내 여부와 무관하게 나머지를 무시하고 스킬을 사용하러 이동
- [x] 일꾼 자원 채취/반납 로직 개선 — 자신이 캐던 자원을 기억해뒀다가 건물 우클릭 시 반납 후 그 자원으로 복귀, 다른 자원을 우클릭하면 자원을 들고 있을 때만 먼저 반납하고 새로 지정한 자원으로 이동
- [x] 메인기지 착륙 시 자원을 보유 중인 모든 일꾼에게 자동으로 리턴 명령(각자 기준 가장 가까운 메인기지 재탐색) 하달

### 데미지 시스템
- [x] 장갑 타입(경장갑/중장갑) × 크기 타입(소형/중형/대형) × 공격 방식(소총/폭발/레이저/화염) 3축 분류
- [x] 공격 방식별 대상 크기 데미지 배율표 — `DamageMultiplierTableSO` 에셋으로 코드와 분리, 인스펙터에서 밸런스 조정 가능(기본값: 소총 100/80/60%, 폭발 70/100/130%, 레이저 100/100/100%, 화염 130/100/60%)
- [x] 유닛별 고유 장갑타입 추가 데미지(%) — 예: 저격수 vs 중장갑 +80%, 스카이랜서 vs 경장갑 +50%, 파이어호크 vs 경장갑 +30%, 가디언 드론 vs 중장갑 +40%
- [x] 최종 데미지 계산 순서: `공격력(연구 보너스 포함) × 크기배율 × 고유보너스배율 → 반올림 → 대상 고정방어력 감산 → 최소 1 보장`

### 건물 / 생산
- [x] 건물 배치(그리드 기반, 배치 가능 여부 판정)
- [x] 언덕 벽면 건설 차단 — 점령지 영역이 언덕과 지상을 함께 포함할 때 언덕 벽을 뚫고 짓던 문제 해결, 지형 끝 모서리에 건설 불가 영역을 지정해 Layer2(언덕) 벽면에는 건물을 지을 수 없음
- [x] 점령지(거점) 위 건설/착륙 차단 — 점령지 오브젝트도 건물 판정을 받아 그 위에 건물을 짓거나 리프트 중인 건물이 착륙할 수 없음
- [x] 일꾼이 도달할 수 없는 위치로 건설 명령 시 자동 취소 — 높이가 안 맞거나 경사로 없는 언덕 등, 가장 가까운 위치까지 이동해도 도달 불가능하면 건설 명령을 취소하고 건설 실패 음성 재생
- [x] 프리팹 높이 기반 자동 지면 정렬(`PlacementSystem.GetGroundOffsetY`) — 건물마다 크기가 달라도 뜨거나 파묻히지 않게 자동 계산
- [x] 게임 시작 시 `startPoint` 위치에 메인기지 자동 생성(그리드 등록 포함)
- [x] 메인기지 건설 시 자원(광물/가스)과 최소 이격 거리 규칙(기본 7칸, 인스펙터 조정 가능, 다른 건물엔 미적용)
- [x] 건물 선택, 생산 명령
- [x] 유닛 생산 대기열(최대 5개, 순차 진행) + 진행률 표시
- [x] 생산 대기열 UI(슬롯 5개, 클릭 시 해당 인덱스 취소 후 재출력)
- [x] 대기열 항목 취소 + 취소 시 자원 환불
- [x] 생산 렐리 포인트(집결지) 설정 — 생산 건물 패널 고정 슬롯(6번)에 랠리 버튼(단축키 Y), 누르면 M(이동)처럼 위치 지정 대기 모드로 들어가고 클릭한 위치가 집결지로 확정(건물 우클릭과 동일 동작). 생산 건물을 선택하는 순간 자기 랠리 포인트 위치에 기존 이동 명령 포인터가 잠깐(3초) 표시됨
- [x] 건설 중 건물 기반(`BaseStructure`) — 건설시간에 비례해 체력이 차오르고, 담당 일꾼이 없으면(사망 등) 자동 일시정지, 다른 일꾼을 우클릭으로 투입하면 재개. 건설 중 입은 피해는 완공된 건물의 체력으로 그대로 이어짐
- [x] 건설 취소(전액 환불) — `BaseStructure` 선택 시 Info Panel의 취소 버튼/단축키(T)
- [x] 건설 현장으로 이동 중(아직 `BaseStructure` 생성 전) 다른 명령으로 취소되거나 담당 일꾼이 사망해도 건물 가격 전액 환불
- [x] 생산 건물이 파괴됐을 때 대기열에 남은 유닛 전체 환불
- [x] 완공 시에만 인구수 한도 반영, 파괴 시 반환(200 상한)
- [x] 건물 이동(리프트) — 공중으로 띄워 그리드 점유 해제 후 자유 이동/착륙 위치 지정, 이동 중에도 발밑 지형을 따라가는 지형 추적 비행 + 메쉬 피벗 오프셋 보정으로 이륙/이동/착륙 전 구간 고도 기준 일치, 공중 상태에선 생산·커맨드 잠김(Land/Move만 노출), 생산 대기열이 있으면 이륙 차단 (아래 "건물 이동(리프트) 시스템" 참고)
- [x] `BaseStructure`(건설 중 건물 기반) 크기가 실제 건물의 그리드 칸 수(2x2/3x3)에 맞춰 자동 스케일 — 2x2 건물(SupplyDepot/Lab)에 3x3 기반이 튀어나오던 문제 해결

### 자원 / 인구수
- [x] `ResourceManager`로 광물/가스/인구수 중앙 관리, 변경 이벤트로 상단 UI 자동 갱신
- [x] 유닛 생산 시 `ResourceManager.TrySpend`로 자원·인구수 소모, 대기열 가득 참/자원 부족/인구수 부족 시 콘솔 로그로 사유 표시
- [x] 건물 배치 시 자원 소모 연결 — `PlacementSystem.PlaceStructure()`가 `TryConstructBuilding`으로 자원 확인 후 차감
- [x] 인구수 한도 200 상한(`ResourceManager.maxPopulationCap`)
- [x] 유닛 사망 시 인구수 반환(`RTSUnitController.ReleaseUnitPopulation`) — 생산 취소(광물/가스+인구수 전액 환불)와 별개로, 이미 생산된 유닛이 죽을 때는 인구수만 반환
- [x] 인구수 한도 초과분 누적치 보존(`ResourceManager.rawMaxPopulation`) — 캡(200)보다 많이 지어도 내부 누적치는 그대로 유지, 일부가 파괴돼도 남은 누적치가 캡을 넘으면 표시 한도는 캡 값 그대로 유지

### 이펙트 / 모션 연출
- [x] 공격(총구) / 이동(트레일) / 피격(공격 타입별 4종: 총기·폭발·레이저·화염) / 사망 이펙트 — `UnitEffects`, 공용 헬퍼 `EffectPlayer`
- [x] 건물 이착륙 이펙트 — `BuildingEffects`
- [x] 건설 진행 중 지속 이펙트, 완공 순간 이펙트 — `ConstructionEffects`
- [x] 건물/건설중 파운데이션(`BaseStructure`) 피격·파괴(전투로 파괴 시에만, 취소 버튼과는 구분) 이펙트 — `BuildingEffects`/`ConstructionEffects`
- [x] 이동 트레일의 부자연스러운 급회전 보정 — `TrailRotationFollower`(위치는 매 프레임 추적, 회전은 Slerp로 서서히 추적 + 급회전 중 크기/방출량 축소)
- [x] 공중 유닛/리프트 중인 건물 호버링(둥실거림) 애니메이션 — `HoverBob`(DOTween)
- [x] 지상 차량 유닛 이동 중 흔들림 애니메이션 — `VehicleShake`(DOTween)
- [x] 레이더 접시/터렛 등 지속 회전 연출 — `AutoRotate`(DOTween)
- [x] 마우스 커서 상태 전환(기본/선택/이동/공격) — `UserControl`
- [x] ESC로 대기 중인 명령(공격/이동/순찰/랠리/건물이동) 취소 — `UserControl`

### 점령 / 영토
- [x] 거점 점령 시스템(`CaptureSystem`) — 트리거 범위 내 아군/적 유닛 수에 따라 점령치 밀당, 양쪽 다 있으면 교착, Ally↔Neutral↔Enemy 3단계 순환(항상 Neutral 경유), 진행 중일 때만 점령바 노출, 인스펙터 `debugOwner`로 테스트 가능
- [x] 점령 진행치 감쇠/재점령 시스템 — 완전 점령된 거점을 되돌리려면 1배속으로 30초(중립까지)+30초(내 것까지), 그 30초 동안은 계속 이전 소유자 색으로 보이다가 정확히 중립을 지나는 순간에만 전환(`Owner`가 경계 통과 시에만 sticky하게 바뀜). 한 번도 완전 점령된 적 없는 상태(중립 진행중)에서 반대 진영 진행치를 지우는 중이면 자연 감쇠+미는 힘이 겹쳐 2배속. 방치 시 완전 점령 거점은 원래 소유자 쪽으로 서서히 회복, 중립 진행중이던 거점은 0으로 줄어들며 바가 자동으로 사라짐 — 슬라이더 값은 절댓값 하나로 통일해 "꽉 참→줄어듦→반대쪽으로 다시 차오름"이 끊김없이 이어짐
- [x] 점령 타이머 슬라이더도 안개에 가려진 위치면 표시 안 함(`FogVisibility`)
- [x] 다각형 영토(`TerritoryZone`) — 인스펙터 핀 개수 조절만으로 자동 생성/정리, 오목 다각형 판정 가능, 소유자별 외곽선 색 자동 전환(흰/초록/빨강), 여러 영토 등록/질의(`TerritoryManager`, 겹치면 합집합)
- [x] 영토 기반 게임플레이 제한 — 건물 배치는 그리드 칸 전부가 아군 영토 안일 때만 가능(프리뷰 자동 빨간색), 자원 채취는 영토 밖 노드 신규 채취 불가 + 채취 중 영토 상실 시 즉시 중단, 유닛 생산은 영토 밖이면 대기열 유지한 채 타이머만 정지, 건설 진행(`BaseStructure`)도 영토 밖이면 일시정지

### 전장의 안개 (Fog of War)
- [x] 3rd-party 플러그인 `csFogWar`(`AssetFolder/AOSFogWar/`) 연동 — `FogRevealerAgent`를 유닛/건물 프리팹에 부착해 시야 소스로 등록/해제(기존 컨트롤러는 안 건드리는 어댑터 방식)
- [x] 아군이 점령한 `TerritoryZone` 내부는 시야 소스가 없어도 항상 밝게 강제 반영(`TerritoryFogReveal`), 점령이 풀리면 자연히 다시 안개가 낌
- [x] 유닛/건물 전체(9종+6종) 및 `BaseStructure`(건설 중 건물)까지 시야 소스로 연결 완료

### 그래픽 / 비주얼
- [x] URP Volume 포스트프로세싱 — Bloom(붉은끼 tint), Color Adjustments(대비/노출 보정), Tonemapping은 현재 None
- [x] Screen Space Ambient Occlusion(SSAO) — URP Renderer Feature로 적용
- [x] 빌드 프리뷰 고스트/셀 커서/유닛 이동·공격 명령 포인터는 전용 레이어(`Indicators`) + 오버레이 카메라(`Indicator Camera`, Depth Only + PostProcessing 끔)로 분리해 포스트프로세싱(Bloom/Color Adjustments)이 적용되지 않도록 처리
- [x] 3rd-party 유닛/건물/자연 모델링 에셋 임포트 — Canopus-III Low-Poly Sci-Fi Desert Units Set, Yoge Stylized Nature, Animated Sun Skybox, TZ_Futuristic Panel Textures Lite, LowPolyWater_Pack, 전부 Built-in RP 셰이더로 제작돼 있던 것을 URP(Lit/Unlit)로 변환해 마젠타/핑크 깨짐 해결(게임플레이 유닛 프리팹은 아직 전부 기본 프리미티브 메시, 건물 프리팹은 병영에 실제 모델 1개 적용 시작 — 나머지 적용은 로드맵)
- [x] 캠페인 스테이지 0~5 맵 전체 제작 완료 — `Assets/Scenes/Missions/Mission0`~`Mission5` 각 씬이 대응하는 `Maps/Mission0`~`Mission5` 프리팹(YuME 타일맵 기반, `Layer1`/`Layer2` 태그로 언덕 단 구분) 사용, 초기 프로토타입이었던 `TestScene`/`SampleScene`은 대체되어 현재 미사용에 가까움

### UI
- [x] 커맨드 패널(선택 상태별 버튼 자동 전환)
- [x] Info Panel(아이콘/이름/체력, 공격 아이콘 호버 시 공격타입·공격력(xN 배수), 방어 아이콘 호버 시 방어력·장갑타입·크기), `BaseStructure` 선택 시 전용 Info Panel(공격력/방어력 숨김)
- [x] Squad Panel(다중 선택 부대 표시, 개별 클릭 시 단일 선택 전환)
- [x] Squad Panel 페이지네이션 — 12마리 × 5페이지, 최대 60마리, 필요한 페이지 버튼만 노출
- [x] 커맨드/생산 버튼 호버 툴팁(`TooltipUI`), 제목만 있을 때 배경 크기 자동 축소(컴팩트 모드)
- [x] 미니맵 + 미니맵 클릭 시 카메라 이동
- [x] 미니맵 시야 사각형 표시(`MinimapViewIndicator`) — 메인 카메라가 보고 있는 지면 영역을 반투명 사각형으로 표시, 줌/회전에 따라 매 프레임 자동 갱신 + 미니맵 밖으로 안 나가게 클리핑
- [x] 미니맵으로 유닛/건물에 명령 — 대기 중인 명령(A공격 등)이 있으면 미니맵 클릭으로 그 자리에 확정, 우클릭 시 선택된 유닛/건물에 일반 우클릭과 동일한 명령(이동/랠리) 실행. 미니맵 클릭은 실제 지형 콜라이더에 레이캐스트해서 지면 높이를 구함(메인 화면 클릭과 동일 방식)
- [x] 미니맵 색상 마커 — 아군 유닛(초록 원)/적 유닛(빨간 원)/아군 건물(초록 사각형)/적 건물(빨간 사각형) 전용 스프라이트를 유닛/건물 머리 위(Y40~50대)에 배치, 안개(`csFogWar`)가 실제 3D Plane(Y≈1)이라 이렇게 높이 뜬 오브젝트는 깊이 테스트로 안 가려지는 문제가 있어 `FogVisibility`로 안개 상태를 직접 조회해 켜고 끔(적 유닛/건물만 해당 — 아군은 자기 시야로 항상 보임)
- [x] 공격받으면 미니맵에 표시 — 아군 유닛/건물이 적에게 공격받아 경고음이 실제로 재생되는 순간(10초 쿨다운을 통과했을 때)에만 공격받은 위치 Y=40에 3D 마커(`Attacked_MiniMapPointer`)가 뜨고 3초 뒤 자동 사라짐(`MinimapAlertController`)
- [x] 선택한 적이 안개 속으로 들어가면 선택 자동 해제(유닛/건물 모두) — 마커 토글과 같은 안개 조회 결과를 공유해서 매 프레임 중복 조회 안 함
- [x] UI가 인게임 마우스 클릭을 관통하도록 — Unity 내장 `Graphic.raycastTarget`을 끄면 되는 기능(새 코드 불필요), 0스테이지 미션 목표 텍스트(`StageObject`)에 적용해서 그 위를 클릭해도 게임 월드 클릭이 그대로 통과
- [x] 카메라 이동/확대 — 지형 티어(태그 기반) 감지로 언덕마다 줌 범위·현재 높이 자동 보정
- [x] 상단 자원 UI(광물/가스/인구수 실시간 표시)
- [x] 메인 화면(`MainScene`) — Play/Option/Exit 메인 메뉴, Option 패널에 사운드 설정 연결(`MainMenuController`)
- [x] 커맨드 패널 버튼별 키보드 단축키 + 눌림 시각 효과(아래 "키보드 단축키" 참고)
- [x] 유닛/건물별 체력바 UI — `HealthManager`의 `Slider` 필드가 체력 변화에 맞춰 자동 갱신, `HealthBarBillboard`로 카메라의 X(피치)만 따라 회전(Y/Z 고정)
- [x] 부대지정 단축키(컨트롤 그룹) — `Ctrl+숫자` 저장, `Shift+숫자` 병합 추가, 숫자만 눌러 선택
- [x] 미니맵에 자원 노드(광물/가스) 표시
- [x] 유닛/건물 아이콘 이미지 개선

### 사운드
- [x] `SoundManager` 싱글턴 — 주음량/배경음악/효과음/음성 4개 카테고리 볼륨·뮤트 관리, `PlayerPrefs` 영속화(설정 UI가 실제로 값을 저장하기 전까지는 인스펙터 기본값 유지), `AudioSource` 풀 순환 재사용(SFX 16개/Voice 4개)
- [x] BGM — 곡 목록 중 매 판마다 랜덤 1곡, 끝나면 다시 랜덤 무한 반복(직전 곡 연속 방지)
- [x] 유닛/건물 종류별 `SoundBank` 에셋(`UnitSoundBankSO`/`BuildingSoundBankSO`) — 코드 수정 없이 유닛/건물마다 공격/생성/사망/스킬/채취/건설/파괴 SFX와 선택/명령/생성/사망 Voice를 개별 지정, 유닛에 안 묶이는 나레이션은 `GlobalVoiceBankSO`(자원·인구 부족, 피격 경고, 업그레이드 완료)
- [x] 3D 위치 기반 SFX — 카메라 거리에 따라 감쇠(전투/근접 SFX는 가까이 있을 때만 들림), 선택/명령 확인음과 대사(Voice)는 항상 2D로 또렷하게
- [x] 명령/선택 확인음 전용 단일 채널 — 재생 중이면 새 요청을 버리고 끝난 뒤부터 재생(연속 명령 시 소리가 겹치지 않음)
- [x] 동시다발 SFX/Voice 스팸 방지 — 같은 사운드가 짧은 시간 내 재요청되면 무시(최소 재생 간격), 동시 재생 개수 상한 초과 시 무시(여러 유닛이 한 프레임에 공격/사망해도 소리가 무제한으로 안 겹침)
- [x] "적에게 공격받음" 경고음이 아군사격(오인사격)에는 울리지 않음 — 공격자 진영 정보(`isEnemyAttacker`)가 데미지 이벤트를 타고 끝까지 전달됨
- [x] 볼륨 슬라이더/뮤트 토글 UI(`SoundSettingsPanel`) — `SoundManager` API 연결 + `MainScene`의 "Option" 패널에 실제 배치 완료
- [x] 유닛별 사망 사운드
- [x] 건물 이륙/착륙 사운드
- [x] 건물 파괴 사운드

### 캠페인 / 미션
- [x] 캠페인 기획 및 스테이지 0~5 구성 — `Assets/Scenes/Missions/Mission0`~`Mission5`, 각 스테이지 맵 꾸미기 완료
- [x] 스테이지별 목표 오브젝트 스크립트(`Stage0Objectives`~`Stage5Objectives`) — 주목표/서브목표를 스테이지마다 구성. 유물/데이터 확보처럼 트리거 기반으로 획득/운반해 완료하는 목표, 적 건물 전멸처럼 리스트 변화가 있을 때만(매 프레임이 아니라) 갱신되는 목표, 오브젝트 파괴 여부를 구독해 완료/실패를 가르는 목표 등 목표 성격에 맞는 감지 방식을 각각 사용
- [x] 미션 선택 화면(`MissionSelectManager`) — 스테이지 0~5 버튼, 마우스 호버 시 미션 이름(번역 지원 + `<이름>` 장식) 툴팁, 미해금 스테이지 잠금 처리, 메인화면으로 돌아가기
- [x] 씬 간 이동 연결 — 메인화면 → 미션 선택 → 각 스테이지, 스테이지 내 옵션 패널에서 이전/다음 미션 이동 및 메인화면 복귀
- [x] 외계종족(스포어 브루드) 신규 진영 구현 — 유닛 3종(Ripfang/Spitter/Skitterwing) + 건물 3종(Hive Core/Spawning Pit/Bio-Reactor)
- [x] 아군 OC(구조 가능한 유닛) 구현 — NTA(플레이어) 유닛의 강제공격 대상은 되지만 자동 공격 대상으로는 인식되지 않음, `EnemyUnitController`/`EnemyBuildingController`와 별개로 `AllyController`/`AllyBuildingController`/`AllyAttackRange`가 피아식별만 반대로 동일 로직을 담당(doc/0452)
- [x] 버전/변경 이력 관리 — `doc/0001-`부터 번호순으로 세션별 요청·코드 변경 내역을 전부 기록(사실상의 패치노트 로그, 아래 "개발 프로세스 메모" 참고)

### 로컬라이제이션(다국어)
- [x] 영어/한글 텍스트를 `Assets/Resources/Localization/en.json`·`ko.json`로 외부화, `LocalizationManager` 싱글턴이 현재 언어에 맞는 JSON을 읽어 조회 제공
- [x] 정적 UI 라벨(스크립트로 갱신되지 않는 버튼/텍스트)도 `LocalizedText` 컴포넌트로 자동 번역
- [x] 메인화면 EN/KR 버튼으로 즉시 언어 전환 — `PlayerPrefs`로 씬을 넘어가도 선택한 언어 유지, 버튼 클릭 즉시(오브젝트를 껐다 켤 필요 없이) 화면의 모든 텍스트가 갱신됨
- [x] 유닛/건물 이름·설명(생산 버튼 툴팁 + Info Panel 설명) 번역 — `unit.<진영>.<ID>.name/desc/info`, `building.<진영>.<ID>.name/desc/info` 키 체계, 번역 누락/매니저 없음/조회 예외 시 `ScriptableObject`에 적힌 원문을 그대로 표시하는 안전장치 포함(`LocalizationManager.GetTextOrFallback`)
- [x] 미션 선택 화면의 미션명도 번역 지원
- [x] 미션 오브젝트(유물/연구 데이터베이스) 이름·설명 번역 — `missionitem.<id>.name/desc` 키 체계, Info Panel에 설명까지 표시(doc/0490)
- [x] 자원 노드(광물/가스) 명칭·설명 번역 — 기존 "Ore"/"Gas"를 세계관에 맞게 "아이로나이트 광석(Ironite Ore)"/"페트로나이트(Petronite)"로 재정의(doc/0493), `resource.ore/gas.name/desc` 키 체계로 Info Panel에 이름+로어 설명 표시(doc/0494)

## 로드맵 (미구현)

- [ ] Enemy AI 구현(스크립트로 동작하는 진짜 "적 지휘관") — 지금은 적 유닛/건물이 씬에 미리 배치되고 `EnemyUnitController`의 기초 AI(사거리 내 자동 교전/이동/공격-이동)만 동작함. 시간에 맞춰 공격 병력을 모아 보내는 타이머 기반 웨이브(예: 5/10/15분 간격), 점령지에 별동대를 보내 탈환을 노리는 로직 등 전략적 판단을 내리는 AI 디렉터가 필요 — `EnemyController`(적 진영 조종 스크립트)와 아군 OC 조종 스크립트 별도 제작 예정
- [ ] 외계종족 전용 공격/사망 이펙트 — 현재는 기존 이펙트를 재사용 중, 외계종족만의 별도 비주얼 준비 예정
- [ ] 서브 스테이지 구성 — 메인 스테이지 0~5 사이/주변에 약 4개 서브 스테이지 기획 및 구현 예정(기획 단계)
- [ ] 스테이지 사이 브리핑룸 구현 — 미션 시작 전 현재 상황(스토리)/목표를 텍스트+음성으로 안내하고 캐릭터 얼굴을 보여주는 연출
- [ ] 건물 고유 스킬 추가 — 유닛 고급 특성(스킬 선택)처럼 건물 전용 고유 스킬은 아직 기획/구현 전
- [ ] 건물 선택 사운드 — 유닛별 사망/건물 이착륙·파괴 사운드는 완료, 건물 선택 사운드만 남음
- [ ] UI 디자인 개선 — 버튼 하단 이미지 등 전반적인 UI 비주얼 개선
- [ ] 1대1(플레이어 vs AI 진영) 대전용 AI — 위 Enemy AI와는 별도로 구상 필요
- [ ] 사망 시 래그돌/사망 애니메이션 — 현재는 사망 즉시 `Destroy(gameObject)` + 파티클 스폰만 지원(옵션 A), 오브젝트를 유지한 채 애니메이션 재생 후 지연 파괴하는 구조(옵션 B, doc/0105 3.5절)는 미구현
- [ ] `AttackRange`의 자동 사거리 탐지가 `BaseStructure`(건설 중인 건물)를 대상으로 삼는 경로 — 현재는 A 모드 강제 공격(오인사격 포함)으로만 공격 가능하고, 자동 교전 대상에는 포함되지 않음
- [ ] 지원기(Support Ship) 유닛 — 공격 없이 주변 아군 버프를 주는 티어3 유닛으로 구상 중, 프리팹/SO 데이터/버프 시스템 전부 미착수
- [ ] 점령지(거점) 미니맵 마커 — 유닛/건물은 미니맵에 원/사각형으로 표시되지만, 거점(`CaptureSystem`)은 아직 전용 미니맵 마커(노란 원 예정)가 없음
- [ ] Pretendard 다른 폰트 웨이트 추가 — 현재 Black 웨이트 SDF 폰트 애셋 하나만 생성돼 있어 전체 텍스트에 통일 적용 중, Regular 등 다른 웨이트가 필요해지면 Font Asset Creator로 추가 생성 필요

## UI 설계 노트 (기획 원문 정리)

패널을 미리 생성해두고, 선택 상태에 따라 버튼 데이터(아이콘/콜백)만 채우거나 비우는 방식 (`UIController.SetCommands` 등).

- 일꾼: 이동 / 공격 / 정지 / 순찰 / 홀드 / 복귀 / 건설
- 공격 유닛: 이동 / 공격 / 정지 / 순찰 / 홀드
- 건설 모드: 사령부 / 보급고 / 병영 / 공장 / 공항 / 연구소
- 메인기지(사령부): 일꾼 생산
- 티어1(병영): 마린, 파벳(Vulture)
- 티어2(공장): 벌처, 탱크, 골리앗
- 티어3(공항): 레이스, 가디언

## 유닛 분류 (기획 원문 정리)

| 분류 | 가능 명령 |
| --- | --- |
| 일꾼 | 채광, 건설, 이동, 공격, 정지, 홀드, 순찰 |
| 지상 전투 유닛 | 이동, 공격, 정지, 홀드, 순찰 |
| 공중 전투 유닛 | 이동, 공격, 정지, 홀드, 순찰 (NavMesh 미사용, 지형 무시 이동) |

## 일꾼 채취 로직

1. 자원 우클릭 → 자원 위치로 이동
2. 도착 후 대기열(`ResourceNode.workerQueue`, 기본 정원 `waitWorkerCount = 2`, 인스펙터로 조절 가능) 확인
   - 자리가 있으면 등록 후 자기 차례(맨 앞)가 될 때까지 대기
   - 꽉 찼으면 자신 기준 반경 10(`alternateResourceSearchRadius`) 내에서 더 한가한 다른 자원을 우선 탐색, 없으면 그냥 이 노드 대기열에 줄을 섬
3. 차례가 되면 채취(`gatherDuration`초) 후 자원을 들고 최근접 **메인기지**로 이동, 반납 (광물/가스 공통, 다른 건물엔 반납하지 않음)
4. 다른 명령이 들어와 채취가 중단되면 대기열에서 즉시 제외
5. 반납 후 원래 캐던 노드가 남아있으면 복귀, 고갈됐거나 반납할 메인기지가 없으면 반경 10 내 다른 자원으로 재이동을 무한 반복

## 건설 진행 시스템

1. 건설모드에서 건물 종류를 고르고 유효한 위치를 클릭하면, 그 즉시 그리드 셀을 예약하고(다른 곳에 겹쳐 짓지 못하게) `TryConstructBuilding`으로 자원/인구수를 확인 후 차감, 선택돼 있던 일꾼을 그 자리로 보내고 건설모드는 바로 종료됩니다. 클릭한 자리엔 일꾼이 도착할 때까지 정적 고스트가 남습니다.
2. 일꾼이 도착하면 고스트가 사라지고 `BaseStructure`(건물 기반)가 생성되어 일꾼이 붙습니다. 이 순간부터 일꾼은 `건물이 완공될 때까지 다른 명령을 받지 않습니다.`
3. `BaseStructure`는 담당 일꾼이 붙어있는 동안에만 건설시간이 줄어들고 체력이 (완공될 건물의 최대체력 ÷ 건설시간)만큼 초당 차오릅니다. 담당 일꾼이 없으면(죽었을 때 등) 건설이 자동으로 일시정지됩니다.
4. 건설이 일시정지된 `BaseStructure`를 다른 일꾼으로 우클릭하면, 그 일꾼이 이동해 붙어서 건설을 재개합니다(콜라이더 표면의 가장 가까운 지점으로 이동 — `BaseStructure`에 `NavMeshObstacle`이 있어 중심점 자체엔 도달할 수 없기 때문).
5. `BaseStructure`를 좌클릭으로 선택하면 마커가 켜지고 Info Panel에 아이콘/이름/체력(차오르는 값, 공격력·방어력은 숨김)이 표시되며, "Cancel" 버튼/단축키(T)로 언제든 건설을 취소할 수 있습니다 — 취소(또는 파괴) 시 건물 가격 전액이 환불되고, 담당 일꾼은 해제되어 다시 명령을 받을 수 있는 상태로 돌아갑니다.
6. 건설시간이 다 되면 완공될 건물이 실제로 생성되고(프리팹 높이에 맞춰 자동으로 지면에 정렬), 그 건물이 제공하는 인구수 한도가 반영된 뒤 `BaseStructure`는 스스로 파괴됩니다. 건설 중 입은 피해가 있었다면 그 체력값이 완공된 건물에도 그대로 이어집니다.

## 건물 이동(리프트) 시스템

1. 리프트 가능한 건물(`BuildingController.canLift`, 기본값 켜짐)을 선택하면 커맨드 패널 마지막 슬롯에 "Lift Off" 버튼(단축키 `L`)이 뜹니다. 누르면 그리드에서 자신의 위치를 즉시 해제하고 공중유닛과 같은 방식(직접 좌표 보간)으로 수직 상승합니다(기본 `liftHeight` 5, 건물 메쉬의 피벗-지면 오프셋까지 반영).
2. 공중에 뜬 동안엔 해당 건물의 생산/커맨드 패널이 전부 잠기고 "Land"(단축키 `L`) + "Move"(단축키 `M`) 버튼만 노출됩니다. 생산 대기열에 유닛이 남아있으면 애초에 이륙 자체가 차단됩니다.
3. **자유 이동**: 공중유닛처럼 우클릭(또는 Move 버튼 → 좌클릭)으로 목적지를 지정하면 그 지점 상공으로 수평 이동합니다. 이동 중에도 매 프레임 발밑 지형(`groundLayer`)을 재측정해 "지형 높이 + 피벗 오프셋 + liftHeight"를 목표 고도로 삼으므로, 언덕 능선을 실제로 벗어나는 순간에 맞춰 고도가 자연스럽게 오르내립니다(이륙 시점과 이동 중 고도 기준이 항상 일치).
4. **착륙**: Land 버튼을 누르면 착륙 위치 프리뷰가 뜨고, 유효한 자리를 클릭하면 그 즉시 그리드가 예약되고 클릭한 자리에 고정 고스트가 남습니다. 건물은 그 지점 상공까지 수평 이동한 뒤 수직 하강해 착륙하고, 그리드에 새 위치로 재등록되며 지상 상태로 돌아갑니다.
5. 착륙 위치로 비행 중(또는 자유 이동 중) 건물이 파괴되면 예약된 그리드 셀과 고스트가 자동으로 정리됩니다.
6. 다른 공중유닛/이륙한 건물을 따라가거나 강제 공격할 때는 그 대상의 "이미 공중에 뜬" 좌표에 고도를 또 더하지 않아서, 대상 머리 위로 솟구치는 고도 중첩 현상이 발생하지 않습니다.

## 부대 지정(컨트롤 그룹)

- `Ctrl + 숫자(1~9,0)`: 현재 선택된 유닛/건물을 그 번호의 부대로 저장(기존 내용 덮어씀). 아무것도 선택하지 않은 채로 누르면 무시됩니다.
- `Shift + 숫자(1~9,0)`: 현재 선택 중 그 부대에 아직 없는 대상만 추가(기존 멤버 유지) — 한 유닛이 여러 부대에 동시에 속할 수 있습니다.
- 숫자만 누르면: 저장된 부대를 선택 상태로 복원합니다. 그사이 죽거나 파괴된 대상은 자동으로 걸러지고, 부대가 완전히 비었으면 기존 선택을 그대로 둡니다.
- 부대를 숫자로 다시 선택하면, 대기 중이던 공격/이동/순찰(A/M/P) 명령 모드는 자동으로 취소되고 마우스를 따라다니던 포인터 마커도 함께 사라집니다(랠리/건물 이동 대기 모드는 영향받지 않음).
- 적/자원/건설 중인 구조체(`BaseStructure`)는 부대 지정 대상에서 제외됩니다.

## 미니맵 범례

| 대상 | 표시 |
| --- | --- |
| 아군 유닛 | 초록색 원 |
| 적 유닛 | 빨간색 원 |
| 아군 건물 | 초록색 사각형 |
| 적 건물 | 빨간색 사각형 |
| 공격받은 위치 | 3초간 표시되는 마커(경고음이 실제로 재생될 때만, 10초 쿨다운) |
| 점령지(거점) | 미구현 — 노란색 원으로 표시할 계획(로드맵 참고) |

유닛/건물 머리 위 Y40~50대에 전용 스프라이트(레이어 `Unit`/`Enemy`)를 배치하는 방식. 안개(`csFogWar`)가
실제 3D Plane(Y≈1)이라 이렇게 높이 뜬 오브젝트는 깊이 테스트로는 안 가려져서, `FogVisibility` 헬퍼로
안개 상태를 직접 조회해 렌더러를 켜고 끈다(적 유닛/건물만 - 아군은 자기 시야로 항상 보임).

## 키보드 단축키

버튼(`ProductionSlot`)이 자기 단축키를 직접 감지해서(`Update()`) 눌리면 실제 마우스 클릭과 동일한 `PointerDown/Up/Click` 이벤트를 재현합니다 — 명령 실행과 버튼 눌림 시각 효과(기존 Transition 색상/스프라이트)가 동시에 처리됩니다. 슬롯이 비활성 상태(해당 패널이 안 떠 있음)면 `Update()` 자체가 호출되지 않아 단축키도 자동으로 죽어있습니다.

| 상황 | 단축키 |
| --- | --- |
| 유닛 명령(Worker/AttackUnit 공통) | 공격 A · 이동 M · 정지 S · 순찰 P · 홀드 H |
| 유닛 명령(Worker 전용) | 자원 반환 R · 건설모드 진입 B |
| 건설모드(건물 선택) | 사령부 C · 보급고 S · 병영 B · 공장 F · 우주공항 P · 연구소 L · 건설모드 나가기 T |
| 생산(메인기지) | 일꾼 W |
| 생산(병영) | 어썰트 트루퍼 A · 스카웃 드론 S · Sharpshooter S (스카웃 드론과 단축키 중복 — 조정 필요) |
| 생산(공장) | 레인저 IFV I · 펄스탱크 P · SkyLancer S |
| 생산(공항) | 파이어호크 F · 가디언 드론 D |
| `BaseStructure` 선택 시 | 건설 취소(환불) T |
| 생산 건물 선택(MainBase/Tier1/Tier2/Tier3) | 랠리(집결지 지정) Y — 슬롯 6, 클릭 후 위치 클릭 시 확정(건물 우클릭과 동일 동작) |
| 건물 선택(지상) | 리프트(이륙) L |
| 건물 선택(공중) | 착륙 L · 이동(자유 비행) M |
| 유닛/건물 선택 시(공통) | 부대 지정 저장 Ctrl+숫자(1~9,0) · 병합 추가 Shift+숫자(1~9,0) · 부대 선택 숫자만 |

같은 키라도 서로 다른 선택 상태(예: 유닛 선택 중의 A=공격, 병영 생산 중의 A=어썰트 트루퍼)에서는 절대 동시에 활성화되지 않으므로 문제없이 재사용됩니다.

## 이전작(네오테크워즈)과 다른 점

- [x] 공중 유닛 구현
- [x] 생산 대기열 버그 해결
- [x] 렐리 포인트 문제 해결
- [x] 선택한 유닛 보여주기(Info Panel)
- [x] 부대 단위 보여주기(Squad Panel, 페이지네이션까지 확장)
- [ ] UI 버튼 하단 이미지 등 UI 디자인 개선

## 해결된 이슈

- **좁은 언덕 경사로에서 유닛 들썩거림**: NavMeshSurface가 좁은 경사로를 구울 때 미세한 굴곡이 생겨 유닛이 물리듯 떨리던 문제 — 코드가 아니라 경사로 지형 크기 조정으로 해결됨.
- **3rd-party 에셋 머티리얼 마젠타/핑크 깨짐**: Canopus-III(desert units), Yoge(Stylized Nature) 에셋이 Built-in RP 전용 셰이더로 제작돼 URP 프로젝트에서 렌더 에러 색으로 보이던 문제 — 총 40개 `.mat`을 URP Lit 셰이더로 변환해 해결.
- **셀 커서(cellIndicator)가 건물 가운데에 떠 보임**: 건물 높이만큼 이미 보정된 프리뷰 좌표를 셀 커서에도 그대로 재사용해서 생긴 문제 — 셀 커서는 순수 지면 좌표만 쓰도록 분리.
- **공중 유닛이 다른 공중유닛/건물을 따라갈 때 머리 위로 솟구침**: 이미 공중 고도가 반영된 좌표에 다시 고도를 더해서 생긴 고도 중첩 버그 — 목적지가 이미 공중인지 판별해서 중복으로 더하지 않도록 수정.
- **Follow(따라가기) 중 서로 밀어붙이거나 겹친 채로 멈추지 않음**: 거리 조건 없이 매 프레임 정확한 좌표로 재이동을 지시하던 것이 원인 — 유닛 크기(반경) 비례 정지 거리 도입으로 해결.
- **지형 추적 비행 도입 후 착륙이 안 됨**: 도착 판정이 매 프레임 갱신되는 실측 지형 목표가 아니라 미리 계산해둔 옛 목표값과 비교하고 있었던 버그 — 실시간 목표값과 비교하도록 통일해 해결.
- **`groundLayer`/`airGroundLayer`가 일부 프리팹에서 동작하지 않음**: LayerMask를 스칼라 정수로 잘못 직렬화해 Unity가 빈 값으로 인식하던 문제 — 올바른 구조체 포맷으로 재작성.
- **Lab 체력바가 실제 체력과 무관하게 움직임**: `HealthManager.healthSlider` 연결 누락 + 체력바 슬라이더가 마우스로 드래그 가능한 상태였던 두 문제가 겹친 것 — 연결 추가 + 슬라이더 `Interactable` 비활성화로 해결.
- **인구수 한도(200) 초과분이 보급고 파괴 시 통째로 사라짐**: 캡이 이미 적용된 값을 필드에 그대로 저장해 "캡을 넘겨 지었다"는 정보 자체가 소실되던 문제 — 캡 미적용 누적치(`rawMaxPopulation`)를 별도로 유지하고, 노출/판정 시점에만 캡을 씌우도록 수정.
- **지속형 파티클(이동 트레일 등)이 반복 재생 도중 여러 번 겹쳐 재생됨**: looping이 꺼진 파티클을 지속형(부모 부착)으로 스폰하면 자기 duration만큼만 방출하고 멈춰버리던 문제 — 지속형 스폰 시 loop 강제 on, 발사 후 잊기 스폰 시 loop 강제 off로 용도별 분리.
- **이동 트레일이 급회전 시 부자연스럽게 홱 돌거나, 이동 중간에 멈춤**: 부모-자식으로 직접 붙이면 회전이 매 프레임 즉시 동기화되던 문제 — `TrailRotationFollower`로 위치는 추적, 회전만 Slerp로 분리해 해결(관련 세부 버그는 doc/0112~0113 참고).
- **건물 배치 프리뷰 Y좌표가 실제 지형이 아니라 그리드 셀 크기로 스냅됨**: `GetGroundPosition`이 Y값을 `grid.CellToWorld`/`WorldToCell` 왕복으로 재계산한 게 원인 — 레이캐스트로 측정한 실제 지면 Y를 5개 호출부 전체에 파라미터로 그대로 전달하도록 수정.
- **`TerritoryZone` 외곽선이 플레이 모드에서 안 보임**: `LineRenderer`에 머티리얼이 비어있던 게 원인 — URP Unlit 셰이더를 런타임에 복제해 자동 생성.
- **`TerritoryZone` 핀이 플레이 모드 진입/종료 시 중복되거나 초기화됨**: 도메인 리로드 도중 Transform 참조가 복구되기 전에 `OnValidate`가 실행된 게 원인 — `EditorApplication.isPlayingOrWillChangePlaymode` 가드 추가.
- **`CaptureSystem`이 같은 오브젝트가 아닌 자식 오브젝트의 `TerritoryZone`을 못 찾음**: `GetComponent` → `GetComponentInChildren(true)`로 수정.
- **아군 강제공격(A모드) 중 근처 다른 적에게 타겟이 가로채짐**: `AttackRange.GetPreferredTarget()`이 `orderedTarget`이 null이면 `friendlyTarget` 여부를 안 보고 곧장 최근접 적으로 폴백하던 게 원인 — `friendlyTarget` 우선 확인 후 폴백하도록 수정.
- **건물 클릭 후 드래그하면 건물+유닛이 동시에 선택됨**: 좌클릭 선택이 클릭 즉시 실행돼 드래그 시작을 구분 못한 게 원인 — 모든 좌클릭 선택(유닛/적/건물/`BaseStructure`/자원)을 마우스 뗄 때로 통일 지연, Shift 없이 드래그박스 선택 시 기존 선택을 정상적으로 교체.
- **`TZ_Futuristic Panel Textures Lite`(15개 머티리얼), `LowPolyWater_Pack`(`IslandMat` + 커스텀 `WaterShaded` 수면 셰이더)도 마젠타로 깨짐**: 기존 Canopus/Yoge와 동일하게 Built-in RP 전용 셰이더였던 게 원인 — URP로 변환(`WaterShaded`는 죽은 코드였던 GrabPass도 함께 제거).

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.

## 개발 프로세스 메모

세션마다 사용자 요청과 변경 내역(코드가 바뀐 경우 변경 전/후 코드 포함)을 `doc/0001-...` 형식의 번호 매긴 마크다운 파일로 남깁니다. 특정 기능이 "왜" 지금 형태인지, 또는 정확히 어떤 코드가 바뀌었는지 궁금하면 `doc/` 폴더의 관련 번호 문서를 먼저 확인하세요. 번호는 세션이 바뀌어도 이어집니다. (`Docs/*.md`는 세션 로그가 아니라 스크립트별 코드 문서 전용입니다.)
