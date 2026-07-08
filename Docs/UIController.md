# UIController

`Assets/Scripts/UI/UIController.cs`

## 개요

하단 커맨드 패널(선택된 유닛/건물/BaseStructure에 따른 명령 버튼들), 자원 표시, 생산 대기열 UI를 총괄하는 컨트롤러. `RTSUnitController`가 현재 선택 상태에 맞는 `ShowXXXPanel` 메서드를 호출해 패널 내용을 갱신한다.

## 상태 / 데이터 구조

| 타입 | 설명 |
|---|---|
| `UISelectionState` | `None, Worker, CombatUnit, BuildMode, Tier1Building, Tier2Building, Tier3Building, MainBase, BaseStructureSelect` — 현재 커맨드 패널이 어떤 종류인지 |
| `ButtonAction` (struct) | 버튼 하나의 동작 + 툴팁 정보: `Callback`(Action), `Title`, `Description`, `Ore/Gas/Population`(비용), `HasCost`, `Shortcut`(KeyCode, 기본 `None`). `Simple(callback, title, description, shortcut=None)`/`WithCost(callback, title, description, ore, gas, population, shortcut=None)` 팩토리로 생성 |
| `CommandButtonData` (struct) | 커맨드 패널 버튼 하나에 필요한 실제 표시 데이터: `Icon`(Sprite) + `ButtonAction`의 모든 필드(`Callback`/`Interactable`/`Title`/`Description`/`Ore`/`Gas`/`Population`/`HasCost`/`Shortcut`)를 그대로 옮겨 담음 |

## 주요 필드

| 필드 | 설명 |
|---|---|
| `panelRoot`, `slots[]` | 커맨드 패널 루트 오브젝트와 버튼 슬롯 배열(`ProductionSlot[]`) |
| Command/Building/Unit Icons | 이동/공격/정지/순찰/홀드/반환/건설 아이콘, 건물별 아이콘, 유닛별 아이콘 (모두 SerializeField) |
| `cancelIcon` | 취소 버튼 공용 아이콘 — 건설모드 패널의 Cancel과 `BaseStructure` 선택 시 취소(환불) 버튼이 함께 재사용 |
| `emptyQueueIcons[]` | 빈 생산 대기열 슬롯에 표시할 "다음 슬롯 번호" 아이콘 |
| `OreText`, `GasText`, `PopulationText` | 자원/인구 표시 텍스트 (TextMeshPro) |
| `queueSlots[]`, `database`(UnitDataSO), `progressSlider` | 생산 대기열 UI 및 진행률 슬라이더 |
| `currentQueue`, `isShowingProductionQueue` | 현재 표시 중인 대기열 캐시 |
| `infoPanel`, `infoIcon`, `infoNameText`, `infoHpText`, `attackDamageImage`, `armorImage` | Info_panel 구성 요소 (체력/공격력/방어력 호버 툴팁 포함) |
| `infoBoundHealth` | Info_panel이 현재 구독 중인 `HealthManager` (대상이 바뀌면 이전 구독 해제 후 갈아끼움) |
| `squadPanel`, `squadSlots[]`, `squadPageButtons[]` | 다중 유닛 선택 시 Squad_panel(최대 60마리, 12마리×5페이지) |

## 메소드

### 생명주기 / 자원 표시
| 메소드 | 설명 |
|---|---|
| `Start()` | `RTSUnitController` 참조 캐싱, 패널/대기열/Info/Squad UI 초기화, 페이지 버튼·스탯 호버 툴팁 셋업 |
| `Update()` | 매 프레임 `UpdateProductionProgress()` + `UpdateResourceUI()` 호출 |
| `UpdateResourceUI()` (private) | 자원(광물/가스)과 인구수 텍스트를 매 프레임 최신 값으로 갱신 |

### 커맨드 패널 공통
| 메소드 | 설명 |
|---|---|
| `ClearPanel()` | 커맨드 패널을 비우고 숨긴다 (선택 해제 시 호출) |
| `ShowPanel(state, commands[])` | 범용 패널 표시: 지정한 상태로 전환하고 주어진 커맨드 버튼들을 슬롯에 채운다 |
| `SetCommands(commands[])` (private) | 커맨드 패널의 각 슬롯에 데이터를 채우거나(모자란 슬롯은) 비운다. 각 슬롯이 `CommandButtonData.Shortcut`을 그대로 물려받아 스스로 단축키를 감지하게 됨(`ProductionSlot` 참고) |
| `AddCancelCommand(commands, onCancel)` (private) | 기존 커맨드 배열 끝에 취소(Cancel) 버튼 하나를 추가한 새 배열을 만들어 반환 (슬롯 수 초과 시 잘라냄) |

