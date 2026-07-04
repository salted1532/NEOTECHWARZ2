# RTSUnitController

`Assets/Scripts/System/RTSUnitController.cs`

## 개요

RTS 게임 전체를 총괄하는 중앙 상태 관리자. 유닛/건물 선택 상태, 전체 유닛/건물/자원노드 목록, UI 갱신, 생산/건설 자원 소모 검증 등 여러 시스템(`UserControl`, `UIController`, `PlacementSystem`, `ResourceManager`)을 연결하는 허브 역할을 한다.

## 상태 정의

| 열거형 | 값 | 설명 |
|---|---|---|
| `SelectState` | `None, UnitSelect, BuildingSelect, EnemySelect, OreSelect, BuildMode` | 현재 무엇이 선택되어 있는지 |
| `UnitState` | `None, Worker, AttackUnit` | 선택된 유닛의 종류 (태그 기반 판정) |
| `BuildingState` | `None, MainBaseSelect, Tier1Select, Tier2Select, Tier3Select, SupplyDepot, Lab` | 선택된 건물의 종류 (태그 기반 판정) |
| `UnitID` (static class) | `Worker=1, Marine=2, Vulture=3, Goliath=4, Tank=5, Wraith=6, Guardian=7` | 유닛 데이터 ID 상수 |

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `selectedUnitList` / `selectedBuildingList` | `List<UnitController>` / `List<BuildingController>` | 현재 선택된 유닛/건물 |
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
| `SelectUnit(unit)` (private) | 태그로 `UnitState`(Worker/AttackUnit) 판정 후 선택 처리 |
| `DeselectUnit(unit)` (private) | 특정 유닛 선택 해제 |
| `GetSelectedUnits()` | 현재 선택된 유닛 목록 반환 |
| `MoveSelectedUnits(end)` | 선택된 유닛 전체 이동 명령 |
| `AttackSelectedUnits(end)` / `AttackGroundSelectedUnits(end)` | 유닛/바닥 공격 명령 |
| `StopSelectedUnits()` / `HoldSelectedUnits()` | 정지/홀드 명령 |
| `EnterReturnMode()` | 선택된 일꾼들에게 자원 반환(Return Cargo) 명령 |
| `PatrolSelectedUnits(end)` | 순찰 명령 |
| `GatherSelectedUnits(node)` | 자원 채취 명령 |
| `MoveToBuildingSelectedUnits(building)` | 건물 우클릭 명령 (일꾼이 자원을 들고 있으면 반환, 아니면 이동) |

### 건물 선택 관련
| 메소드 | 설명 |
|---|---|
| `ClickSelectBuilding` / `ShiftClickSelectBuilding` | 유닛 선택과 동일한 패턴의 좌클릭/Shift+클릭 |
| `SelectBuilding(building)` | 태그로 `BuildingState`(MainBase/Tier1~3/SupplyDepot/Lab) 판정 후 선택 처리 |
| `Deselectbuilding(building)` (private) | 특정 건물 선택 해제 |
| `SetRallySelectBuilding(position)` | 선택된 건물들의 랠리 포인트 설정 |

### 공통 선택
| 메소드 | 설명 |
|---|---|
| `DeselectAll()` | 모든 유닛/건물 선택 해제 (건설 모드 중에는 무시) |

### UserControl 상태 전환
| 메소드 | 설명 |
|---|---|
| `EnterMoveMode()` / `EnterAttackMode()` / `EnterPatrolMode()` / `EnterRallyMode()` | `UserControl.SetOrderState(...)`를 호출해 다음 클릭이 어떤 명령인지 지정 |

### 생산/건설 관련
| 메소드 | 설명 |
|---|---|
| `SpawnUnit(unitID)` | 선택된 건물들에게 `BuildingController.SpawnUnit` 위임 (자원 소모 없이 그냥 큐잉) |
| `GetProductionQueue()` / `GetProductionProgress()` / `CancelProduction(index)` | 선택된 첫 번째 건물의 대기열 정보 조회/취소 (UI 표시용) |
| `TryProduceUnit(unitID)` | 유닛 데이터 조회 → `ResourceManager.TrySpend`로 자원 확인 및 소모 → 성공 시 `SpawnUnit` 호출 |
| `TryConstructBuilding(buildingID)` | 건물 데이터 조회 → `ResourceManager.TrySpend`로 자원 확인 및 소모 (배치 확정 직전에 호출하도록 설계됨) |

### UI 관련
| 메소드 | 설명 |
|---|---|
| `UpdateUI()` (private) | `RTScurrentSate`/`BuildingSelectState`/`UnitSelectState`에 따라 `UIController`의 알맞은 `ShowXXXPanel`을 호출해 커맨드 패널과 생산 대기열 UI를 갱신 |

### 상태 전환 / 조회
| 메소드 | 설명 |
|---|---|
| `BuildModeOn()` | 건설 모드 진입 |
| `ReturnState()` | 상태를 `UnitSelect`로 초기화 |
| `GetOre/GetGas/GetPopulation/GetMaxPopulation/AddOre/AddGas` | `ResourceManager`로의 단순 패스스루 |
| `IsNone/IsUnitSelect/IsBuildingSelect/IsEnemySelect/IsOreSelect/IsBuildMode` | `RTScurrentSate` 확인용 |
| `IsBuildingNone/IsMainBase/IsTier1Building/IsTier2Building/IsTier3Building/IsSupplyDepot/IsLab` | `BuildingSelectState` 확인용 |
| `TestMethod()` | UI 버튼 연결 테스트용 빈 메서드 |

## 연관 컴포넌트

모든 게임플레이 시스템(`UserControl`, `UIController`, `UnitController`, `BuildingController`, `ResourceManager`, `ResourceNode`, `PlacementSystem`)이 이 컨트롤러를 통해 서로 연결된다. 이 클래스가 사실상 게임의 전역 상태(싱글턴 역할)를 담당한다.
