# PlacementSystem

`Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 개요

건물 배치 시스템의 핵심 컨트롤러. 배치 모드 시작/취소, 그리드 위치 계산, 배치 가능 여부(겹침 + 유닛/장애물 충돌) 판정, 실제 건물 생성을 담당한다.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `blockingLayers` | `LayerMask` | 배치를 막는 물리 레이어(유닛, 장애물 등) |
| `mouseIndicator`, `cellIndicator` | `GameObject` | 마우스/셀 위치 표시용 오브젝트 |
| `inputManager` | `InputManager` | 클릭/ESC 이벤트 및 마우스 월드 좌표 제공 |
| `grid` | `Grid` | 월드 ↔ 그리드 좌표 변환 |
| `database` | `BuildingDataSO` | 건물 스펙 데이터베이스 |
| `preview` | `PreviewSystem` | 배치 프리뷰(고스트) 표시 |
| `yOffset` | `float` | 건물 높이 오프셋 (프리뷰 + 실제 공통 적용) |
| `selectedObjectIndex` | `int` | 현재 배치 중인 건물의 `database` 인덱스 (-1이면 배치 모드 아님) |
| `StructureData` | `GridData` | 그리드 점유 정보 |
| `placedGameObject` | `List<GameObject>` | 지금까지 배치된 건물 목록 |
| `lastDectectedPosition` | `Vector3Int` | 마지막으로 감지된 그리드 셀 (셀 변경 시에만 프리뷰 갱신하기 위한 캐시) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 배치 상태 초기화, `GridData` 생성 |
| `StartPlacement(int ID)` | ID에 해당하는 건물 데이터를 찾아 배치 모드 시작 (프리뷰 표시 + 클릭/ESC 이벤트 구독). ID가 0이면 선택 해제로 취급 |
| `PlaceStructure()` (private, `OnClicked` 핸들러) | 현재 마우스 위치가 배치 가능하면(그리드 겹침 없음 + 장애물 없음) 실제 건물을 생성하고 그리드에 점유 정보를 등록. NavMeshObstacle을 다시 활성화한다 |
| `GetPlacementWorldPosition(gridPos, size)` (private) | Grid → World 변환 + 중앙정렬 + Y 오프셋을 통합 처리 (프리뷰/실제 건물 동일 기준) |
| `IsBlocked(worldPos, size)` (private) | 건물이 들어설 영역에 유닛/장애물 등 `blockingLayers`에 속한 콜라이더가 있는지 `Physics.OverlapBox`로 검사. 그리드 셀 점유 체크와 별개로 실제 3D 공간상의 충돌까지 추가로 막는다 |
| `StopPlacement()` | 배치 모드를 종료하고 프리뷰/이벤트 구독을 정리 |
| `Update()` | 배치 모드일 때만 동작: 마우스가 새 그리드 셀로 이동하면 유효성(valid)을 재계산해 프리뷰 색상/위치를 갱신 |

## 연관 컴포넌트

- **InputManager**: 클릭/ESC 이벤트, 마우스 월드 좌표
- **PreviewSystem**: 배치 가능 여부에 따른 시각 피드백
- **GridData**: 그리드 점유 판정
- **BuildingDataSO**: 건물 스펙(크기, 프리팹 등)
- **RTSUnitController**: `TryConstructBuilding`으로 배치 확정 전 자원 소모 검증 (현재 코드에는 직접 연결 지점이 없어 확장 시 `PlaceStructure()`에서 호출하는 것을 고려할 수 있음)
