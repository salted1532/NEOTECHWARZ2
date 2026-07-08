# BuildingController

`Assets/Scripts/Building/BuildingController.cs`

## 개요

건물 오브젝트에 부착되는 컨트롤러. 선택 표시, 랠리 포인트(집결지) 관리, 자식 `UnitSpawner`를 통한 유닛 생산 위임, 사망 처리(대기열 환불 + 인구수 반환 포함)를 담당한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `buildingMarker` | `GameObject` (SerializeField) | 선택 시 활성화되는 테두리/표시 오브젝트 (공격 대상 지정 피드백 깜빡임에도 재사용) |
| `icon`, `buildingID` | (SerializeField) | Info_panel 아이콘, `BuildingDataSO.ID`와 매칭되는 식별자 |
| `markerFlashInterval`, `markerFlashCount` | (SerializeField) | 공격 대상 지정 피드백 깜빡임 간격/횟수 |
| `UnitSpawner` | `UnitSpawner` | 자식 컴포넌트. 실제 생산 대기열 처리를 위임받음 (생산 불가 건물은 null일 수 있음) |
| `RallyPosition` | `Vector3` | 생산된 유닛이 스폰 후 이동할 집결 지점 (기본값: 건물 위치 + (0,0,-2)) |
| `rtsController`, `markerFlashRoutine` | private | 전역 컨트롤러 참조, 깜빡임 코루틴 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 마커 비활성화, `RTSUnitController.BuildingList`에 자신을 등록, 자식 `UnitSpawner` 캐싱, 기본 랠리 포인트 설정 |
| `SelectBuilding()` / `DeselecBuilding()` | 선택/선택 해제 시 `buildingMarker` on/off |
| `FlashMarker()` | 공격 대상으로 지정됐을 때 마커를 0.3초 간격 3회 깜빡임 (끝나면 실제 선택 상태로 복원) |
| `SetRallyPosition(position)` | 랠리 포인트를 지정 위치로 변경 |
| `SpawnUnit(unitID)` | 유닛 ID를 생산 대기열에 추가하도록 `UnitSpawner.Enqueue`에 위임 |
| `GetRallyPos()` | 현재 랠리 포인트 반환 |
| `GetIcon()` / `GetBuildingID()` | Info_panel 표시용 조회 |
| `GetProductionQueue()` | 현재 생산 대기열 목록 반환 (UI 표시용, `UnitSpawner`에 위임) |
| `GetProductionProgress()` | 현재 생산 중인 항목의 진행률(0~1) 반환 (`UnitSpawner`에 위임) |
| `IsProductionQueueFull()` | 생산 대기열이 가득 찼는지 (`UnitSpawner`에 위임, `UnitSpawner`가 없으면 false) — 자원을 소모하기 전에 `RTSUnitController.TryProduceUnit`이 먼저 확인 |
| `CancelProduction(index)` | 대기열의 특정 항목 생산 취소 (`UnitSpawner`에 위임). 환불에 쓸 수 있도록 **취소된 유닛ID를 반환**(무효면 -1) |
| `ClearProductionQueue()` | 파괴 시 대기열에 남아있던 항목 전체를 반환(제거) — `UnitSpawner`가 없는 건물(생산 불가)은 null |
| `Die()` | `RTSUnitController.RefundProductionQueue(ClearProductionQueue())`로 대기열 남은 유닛 환불 → `BuildingList`/`selectedBuildingList`에서 제거 → `RemoveMaxPopulationForBuilding(buildingID)`로 이 건물이 제공하던 인구수 한도 반환 → 파괴. `HealthManager`의 `IDestructible` 구현체로 호출됨 |

## 연관 컴포넌트

- **RTSUnitController**: `BuildingList`에 자신을 등록/해제, `RefundProductionQueue`/`RemoveMaxPopulationForBuilding` 호출
- **UnitSpawner**: 생산 대기열의 실제 로직을 위임받아 처리
- **HealthManager**: 체력이 0이 되면 `IDestructible.Die()`를 통해 이 컴포넌트의 `Die()`를 호출
- **BaseStructure**: 건설이 완료되면 이 `BuildingController`(및 같은 프리팹의 `HealthManager`)를 가진 실제 건물이 스폰됨 — `BaseStructure.CompleteConstruction()`이 완공될 건물 프리팹에서 `GetIcon()`/`HealthManager.GetMaxHealth()`를 미리 읽어와 Info_panel/체력 표시에 재사용
