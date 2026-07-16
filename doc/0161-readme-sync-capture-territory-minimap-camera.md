# 0161 - README 갱신 (점령/영토 시스템, 미니맵 시야 사각형, 카메라 줌 지형 티어, 맵 복원, 버그픽스 7건) — 제안

**날짜:** 2026-07-17

## 요청 내용

> 현재 프로젝트를 분석하고 Readme 파일 갱신해줘 (+ 사용자가 파악하고 있는 TODO 리스트 첨부)

## 조사 내용

README는 마지막으로 `doc/0121`(2026-07-14)까지 반영되어 있었다. 그 이후 `doc/0122`~`doc/0160`까지 39개 세션이 진행됐고, 코드를 직접 읽고(`CaptureSystem.cs`, `TerritoryZone.cs`, `TerritoryManager.cs`, `MinimapViewIndicator.cs`, `Docs/MinimapViewIndicator.md`) 리서치 서브에이전트로 나머지 세션 로그를 교차검증한 결과, 아래 내용이 README에 전혀 반영되지 않은 상태였다.

1. **`Assets/Scripts/CaptureSystem/`(신규 폴더, README에 전혀 없음)**
   - `CaptureSystem`: 거점(비콘)에 부착. 트리거 범위 내 아군(`UnitController`)/적(`EnemyController`) 유닛 수를 각각 세서, 부호 있는 점령치(`controlValue`, 기본 ±30초)를 있는 쪽으로 밀고 당김. 양쪽 다 있으면 교착(진행 정지). 소유자(`Neutral`/`Ally`/`Enemy`)는 양 끝(±captureDuration)에 도달했을 때만 바뀌고, 항상 Neutral을 거쳐야 반대 진영으로 못 감(Ally↔Neutral↔Enemy 순환). 점령이 실제로 진행 중일 때만 점령바(`Slider`) 노출. 인스펙터에 `debugOwner` 드롭다운으로 테스트용 강제 전환 가능.
   - `TerritoryZone`: 인스펙터에서 `pinPoints` 리스트 Size만 늘리면 빈 슬롯에 핀 오브젝트가 자동 생성되고, 씬 뷰에서 핀을 옮기면 그 다각형(오목 다각형 포함)이 영토 범위가 됨(`Contains()`는 crossing-number 판정). 소유자에 따라 외곽선(`LineRenderer`, 런타임 복제 머티리얼) 색이 흰색/초록/빨강으로 자동 전환.
   - `TerritoryManager`: 씬의 모든 `TerritoryZone`을 등록해두고 "이 좌표가 특정 진영 영토 안인가?"를 질의(`IsInsideAlliedTerritory`), 여러 영토가 겹치면 합집합 취급.
   - **영토 기반 실제 게임플레이 제한**(설계문서 수준이 아니라 코드에 적용되어 있음, `doc/0141`/`0142`/`0144` 최종본 기준):
     - `PlacementSystem`: 건물이 차지할 그리드 칸 **전부**가 아군 영토 안이어야 배치 가능, 프리뷰 고스트가 영토 밖이면 자동으로 빨갛게 표시.
     - `UnitController.Gather()` / `FindNearestAvailableResourceNode()`: 아군 영토 밖 자원 노드는 신규 채취 명령 자체가 거부되고 대체 탐색에서도 제외. `doc/0144`에서 정책이 갱신되어, 채취 도중 영토를 상실하면(기존 "이번 왕복은 마저 끝냄" 대신) **즉시 채취를 중단**하도록 변경됨.
     - `UnitSpawner.Produce()`: 생산 건물이 아군 영토 밖이면 대기열은 유지한 채 타이머만 정지.
     - `BaseStructure.Update()`: 담당 일꾼이 있어도 영토 밖이면 건설 진행(체력 상승)이 동일하게 일시정지.
     - 참고: 시작 지점 메인기지가 "자기 영토" 취급되려면 `CaptureSystem`과 별개로 `Owner=Ally`로 고정된 `TerritoryZone`을 게임 시작 지점에 씬 배치해둬야 함(코드가 아니라 씬 셋업 문제, `doc/0141`).

