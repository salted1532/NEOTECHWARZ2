# NEOTECHWARZ2

Unity로 제작 중인 스타크래프트 스타일의 RTS(실시간 전략) 게임입니다. 이전 프로젝트 **네오테크워즈**의 문제점과 미구현 기능을 보완하고, UI/그래픽을 개선한 후속작으로 개발하고 있습니다.

## 기술 스택

| 구분 | 내용 |
|------|------|
| 엔진 | Unity 6000.4.8f1 |
| 렌더 파이프라인 | Universal Render Pipeline (URP) 17.4.0 |
| 입력 | Unity Input System 1.19.0 |
| 길찾기 | AI Navigation (NavMesh) 2.0.12 |
| UI | UGUI, TextMesh Pro |

## 프로젝트 구조

```
Assets/
├─ Scripts/
│  ├─ Building/        # 건물 컨트롤러
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라/미니맵 이동·조작
│  ├─ Enemy/            # 적 유닛 컨트롤러 (마커/스탯 데이터만, AI 로직은 미구현)
│  ├─ Resource/         # 자원 노드 및 자원 관리
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러, 툴팁
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리
├─ Scenes/              # 게임 씬 (SampleScene 등)
├─ prefabs/             # 유닛/건물 프리팹
├─ Material, Shader/    # 머티리얼 및 커스텀 셰이더
└─ Settings/            # URP 렌더 파이프라인 설정

doc/                     # 기능별 설계 노트 (생산 대기열 UI, Info Panel, 미니맵 등)
Docs/                    # 스크립트별 코드 문서(역할/필드/메소드) + 세션별 작업 로그(0001~)
```

> `FogOfWar/` 폴더는 아직 없습니다 — 전장의 안개는 미구현 상태입니다 (아래 로드맵 참고).

### 핵심 스크립트

각 스크립트의 상세 문서(필드, 메소드별 동작 방식)는 [`Docs/`](Docs) 폴더에 스크립트 이름과 동일한 파일명으로 정리되어 있습니다.

| 스크립트 | 역할 | 문서 |
|---|---|---|
| `RTSUnitController` | 유닛/건물 선택 상태, 전체 목록, UI 갱신, 생산·건설 자원 검증을 총괄하는 중앙 허브 | [doc](Docs/RTSUnitController.md) |
| `UserControl` | 마우스/키보드 입력을 해석해 선택·명령을 `RTSUnitController`에 전달 | [doc](Docs/UserControl.md) |
| `UnitController` | 유닛의 이동/전투/순찰/자원 채취 상태머신 (지상+공중 유닛 공통) | [doc](Docs/UnitController.md) |
| `AttackRange` | 사거리 내 적 감지 및 자동 공격/추격 | [doc](Docs/AttackRange.md) |
| `BuildingController` | 건물 선택, 랠리 포인트, 생산 위임, 사망 처리 | [doc](Docs/BuildingController.md) |
| `UnitSpawner` | 건물의 유닛 생산 대기열(FIFO) 관리 및 스폰 | [doc](Docs/UnitSpawner.md) |
| `PlacementSystem` | 그리드 기반 건물 배치, 배치 가능 여부 판정 | [doc](Docs/PlacementSystem.md) |
| `GridData` | 그리드 셀 점유 정보 관리 (순수 데이터 클래스) | [doc](Docs/GridData.md) |
| `PreviewSystem` | 배치 프리뷰(고스트 오브젝트) 및 셀 커서 표시 | [doc](Docs/PreviewSystem.md) |
| `InputManager` | 건물 배치 전용 입력 처리 (클릭/ESC/마우스 좌표) | [doc](Docs/InputManager.md) |
| `ResourceManager` | 팀의 광물/가스/인구수 중앙 관리 | [doc](Docs/ResourceManager.md) |
| `ResourceNode` | 자원 채취 지점, 대기열(줄서기) 및 고갈 처리 | [doc](Docs/ResourceNode.md) |
| `HealthManager` | 체력/데미지/치유/사망 처리 공용 컴포넌트 | [doc](Docs/HealthManager.md) |
| `UnitDataSO` | 유닛 스탯(체력/공격력/비용 등) 데이터베이스 | [doc](Docs/UnitDataSO.md) |
| `BuildingDataSO` | 건물 스펙(비용/크기/생산시간 등) 데이터베이스 | [doc](Docs/BuildingDataSO.md) |
| `CameraControl` | RTS 시점 카메라 이동/줌 | [doc](Docs/CameraControl.md) |
| `UIController` | 커맨드 패널, 생산 대기열, 자원 표시 UI 총괄 | [doc](Docs/UIController.md) |
| `ProductionSlot` | 커맨드/생산 대기열의 버튼 슬롯 하나 | [doc](Docs/ProductionSlot.md) |

