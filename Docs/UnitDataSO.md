# UnitDataSO

`Assets/Scripts/ScriptableObject/UnitDataSO.cs`

## 개요

게임 내 모든 유닛 종류의 데이터를 담는 `ScriptableObject` 데이터베이스. 에디터에서 에셋(`[CreateAssetMenu]`)으로 생성해 관리하며, `UnitSpawner`/`RTSUnitController`/`UIController` 등이 ID로 스펙을 조회할 때 사용한다.

## UnitDataSO

| 필드 | 타입 | 설명 |
|---|---|---|
| `unitData` | `List<UnitData>` | 유닛 스펙 목록 |

## UnitData

유닛 하나의 스펙을 정의하는 데이터 항목 (`[System.Serializable]`).

| 필드 | 타입 | 설명 |
|---|---|---|
| `unitName` | `string` | 유닛 이름 |
| `ID` | `int` | 코드에서 유닛을 식별하는 고유 ID (`RTSUnitController.UnitID` 상수와 매칭) |
| `hp` | `int` | 체력 |
| `attackDamge` | `int` | 공격력 |
| `attackRange` | `int` | 공격 사거리 |
| `mineral` / `gas` | `int` | 생산 비용 |
| `population` | `int` | 이 유닛을 생산하는 데 필요한 인구수(보급) 비용 |
| `productionTime` | `int` | 생산 소요 시간 |
| `Icon` | `Sprite` | UI 아이콘 |
| `Prefab` | `GameObject` | 스폰될 프리팹 |

## 연관 컴포넌트

- **UnitSpawner**: `Enqueue`/`Spawn`에서 ID로 `UnitData`를 조회해 생산시간/프리팹 사용
- **RTSUnitController**: `TryProduceUnit(unitID)`에서 비용 조회 후 `ResourceManager.TrySpend` 호출
- **UIController**: `UpdateQueue`에서 대기열 항목의 아이콘을 표시할 때 조회