2. **`MinimapViewIndicator`**(`Assets/Scripts/Camera/`, `Docs/MinimapViewIndicator.md` 있음) — 메인 카메라 화면 네 꼭짓점을 지면에 투영 후 미니맵 카메라 기준으로 역투영해서, 메인 카메라가 실제로 보고 있는 영역을 미니맵 위에 반투명 사각형으로 표시. 줌(카메라 높이)·Q/E 회전이 바뀌어도 매 프레임 자동 반영, 미니맵 이미지 경계 밖으로 나가지 않게 클리핑(`doc/0156`). **사용자 TODO의 "줌했을 때 미니맵 사각형 크기 조정" 항목이 실제로 구현되어 있음을 확인.**

3. **`CameraControl`의 지형 티어 기반 줌 보정** — `SampleTerrainTier()`가 `Layer1`/`Layer2` 태그를 레이캐스트+부모 탐색으로 읽어서, 지형 티어(언덕 단)가 바뀔 때마다 `tierZoomStep`(기본 5)만큼 줌 최소/최대 범위와 현재 카메라 높이를 함께 보정. 홈베이스 복귀(Space)도 `currentTerrainTier`를 리셋(`doc/0160` 버그픽스 포함).

4. **`Assets/Scripts/Animation/AutoRotate.cs`**(신규 스크립트) — 레이더 접시/터렛 헤드 등을 DOTween으로 조건 없이 지속 회전시키는 컴포넌트.

5. **맵(스테이지) 복원 완료** — `Assets/Scenes/TestScene.unity`가 `Assets/prefabs/Maps/Mission1.prefab`(YuME 타일맵 기반, `Layer1`/`Layer2` 태그로 언덕 단 구분)을 사용 중. `Mission2~5.prefab`도 파일은 존재하지만 아직 미사용. **로드맵의 "맵(스테이지) 제작" 항목은 완료로 이동해야 함.**

6. **미반영 버그픽스 7건** (해결된 이슈 절에 추가 대상):
   - 건물 배치 프리뷰 Y좌표가 실제 지형이 아니라 그리드 셀 크기로 스냅되던 버그 — `GetGroundPosition`이 Y를 `grid.CellToWorld`/`WorldToCell` 왕복으로 재계산한 게 원인, 실측 지면 Y를 5개 호출부에 파라미터로 그대로 전달하도록 수정.
   - `TerritoryZone` 외곽선이 플레이 모드에서 안 보이던 버그 — `LineRenderer`에 머티리얼이 비어있던 게 원인, URP Unlit 셰이더 런타임 복제로 해결.
   - `TerritoryZone` 핀이 플레이 모드 진입/종료 시 중복·초기화되던 버그 — 도메인 리로드 중 Transform 참조 복구 전에 `OnValidate`가 실행된 게 원인, `EditorApplication.isPlayingOrWillChangePlaymode` 가드로 해결.
   - `CaptureSystem`이 형제가 아닌 자식 오브젝트의 `TerritoryZone`을 못 찾던 버그 — `GetComponent` → `GetComponentInChildren(true)`.
   - 아군 강제공격(A모드) 중 근처 다른 적에게 타겟이 가로채지던 버그 — `AttackRange.GetPreferredTarget()`이 `orderedTarget`이 null일 때 `friendlyTarget` 여부를 안 보고 곧장 최근접 적으로 폴백한 게 원인, `friendlyTarget` 우선 확인으로 수정.
   - 건물 클릭 후 드래그하면 건물+유닛이 동시에 선택되던 버그 — 좌클릭 선택이 클릭 즉시 실행돼 드래그 시작과 구분 못한 게 원인, 모든 좌클릭 선택(유닛/적/건물/`BaseStructure`/자원)을 마우스 뗄 때로 통일 지연.
   - `TZ_Futuristic Panel Textures Lite`(15개 머티리얼), `LowPolyWater_Pack`(`IslandMat` + 커스텀 `WaterShaded` 셰이더)도 Built-in RP 전용이라 마젠타로 깨지던 문제 — 기존 Canopus/Yoge와 동일 패턴으로 URP 변환(`WaterShaded`는 죽은 코드였던 GrabPass도 함께 제거).

7. **미반영 대상 아님(제외 확인)**: 카메라 줌이 특정 상황에서 영구히 막히는 버그(`doc/0153`)는 원인 진단만 하고 사용자가 수정을 원치 않아 코드 변경 없이 종료됨 — README에 반영할 "변경"이 없으므로 이번 갱신에서 제외.