## 주요 기능

- **유닛 시스템**: 단일/다수 선택, 이동, 공격, 정지, 홀드, 순찰, NavMesh를 사용하지 않는 공중 유닛 이동, 일꾼 자원 채취
- **건물 시스템**: 그리드 기반 배치, 생산 대기열, 렐리 포인트 지정
- **자원 시스템**: `ResourceManager` 기반 광물/가스/인구수 관리, 자원 노드 대기열(줄서기)
- **전투**: 사거리 기반 자동 교전, 공격력/방어력 스탯, 적 강제 지정, 아군 강제 공격(오인사격)
- **UI**: 패널 기반 커맨드 UI, Info Panel(공격력/방어력 호버 툴팁), Squad Panel(최대 60마리 페이지네이션), 생산 대기열 UI, 미니맵

> 스크립트별 상세 동작 방식은 위 표의 [`Docs/`](Docs) 링크를 참고하세요. 기능별 설계 노트는 [doc/ProductionQueue_UI_설계.md](doc/ProductionQueue_UI_설계.md), [doc/select-info-panel-design.md](doc/select-info-panel-design.md), [doc/minimap-click-to-move-design.md](doc/minimap-click-to-move-design.md)에 별도로 남아 있습니다.

## 시작하기

1. [Unity Hub](https://unity.com/download)에서 **Unity 6000.4.8f1** 버전을 설치합니다.
2. 저장소를 클론한 뒤 Unity Hub에서 프로젝트 폴더를 엽니다.
3. `Assets/Scenes/SampleScene`을 실행하여 플레이합니다.

## 구현 완료 기능

### 선택 / 피드백
- [x] 유닛 선택 / 다중 선택(드래그, 쉬프트 클릭)
- [x] 적 유닛 선택, 적 건물 선택, 중립 자원 노드 선택
- [x] 자원 채취지 노란색 선택 피드백
- [x] 적 유닛/건물 공격 지정 시 빨간색 마커 깜빡임 피드백
- [x] 적 유닛/적 건물 선택 → 아군 유닛의 공격 대상 지정을 강제(A 모드)
- [x] 아군 유닛/건물에 대한 강제 공격("오인사격", A 모드에서 아군 좌클릭) — 원래 TODO 목록엔 없었지만 이미 구현되어 있어 이번에 정리에 포함

### 유닛
- [x] 이동, 정지, 홀드, 순찰 (지상/공중 공통)
- [x] 공중 유닛 이동 — NavMesh 대신 직접 좌표 보간이라 지형 제약을 받지 않음, 공중 유닛끼리 겹침 자동 분리
- [x] 공격 — 사거리 내 적 감지 후 메소드로 데미지 적용 (`AttackRange` + `UnitController.Attack`)
- [x] 공격력 / 방어력 필드(`UnitController`, `EnemyController`) + Info Panel 호버 시 "Attack Damge : N" / "Armor : N" 툴팁
- [x] 일꾼 자원 채취 (아래 "일꾼 채취 로직" 참고)
- [ ] 일꾼의 "건물 건설" 동작 — 현재 건물은 배치 즉시 완공 상태로 생성됨(공사 진행 개념 없음)

### 건물 / 생산
- [x] 건물 배치(그리드 기반, 배치 가능 여부 판정)
- [x] 건물 선택, 생산 명령
- [x] 유닛 생산 대기열(최대 5개, 순차 진행) + 진행률 표시
- [x] 생산 대기열 UI(슬롯 5개, 클릭 시 해당 인덱스 취소 후 재출력)
- [x] 대기열 항목 취소
- [x] 생산 렐리 포인트(집결지) 설정
- [ ] 건물 이동
- [ ] 대기열 취소 시 자원 환불
- [ ] 건설 취소(공사 도중 취소 개념 자체가 없음)

### 자원 / 인구수
- [x] `ResourceManager`로 광물/가스/인구수 중앙 관리, 변경 이벤트로 상단 UI 자동 갱신
- [x] 유닛 생산 시 `ResourceManager.TrySpend`로 자원·인구수 소모
- [ ] 건물 배치 시 자원 소모 연결 — `PlacementSystem.PlaceStructure()`가 `ResourceManager`를 호출하지 않아 건물이 무료로 즉시 배치됨(연결 필요)

### UI
- [x] 커맨드 패널(선택 상태별 버튼 자동 전환)
- [x] Info Panel(아이콘/이름/체력, 공격력·방어력 호버 툴팁)
- [x] Squad Panel(다중 선택 부대 표시, 개별 클릭 시 단일 선택 전환)
- [x] Squad Panel 페이지네이션 — 12마리 × 5페이지, 최대 60마리, 필요한 페이지 버튼만 노출
- [x] 커맨드/생산 버튼 호버 툴팁(`TooltipUI`), 제목만 있을 때 배경 크기 자동 축소(컴팩트 모드)
- [x] 미니맵 + 미니맵 클릭 시 카메라 이동
- [x] 카메라 이동/확대
- [ ] 유닛/건물별 체력바 UI(월드 스페이스, 머리 위 표시)
- [ ] 부대지정 단축키(컨트롤 그룹)

## 로드맵 (미구현)

- [ ] 일꾼의 실제 "건물 건설" 동작 + 건물 배치 자원 소모 연결
- [ ] 건물 이동
- [ ] 부대지정 단축키 + 단축키로 부대 목록 선택
- [ ] 유닛/건물 체력바 UI
- [ ] 유닛/건물 모델링 추가
- [ ] 공격 이펙트(VFX) 추가
- [ ] 전장의 안개(Fog of War) 구현
- [ ] 맵 제작
- [ ] Enemy AI 구현 — `EnemyController`는 현재 마커/아이콘/공격력·방어력 데이터만 갖고 있고, 실제로 공격하거나 이동하는 AI 로직은 없음(플레이어 유닛이 일방적으로 공격하는 대상)
- [ ] 유닛/건물 사운드, 사운드 매니저
- [ ] 메인 화면, 설정창 UI
- [ ] 생산 대기열 환불, 건설 취소
- [ ] UI 버튼 하단 이미지 등 비주얼 개선

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
3. 차례가 되면 채취(`gatherDuration`초) 후 자원을 들고 최근접 반납 건물로 이동, 반납
4. 다른 명령이 들어와 채취가 중단되면 대기열에서 즉시 제외
5. 반납 후 원래 캐던 노드가 남아있으면 복귀, 고갈됐거나 반납 건물이 없으면 반경 10 내 다른 자원으로 재이동을 무한 반복

## 이전작(네오테크워즈)과 다른 점

- [x] 공중 유닛 구현
- [x] 생산 대기열 버그 해결
- [x] 렐리 포인트 문제 해결
- [x] 선택한 유닛 보여주기(Info Panel)
- [x] 부대 단위 보여주기(Squad Panel, 페이지네이션까지 확장)
- [ ] UI 버튼 하단 이미지 등 UI 디자인 개선

## 개발 프로세스 메모

세션마다 사용자 요청과 변경 내역을 `Docs/0001-...` 형식의 번호 매긴 마크다운 파일로 남깁니다. 특정 기능이 "왜" 지금 형태인지 궁금하면 `Docs/` 폴더의 관련 번호 문서를 먼저 확인하세요. (번호 없는 `Docs/*.md`는 스크립트별 코드 문서, `doc/*.md`는 기능별 설계 노트로 별개입니다.)
