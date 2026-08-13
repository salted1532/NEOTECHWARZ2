# 0575 - 모든 적 건물 공격 시 주변 병력 소집 + 홈 빌딩은 2배 반경 (구현 완료)

## Q1. 홈 빌딩 말고 다른 적 건물도 공격받으면 주변 유닛이 오는가? (변경 전 상태)

**아니오였다.** `EnemyAIDirector.OnEnable()`은 인스펙터의 `homeBuildings` 리스트(doc/0535)에 들어있는
건물에만 `OnDamaged` 핸들러를 등록했다. 그 리스트에 없는 다른 적 건물(생산 건물, 배치형 방어 유닛
주변 건물 등)은 공격받아도 `HandleBaseAttacked` 소집 로직이 실행되지 않았다.

## Q2. 요청 (최종)

1. **모든 적 건물**이 공격받으면 주변 유닛을 부르도록 확장.
2. 그중 `homeBuildings`로 지정한 건물은 `defenseRadius`의 **2배** 반경에서 소집, 나머지는 기존
   `defenseRadius` 그대로.

## 구현

`Assets/Scripts/System/EnemyAIDirector.cs`

### 1) 구독 대상을 `homeBuildings` → 씬의 모든 적 건물로 확장

`EnemyBuildingController`엔 이미 씬 전체 적 건물 레지스트리가 있었다(`ActiveBuildings` +
`OnActiveBuildingsChanged`, 건물 파괴/스테이지 목표용으로 기존에 존재). 이 레지스트리를 그대로 재사용해
건물이 생기거나 파괴될 때마다 핸들러를 맞춘다.

```diff
-    private void OnEnable()
-    {
-        foreach (EnemyBuildingController building in homeBuildings)
-        {
-            if (building == null || building.GetHealthManager() == null)
-                continue;
-
-            EnemyBuildingController capturedBuilding = building;
-            System.Action<int, Vector3, AttackEffectType, bool> handler =
-                (damage, attackerPosition, type, isEnemyAttacker) => HandleBaseAttacked(capturedBuilding, attackerPosition, isEnemyAttacker);
-
-            baseDefenseHandlers[building] = handler;
-            building.GetHealthManager().OnDamaged += handler;
-        }
-    }
-
-    private void OnDisable()
-    {
-        foreach (var pair in baseDefenseHandlers)
-            if (pair.Key != null && pair.Key.GetHealthManager() != null)
-                pair.Key.GetHealthManager().OnDamaged -= pair.Value;
-
-        baseDefenseHandlers.Clear();
-    }
+    private void OnEnable()
+    {
+        EnemyBuildingController.OnActiveBuildingsChanged += SyncBuildingDefenseHandlers;
+        SyncBuildingDefenseHandlers();
+    }
+
+    private void OnDisable()
+    {
+        EnemyBuildingController.OnActiveBuildingsChanged -= SyncBuildingDefenseHandlers;
+
+        foreach (var pair in baseDefenseHandlers)
+            if (pair.Key != null && pair.Key.GetHealthManager() != null)
+                pair.Key.GetHealthManager().OnDamaged -= pair.Value;
+
+        baseDefenseHandlers.Clear();
+    }
+
+    private void SyncBuildingDefenseHandlers()
+    {
+        List<EnemyBuildingController> stale = new List<EnemyBuildingController>();
+        foreach (var pair in baseDefenseHandlers)
+            if (pair.Key == null || !EnemyBuildingController.ActiveBuildings.Contains(pair.Key))
+                stale.Add(pair.Key);
+
+        foreach (EnemyBuildingController building in stale)
+        {
+            if (building != null && building.GetHealthManager() != null)
+                building.GetHealthManager().OnDamaged -= baseDefenseHandlers[building];
+            baseDefenseHandlers.Remove(building);
+        }
+
+        foreach (EnemyBuildingController building in EnemyBuildingController.ActiveBuildings)
+        {
+            if (building == null || baseDefenseHandlers.ContainsKey(building) || building.GetHealthManager() == null)
+                continue;
+
+            EnemyBuildingController capturedBuilding = building;
+            System.Action<int, Vector3, AttackEffectType, bool> handler =
+                (damage, attackerPosition, type, isEnemyAttacker) => HandleBaseAttacked(capturedBuilding, attackerPosition, isEnemyAttacker);
+
+            baseDefenseHandlers[building] = handler;
+            building.GetHealthManager().OnDamaged += handler;
+        }
+    }
```

부수 효과: `EnemyBuildingController`는 자기 `Start()`에서 `healthManager`를 먼저 캐싱한 뒤
`ActiveBuildings.Add(this)`로 등록하므로, 이 이벤트로 걸리는 건물은 항상 `GetHealthManager()`가
준비된 상태다 - 기존 `homeBuildings` 방식은 director의 `OnEnable()`이 건물의 `Start()`보다 먼저 돌 수
있어(Unity 실행 순서상 OnEnable(all) → Start(all)) `GetHealthManager() == null`로 조용히 스킵될
가능성이 있었다.

### 2) 반경을 건물 종류에 따라 분기

```diff
     private void HandleBaseAttacked(EnemyBuildingController building, Vector3 attackerPosition, bool isEnemyAttacker)
     {
         if (isEnemyAttacker)
             return;

-        foreach (EnemyUnitController unit in FindNearbyEnemyUnits(building.transform.position))
+        float radius = homeBuildings.Contains(building) ? defenseRadius * 2f : defenseRadius;
+
+        foreach (EnemyUnitController unit in FindNearbyEnemyUnits(building.transform.position, radius))
             if (!deployed.Contains(unit) && unit.IsIdle())
                 unit.AttackMoveTo(attackerPosition);
     }

-    private List<EnemyUnitController> FindNearbyEnemyUnits(Vector3 center)
+    private List<EnemyUnitController> FindNearbyEnemyUnits(Vector3 center, float radius)
     {
         List<EnemyUnitController> found = new List<EnemyUnitController>();

-        foreach (Collider hit in Physics.OverlapSphere(center, defenseRadius))
+        foreach (Collider hit in Physics.OverlapSphere(center, radius))
             ...
```

`FindNearbyEnemyUnits`는 `HandleBaseAttacked`에서만 호출되므로 시그니처 변경에 따른 다른 호출부 영향
없음.

### 3) 인스펙터 헤더/주석 갱신

`homeBuildings`/`defenseRadius` 필드의 역할이 "구독 대상 목록"에서 "2배 반경을 쓸 건물 목록"으로
바뀐 것을 반영해 헤더 주석을 업데이트.

## 결과

- 씬의 모든 `EnemyBuildingController`(생산 건물 포함, 나중에 생기거나 파괴되는 것 포함)가 공격받으면
  주변 유휴 적 유닛을 불러 반격하러 보낸다.
- `homeBuildings`로 지정한 건물만 `defenseRadius * 2`(기본 30) 반경, 나머지는 `defenseRadius`
  그대로(기본 15).
- 컴파일 확인: `uloop compile` → `Success: true, ErrorCount: 0` (기존 경고 40개는 이 변경과 무관).
