# 0350 — 버그수정(조사): 3x3 건물을 인접 배치 시 일꾼 도착(건설 시작) 시점에 오판정으로 취소되는 문제

**날짜:** 2026-08-01

## 질문

"건물을 딱 붙여서 지을때 아직도 안지어지는 버그가 있네. 정확히는 base구조물 건물 지어지는 와중에 오브젝트가
건물 위에는 건물 못지는거 때문에 막히는거 같아. 그 프리팹이 크기가 미세하게 커서 그런거 같아. 완성된 건물에는
그리드로 건설이 막혀 있으니깐 괜찮은거 같아."

[[0345-bugfix-testing-feedback-batch]]의 "버그 9"에서 `IsBlocked()`의 물리 판정 여유(`margin`)를 `0.02` → `0.1`로
늘리는 수정을 이미 적용했지만, 그 문서에도 "가설 기반, 재테스트 필요"라고 남겨뒀던 항목이고 — 이번 재현 보고로
그 수정만으로는 해결되지 않았음이 확인됨.

## 원인 확인

`BaseStructure` 프리팹 자체(콜라이더 크기, 스케일)는 원인이 아니었다. `Assets/prefabs/NTA(OC)/Building/BaseStructure.prefab`의
`BoxCollider`는 `m_Size: {1,1,1}` 로컬 크기이고, `BaseStructure.Initialize()`(`Assets/Scripts/Building/BaseStructure.cs:62-63`)가
`transform.localScale`을 `buildingSize * cellSize`로 정확히 맞춰 덮어쓰므로, 완성된 건물(`MainBase.prefab` 등, 동일하게
`m_Size:{1,1,1}` + 고정 스케일)과 풋프린트 산정 방식이 완전히 동일함 — "프리팹이 미세하게 크다"는 체감과 달리 실측 크기 차이는 없음.

진짜 원인은 **`PlacementSystem.StartConstruction()`이 이미 중앙정렬까지 끝난 좌표를 `IsBlocked()`에 다시 넣어서
중앙정렬 연산이 두 번 적용되는 버그**다.

- `PlacementSystem.cs:173` `Vector3 groundPos = GetGroundPosition(gridPos, data.Size, mousePos.y);` — 여기서 이미
  "건물이 차지하는 N×N 칸 전체의 기하학적 중심" 좌표로 변환됨(`GetGroundPosition`이 `(size-1) * cellSize * 0.5`만큼
  기준 셀 코너에서 밀어서 중심을 구함, `PlacementSystem.cs:345-359`).
- `PlacementSystem.cs:211` `StartConstruction()`이 이 이미-중심좌표인 `groundPos`를 그대로 `IsBlocked(groundPos, data.Size, worker.gameObject)`에 넘김.
- `IsBlocked()` 내부(`PlacementSystem.cs:385-421`)는 받은 좌표를 "아직 안 스냅된 마우스 좌표"로 취급해서
  `grid.WorldToCell(worldPos)`로 **다시 셀을 역산**한 뒤(`PlacementSystem.cs:389`), 그 셀 기준으로 `GetGroundPosition`을
  **한 번 더** 호출해서 중심을 또 계산한다(`PlacementSystem.cs:362-365, GetPlacementWorldPosition`).
- 문제는 `GetGroundPosition`이 만든 "중심 좌표"를 `grid.WorldToCell`로 되돌리면 **원래의 기준 셀(gridPos)로 돌아오지
  않는다**는 것. `Grid.CellToWorld`/`WorldToCell`은 셀의 "아래쪽 모서리" 기준이라, 중심 오프셋이 셀 하나 폭(`cellSize`)의
  절반을 넘는 크기(N이 3 이상일 때 `(N-1)*0.5 >= 1`)면 `WorldToCell`이 **한 칸 옆 셀**로 잘못 판정한다.
  - 2x2 건물(SupplyDepot/Lab): 오프셋 = `(2-1)*0.5 = 0.5` → 같은 셀로 되돌아옴(우연히 안전).
  - **3x3 건물(CommandCenter/Barracks/Factory/Spaceport): 오프셋 = `(3-1)*0.5 = 1.0` → 정확히 한 칸(2유닛) 옆
    셀로 잘못 판정됨.**
  - 그 잘못된 셀을 기준으로 `GetGroundPosition`을 또 호출하면 중심이 X/Z 양쪽으로 **정확히 2유닛(cellSize 1칸)만큼
    실제 풋프린트 중심에서 벗어난 자리**에 판정 박스가 세워진다.
- 결과: 3x3 건물이 자기 자리에 도착해서 `StartConstruction()`이 실행되는 순간, 실제로 검사해야 할 곳이 아니라
  **자기 건물 자리에서 +X/+Z 방향으로 한 칸 밀린 위치**를 장애물 검사 박스로 검사한다. 그 밀린 위치(정확히는 원래
  풋프린트의 절반만 겹치고 나머지 절반은 이웃 칸까지 침범)에 다른 건물(딱 붙여 지은 이웃)이 있으면 `IsBlocked()`가
  `true`를 반환 → `worker.PlayBuildFailVoice()` + `CancelReservedConstruction()` + `RefundBuilding()`으로 **건설이
  취소·환불**된다. 클릭 시점(`PlaceStructure()`)의 `IsBlocked(mousePos, ...)`는 원본 마우스 좌표(아직 중심정렬 전)를
  그대로 넘기므로 이 이중 오프셋이 없어 정상 통과하고, 일꾼이 도착한 뒤(`StartConstruction()`)에야 실패한다 — 사용자가
  "base구조물 지어지는 와중에 막힌다"고 관찰한 것과 정확히 일치한다.
