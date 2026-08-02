# 0360 — 제안: 선택한 적 유닛이 안개 속으로 들어가면 선택 해제

**날짜:** 2026-08-02

## 요청

"만약 적 유닛을 선택했는데 안개 안으로 들어가버리면 적 선택이 해제 되도록 해줘"

## 현재 구조

`RTSUnitController.cs`의 `selectedEnemyList`(적은 항상 단일 선택, `ClickSelectEnemy`/`SelectEnemy` 참고)가
선택된 적을 들고 있다. 적 건물도 같은 패턴으로 `selectedEnemyBuilding`을 들고 있고, 건물이 파괴될 때
`ClearSelectedEnemyBuildingIfMatches(building)`을 호출해 유령 참조를 정리한다(`RTSUnitController.cs:720`).
이번에도 같은 패턴(`ClearSelectedEnemyIfMatches`)을 추가해서 재사용한다.

`EnemyUnitController`는 이미 `doc/0356`부터 매 프레임 `FogVisibility.IsRevealed(fogWar,
transform.position, ...)`로 자기 자신의 안개 상태를 확인하고 있다(미니맵 마커 토글용) — 같은 결과값을
선택 해제 판단에도 그대로 재사용하면 안개 조회가 중복되지 않는다.

## 제안

**`Assets/Scripts/System/RTSUnitController.cs`** — 기존 `ClearSelectedEnemyBuildingIfMatches`와
동일한 패턴으로 추가:

```diff
+    // 안개에 가려지는 등, 외부 이벤트로 인해 특정 적 유닛의 선택을 해제해야 할 때 호출한다
+    // (ClearSelectedEnemyBuildingIfMatches와 동일한 패턴, doc/0360).
+    public void ClearSelectedEnemyIfMatches(EnemyUnitController enemy)
+    {
+        if (!selectedEnemyList.Contains(enemy))
+            return;
+
+        enemy.DeselectEnemy();
+        selectedEnemyList.Remove(enemy);
+        RTScurrentSate = SelectState.None;
+    }
```

**`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`** — 기존 `UpdateMinimapIconVisibility()`를
안개 조회 결과를 공유하도록 확장(이름도 실제 하는 일에 맞게 변경):

```diff
-        AttackMoveTick();
-        UpdateMinimapIconVisibility();
+        AttackMoveTick();
+        UpdateFogVisibility();
     }

-    // 미니맵 마커를 안개 상태에 맞춰 켜고 끈다 (doc/0356 - 마커가 Y40대라 안개 Plane 깊이 테스트로는
-    // 안 가려지는 문제의 대안). 안개 조회 로직 자체는 공용 헬퍼로 뽑음 (doc/0358).
-    private void UpdateMinimapIconVisibility()
-    {
-        if (minimapIcon == null)
-            return;
-
-        minimapIcon.enabled = FogVisibility.IsRevealed(fogWar, transform.position, minimapFogVisibilityMargin);
-    }
+    // 미니맵 마커 토글(doc/0356)과 선택 해제(doc/0360)가 같은 안개 조회 결과를 공유한다 - 매 프레임
+    // 두 번 물어볼 필요 없음.
+    private void UpdateFogVisibility()
+    {
+        bool revealed = FogVisibility.IsRevealed(fogWar, transform.position, minimapFogVisibilityMargin);
+
+        if (minimapIcon != null)
+            minimapIcon.enabled = revealed;
+
+        if (!revealed)
+            rtsController?.ClearSelectedEnemyIfMatches(this);
+    }
```

`rtsController`는 이미 `Start()`에서 캐싱돼 있는 필드를 그대로 재사용한다.

## 확인 필요 사항

- 적 "유닛"만 요청하셨는데, 적 "건물"(`EnemyBuildingController`/`selectedEnemyBuilding`)도 같은
  방식으로 안개에 가려지면 선택 해제되길 원하시는지, 아니면 유닛만 우선 적용할지
- 안개 속으로 들어가는 순간 선택이 툭 풀리는 방식(중간 연출 없음)으로 충분한지

## 적용 (2026-08-02)

"건물도 같이 적용시켜줘" — 유닛/건물 둘 다 적용.

- **`Assets/Scripts/System/RTSUnitController.cs`**: 설계안대로 `ClearSelectedEnemyIfMatches(EnemyUnitController)`
  신규 추가(`enemy.DeselectEnemy()` 호출 후 리스트에서 제거 + 상태 초기화). 기존
  `ClearSelectedEnemyBuildingIfMatches(EnemyBuildingController)`에는 `building.DeselectEnemyBuilding()`
  호출을 추가해서 안개로 가려질 때도 선택 마커가 정확히 꺼지도록 함(기존 Die() 경로는 어차피 바로
  파괴되므로 영향 없음).
- **`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`**: `UpdateMinimapIconVisibility()`를
  `UpdateFogVisibility()`로 확장 — 안개 조회 결과 하나를 미니맵 마커 토글과 `rtsController
  .ClearSelectedEnemyIfMatches(this)` 호출에 함께 씀.
- **`Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`**: `fogWar` 필드 추가(`Start()`에서
  캐싱), 건물은 안 움직이지만 플레이어 쪽 시야는 계속 바뀌므로 신규 `Update()`에서 매 프레임
  `FogVisibility.IsRevealed()`를 확인해 안 보이면 `rtsController.ClearSelectedEnemyBuildingIfMatches(this)` 호출.

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 — 신규 1건은 `EnemyBuildingController`에 추가된
`FindFirstObjectByType` obsolete 경고로 기존 프로젝트 전역 패턴과 동일, 새로운 문제 아님).