### 생산 대기열 UI
| 메소드 | 설명 |
|---|---|
| `UpdateQueue(queue, onCancel)` | 생산 대기열 슬롯 UI 갱신: 큐에 있는 만큼 아이콘/취소 콜백을 채우고 나머지는 빈 슬롯으로 표시 |
| `ShowProductionUI(queue, onCancel)` | 생산 대기열 UI를 표시 상태로 전환하고 즉시 갱신 |
| `HideProductionUI()` | 생산 대기열 & 진행시간 UI를 숨기고 초기화 (생산 건물이 아닌 대상 선택 시 등) |
| `SetEmptyQueueSlot(index)` (private) | 빈 대기열 슬롯에 "다음 슬롯 번호" 아이콘을 비활성 상태로 표시 |
| `UpdateProductionProgress()` (private) | 생산시간 표시(프로그레스 바) 갱신: 대기열 맨 앞 항목의 진행률을 슬라이더에 반영 |

### Info_panel (단일 유닛/건물/자원/BaseStructure 선택 시)
| 메소드 | 설명 |
|---|---|
| `ShowInfoPanel(icon, name, health)` | 건물/자원 등 공격력·방어력이 없는 대상용 (0으로 표시, 아이콘은 계속 보임) |
| `ShowInfoPanel(icon, name, health, attackDamage, armor)` | 유닛/적 선택 시 공격력/방어력도 함께 표시 (`SetCombatStatsVisible(true)`) |
| `SetCombatStatsVisible(visible)` (private) | 공격력/방어력 아이콘 자체를 보이거나 숨김 |
| `SetupInfoStatHoverTooltips()` / `AddStatHoverTooltip(image, textProvider)` (private) | `attackDamageImage`/`armorImage`에 호버 이벤트를 걸어 `TooltipUI`로 수치 표시 |
| `HideInfoPanel()` | Info_panel을 숨기고 HealthManager 구독 해제 |
| `ShowResourceInfoPanel(icon, resourceName, remainingAmount)` | 자원 노드(광물/가스) 선택 시: 공격력/방어력 숨김(`SetCombatStatsVisible(false)`), 체력 텍스트 자리에 남은 채취량 표시, HealthManager 구독 안 함 |
| `ShowBaseStructureInfoPanel(icon, buildingName, health)` | `BaseStructure`(건설 중) 선택 시: 공격력/방어력 숨김, 체력은 실제 `HealthManager`를 그대로 구독(`BindInfoHealth`)해서 건설 진행에 따라 자동 갱신 |
| `ShowBaseStructureCommandPanel(onCancelConstruction)` | `BaseStructure` 선택 시 커맨드 패널: 취소(환불) 버튼 하나만 표시(`cancelIcon` 재사용), `CurrentState`를 `BaseStructureSelect`로 전환 |
| `BindInfoHealth(health)` (private) | Info_panel이 구독 중인 `HealthManager`를 교체. 같은 대상이면 재구독 안 함, 대상 변경/해제 시 이전 구독 해제 |
| `UpdateInfoHpText(currentHp, maxHealth)` (private) | 체력 텍스트를 `"현재/최대"` 형식으로 갱신 (`OnHealthChanged` 이벤트 콜백) |

### Squad Panel (유닛 다중 선택 시)
| 메소드 | 설명 |
|---|---|
| `ShowSquadPanel(units, onSelectUnit)` | `selectedUnitList`를 12마리씩 페이지로 나눠 표시. 선택 내용이 실제로 바뀐 경우에만 페이지를 0으로 리셋 |
| `SelectSquadPage(page)` | 페이지 버튼 클릭 시 해당 페이지의 12마리로 슬롯을 다시 채움 |
| `RefreshSquadSlots()` (private) | 현재 페이지 기준으로 `squadSlots`를 채움 |
| `UpdateSquadPageButtons(pageCount)` (private) | 선택된 유닛 수로 채워지는 페이지 버튼만 보이도록 켬 |
| `SetupSquadPageButtons()` (private) | 페이지 버튼들의 `onClick`을 코드에서 연결 |
| `SquadUnitsEqual(current, incoming)` (private static) | 두 유닛 목록이 순서까지 동일한지 비교 |
| `HideSquadPanel()` | Squad_panel을 숨기고 슬롯/페이지 상태 초기화 |

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

- **RTSUnitController**: `UpdateUI()`에서 현재 선택 상태에 맞는 `ShowXXXPanel`을 호출하며, 각 `ButtonAction`에 `KeyCode` 단축키를 함께 채워 넘김
- **ProductionSlot**: 커맨드/대기열 버튼 슬롯의 실제 표시 + 단축키 자가 감지/눌림 효과를 담당
- **TooltipUI**: Info_panel 스탯 호버 및 `ProductionSlot` 호버 시 툴팁 표시
- **UnitDataSO**: 대기열 아이템의 아이콘 조회