기존 doc/ 최신 번호는 0160 → 이 문서가 0161. [[confirm_before_implementing]]에 따라 아래는 계획안이며, `README.md`는 아직 수정하지 않았다.

## 계획한 코드(문서) 변경

### 1. 프로젝트 구조 트리 — `CaptureSystem/` 폴더 추가, `Scenes/`·`prefabs/` 설명 갱신

**기존 코드** (`README.md` 17~43번째 줄):
```
Assets/
├─ Scripts/
│  ├─ Animation/        # 공중유닛/리프트 건물 호버링(HoverBob), 지상 차량 이동 셰이크(VehicleShake) - DOTween 기반
│  ├─ Building/        # 건물 컨트롤러, 건설 중 건물 기반(BaseStructure)
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라/미니맵 이동·조작
│  ├─ Effects/          # 공격/이동/피격/사망/건물 이착륙/건설 이펙트 재생 시스템(EffectPlayer 등)
│  ├─ Enemy/            # 적 유닛 컨트롤러 (마커/스탯 데이터만, AI 로직은 미구현)
│  ├─ Resource/         # 자원 노드 및 자원 관리 (`ResourceController.cs`는 미사용 빈 스텁)
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러, 툴팁
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리, 마우스 커서 상태 전환
├─ Scenes/              # 게임 씬 (SampleScene 등)
├─ prefabs/             # 유닛/건물 프리팹 (`NTA/`, 현재는 기본 프리미티브 메시 사용)
├─ AssetFolder/         # 3rd-party 모델링/스카이박스 에셋 (Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox) — 임포트 + URP 머티리얼 변환 완료, 게임플레이 프리팹(`prefabs/NTA/`)에는 아직 미적용
├─ Material, Shader/    # 머티리얼 및 커스텀 셰이더
└─ Settings/            # URP 렌더 파이프라인 설정 + 포스트프로세싱 Volume Profile(Bloom/Color Adjustments/SSAO)
```

**변경 코드:**
```
Assets/
├─ Scripts/
│  ├─ Animation/        # 공중유닛/리프트 건물 호버링(HoverBob), 지상 차량 이동 셰이크(VehicleShake), 지속 회전(AutoRotate) - DOTween 기반
│  ├─ Building/        # 건물 컨트롤러, 건설 중 건물 기반(BaseStructure)
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라/미니맵 이동·조작, 지형 티어별 줌 범위 보정, 미니맵 시야 사각형 표시
│  ├─ CaptureSystem/    # 거점 점령(밀당식 Ally↔Neutral↔Enemy 순환) + 다각형 영토 판정(TerritoryZone/TerritoryManager)
│  ├─ Effects/          # 공격/이동/피격/사망/건물 이착륙/건설 이펙트 재생 시스템(EffectPlayer 등)
│  ├─ Enemy/            # 적 유닛 컨트롤러 (마커/스탯 데이터만, AI 로직은 미구현)
│  ├─ Resource/         # 자원 노드 및 자원 관리 (`ResourceController.cs`는 미사용 빈 스텁)
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러, 툴팁
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리, 마우스 커서 상태 전환
├─ Scenes/              # 게임 씬 (SampleScene, TestScene — TestScene이 1스테이지 맵(Mission1) 복원용 씬)
├─ prefabs/             # 유닛/건물 프리팹 (`NTA/`, 현재는 기본 프리미티브 메시 사용), 맵 프리팹(`Maps/Mission1~5`, YuME 타일맵 기반 — 실제 사용 중인 건 Mission1뿐)
├─ AssetFolder/         # 3rd-party 모델링/스카이박스 에셋 (Canopus-III Sci-Fi Desert Units, Yoge Stylized Nature, Animated Sun Skybox, TZ_Futuristic Panel Textures, LowPolyWater_Pack) — 임포트 + URP 머티리얼 변환 완료, 게임플레이 프리팹(`prefabs/NTA/`)에는 아직 미적용
├─ Material, Shader/    # 머티리얼 및 커스텀 셰이더
└─ Settings/            # URP 렌더 파이프라인 설정 + 포스트프로세싱 Volume Profile(Bloom/Color Adjustments/SSAO)
```