- 완성된 건물끼리 딱 붙여 배치하려는 시도가 "그리드로 막혀 있어서 괜찮다"고 한 부분은 별개다 — 그건 애초에 겹치는
  칸에 대한 정상적인 `CanPlaceObejctAt` 거부(의도된 동작)이고, 이번 버그와는 무관하다.
- 왜 3x3 건물만 겪는지: `BuildingDataSO`상 3x3 건물은 CommandCenter/Barracks/Factory/Spaceport, 2x2는
  SupplyDepot/Lab뿐이라 이 프로젝트의 건물 크기 조합에서는 정확히 "3x3만 위험" 조건과 일치한다.

## 제안 수정

`IsBlocked()`가 "아직 스냅 안 된 좌표"와 "이미 중심정렬된 좌표"를 구분 못 하는 게 근본 원인이므로, 중심 좌표를 받는
전용 경로를 하나 더 둬서 이중 변환 자체를 없앤다 (`Assets/Scripts/BuildSystem/PlacementSystem.cs`):

```diff
-    private bool IsBlocked(Vector3 worldPos, Vector2Int size, GameObject ignoreObject = null)
+    // worldPos: 아직 그리드에 스냅되지 않은 원시 좌표(마우스 위치 등) - 내부에서 셀을 역산해 중심을 구한다.
+    private bool IsBlocked(Vector3 worldPos, Vector2Int size, GameObject ignoreObject = null)
     {
-        Vector3 cellSize = grid.cellSize;
-
-        Vector3 center = GetPlacementWorldPosition(grid.WorldToCell(worldPos), size, worldPos.y);
+        Vector3 center = GetPlacementWorldPosition(grid.WorldToCell(worldPos), size, worldPos.y);
+        return IsBlockedAtCenter(center, size, ignoreObject);
+    }

+    // groundCenter: 이미 건물 풋프린트의 기하학적 중심으로 계산된 좌표(GetGroundPosition의 반환값 등).
+    // 다시 WorldToCell로 역산하지 않고 그대로 사용한다 - 되돌리면 3x3 이상 건물에서 한 칸 옆으로 오판정되는 버그가 있었음.
+    private bool IsBlockedAtCenter(Vector3 groundCenter, Vector2Int size, GameObject ignoreObject = null)
+    {
+        Vector3 cellSize = grid.cellSize;
+        Vector3 center = groundCenter + Vector3.up * yOffset;

         const float margin = 0.1f;
         Vector3 halfExtents = new Vector3(
             size.x * cellSize.x * 0.5f - margin,
             1f,
             size.y * cellSize.z * 0.5f - margin
         );

         Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, blockingLayers);
         ...
     }
```

`StartConstruction()`(`PlacementSystem.cs:211`)에서 이미 중심좌표인 `groundPos`를 넘길 땐 새 경로를 쓰도록 교체:

```diff
-        if (IsBlocked(groundPos, data.Size, worker.gameObject))
+        if (IsBlockedAtCenter(groundPos, data.Size, worker.gameObject))
```

`PlaceStructure()`/`PlaceRelocatedBuilding()`/`Update()`의 기존 `IsBlocked(mousePos, ...)` 호출 3곳은 원시 마우스
좌표를 넘기는 게 맞으므로 그대로 둔다(수정 불필요, `IsBlocked()`가 내부적으로 새 헬퍼를 호출하도록만 리팩터).

## 적용 결과 (2026-08-01)

사용자 확인 후 제안한 diff 그대로 적용.

- **`Assets/Scripts/BuildSystem/PlacementSystem.cs`**: 기존 `IsBlocked(Vector3 worldPos, ...)`는 내부 로직을
  `IsBlockedAtCenter(Vector3 groundCenter, ...)`로 옮기고, 자신은 `grid.WorldToCell(worldPos)` → `GetPlacementWorldPosition()`으로
  중심을 구한 뒤 `IsBlockedAtCenter()`를 호출하는 얇은 래퍼로 변경. `StartConstruction()`의 `IsBlocked(groundPos, ...)` 호출을
  `IsBlockedAtCenter(groundPos, ...)`로 교체해 이미 중심정렬된 좌표가 다시 `WorldToCell`을 거치지 않도록 함.
  `PlaceStructure()`/`PlaceRelocatedBuilding()`/`Update()`의 기존 `IsBlocked(mousePos, ...)` 호출 3곳은 원시 마우스 좌표를
  넘기는 게 맞아서 그대로 둠(수정 불필요).
- `npx uloop-cli compile` 통과 (에러 0, 경고 27개 — 전부 이번 변경과 무관한 기존 경고(`FindFirstObjectByType` 등), 이번 변경으로 인한 신규 경고 없음).

**확인 필요 사항**: Unity 에디터에서 CommandCenter/Barracks/Factory/Spaceport(3x3)를 서로 딱 붙여서 지어도 일꾼 도착
시점에 취소되지 않는지, 기존 2x2(SupplyDepot/Lab) 인접 배치는 계속 정상인지 실제 플레이로 확인 부탁.
