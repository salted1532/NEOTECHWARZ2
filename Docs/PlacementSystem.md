# PlacementSystem

`Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 개요

건물 배치 시스템의 핵심 컨트롤러. 배치 모드 시작/취소, 그리드 위치 계산, 배치 가능 여부(겹침 + 유닛/장애물 충돌 + 자원/인구 + 담당 일꾼) 판정을 담당한다. **건물을 클릭 즉시 완성된 상태로 만들지 않는다** — 배치가 확정되면 그리드만 즉시 예약하고, 선택된 일꾼을 그 자리로 보내(`UnitController.GoBuild`) 도착하면 `BaseStructure`(건설 중 건물 기반)를 생성한다. 실제로 완성된 건물은 `BaseStructure`가 건설시간이 다 되면 스스로 생성한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `blockingLayers` | `LayerMask` | 배치를 막는 물리 레이어(유닛, 장애물 등) |
| `mouseIndicator`, `cellIndicator` | `GameObject` | 마우스/셀 위치 표시용 오브젝트 |
| `inputManager` | `InputManager` | 클릭/ESC 이벤트 및 마우스 월드 좌표 제공 |
| `grid` | `Grid` | 월드 ↔ 그리드 좌표 변환 |
| `database` | `BuildingDataSO` | 건물 스펙 데이터베이스 |
| `preview` | `PreviewSystem` | 배치 프리뷰(고스트) 표시 |
| `yOffset` | `float` | `IsBlocked()`의 물리 충돌 검사 박스 중심 높이 전용 고정 오프셋(기본 1) — 실제 배치 위치 계산에는 더 이상 쓰이지 않음(아래 `GetGroundOffsetY` 참고) |
| `selectedObjectIndex` | `int` | 현재 배치 중인 건물의 `database` 인덱스 (-1이면 배치 모드 아님) |
| `StructureData` | `GridData` | 그리드 점유 정보 |
| `placedGameObject` | `List<GameObject>` | 지금까지 배치된 오브젝트 목록(고스트/BaseStructure/완성 건물 순으로 같은 인덱스 자리를 갱신) |
| `lastDectectedPosition` | `Vector3Int` | 마지막으로 감지된 그리드 셀 (셀 변경 시에만 프리뷰 갱신하기 위한 캐시) |
| `rtsController` | `RTSUnitController` | `Start()`에서 `FindFirstObjectByType`로 획득. 일꾼 조회(`GetSelectedWorker`)와 자원 확인/소모(`TryConstructBuilding`)에 사용 |
| `baseStructurePrefab` | `GameObject` (SerializeField) | 건설 중 표시할 공용 "건물 기반"(`BaseStructure`) 프리팹. **씬 파일을 직접 건드리지 않고 추가한 필드라 인스펙터에서 직접 연결해야 함** |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 배치 상태 초기화, `GridData` 생성, `rtsController` 획득 |
| `StartPlacement(int ID)` | ID에 해당하는 건물 데이터를 찾아 배치 모드 시작 (프리뷰 표시 + 클릭/ESC 이벤트 구독). ID가 0이면 선택 해제로 취급 |
| `PlaceStructure()` (private, `OnClicked` 핸들러) | 그리드 겹침 → 물리 충돌(`IsBlocked`) → 선택된 가용 일꾼(`GetSelectedWorker`, 건설 중인 일꾼은 제외) → 자원/인구 확인 및 소모(`TryConstructBuilding`) 순으로 검증한다. 전부 통과하면 그리드 셀을 즉시 예약하고(`placedGameObject`에 `null` 자리부터 추가), 그 자리에 고정 건설 고스트(`PreviewSystem.SpawnConstructionGhost`)를 띄운 뒤 일꾼을 그 위치로 보낸다(`UnitController.GoBuild`, 도착 시 `StartConstruction`, 취소 시 `CancelReservedConstruction`). 클릭 한 번으로 배치가 확정되므로 건설모드도 곧바로 종료한다(`StopPlacement` + `RTSUnitController.ReturnState`) |
| `StartConstruction(data, groundPos, gridPos, placedIndex, ghost, worker)` (private, `GoBuild`의 `onArrived` 콜백) | 고스트를 파괴하고, `BaseStructure` 자신의 높이에 맞는 위치에 `baseStructurePrefab`을 생성한 뒤 `structure.Initialize(buildingID, buildTime, groundPos, 취소콜백)`으로 초기화. `placedGameObject`의 예약 자리를 이 오브젝트로 채우고 `worker.BeginConstruction(structure)`으로 일꾼을 붙인다 |
| `CancelReservedConstruction(gridPos, ghost)` (private) | 그리드 예약을 해제(`StructureData.RemoveObjectAt`)하고 고스트가 있으면 파괴. 두 가지 경로에서 재사용됨: (1) 일꾼이 도착하기 전에 다른 명령으로 건설 이동이 취소된 경우(`GoBuild`의 `onCancelled`, 고스트 있음) (2) 플레이어가 이미 생성된 `BaseStructure`를 직접 취소한 경우(`BaseStructure.CancelConstruction`이 호출하는 콜백, 고스트는 이미 없으므로 `null` 전달) |
| `GetGroundPosition(gridPos, size)` (private) | Grid → World 변환 + XZ 중앙정렬만 수행 (Y는 그리드 기준 지면 그대로, 오프셋 없음) |
| `GetPlacementWorldPosition(gridPos, size)` (private) | `IsBlocked()` 전용 - 프리팹에 상관없이 대략적인 충돌 검사 박스 중심 높이만 필요하므로 `GetGroundPosition` + 고정 `yOffset` |
| `GetGroundOffsetY(GameObject prefab)` (**public static**) | 프리팹의 `MeshFilter.sharedMesh.bounds`(로컬 바운드)와 `transform.localScale.y`를 이용해 "피벗이 정확히 지면(바닥)에 닿도록" 필요한 Y 오프셋을 자동 계산한다. 메쉬가 없으면 안전한 기본값(1)으로 대체. 기존 건물들(모두 `localScale.y = 2`인 Cube 메쉬)은 계산 결과가 예전 고정값 1과 정확히 일치해 회귀가 없고, `BaseStructure`처럼 다른 높이를 가진 프리팹도 자동으로 올바른 높이에 배치된다. `static`이라 `BaseStructure.CompleteConstruction()`에서도 그대로 재사용됨 |
| `IsBlocked(worldPos, size)` (private) | 건물이 들어설 영역에 유닛/장애물 등 `blockingLayers`에 속한 콜라이더가 있는지 `Physics.OverlapBox`로 검사. 그리드 셀 점유 체크와 별개로 실제 3D 공간상의 충돌까지 추가로 막는다 |
| `StopPlacement()` | 배치 모드를 종료하고 프리뷰/이벤트 구독을 정리 |
| `Update()` | 배치 모드일 때만 동작: 마우스가 새 그리드 셀로 이동하면 유효성(valid)을 재계산해 프리뷰 위치(`GetGroundPosition` + `GetGroundOffsetY`)/색상을 갱신 |

## 연관 컴포넌트

- **InputManager**: 클릭/ESC 이벤트, 마우스 월드 좌표
- **PreviewSystem**: 배치 가능 여부에 따른 시각 피드백(마우스 프리뷰) + 배치 확정 후 고정 고스트(`SpawnConstructionGhost`)
- **GridData**: 그리드 점유 판정
- **BuildingDataSO**: 건물 스펙(크기, 비용, 생산시간, 프리팹 등)
- **RTSUnitController**: `GetSelectedWorker()`(건설 중이 아닌 일꾼만), `TryConstructBuilding()`(자원/인구 확인+소모), `ReturnState()`
- **UnitController**: `GoBuild(destination, onArrived, onCancelled)`로 일꾼을 건설 위치로 보내고, 도착 시 `BeginConstruction(structure)`으로 일꾼을 건설 상태로 잠금
- **BaseStructure**: 건설 중 표시되는 실제 오브젝트. `Initialize`/`AttachBuilder`/`CancelConstruction`을 이 클래스에서 호출