### 2. 핵심 스크립트 표 — `CameraControl` 설명 보강 + `MinimapViewIndicator`/`CaptureSystem`/`TerritoryZone`/`TerritoryManager`/`AutoRotate` 행 추가, 안내 문구 갱신

**기존 코드** (69~76번째 줄):
```
| `CameraControl` | RTS 시점 카메라 이동/줌 | [doc](Docs/CameraControl.md) |
| `MinimapController` | 미니맵 표시 및 클릭 시 카메라 이동 | [doc](Docs/MinimapController.md) |
| `EnemyController` | 적 유닛 선택/마커/스탯 데이터 (AI 로직은 미구현) | [doc](Docs/EnemyController.md) |
| `UIController` | 커맨드 패널, 생산 대기열, 자원 표시 UI 총괄, 버튼별 키보드 단축키 데이터 보유 | [doc](Docs/UIController.md) |
| `ProductionSlot` | 커맨드/생산 대기열의 버튼 슬롯 하나, 자기 단축키 자동 감지 + 눌림 효과 재현 | [doc](Docs/ProductionSlot.md) |
| `TooltipUI` | 버튼/스탯 호버 시 툴팁 표시 | [doc](Docs/TooltipUI.md) |
| `HealthBarBillboard` | 체력바 UI가 카메라의 X(피치) 각도만 따라 회전(Y/Z 고정)하도록 하는 빌보드 컴포넌트 | [doc](Docs/HealthBarBillboard.md) |
| `EffectPlayer` | 이펙트 프리팹(파티클/사운드) 스폰·자동 파괴 공용 정적 헬퍼 — 단발/다중지점/지속형 재생 지원 | [doc](doc/0105-effect-system-integration-design.md) |
| `HitEffectSet` | 공격 타입(총기/폭발/레이저/화염)별 피격 이펙트 프리팹 묶음(직렬화 클래스) | [doc](doc/0108-hit-effect-attack-type-variants.md) |
| `UnitEffects` | 유닛의 공격(총구)/이동(트레일)/피격/사망 이펙트 재생 전담 컴포넌트 | [doc](doc/0105-effect-system-integration-design.md) |
| `BuildingEffects` | 건물의 이착륙/피격/파괴 이펙트 재생 전담 컴포넌트 | [doc](doc/0116-building-destroy-effect.md) |
| `ConstructionEffects` | `BaseStructure`의 건설 중 지속/완공/피격/파괴 이펙트 재생 전담 컴포넌트 | [doc](doc/0117-construction-destroy-effect.md) |
| `TrailRotationFollower` | 지속형 이펙트가 부착 지점을 부모-자식으로 즉시 따라가지 않고, 위치는 매 프레임 추적하되 회전만 Slerp로 서서히 따라가게 하는 컴포넌트(급회전 중 축소 포함) | [doc](doc/0118-move-trail-smooth-rotation-follow-design.md) |
| `HoverBob` | 공중 유닛/리프트 중인 건물의 비주얼 자식 오브젝트를 DOTween으로 둥실거리게 하는 컴포넌트 | [doc](doc/0119-dotween-hover-bob-design.md) |
| `VehicleShake` | 지상 차량 유닛이 이동 중일 때 DOTween으로 흔들림을 재현하는 컴포넌트 | [doc](doc/0120-vehicle-shake-and-animation-folder.md) |

> 위 8개(`EffectPlayer`~`VehicleShake`)는 아직 `Docs/` 폴더에 필드/메소드 상세 문서가 없어 관련 `doc/` 세션 로그로 대신 링크했습니다.
```

