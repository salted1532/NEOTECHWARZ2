# RTSUnitController

`Assets/Scripts/System/RTSUnitController.cs`

## 개요

RTS 게임 전체를 총괄하는 중앙 상태 관리자. 유닛/건물/적/자원노드/건설중인 건물기반(`BaseStructure`) 선택 상태, 전체 유닛/건물/자원노드 목록, UI 갱신, 생산/건설 자원·인구수 검증 및 환불 등 여러 시스템(`UserControl`, `UIController`, `PlacementSystem`, `ResourceManager`)을 연결하는 허브 역할을 한다.

## 상태 정의

| 열거형 | 값 | 설명 |
|---|---|---|
| `SelectState` | `None, UnitSelect, BuildingSelect, EnemySelect, OreSelect, BaseStructureSelect, BuildMode` | 현재 무엇이 선택되어 있는지 |
| `UnitState` | `None, Worker, AttackUnit` | 선택된 유닛의 종류 (태그 기반 판정) |
| `BuildingState` | `None, MainBaseSelect, Tier1Select, Tier2Select, Tier3Select, SupplyDepot, Lab` | 선택된 건물의 종류 (태그 기반 판정) |
| `UnitID` (static class) | `Worker=1, Marine=2, Vulture=3, Goliath=4, Tank=5, Wraith=6, Guardian=7` | 유닛 데이터 ID 상수 |
| `BuildingID` (static class) | `CommandCenter=1, SupplyDepot=2, Barracks=3, Factory=4, Airport=5, Lab=6` | 건물 데이터 ID 상수, `ShowBuildPanel` 버튼 순서와 매칭 |

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `selectedUnitList` / `selectedBuildingList` / `selectedEnemyList` | `List<...>` | 현재 선택된 유닛/건물/적 |
| `selectedResourceNode` | `ResourceNode` | 광물/가스는 항상 단일 선택 |
| `selectedBaseStructure` | `BaseStructure` | 건설 중인 건물 기반도 항상 단일 선택 |
| `UnitList` / `BuildingList` / `ResourceNodeList` | `List<...>` | 맵에 존재하는 모든 유닛/건물/자원 노드 (각 컴포넌트의 `Start()`에서 자가 등록, `Die()`에서 자가 제거) |
| `userControl`, `uIController`, `PlacementSystem`, `resourceManager`, `unitDatabase`, `buildingDatabase` | (SerializeField) | 연결된 하위 시스템 참조 |
| `RTScurrentSate` / `BuildingSelectState` / `UnitSelectState` | 각 상태 열거형 | 현재 선택/모드 상태 |

## 메소드

### 생명주기
| 메소드 | 설명 |
|---|---|
| `Awake()` | 리스트 초기화 |
| `Update()` | 파괴된(null) 항목을 리스트에서 정리 후 `UpdateUI()` 호출 |

