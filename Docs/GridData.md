# GridData

`Assets/Scripts/BuildSystem/GridData.cs`

## 개요

그리드 셀(`Vector3Int`) 단위로 어떤 오브젝트가 어느 칸을 점유하고 있는지 관리하는 순수 데이터 클래스(`MonoBehaviour` 아님). `PlacementSystem`이 배치 가능 여부 판정 및 점유/해제 처리에 사용한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `placedObjects` | `Dictionary<Vector3Int, PlacementData>` | 점유된 셀 좌표 → 그 칸을 차지한 오브젝트 정보 매핑 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `AddObjectAt(gridPosition, objectSize, ID, placedObjectIndex)` | 지정 위치에 `objectSize` 크기의 오브젝트를 등록(점유 처리)한다. 이미 점유된 셀과 겹치면 예외를 던지므로, 호출 전 반드시 `CanPlaceObejctAt`으로 먼저 확인해야 한다 |
| `CalculatePositions(gridPosition, objectSize)` (private) | 기준 좌표에서 `objectSize(x, y)` 크기만큼의 모든 셀 좌표를 계산해 반환 |
| `CalculatePositionsPublic(gridPosition, objectSize)` | `CalculatePositions`의 외부 공개용 래퍼 |
| `CanPlaceObejctAt(gridPosition, objectSize)` | 해당 위치/크기에 오브젝트를 배치할 수 있는지(겹치는 칸이 없는지) 확인 |
| `GetRepresentationIndex(gridPosition)` (internal) | 해당 셀에 배치된 오브젝트의 인덱스(`placedGameObject` 리스트 상의 인덱스)를 반환. 없으면 -1 |
| `RemoveObjectAt(gridPosition)` (internal) | 해당 셀을 점유한 오브젝트가 차지하던 모든 셀을 일괄 해제(제거) |

## PlacementData

그리드에 배치된 오브젝트 하나에 대한 점유 정보.

| 필드/프로퍼티 | 타입 | 설명 |
|---|---|---|
| `occupiedPositions` | `List<Vector3Int>` | 이 오브젝트가 차지한 모든 셀 좌표 |
| `ID` | `int` | 건물 데이터 ID |
| `PlacedObjectIndex` | `int` | `PlacementSystem.placedGameObject` 리스트 상의 인덱스 |

## 연관 컴포넌트

- **PlacementSystem**: 배치 가능 여부 판정(`CanPlaceObejctAt`) 및 점유 등록/해제(`AddObjectAt`/`RemoveObjectAt`)에 이 클래스를 사용
