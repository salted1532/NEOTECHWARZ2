# 0525. 배치/착륙 불가(빨간 프리뷰) 위치 클릭 시 경고 - 제안

**날짜:** 2026-08-11

## 요청 내용

> 건설 불가나 착륙 불가능한곳에(프리뷰가 빨간색)인데 클릭하면 그때도 나오도록해줘
> (이어서) 모든 실패 행동에 다 공통적으로 나오도록해줘

[[doc/0524]]에서 `UIController.ShowWarning()`에 공통 실패 SFX를 걸어뒀지만, 그건 `ShowWarning()`이
실제로 호출될 때만 작동한다. 건물 배치(`PlacementSystem.PlaceStructure`)와 착륙 위치 선택
(`PlacementSystem.PlaceRelocatedBuilding`)은 프리뷰가 빨간색(배치 불가)인 자리를 클릭해도 지금은
`ShowWarning()`을 아예 호출하지 않고 조용히 `return`만 해서 - 텍스트도 SFX도 전혀 안 나온다.

## 조사 내용

`PlacementSystem.cs`의 프리뷰 색상은 `Update()` (596~601행)에서 아래 6개 조건을 전부 만족해야
흰색(valid), 하나라도 실패하면 빨간색(invalid)이다:

```csharp
bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
    && !IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null)
    && !IsTooCloseToResource(data.ID, gridPos, data.Size)
    && IsInsideAlliedTerritory(gridPos, data.Size)
    && IsFootprintTerrainFlat(gridPos, data.Size)
    && HasTerrainMargin(gridPos, data.Size);
```

그런데 실제 클릭 핸들러 `PlaceStructure()`(149~173행)와 `PlaceRelocatedBuilding()`(298~303행)는 이
6개 조건을 각각 별도의 `if (...) return;`으로 다시 검사한다 - 즉 같은 조건이 세 곳(Update 1번 +
클릭 핸들러 2번)에 중복돼 있다. 지금 요청대로 6곳 × 2 클릭 핸들러 = 12곳에 개별로 경고를 추가하면
중복이 더 늘어나고, 나중에 조건이 하나 추가/변경될 때 세 곳을 매번 같이 고쳐야 하는 채로 남는다.

그래서 이번엔 이 6개 조건을 판정용 메서드 `IsValidPlacement(...)` 하나로 합쳐서 `Update()`와 두
클릭 핸들러가 전부 이 메서드 하나만 부르게 한다 - 조건이 어긋날 일이 없어지고, 클릭 핸들러 쪽엔
"실패하면 경고" 한 줄만 추가하면 된다.

- 건물 배치 클릭(`PlaceStructure`) 실패 → 기존 `warning.constructionfail`("다른 곳에 건설하세요.")
  키를 그대로 재사용 - 같은 상황(여기엔 못 지음)이라 새 키가 필요 없음.
- 착륙 위치 클릭(`PlaceRelocatedBuilding`) 실패 → 건설이 아니라 착륙이므로 새 키
  `warning.landingblocked`("다른 곳에 착륙하세요." / "Land elsewhere.") 추가.

두 경고 모두 [[doc/0524]]에서 `ShowWarning()`에 걸어둔 공통 실패 SFX가 그대로 같이 울린다 - 이 SFX
쪽은 추가 작업 없음.

(참고: `worker == null`일 때(선택된 일꾼 없음) `return`하는 부분과, 자원/인구 부족으로
`TryConstructBuilding`이 실패하는 부분은 프리뷰 색상과 무관한 별개의 실패라 이번 요청 범위 밖 -
`TryConstructBuilding` 실패는 이미 자체적으로 `ShowWarning()`을 호출하고 있어 그대로 SFX가 나옴.)

## 변경 계획

### `PlacementSystem.cs`

**1. 공용 판정 메서드 추가** (Update() 바로 위):
```diff
+    // 배치 가능 여부 판정 (프리뷰 색상 갱신 + 클릭 검증 공용) - Update()와 클릭 핸들러가 항상 같은
+    // 조건을 쓰도록 한 곳에 모은다 (doc/0525 - 예전엔 세 곳에 조건이 중복돼 있었음).
+    private bool IsValidPlacement(Vector3 mousePos, Vector3Int gridPos, BuildingData data, GameObject ignoreObject)
+    {
+        return StructureData.CanPlaceObejctAt(gridPos, data.Size)
+            && !IsBlocked(mousePos, data.Size, ignoreObject)
+            && !IsTooCloseToResource(data.ID, gridPos, data.Size)
+            && IsInsideAlliedTerritory(gridPos, data.Size)
+            && IsFootprintTerrainFlat(gridPos, data.Size)
+            && HasTerrainMargin(gridPos, data.Size);
+    }
+
     void Update()
     {
         ...
-            bool valid = StructureData.CanPlaceObejctAt(gridPos, data.Size)
-                && !IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null)
-                && !IsTooCloseToResource(data.ID, gridPos, data.Size)
-                && IsInsideAlliedTerritory(gridPos, data.Size)
-                && IsFootprintTerrainFlat(gridPos, data.Size)
-                && HasTerrainMargin(gridPos, data.Size);
+            bool valid = IsValidPlacement(mousePos, gridPos, data, worker != null ? worker.gameObject : null);
```

