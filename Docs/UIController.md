# UIController

`Assets/Scripts/UI/UIController.cs`

## 개요

하단 커맨드 패널(선택된 유닛/건물에 따른 명령 버튼들), 자원 표시, 생산 대기열 UI를 총괄하는 컨트롤러. `RTSUnitController`가 현재 선택 상태에 맞는 `ShowXXXPanel` 메서드를 호출해 패널 내용을 갱신한다.

## 상태 / 데이터 구조

| 타입 | 설명 |
|---|---|
| `UISelectionState` | `None, Worker, CombatUnit, BuildMode, Tier1Building, Tier2Building, Tier3Building, MainBase` — 현재 커맨드 패널이 어떤 종류인지 |
| `CommandButtonData` (struct) | 커맨드 패널 버튼 하나에 필요한 데이터: `Icon`(Sprite), `Callback`(Action), `Interactable`(bool) |

## 주요 필드

| 필드 | 설명 |
|---|---|
| `panelRoot`, `slots[]` | 커맨드 패널 루트 오브젝트와 버튼 슬롯 배열 |
| Command/Building/Unit Icons | 이동/공격/정지/순찰/홀드/반환/건설 아이콘, 건물별 아이콘, 유닛별 아이콘 (모두 SerializeField) |
| `emptyQueueIcons[]` | 빈 생산 대기열 슬롯에 표시할 "다음 슬롯 번호" 아이콘 |
| `OreText`, `GasText`, `PopulationText` | 자원/인구 표시 텍스트 (TextMeshPro) |
| `queueSlots[]`, `database`(UnitDataSO), `progressSlider` | 생산 대기열 UI 및 진행률 슬라이더 |
| `currentQueue`, `isShowingProductionQueue` | 현재 표시 중인 대기열 캐시 |

## 메소드

### 생명주기 / 자원 표시
| 메소드 | 설명 |
|---|---|
| `Start()` | `RTSUnitController` 참조 캐싱, 패널/대기열 UI 초기화 |
| `Update()` | 매 프레임 `UpdateProductionProgress()` + `UpdateResourceUI()` 호출 |
| `UpdateResourceUI()` (private) | 자원(광물/가스)과 인구수 텍스트를 매 프레임 최신 값으로 갱신 |

### 커맨드 패널 공통
| 메소드 | 설명 |
|---|---|
| `ClearPanel()` | 커맨드 패널을 비우고 숨긴다 (선택 해제 시 호출) |
| `ShowPanel(state, commands[])` | 범용 패널 표시: 지정한 상태로 전환하고 주어진 커맨드 버튼들을 슬롯에 채운다 |
| `SetCommands(commands[])` (private) | 커맨드 패널의 각 슬롯에 데이터를 채우거나(모자란 슬롯은) 비운다 |
| `AddCancelCommand(commands, onCancel)` (private) | 기존 커맨드 배열 끝에 취소(Cancel) 버튼 하나를 추가한 새 배열을 만들어 반환 (슬롯 수 초과 시 잘라냄) |

### 생산 대기열 UI
| 메소드 | 설명 |
|---|---|
| `UpdateQueue(queue, onCancel)` | 생산 대기열 슬롯 UI 갱신: 큐에 있는 만큼 아이콘/취소 콜백을 채우고 나머지는 빈 슬롯으로 표시 |
| `ShowProductionUI(queue, onCancel)` | 생산 대기열 UI를 표시 상태로 전환하고 즉시 갱신 |
| `HideProductionUI()` | 생산 대기열 & 진행시간 UI를 숨기고 초기화 (생산 건물이 아닌 대상 선택 시 등) |
| `SetEmptyQueueSlot(index)` (private) | 빈 대기열 슬롯에 "다음 슬롯 번호" 아이콘을 비활성 상태로 표시 |
| `UpdateProductionProgress()` (private) | 생산시간 표시(프로그레스 바) 갱신: 대기열 맨 앞 항목의 진행률을 슬라이더에 반영 |

### 선택별 패널
| 메소드 | 설명 |
|---|---|
| `ShowWorkerPanel(...)` | 일꾼 선택 패널 (이동/공격/정지/순찰/홀드/반환/건설 버튼) |
| `ShowAttackUnitPanel(...)` | 전투유닛 선택 패널 (이동/공격/정지/순찰/홀드 버튼) |
| `ShowBuildPanel(...)` (건물 종류별 버튼 오버로드) | 건설모드 패널 (건물 종류별 버튼 + 취소 버튼) |
| `ShowBuildPanel(buildingCommands[], onCancel)` (오버로드) | 건설모드 패널 표시 (커맨드 배열 뒤에 취소 버튼을 자동으로 추가) |
| `ShowMainBasePanel(onTrainWorker)` | 본진(커맨드센터) 선택 패널 (일꾼 생산 버튼) |
| `ShowBarracksPanel(onMarine, onFirebat)` | 병영(Tier1 건물) 선택 패널 (마린/벌처 생산 버튼) |
| `ShowFactoryPanel(onGoliath, onTank)` | 공장(Tier2 건물) 선택 패널 (골리앗/탱크 생산 버튼) |
| `ShowAirportPanel(onWraith, onGuardian)` | 우주공항(Tier3 건물) 선택 패널 (레이스/가디언 생산 버튼) |

## 연관 컴포넌트

- **RTSUnitController**: `UpdateUI()`에서 현재 선택 상태에 맞는 `ShowXXXPanel`을 호출
- **ProductionSlot**: 커맨드/대기열 버튼 슬롯의 실제 표시를 담당
- **UnitDataSO**: 대기열 아이템의 아이콘 조회
