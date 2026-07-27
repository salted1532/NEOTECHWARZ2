# 0247 - 게임 시작 시 건물이 자기 크기에 맞춰 그리드 정렬 + 그리드 점유 등록 (아군/적 둘 다)

## 요청

게임 시작 시 건물 스크립트에서 자신의 위치를 자신의 크기와 그리드에 맞게 위치하도록 하고, 그리드에도
자신의 그리드 위치가 건설되었다고 추가해달라는 요청. [[0246]]에서 처리한 건 Y(지면 높이)뿐이었고, X/Z를
그리드 셀에 맞춰 정렬하는 것과 `PlacementSystem`의 그리드 점유 정보(`GridData`)에 자신을 등록하는 것은
빠져 있었음.

## 문제: 씬에 미리 배치된 건물은 그리드에 대해 완전히 무지했음

정상 건설 흐름(`PlacementSystem.PlaceStructure` → `StartConstruction` → `BaseStructure.CompleteConstruction`)을
거친 건물만 그리드 셀에 정확히 맞춰 스폰되고 `SetGridInfo()`로 `GridData`에 등록됐음. 반면 씬 에디터에
직접 끌어다 놓은 건물(시작 건물, 테스트용 건물, 캠페인의 적 건물 전부)은:

- 위치가 그리드 셀 중앙에 맞다는 보장이 없었음(에디터에서 대충 배치하면 그대로)
- `hasGridPosition`이 계속 `false`로 남아 `GridData`에 전혀 등록되지 않았음 → 나중에 플레이어가 그
  자리에 겹쳐서 새 건물을 지으려 해도 `StructureData.CanPlaceObejctAt`이 이를 막지 못함(그 칸이 이미
  건물로 차 있다는 걸 시스템이 모르므로)

## 수정 내용

**`Assets/Scripts/BuildSystem/PlacementSystem.cs`**
- `StructureData = new();`를 `Start()`에서 `Awake()`로 이동. 유니티는 씬의 모든 오브젝트의 `Awake()`가
  전부 끝난 뒤에야 `Start()`들이 실행되지만, 서로 다른 게임오브젝트의 `Start()`끼리의 실행 순서는
  보장하지 않는다. `BuildingController.Start()`/`EnemyBuildingController.Start()`가 이 스크립트의
  `StructureData`를 참조해야 하는데, 만약 `PlacementSystem.Start()`보다 먼저 실행되면 `StructureData`가
  아직 `null`인 상태로 호출될 위험이 있어 `Awake()`로 옮겨 항상 준비되도록 함.
- `GetGroundPosition(Vector3Int, Vector2Int, float)`을 `private` → `public`으로 변경 (건물 스크립트가
  자기 그리드 셀 중앙 좌표를 계산할 때 재사용).
- `WorldToGridCell(Vector3 worldPos)` 추가 - `grid.WorldToCell`의 공개 래퍼. 건물이 자기 현재 위치로부터
  자기 그리드 셀을 역산할 때 사용.
- `RegisterBuildingGrid(GameObject buildingObject, Vector3Int gridPos, Vector2Int size, int id)` 추가 -
  `StructureData.CanPlaceObejctAt`으로 먼저 확인(겹치면 `Debug.LogWarning`만 남기고 `false` 반환 -
  `GridData.AddObjectAt`은 겹치면 예외를 던지므로 반드시 먼저 확인해야 함), 통과하면 `placedGameObject`에
  추가하고 `StructureData.AddObjectAt`으로 실제 등록 후 `true` 반환.

**`Assets/Scripts/Building/BuildingController.cs`**
- `Start()`에서 `SnapToGround()` 직후, `!hasGridPosition`일 때만(=정상 건설 흐름을 거치지 않은 건물만)
  `RegisterToGridIfPossible()` 호출.
- `RegisterToGridIfPossible()` 추가: `rtsController.GetBuildingData(buildingID)`로 자기 크기(`Size`)를
  조회 → `placementSystem.WorldToGridCell(transform.position)`으로 그리드 셀 역산 →
  `placementSystem.RegisterBuildingGrid(...)`로 등록 시도 → 성공하면 `placementSystem.GetGroundPosition(...)`
  으로 XZ를 그리드 셀 중앙에 맞춰 재배치(Y는 방금 `SnapToGround()`로 맞춘 값을 그대로 넘겨 유지 - [[0150]]과
  동일하게 그리드 셀 크기로 Y를 되돌리면 지형 높이와 어긋나므로 건드리지 않음) → `SetGridInfo(gridPos)`로
  마무리.

**`Assets/Scripts/System/RTSUnitController.cs`**
- `GetBuildingData(int buildingID)` 추가 - 기존 `buildingDatabase`(NTA용, 지금까지 내부적으로만 쓰였음)를
  외부에서 조회할 수 있게 공개하는 접근자. `GetUnitData`/`GetEnemyBuildingData`와 동일한 패턴.

**`Assets/Scripts/Enemy/EnemyBuildingController.cs`**
- `placementSystem` 필드 추가, `Start()`에서 `FindFirstObjectByType<PlacementSystem>()`로 확보.
- `RegisterToGridIfPossible()` 추가 - `BuildingController`와 동일한 패턴, `enemyBuildingID`로
  `GetEnemyBuildingData()` 조회 후 그리드 등록 + XZ 정렬. `SnapToGround()` 다음, `ApplyBuildingData()`
  이전에 호출.

## 참고

- `GridData.AddObjectAt`이 겹치는 칸에 예외를 던지는 구조라, 캠페인 씬에서 실수로 건물 두 개를 겹쳐
  배치해두면 뒤에 `Start()`가 실행되는 쪽이 등록에 실패하고(경고 로그만 남고 조용히 무시) 그리드 정렬도
  건너뛴다 - 크래시는 안 나지만, 씬 배치가 겹쳤다는 신호이므로 콘솔 경고를 확인해서 고쳐야 함.
- 건물이 파괴될 때(`Die()`) 그리드 점유를 해제하는 로직은 아군/적 둘 다 원래 없었음(`LiftOff()`만
  `ReleaseBuildingGrid`를 호출) - 이번 요청 범위 밖이라 손대지 않음. 필요해지면 별도로 요청해달라.

## 변경 파일

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/Enemy/EnemyBuildingController.cs`
