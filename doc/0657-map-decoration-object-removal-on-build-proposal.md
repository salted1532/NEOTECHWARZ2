# 0657 - 건설 시 그리드 위 장식 오브젝트(나무/풀) 자동 삭제 (제안)

## 요청
맵에 나무/풀 같은 장식 오브젝트(콜라이더 없음, 메쉬만 존재)를 배치할 예정. 해당 오브젝트를 tag 또는 layer로 "MapObject"라 구분해두고, 그 위에 건물을 건설하면 자동으로 삭제되게 하고 싶음.

## 현재 상태
- 건물 배치/충돌 판정은 두 트랙으로 이뤄진다.
  1. `GridData`(`Assets/Scripts/BuildSystem/GridData.cs`) - Vector3Int 그리드 셀 단위 점유 딕셔너리. `PlacementSystem.StructureData`가 들고 있고, `CanPlaceObejctAt`/`AddObjectAt`/`RemoveObjectAt`으로 겹침만 순수하게 검사한다.
  2. `PlacementSystem.IsBlockedAtCenter()`(`PlacementSystem.cs:411`) - `Physics.OverlapBox` + `blockingLayers`(LayerMask)로 실제 3D 콜라이더 충돌을 검사한다.
- 장식 오브젝트는 **콜라이더가 없다고 했으므로** 2번 물리 검사에 애초에 걸리지 않는다 - Layer를 뭘로 주든 `Physics.OverlapBox`는 콜라이더가 없으면 아무것도 찾지 못한다. 즉 지금 상태 그대로면 나무 위에 건물을 지어도 배치 자체는 막히지 않고, 나무만 건물 속에 그대로 남아 시각적으로 파묻힌다.
- 건물이 실제로 생성되는 시점은 클릭 즉시가 아니라 `PlacementSystem.StartConstruction()`(`PlacementSystem.cs:206`) - 일꾼이 도착한 뒤 `BaseStructure` 프리팹을 `groupPos`에 인스턴스화한다. 클릭 시점(`PlaceStructure()`)엔 그리드만 예약(`StructureData.AddObjectAt`)하고 고스트만 세워둔다.
- `GridData`에 이미 `CalculatePositionsPublic(gridPosition, objectSize)`(`GridData.cs:45`)이 공개돼 있어, 건물이 차지할 모든 그리드 셀 좌표 리스트를 별도 계산 없이 그대로 재사용할 수 있다.

## 결정 사항 (확인 완료)
- **구분 방식: Tag** ("MapObject"). 장식 오브젝트는 콜라이더가 없어 Layer 기반 물리 검사(OverlapBox/Raycast)로는 애초에 찾을 수 없다 - Layer를 쓸 이유가 없다. `GameObject.CompareTag`/`FindGameObjectsWithTag`로 직접 찾는 Tag 방식이 맞다. `ProjectSettings/TagManager.asset`의 `tags` 목록에 `MapObject`를 추가한다 (현재 비어있는 태그 슬롯 없음 - 목록 끝에 추가).
- **삭제 시점: 실제 건설 시작(`StartConstruction`) 때.** 클릭 즉시(`PlaceStructure`)가 아니라, 일꾼이 도착해 `BaseStructure`가 실제로 생성되는 순간에 삭제한다. 이렇게 하면 일꾼이 도착하기 전 건설이 취소돼도(다른 이동 명령 등, `onCancelled` 콜백) 나무가 이미 사라져버리는 손실이 없다.

## 제안 설계
1. `ProjectSettings/TagManager.asset`의 `tags:` 목록에 `MapObject` 추가.
2. 장식 오브젝트 프리팹(나무/풀)에 `MapObject` 태그 부여 (콜라이더/스크립트 없이 메쉬만 - 요청대로 태그만 붙이면 됨, 별도 컴포넌트 불필요).
3. `PlacementSystem.cs`에 헬퍼 추가:
   ```csharp
   // 해당 그리드 셀 범위 안에 있는 장식 오브젝트(MapObject 태그)를 전부 제거한다.
   // 콜라이더가 없어 물리 검사(IsBlockedAtCenter)로는 찾을 수 없으므로 태그로 직접 조회한다.
   private void RemoveMapObjectsInFootprint(Vector3Int gridPos, Vector2Int size)
   {
       HashSet<Vector3Int> occupiedCells = new(StructureData.CalculatePositionsPublic(gridPos, size));
       foreach (GameObject deco in GameObject.FindGameObjectsWithTag("MapObject"))
       {
           if (occupiedCells.Contains(grid.WorldToCell(deco.transform.position)))
               Destroy(deco);
       }
   }
   ```
4. `StartConstruction()`(`PlacementSystem.cs:206`)에서, 장애물 재검사(`IsBlockedAtCenter`) 통과 직후 · `BaseStructure` 인스턴스화 직전에 `RemoveMapObjectsInFootprint(gridPos, data.Size)` 호출.

## 범위 밖
- 건물 리프트 재배치 착륙(`PlaceRelocatedBuilding`/`BeginRelocationFlight`) 시점의 장식 제거 - 요청은 "건설" 한정이라 포함하지 않음. 필요하면 같은 헬퍼를 `onLanded` 콜백에서 재사용해 후속 추가 가능.
- 게임 시작 시 즉시 스폰되는 메인기지(`SpawnStartingMainBase`) 주변 장식 제거 - 시작 위치는 보통 미리 정리된 자리라고 가정, 필요시 후속 추가.
- 나무/풀을 씬에 실제로 배치하는 작업(스캐터링, 프리팹 제작) 자체 - 이 문서는 "건설 시 제거" 로직만 다룸.

## 구현 완료
- `ProjectSettings/TagManager.asset`: `tags` 목록에 `MapObject` 추가.
- `PlacementSystem.cs`: `RemoveMapObjectsInFootprint(gridPos, size)` 헬퍼 추가 - `StructureData.CalculatePositionsPublic`으로 얻은 점유 셀 집합과 `FindGameObjectsWithTag("MapObject")`를 대조해 겹치는 오브젝트를 `Destroy`.
- `StartConstruction()`에서 고스트 삭제 직후 · `BaseStructure` 인스턴스화 직전에 호출.
- 컴파일 확인: 에러 0, 기존 경고(무관한 `FindFirstObjectByType` obsolete 등) 49개만 유지, 신규 에러/경고 없음.

## 남은 작업 (사용자)
- 나무/풀 프리팹에 `MapObject` 태그를 직접 부여해야 한다 (프리팹/씬 작업은 이 문서 범위 밖, 코드 쪽은 완료).
