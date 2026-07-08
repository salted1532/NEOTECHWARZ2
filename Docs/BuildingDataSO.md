# BuildingDataSO

`Assets/Scripts/ScriptableObject/BuildingDataSO.cs`

## 개요

게임 내 모든 건물 종류의 데이터를 담는 `ScriptableObject` 데이터베이스. 에디터에서 에셋(`[CreateAssetMenu]`)으로 생성해 관리하며, `PlacementSystem`/`RTSUnitController`/`BaseStructure` 등이 ID로 스펙을 조회할 때 사용한다.

## BuildingDataSO

| 필드 | 타입 | 설명 |
|---|---|---|
| `buildingData` | `List<BuildingData>` | 건물 스펙 목록 |

## BuildingData

건물 하나의 스펙을 정의하는 데이터 항목 (`[System.Serializable]`).

| 필드 | 타입 | 설명 |
|---|---|---|
| `Name` | `string` | 건물 이름 |
| `description` | `string` | 건설 버튼 툴팁에 표시할 설명 (비워두면 `"Construct {Name}."` 기본 문구) |
| `ID` | `int` | 코드에서 건물을 식별하는 고유 ID (`RTSUnitController.BuildingID` 상수와 매칭) |
| `Size` | `Vector2Int` | 그리드 상에서 차지하는 칸 크기 (x, y) |
| `mineral` | `int` | 건설 비용(광물) — `TryConstructBuilding`이 실제로 소모 |
| `gas` | `int` | 건설 비용(가스) — `TryConstructBuilding`이 실제로 소모 |
| `population` | `int` | 건설 버튼 툴팁에 "인구수 비용"으로 표시만 되는 값. **실제로는 소모되지 않음** — `TryConstructBuilding`은 mineral/gas만 확인·차감하고 population은 넘기지 않는다. 인구수 한도 증가와는 무관한 별개 필드이니 혼동 주의 |
| `maxpopulationamount` | `int` | 이 건물이 **완공됐을 때** 최대 인구수 한도에 실제로 더해지는 값(현재 MainBase/SupplyDepot에서 사용). `BaseStructure.CompleteConstruction()`이 `RTSUnitController.AddMaxPopulation(data.maxpopulationamount)`로 반영하고, `BuildingController.Die()`가 파괴 시 `RemoveMaxPopulationForBuilding`으로 같은 값만큼 되돌린다. `ResourceManager.maxPopulationCap`(기본 200)을 넘지 않도록 클램프됨 |
| `productionTime` | `int` | 건설 소요 시간(초) — 일꾼이 붙어서 건설 중일 때 `BaseStructure`가 이 시간에 걸쳐 체력을 채움 |
| `Prefab` | `GameObject` | 완공된 건물 프리팹. `BaseStructure.Initialize()`가 이 프리팹의 `HealthManager.GetMaxHealth()`/`BuildingController.GetIcon()`을 미리 읽어와 건설 중 표시용 최대체력/아이콘으로 사용 |

## 연관 컴포넌트

- **PlacementSystem**: `StartPlacement(ID)`에서 `buildingData.FindIndex`로 건물 스펙 조회, 배치 시 `Prefab`/`Size` 사용. `PlaceStructure()`가 `RTSUnitController.TryConstructBuilding(data.ID)`로 자원을 먼저 확인·차감한 뒤에만 실제로 배치를 진행
- **BaseStructure**: 건설 중(`Initialize`)에는 `Prefab`에서 최대체력/아이콘을 미리 읽어오고, 완공 시(`CompleteConstruction`) 실제 `Prefab`을 스폰하며 `maxpopulationamount`로 인구수 한도를 늘림
- **RTSUnitController**: `TryConstructBuilding(buildingID)`/`RefundBuilding(buildingID)`(취소 시 mineral/gas 환불)/`RemoveMaxPopulationForBuilding(buildingID)`(파괴 시 인구수 반환)에서 조회