### 유닛 선택 관련
| 메소드 | 설명 |
|---|---|
| `ClickSelectUnit(unit)` | 좌클릭 선택: 기존 선택 해제 후 단일 선택 |
| `ShiftClickSelectUnit(unit)` | Shift+클릭: 이미 선택돼 있으면 해제, 아니면 추가 선택 |
| `DragSelectUnit(unit)` | 드래그 박스 선택: 아직 선택 안 된 유닛을 추가 |
| `SelectUnit(unit)` (private) | 태그로 `UnitState`(Worker/AttackUnit) 판정 후 선택 처리 (건설모드 중엔 무시) |
| `DeselectUnit(unit)` (private) | 특정 유닛 선택 해제 |
| `GetSelectedUnits()` | 현재 선택된 유닛 목록 반환 |
| `GetSelectedWorker()` | 선택된 유닛 중 첫 번째가 "Worker" 태그이고 건설 중(`IsConstructing()`)이 아니면 반환, 아니면 null — 건설모드는 `SelectUnit`이 새 선택을 막아 이 값이 건설모드 진입 시점의 일꾼으로 유지됨 |
| `AssignBuilderToStructure(structure)` | 건설이 중단된 `BaseStructure`를 우클릭했을 때 선택된 일꾼(`GetSelectedWorker()`)을 그 자리로 보낸다. `NavMeshObstacle` 때문에 중심점엔 도달할 수 없어 `Collider.ClosestPoint`로 표면의 가장 가까운 지점을 목적지로 계산 후 `UnitController.GoBuild` 호출, 도착 시 `BeginConstruction`으로 건설 재개 |
| `MoveSelectedUnits(end)` | 선택된 유닛 전체 이동 명령 |
| `AttackSelectedUnits(target)` | 특정 적 유닛을 추격 공격 (우클릭 적 클릭 / A 모드에서 적 클릭) |
| `AttackGroundSelectedUnits(end)` | 바닥 공격-이동 명령 (A 모드에서 땅 클릭) |
| `AttackFriendlySelectedUnits(target)` | 아군 강제 공격 (A 모드에서 아군 좌클릭, 대상 자신은 스킵) |
| `FollowSelectedUnits(target)` | 아군 유닛 우클릭 = 계속 따라다니기. Idle 상태를 유지해 도중에 만나는 적은 `AttackRange`가 자동 교전(대상 자신은 스킵) |
| `AttackFriendlyBuildingSelectedUnits(target)` | 아군 건물 강제 공격 (A 모드에서 아군 건물 좌클릭) |
| `StopSelectedUnits()` / `HoldSelectedUnits()` | 정지/홀드 명령 |
| `EnterReturnMode()` | 선택된 일꾼들에게 자원 반환(Return Cargo) 명령 |
| `PatrolSelectedUnits(end)` | 순찰 명령 |
| `GatherSelectedUnits(node)` | 자원 채취 명령 |
| `MoveToBuildingSelectedUnits(building)` | 건물 우클릭 명령 (일꾼이 자원을 들고 있으면 반환, 아니면 이동) |

### 건물 선택 관련
| 메소드 | 설명 |
|---|---|
| `ClickSelectBuilding` / `ShiftClickSelectBuilding` | 유닛 선택과 동일한 패턴의 좌클릭/Shift+클릭 |
| `SelectBuilding(building)` | 태그로 `BuildingState`(MainBase/Tier1~3/SupplyDepot/Lab) 판정 후 선택 처리. 생산 건물(MainBase/Tier1/2/3)이면 선택하는 순간 자기 랠리 포인트 위치에 이동 포인터를 3초간 표시(`userControl.ShowMovePointerAt`) |
| `Deselectbuilding(building)` (private) | 특정 건물 선택 해제 |
| `SetRallySelectBuilding(position)` | 선택된 건물들의 랠리 포인트 설정 — 랠리 버튼(슬롯 6, 단축키 Y, `RallyButtonAction()`)이나 건물 우클릭으로 호출됨 |
| `RallyButtonAction()` (private) | 생산 건물 패널의 랠리 버튼 데이터(`EnterRallyMode`, 단축키 Y) — `UpdateUI()`가 MainBase/Tier1/2/3 케이스마다 `uIController.ShowBuildingRallyCommand()`에 넘김 |
| `ClearSelectedEnemyBuildingIfMatches(building)` | 적 건물이 파괴되거나(`Die()`) 안개에 가려져서(`EnemyBuildingController.Update()`) 선택 상태가 유령 참조로 남지 않도록 정리 — 매칭되면 `building.DeselectEnemyBuilding()`도 호출 |

### 적 선택 관련
| 메소드 | 설명 |
|---|---|
| `ClickSelectEnemy(enemy)` | 좌클릭 선택 (적은 항상 단일 선택) |
| `SelectEnemy(enemy)` (private) | 건설모드 중이 아니면 `EnemySelect` 상태로 전환 후 선택 |
| `ClearSelectedEnemyIfMatches(enemy)` | 안개에 가려지는 등 외부 이벤트로 특정 적 유닛의 선택을 해제해야 할 때 호출(`EnemyUnitController.UpdateFogVisibility()`) — `enemy.DeselectEnemy()` 호출 후 목록에서 제거 |

