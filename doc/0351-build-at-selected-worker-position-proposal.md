# 0351 — 건설모드: 선택된 일꾼의 현재 위치에도 건물 배치 허용 (제안)

**날짜:** 2026-08-01

## 요청

"건설모드에서 현재 선택된 일꾼에 경우는 일꾼 위치에 건물을 지을수 있게 해줘"

## 원인 확인

`PlacementSystem.IsBlocked()`(`Assets/Scripts/BuildSystem/PlacementSystem.cs:381-421`)는 `Physics.OverlapBox`로
`blockingLayers`(Unit/Enemy/Building/Ore 레이어)에 속한 콜라이더가 배치 영역에 있으면 무조건 배치를 막는다. 일꾼도
`Unit` 레이어라 이 검사에 걸리므로, 지금은 **일꾼이 서 있는 칸에는 그 어떤 건물도 못 짓는다** — 클릭 순간의 미리보기
(`Update()`, `PlacementSystem.cs:487-512`)도 빨간색(불가)으로 뜨고, 실제 클릭(`PlaceStructure()`, `PlacementSystem.cs:139-202`)도
`IsBlocked(mousePos, data.Size)`에서 막혀 배치가 거부된다.

`IsBlocked()`엔 이미 `ignoreObject` 파라미터가 있다(`StartConstruction()`에서 도착한 담당 일꾼 자신을 장애물 판정에서
빼는 용도로 씀, doc/0268). 같은 메커니즘을 재사용하면 된다 — 지금은 `Update()`/`PlaceStructure()` 두 호출부가
`ignoreObject`를 안 넘기고 있을 뿐.

## 제안 수정

`Assets/Scripts/BuildSystem/PlacementSystem.cs`:

- **`PlaceStructure()`**: 일꾼 조회(`rtsController.GetSelectedWorker()`)를 `IsBlocked()` 호출보다 앞으로 옮기고,
  조회한 일꾼을 `ignoreObject`로 넘긴다. (원래 있던 아래쪽의 "건설 맡을 일꾼 없으면 return" 체크는 그대로 유지 —
  같은 값을 재사용하도록 중복 조회 제거.)

```diff
         if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
             return;

-        // ⭐ 유닛 체크 추가
-        if (IsBlocked(mousePos, data.Size))
+        // 현재 선택된 일꾼은 장애물 판정에서 제외 - 일꾼이 서 있는 자리에도 건물을 지을 수 있게(요청)
+        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
+
+        // ⭐ 유닛 체크 추가
+        if (IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null))
             return;

         // ⭐ 자원(광물/가스)과 너무 가까우면 배치 불가
         if (IsTooCloseToResource(data.ID, gridPos, data.Size))
             return;

         // ⭐ 아군 영토 밖이면 배치 불가
         if (!IsInsideAlliedTerritory(gridPos, data.Size))
             return;

-        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
         if (worker == null)
             return; // 건설을 맡을 일꾼이 없으면 배치하지 않음
```

- **`Update()`(미리보기)**: 같은 이유로 미리보기 유효성 판정에도 동일하게 반영해야 마우스를 일꾼 위에 올렸을 때
  미리보기가 빨간색으로 오판되지 않는다.

```diff
         if (lastDectectedPosition != gridPos)
         {
             var data = database.buildingData[selectedObjectIndex];

+            UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
+
             bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
-                && !IsBlocked(mousePos, data.Size)
+                && !IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null)
                 && !IsTooCloseToResource(data.ID, gridPos, data.Size)
                 && IsInsideAlliedTerritory(gridPos, data.Size);
```

## 범위/제외

- `IsBlocked()` 시그니처(3번째 인자 `ignoreObject`)는 이미 있으므로 변경 없음 — 호출부 2곳만 수정.
- 건물 리프트 이동(착륙 위치 선택, `PlaceRelocatedBuilding()`)은 일꾼과 무관한 기능이라 이번 변경 대상 아님.
- 일꾼이 아닌 다른 유닛(전투유닛 등)이 그 자리에 서 있는 경우는 여전히 막힘(요청 범위가 "선택된 일꾼"으로 한정됨).
- 실제로 일꾼이 그 자리에서 비켜야 건설이 시작되는지 여부: `StartConstruction()`은 어차피 도착한 담당 일꾼만
  제외하고 검사하므로, 이번 변경 후에도 동작은 일관됨 — 클릭 시점에 이미 그 자리에 서 있던 그 일꾼이 그대로 담당
  일꾼이 되는 것뿐, 이동 없이 그 자리에서 바로 건설이 시작됨.

## 적용 결과 (2026-08-01)

사용자 확인 후 제안한 diff 그대로 적용.

- **`Assets/Scripts/BuildSystem/PlacementSystem.cs`**: `PlaceStructure()`에서 `rtsController.GetSelectedWorker()` 조회를
  `IsBlocked()` 호출보다 앞으로 옮기고 그 결과를 `ignoreObject`로 전달, 아래쪽의 중복 조회는 제거. `Update()`(미리보기
  유효성 판정)에도 동일하게 선택된 일꾼을 `ignoreObject`로 넘기도록 추가.
- `npx uloop-cli compile` 통과 (에러 0, 경고 27개 — 전부 이번 변경과 무관한 기존 경고, 신규 경고 없음).

**확인 필요 사항**: Unity 에디터에서 일꾼을 선택한 채 그 일꾼이 서 있는 칸에 건물을 지어봐서 (1) 미리보기가 초록색으로
뜨는지 (2) 클릭 시 실제로 그 자리에서 바로 건설이 시작되는지 확인 부탁.
