# BuildingController

`Assets/Scripts/Building/BuildingController.cs`

## 개요

건물 오브젝트에 부착되는 컨트롤러. 선택 표시, 랠리 포인트(집결지) 관리, 자식 `UnitSpawner`를 통한 유닛 생산 위임, 사망 처리를 담당한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `buildingMarker` | `GameObject` (SerializeField) | 선택 시 활성화되는 테두리/표시 오브젝트 |
| `UnitSpawner` | `UnitSpawner` | 자식 컴포넌트. 실제 생산 대기열 처리를 위임받음 |
| `RallyPosition` | `Vector3` | 생산된 유닛이 스폰 후 이동할 집결 지점 (기본값: 건물 위치 + (0,0,-2)) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 마커 비활성화, `RTSUnitController.BuildingList`에 자신을 등록, 자식 `UnitSpawner` 캐싱, 기본 랠리 포인트 설정 |
| `SelectBuilding()` | 선택 시 `buildingMarker` 활성화 |
| `DeselecBuilding()` | 선택 해제 시 `buildingMarker` 비활성화 |
| `SetRallyPosition(Vector3 position)` | 랠리 포인트를 지정 위치로 변경 |
| `SpawnUnit(int unitID)` | 유닛 ID를 생산 대기열에 추가하도록 `UnitSpawner.Enqueue`에 위임 |
| `GetRallyPos()` | 현재 랠리 포인트 반환 |
| `GetProductionQueue()` | 현재 생산 대기열 목록 반환 (UI 표시용, `UnitSpawner`에 위임) |
| `GetProductionProgress()` | 현재 생산 중인 항목의 진행률(0~1) 반환 (`UnitSpawner`에 위임) |
| `CancelProduction(int index)` | 대기열의 특정 항목 생산 취소 (`UnitSpawner`에 위임) |
| `Die()` | `RTSUnitController.BuildingList`에서 제거 후 게임오브젝트 파괴. `HealthManager`의 `IDestructible` 구현체로 호출됨 |

## 연관 컴포넌트

- **RTSUnitController**: `BuildingList`에 자신을 등록/해제
- **UnitSpawner**: 생산 대기열의 실제 로직을 위임받아 처리
- **HealthManager**: 체력이 0이 되면 `IDestructible.Die()`를 통해 이 컴포넌트의 `Die()`를 호출