**변경 코드:**
```
| `CameraControl` | RTS 시점 카메라 이동/줌 — 지형 티어(`Layer1`/`Layer2` 태그) 감지로 언덕마다 줌 범위·현재 높이 자동 보정 | [doc](Docs/CameraControl.md) |
| `MinimapController` | 미니맵 표시 및 클릭 시 카메라 이동 | [doc](Docs/MinimapController.md) |
| `MinimapViewIndicator` | 메인 카메라 시야를 미니맵 위에 반투명 사각형으로 표시, 줌/회전에 따라 매 프레임 크기·위치 자동 갱신(미니맵 밖으로 안 나가게 클리핑) | [doc](Docs/MinimapViewIndicator.md) |
| `EnemyController` | 적 유닛 선택/마커/스탯 데이터 (AI 로직은 미구현) | [doc](Docs/EnemyController.md) |
| `CaptureSystem` | 거점 점령 — 트리거 범위 내 아군/적 유닛 수에 따라 부호 있는 점령치를 밀당, 양쪽 다 있으면 교착, Ally↔Neutral↔Enemy 3단계 순환 전환(항상 Neutral 경유), 진행 중일 때만 점령바 노출 | [doc](doc/0146-tug-of-war-capture-cycle-implementation.md) |
| `TerritoryZone` | 인스펙터에서 핀(꼭짓점) 개수만 늘리면 자동 생성되는 다각형 영토 범위(오목 다각형도 판정 가능), 소유자에 따라 외곽선 색이 흰색/초록/빨강으로 자동 전환 | [doc](doc/0133-territoryzone-implementation.md) |
| `TerritoryManager` | 씬의 모든 `TerritoryZone`을 등록해 특정 좌표가 아군 영토 안인지 한 번에 질의(여러 영토가 겹치면 합집합) | [doc](doc/0141-territory-restriction-implementation-design.md) |
| `UIController` | 커맨드 패널, 생산 대기열, 자원 표시 UI 총괄, 버튼별 키보드 단축키 데이터 보유 | [doc](Docs/UIController.md) |
| `ProductionSlot` | 커맨드/생산 대기열의 버튼 슬롯 하나, 자기 단축키 자동 감지 + 눌림 효과 재현 | [doc](Docs/ProductionSlot.md) |
| `TooltipUI` | 버튼/스탯 호버 시 툴팁 표시 | [doc](Docs/TooltipUI.md) |
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

> 위 12개(`EffectPlayer`~`AutoRotate`, `MinimapViewIndicator` 제외)는 아직 `Docs/` 폴더에 필드/메소드 상세 문서가 없어 관련 `doc/` 세션 로그로 대신 링크했습니다.
```

### 3. "주요 기능" 절 — 점령/영토 시스템 항목 추가, 미니맵/카메라 문구 보강

**기존 코드** (95~103번째 줄 중 관련 부분):
```
- **전투**: 사거리 기반 자동 교전, 공격력/방어력 스탯, 적 강제 지정, 아군 강제 공격(오인사격, 완공 건물 + 건설 중인 `BaseStructure` 포함)
- **체력바 UI**: ...
```

**변경 코드:**
```
- **전투**: 사거리 기반 자동 교전, 공격력/방어력 스탯, 적 강제 지정, 아군 강제 공격(오인사격, 완공 건물 + 건설 중인 `BaseStructure` 포함)
- **점령/영토 시스템**: 거점(`CaptureSystem`)은 트리거 범위 내 아군/적 유닛 수에 따라 점령치를 밀당하며 Ally↔Neutral↔Enemy 3단계로 순환 전환(항상 Neutral을 거침), 양쪽이 동시에 있으면 교착. `TerritoryZone`은 인스펙터에서 핀 개수만 조절하면 자동 생성/정리되는 다각형 영토(오목 다각형 포함)로 소유자별 외곽선 색이 자동 전환되고, `TerritoryManager`가 전체 영토를 등록해 좌표 질의를 제공. 건물 배치(칸 전부가 아군 영토 안이어야 함), 자원 채취(영토 밖 노드 채취 불가, 채취 중 영토 상실 시 즉시 중단), 유닛 생산(영토 밖이면 대기열 유지한 채 타이머 정지), 건설 진행(영토 밖이면 담당 일꾼 유무와 별개로 일시정지)이 전부 영토 여부에 실제로 게이팅됨
- **체력바 UI**: ...
```

### 4. "구현 완료 기능" 체크리스트 — "점령 / 영토" 신규 섹션 추가, UI/이펙트/그래픽 섹션에 개별 항목 추가, "맵 제작" 완료 이동