**2. `PlaceStructure()`** - 6개 개별 `if` 블록을 `IsValidPlacement` 한 번 + 실패 시 경고로 교체
(worker를 먼저 가져오도록 순서만 앞당김):
```diff
         var data = database.buildingData[selectedObjectIndex];

-        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
-            return;
-
         // 현재 선택된 일꾼은 장애물 판정에서 제외 - 일꾼이 서 있는 자리에도 건물을 지을 수 있게
         UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;

-        // ⭐ 유닛 체크 추가
-        if (IsBlocked(mousePos, data.Size, worker != null ? worker.gameObject : null))
-            return;
-
-        // ⭐ 자원(광물/가스)과 너무 가까우면 배치 불가
-        if (IsTooCloseToResource(data.ID, gridPos, data.Size))
-            return;
-
-        // ⭐ 아군 영토 밖이면 배치 불가
-        if (!IsInsideAlliedTerritory(gridPos, data.Size))
-            return;
-
-        // ⭐ 절벽/벽면에 걸쳐 있으면 배치 불가 (doc/0376)
-        if (!IsFootprintTerrainFlat(gridPos, data.Size))
-            return;
-
-        // ⭐ 맵 가장자리 1칸 여백 검사 (doc/0380)
-        if (!HasTerrainMargin(gridPos, data.Size))
-            return;
+        if (!IsValidPlacement(mousePos, gridPos, data, worker != null ? worker.gameObject : null))
+        {
+            UIController.Instance?.ShowWarning(LocalizationManager.GetText("warning.constructionfail")); // 빨간 프리뷰 클릭(doc/0525)
+            return;
+        }

         if (worker == null)
             return; // 건설을 맡을 일꾼이 없으면 배치하지 않음
```

**3. `PlaceRelocatedBuilding()`** - 동일 패턴 (ignoreObject 없음, 착륙 전용 경고 키):
```diff
         var data = database.buildingData[selectedObjectIndex];

-        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size)) return;
-        if (IsBlocked(mousePos, data.Size)) return;
-        if (IsTooCloseToResource(data.ID, gridPos, data.Size)) return;
-        if (!IsInsideAlliedTerritory(gridPos, data.Size)) return;
-        if (!IsFootprintTerrainFlat(gridPos, data.Size)) return; // 절벽/벽면에 걸쳐 있으면 배치 불가 (doc/0376)
-        if (!HasTerrainMargin(gridPos, data.Size)) return; // 맵 가장자리 1칸 여백 검사 (doc/0380)
+        if (!IsValidPlacement(mousePos, gridPos, data, null))
+        {
+            UIController.Instance?.ShowWarning(LocalizationManager.GetText("warning.landingblocked")); // 빨간 프리뷰 클릭(doc/0525)
+            return;
+        }
```

### `en.json` / `ko.json`
```json
{ "key": "warning.landingblocked", "value": "Land elsewhere." }
{ "key": "warning.landingblocked", "value": "다른 곳에 착륙하세요." }
```

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Resources/Localization/en.json`, `ko.json`

---

## 적용 (사용자 승인 후)

> 진행 (Recommended)

제안대로 적용함. `IsValidPlacement()` 공용 메서드를 추가하고 `Update()`/`PlaceStructure()`/
`PlaceRelocatedBuilding()` 세 곳이 전부 이걸 쓰도록 교체, 두 클릭 핸들러에 실패 시 `ShowWarning()`
호출을 추가함. `warning.landingblocked` 키를 `en.json`/`ko.json`에 추가함.

`npx uloop-cli compile` 성공 확인 (Error 0개, Warning 37개는 전부 이번 변경과 무관한 기존
`FindFirstObjectByType` obsolete API 경고).

## 변경된 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Resources/Localization/en.json`, `ko.json`
