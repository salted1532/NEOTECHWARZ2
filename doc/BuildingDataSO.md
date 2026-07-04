# BuildingDataSO

`Assets/Scripts/ScriptableObject/BuildingDataSO.cs`

## 개요

게임 내 모든 건물 종류의 데이터를 담는 `ScriptableObject` 데이터베이스. 에디터에서 에셋(`[CreateAssetMenu]`)으로 생성해 관리하며, `PlacementSystem`/`RTSUnitController` 등이 ID로 스펙을 조회할 때 사용한다.

## BuildingDataSO

| 필드 | 타입 | 설명 |
|---|---|---|
| `buildingData` | `List<BuildingData>` | 건물 스펙 목록 |

## BuildingData

건물 하나의 스펙을 정의하는 데이터 항목 (`[System.Serializable]`).

| 필드 | 타입 | 설명 |
|---|---|---|
| `Name` | `string` | 건물 이름 |
| `ID` | `int` | 코드에서 건물을 식별하는 고유 ID |
| `Size` | `Vector2Int` | 그리드 상에서 차지하는 칸 크기 (x, y) |
| `mineral` | `int` | 건설 비용(광물) |
| `gas` | `int` | 건설 비용(가스) |
| `population` | `int` | 이 건물이 추가로 제공(또는 소모)하는 인구수(보급) 용량 |
| `productionTime` | `int` | 생산/건설 소요 시간 |
| `Prefab` | `GameObject` | 배치될 프리팹 |

## 연관 컴포넌트

- **PlacementSystem**: `StartPlacement(ID)`에서 `buildingData.FindIndex`로 건물 스펙 조회, 배치 시 `Prefab`/`Size` 사용
- **RTSUnitController**: `TryConstructBuilding(buildingID)`에서 비용 조회 후 `ResourceManager.TrySpend` 호출