**기존 코드** (154~191번째 줄 중 관련 부분, 섹션 순서: 자원/인구수 → 이펙트/모션 연출 → 그래픽/비주얼 → UI):
```
### 이펙트 / 모션 연출
- [x] 공격(총구) / 이동(트레일) / 피격(공격 타입별 4종: 총기·폭발·레이저·화염) / 사망 이펙트 — `UnitEffects`, 공용 헬퍼 `EffectPlayer`
- [x] 건물 이착륙 이펙트 — `BuildingEffects`
- [x] 건설 진행 중 지속 이펙트, 완공 순간 이펙트 — `ConstructionEffects`
- [x] 건물/건설중 파운데이션(`BaseStructure`) 피격·파괴(전투로 파괴 시에만, 취소 버튼과는 구분) 이펙트 — `BuildingEffects`/`ConstructionEffects`
- [x] 이동 트레일의 부자연스러운 급회전 보정 — `TrailRotationFollower`(위치는 매 프레임 추적, 회전은 Slerp로 서서히 추적 + 급회전 중 크기/방출량 축소)
- [x] 공중 유닛/리프트 중인 건물 호버링(둥실거림) 애니메이션 — `HoverBob`(DOTween)
- [x] 지상 차량 유닛 이동 중 흔들림 애니메이션 — `VehicleShake`(DOTween)
- [x] 마우스 커서 상태 전환(기본/선택/이동/공격) — `UserControl`
- [x] ESC로 대기 중인 명령(공격/이동/순찰/랠리/건물이동) 취소 — `UserControl`

### 그래픽 / 비주얼
- [x] URP Volume 포스트프로세싱 — Bloom(붉은끼 tint), Color Adjustments(대비/노출 보정), Tonemapping은 현재 None
- [x] Screen Space Ambient Occlusion(SSAO) — URP Renderer Feature로 적용
- [x] 빌드 프리뷰 고스트/셀 커서/유닛 이동·공격 명령 포인터는 전용 레이어(`Indicators`) + 오버레이 카메라(`Indicator Camera`, Depth Only + PostProcessing 끔)로 분리해 포스트프로세싱(Bloom/Color Adjustments)이 적용되지 않도록 처리
- [x] 3rd-party 유닛/건물/자연 모델링 에셋 임포트 — Canopus-III Low-Poly Sci-Fi Desert Units Set, Yoge Stylized Nature, Animated Sun Skybox, 전부 Built-in RP 셰이더로 제작돼 있던 것을 URP Lit로 변환해 마젠타/핑크 깨짐 해결(게임플레이 유닛/건물 프리팹에 실제 모델을 적용하는 작업은 아직 로드맵)

### UI
- [x] 커맨드 패널(선택 상태별 버튼 자동 전환)
- [x] Info Panel(아이콘/이름/체력, 공격력·방어력 호버 툴팁), `BaseStructure` 선택 시 전용 Info Panel(공격력/방어력 숨김)
- [x] Squad Panel(다중 선택 부대 표시, 개별 클릭 시 단일 선택 전환)
- [x] Squad Panel 페이지네이션 — 12마리 × 5페이지, 최대 60마리, 필요한 페이지 버튼만 노출
- [x] 커맨드/생산 버튼 호버 툴팁(`TooltipUI`), 제목만 있을 때 배경 크기 자동 축소(컴팩트 모드)
- [x] 미니맵 + 미니맵 클릭 시 카메라 이동
- [x] 카메라 이동/확대
- [x] 상단 자원 UI(광물/가스/인구수 실시간 표시)
- [x] 커맨드 패널 버튼별 키보드 단축키 + 눌림 시각 효과(아래 "키보드 단축키" 참고)
- [x] 유닛/건물별 체력바 UI — `HealthManager`의 `Slider` 필드가 체력 변화에 맞춰 자동 갱신, `HealthBarBillboard`로 카메라의 X(피치)만 따라 회전(Y/Z 고정)
- [x] 부대지정 단축키(컨트롤 그룹) — `Ctrl+숫자` 저장, `Shift+숫자` 병합 추가, 숫자만 눌러 선택

## 로드맵 (미구현)

- [ ] 유닛/건물 모델링 실제 적용 — ...
- [ ] 사망 시 래그돌/사망 애니메이션 — ...
- [ ] 전장의 안개(Fog of War) 구현 — ...
- [ ] 맵(스테이지) 제작
- [ ] Enemy AI 구현 — ...
```