### 자원(Ore/Gas) 선택 관련
| 메소드 | 설명 |
|---|---|
| `ClickSelectResource(node)` | 좌클릭 선택 (자원 노드는 항상 단일 선택) |
| `SelectResource(node)` (private) | `OreSelect` 상태로 전환 후 선택 |
| `ClearSelectedResourceIfMatches(node)` | 채취 중 노드가 고갈되어 파괴될 때 선택 상태가 유령 참조로 남지 않도록 정리 |

### BaseStructure(건설 중인 건물 기반) 선택 관련
| 메소드 | 설명 |
|---|---|
| `ClickSelectStructure(structure)` | 좌클릭 선택 (항상 단일 선택) |
| `SelectStructure(structure)` (private) | `BaseStructureSelect` 상태로 전환 후 선택, 마커 활성화 |
| `ClearSelectedStructureIfMatches(structure)` | 건설이 완료되거나 취소되어 `BaseStructure`가 파괴될 때 선택 상태 정리 |

### 공통 선택
| 메소드 | 설명 |
|---|---|
| `DeselectAll()` | 모든 유닛/건물/적/자원/BaseStructure 선택 해제 (건설 모드 중에는 무시) |

### UserControl 상태 전환
| 메소드 | 설명 |
|---|---|
| `EnterMoveMode()` / `EnterAttackMode()` / `EnterPatrolMode()` / `EnterRallyMode()` | `UserControl.SetOrderState(...)`를 호출해 다음 클릭이 어떤 명령인지 지정 |

### 생산/건설/환불 관련
| 메소드 | 설명 |
|---|---|
| `SpawnUnit(unitID)` | 선택된 건물들에게 `BuildingController.SpawnUnit` 위임 (자원 확인 없이 그냥 큐잉 — 반드시 `TryProduceUnit`을 통해서만 호출되어야 함) |
| `GetProductionQueue()` / `GetProductionProgress()` | 선택된 첫 번째 건물의 대기열 정보 조회 (UI 표시용) |
| `CancelProduction(index)` | 선택된 첫 번째 건물의 대기열 항목 취소, 반환된 유닛ID로 `RefundUnit` 호출(가격 환불) |
| `RefundProductionQueue(queue)` | 생산 건물이 파괴됐을 때(`BuildingController.Die()`) 대기열에 남아있던 유닛들을 전부 환불 |
| `RefundUnit(unitID)` (private) | 유닛 하나의 가격(광물/가스/인구수)만큼 환불 — `TryProduceUnit`이 소모한 것을 그대로 되돌림 |
| `RefundBuilding(buildingID)` | `BaseStructure` 건설을 취소했을 때 건물 가격(광물/가스) 전액 환불 (건설 중엔 인구수를 소모하지 않으므로 인구수 환불은 없음) |
| `CancelSelectedBaseStructure()` | Info_panel의 "Cancel" 버튼/단축키(T)에서 호출, 선택된 `BaseStructure.CancelConstruction()` 실행 |
| `AddMaxPopulation(amount)` | `ResourceManager.AddMaxPopulation` 패스스루 — `BaseStructure`가 건설을 완료하는 순간에만 호출됨 |
| `RemoveMaxPopulationForBuilding(buildingID)` | 건물이 파괴됐을 때 그 건물 종류(`BuildingData.maxpopulationamount`)가 제공하던 인구수 한도를 되돌림 |
| `TryProduceUnit(unitID)` | 선택된 첫 건물의 대기열이 가득 찼으면 `Debug.Log("대기열 가득참!")` 후 반환(자원 미소모). 아니면 `ResourceManager.TrySpend`로 자원/인구 확인 및 소모 → 실패 시 원인을 구분해 `"자원부족!"`/`"인구수부족!"` 로그 후 반환, 성공 시 `SpawnUnit` 호출 |
| `TryConstructBuilding(buildingID)` | 건물 데이터 조회 → `ResourceManager.TrySpend`로 광물/가스만 확인 및 소모 (인구수는 소모 안 함, `PlacementSystem.PlaceStructure()`가 배치 확정 직전에 호출) |