**변경 코드:**
```
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
- [x] 다각형 영토(`TerritoryZone`) — 인스펙터 핀 개수 조절만으로 자동 생성/정리, 오목 다각형 판정 가능, 소유자별 외곽선 색 자동 전환(흰/초록/빨강), 여러 영토 등록/질의(`TerritoryManager`, 겹치면 합집합)
- [x] 영토 기반 게임플레이 제한 — 건물 배치는 그리드 칸 전부가 아군 영토 안일 때만 가능(프리뷰 자동 빨간색), 자원 채취는 영토 밖 노드 신규 채취 불가 + 채취 중 영토 상실 시 즉시 중단, 유닛 생산은 영토 밖이면 대기열 유지한 채 타이머만 정지, 건설 진행(`BaseStructure`)도 영토 밖이면 일시정지

### 그래픽 / 비주얼
- [x] URP Volume 포스트프로세싱 — Bloom(붉은끼 tint), Color Adjustments(대비/노출 보정), Tonemapping은 현재 None
- [x] Screen Space Ambient Occlusion(SSAO) — URP Renderer Feature로 적용
- [x] 빌드 프리뷰 고스트/셀 커서/유닛 이동·공격 명령 포인터는 전용 레이어(`Indicators`) + 오버레이 카메라(`Indicator Camera`, Depth Only + PostProcessing 끔)로 분리해 포스트프로세싱(Bloom/Color Adjustments)이 적용되지 않도록 처리
- [x] 3rd-party 유닛/건물/자연 모델링 에셋 임포트 — Canopus-III Low-Poly Sci-Fi Desert Units Set, Yoge Stylized Nature, Animated Sun Skybox, TZ_Futuristic Panel Textures Lite, LowPolyWater_Pack, 전부 Built-in RP 셰이더로 제작돼 있던 것을 URP(Lit/Unlit)로 변환해 마젠타/핑크 깨짐 해결(게임플레이 유닛/건물 프리팹에 실제 모델을 적용하는 작업은 아직 로드맵)
- [x] 1스테이지 맵 복원 — `TestScene` 씬 + `Mission1` 프리팹(YuME 타일맵 기반, `Layer1`/`Layer2` 태그로 언덕 단 구분), 게임 분위기/색감 확인용 프로토타입(`Mission2~5` 프리팹은 아직 미사용, 캠페인 맵은 추후 별도 제작 예정)

### UI
- [x] 커맨드 패널(선택 상태별 버튼 자동 전환)
- [x] Info Panel(아이콘/이름/체력, 공격력·방어력 호버 툴팁), `BaseStructure` 선택 시 전용 Info Panel(공격력/방어력 숨김)
- [x] Squad Panel(다중 선택 부대 표시, 개별 클릭 시 단일 선택 전환)
- [x] Squad Panel 페이지네이션 — 12마리 × 5페이지, 최대 60마리, 필요한 페이지 버튼만 노출
- [x] 커맨드/생산 버튼 호버 툴팁(`TooltipUI`), 제목만 있을 때 배경 크기 자동 축소(컴팩트 모드)
- [x] 미니맵 + 미니맵 클릭 시 카메라 이동
- [x] 미니맵 시야 사각형 표시(`MinimapViewIndicator`) — 메인 카메라가 보고 있는 지면 영역을 반투명 사각형으로 표시, 줌/회전에 따라 매 프레임 자동 갱신 + 미니맵 밖으로 안 나가게 클리핑
- [x] 카메라 이동/확대 — 지형 티어(태그 기반) 감지로 언덕마다 줌 범위·현재 높이 자동 보정
- [x] 상단 자원 UI(광물/가스/인구수 실시간 표시)
- [x] 커맨드 패널 버튼별 키보드 단축키 + 눌림 시각 효과(아래 "키보드 단축키" 참고)
- [x] 유닛/건물별 체력바 UI — `HealthManager`의 `Slider` 필드가 체력 변화에 맞춰 자동 갱신, `HealthBarBillboard`로 카메라의 X(피치)만 따라 회전(Y/Z 고정)
- [x] 부대지정 단축키(컨트롤 그룹) — `Ctrl+숫자` 저장, `Shift+숫자` 병합 추가, 숫자만 눌러 선택

## 로드맵 (미구현)

- [ ] 유닛/건물 모델링 실제 적용 — ...
- [ ] 사망 시 래그돌/사망 애니메이션 — ...
- [ ] 전장의 안개(Fog of War) 구현 — ...
- [ ] Enemy AI 구현 — ...
```
*(`...`는 기존 문구 그대로 유지, "맵(스테이지) 제작" 줄만 로드맵에서 삭제되어 위 그래픽/비주얼 섹션으로 이동)*

### 5. "해결된 이슈" 절 — 7건 추가

**기존 코드** (298~301번째 줄, 마지막 3개 항목):
```
- **인구수 한도(200) 초과분이 보급고 파괴 시 통째로 사라짐**: ...
- **지속형 파티클(이동 트레일 등)이 반복 재생 도중 여러 번 겹쳐 재생됨**: ...
- **이동 트레일이 급회전 시 부자연스럽게 홱 돌거나, 이동 중간에 멈춤**: ...

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.
```

**변경 코드:**
```
- **인구수 한도(200) 초과분이 보급고 파괴 시 통째로 사라짐**: ...
- **지속형 파티클(이동 트레일 등)이 반복 재생 도중 여러 번 겹쳐 재생됨**: ...
- **이동 트레일이 급회전 시 부자연스럽게 홱 돌거나, 이동 중간에 멈춤**: ...
- **건물 배치 프리뷰 Y좌표가 실제 지형이 아니라 그리드 셀 크기로 스냅됨**: `GetGroundPosition`이 Y값을 `grid.CellToWorld`/`WorldToCell` 왕복으로 재계산한 게 원인 — 레이캐스트로 측정한 실제 지면 Y를 5개 호출부 전체에 파라미터로 그대로 전달하도록 수정.
- **`TerritoryZone` 외곽선이 플레이 모드에서 안 보임**: `LineRenderer`에 머티리얼이 비어있던 게 원인 — URP Unlit 셰이더를 런타임에 복제해 자동 생성.
- **`TerritoryZone` 핀이 플레이 모드 진입/종료 시 중복되거나 초기화됨**: 도메인 리로드 도중 Transform 참조가 복구되기 전에 `OnValidate`가 실행된 게 원인 — `EditorApplication.isPlayingOrWillChangePlaymode` 가드 추가.
- **`CaptureSystem`이 같은 오브젝트가 아닌 자식 오브젝트의 `TerritoryZone`을 못 찾음**: `GetComponent` → `GetComponentInChildren(true)`로 수정.
- **아군 강제공격(A모드) 중 근처 다른 적에게 타겟이 가로채짐**: `AttackRange.GetPreferredTarget()`이 `orderedTarget`이 null이면 `friendlyTarget` 여부를 안 보고 곧장 최근접 적으로 폴백하던 게 원인 — `friendlyTarget` 우선 확인 후 폴백하도록 수정.
- **건물 클릭 후 드래그하면 건물+유닛이 동시에 선택됨**: 좌클릭 선택이 클릭 즉시 실행돼 드래그 시작을 구분 못한 게 원인 — 모든 좌클릭 선택(유닛/적/건물/`BaseStructure`/자원)을 마우스 뗄 때로 통일 지연, Shift 없이 드래그박스 선택 시 기존 선택을 정상적으로 교체.
- **`TZ_Futuristic Panel Textures Lite`(15개 머티리얼), `LowPolyWater_Pack`(`IslandMat` + 커스텀 `WaterShaded` 수면 셰이더)도 마젠타로 깨짐**: 기존 Canopus/Yoge와 동일하게 Built-in RP 전용 셰이더였던 게 원인 — URP로 변환(`WaterShaded`는 죽은 코드였던 GrabPass도 함께 제거).

전체 세션별 변경 이력(코드 변경 전/후 diff 포함)은 [`doc/`](doc) 폴더에 번호순으로 정리돼 있습니다.
```

## 영향받는 파일 (예정)

- `README.md` (프로젝트 구조 트리, 핵심 스크립트 표, 주요 기능, 구현 완료 기능/로드맵, 해결된 이슈 — 5개 절)

## 요약

`doc/0121` 이후 39개 세션 분량(점령/영토 시스템 전체, 미니맵 시야 사각형, 카메라 줌 지형 티어 보정, `AutoRotate`, 맵 복원 완료, 버그픽스 7건)이 README에 미반영 상태였다. 위 변경안은 아직 `README.md`에 적용하지 않았음 — 사용자 확인 후 반영 예정.