### 버튼 툴팁 데이터 구성
| 메소드 | 설명 |
|---|---|
| `UnitButtonAction(callback, unitID, shortcut = None)` (private) | 유닛 생산 버튼용 `ButtonAction` 생성 (제목=유닛명, 비용=광물/가스/인구수, 지정한 `KeyCode`를 버튼 단축키로 함께 전달) |
| `BuildingButtonAction(callback, buildingID, shortcut = None)` (private) | 건물 건설 버튼용 `ButtonAction` 생성 (제목=건물명, 비용=광물/가스/인구수 — 여기서 인구수는 `BuildingData.population`이며 실제로 소모되진 않는 표시용 값, 실질 소모는 mineral/gas만) |
| `GetUnitName(unitID)` / `GetBuildingName(buildingID)` | Info_panel/Cancel 버튼 등에 표시할 유닛/건물 이름 조회 |

### UI 관련
| 메소드 | 설명 |
|---|---|
| `UpdateUI()` (private) | `RTScurrentSate`/`BuildingSelectState`/`UnitSelectState`에 따라 `UIController`의 알맞은 `ShowXXXPanel`을 호출해 커맨드 패널·Info_panel·생산 대기열 UI를 갱신. `BaseStructureSelect`일 땐 `ShowBaseStructureInfoPanel` + `ShowBaseStructureCommandPanel`(Cancel 버튼, 단축키 T)을 함께 호출. `UpdateUnitSkillUI()`를 switch보다 먼저 매 프레임 호출 |
| `UpdateUnitSkillUI()` (private) | 고급유닛 특성 선택 오버레이/스킬 버튼(슬롯 6, Worker는 슬롯 7)을 매 프레임 갱신. `ClearSkillSlotIfShown()`으로 감싸서, 스킬 슬롯에 실제로 뭔가 표시됐던 경우에만 표시가 끝나는 시점에 딱 한 번 정리한다 — 예전엔 유닛이 하나도 선택 안 돼 있으면 매 프레임 무조건 `ClearUnitSkillSlot(useFallbackSlot)`을 호출했는데, `useFallbackSlot`(=`UnitSelectState == Worker`)이 "마지막으로 선택했던 유닛" 기준의 낡은 값이라 건물 등 다른 컨텍스트가 겸용 중인 슬롯을 매 프레임 잘못 지워버리는 버그가 있었다(예: 생산 건물 랠리 버튼이 슬롯 6과 겹치면서 반투명하게 보이거나 눌림 색상 전환이 씹히는 증상) |
| `ClearSkillSlotIfShown()` / `skillSlotShown` / `skillSlotUsedFallback` (private) | 스킬 슬롯이 마지막으로 표시됐을 때 어느 슬롯(폴백 여부)이었는지 기억해뒀다가, 그 슬롯만 딱 한 번 정리 |

### 상태 전환 / 조회
| 메소드 | 설명 |
|---|---|
| `BuildModeOn()` | 건설 모드 진입 |
| `ReturnState()` | 상태를 `UnitSelect`로 초기화 |
| `GetOre/GetGas/GetPopulation/GetMaxPopulation/AddOre/AddGas` | `ResourceManager`로의 단순 패스스루 |
| `IsNone/IsUnitSelect/IsBuildingSelect/IsEnemySelect/IsOreSelect/IsBuildMode` | `RTScurrentSate` 확인용 (`BaseStructureSelect`는 별도 프로퍼티 없이 `RTScurrentSate` 직접 비교로만 쓰임) |
| `IsBuildingNone/IsMainBase/IsTier1Building/IsTier2Building/IsTier3Building/IsSupplyDepot/IsLab` | `BuildingSelectState` 확인용 |
| `TestMethod()` | UI 버튼 연결 테스트용 빈 메서드 |

## 연관 컴포넌트

모든 게임플레이 시스템(`UserControl`, `UIController`, `UnitController`, `BuildingController`, `BaseStructure`, `ResourceManager`, `ResourceNode`, `PlacementSystem`)이 이 컨트롤러를 통해 서로 연결된다. 이 클래스가 사실상 게임의 전역 상태(싱글턴 역할)를 담당한다.
