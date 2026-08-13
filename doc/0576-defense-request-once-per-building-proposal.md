# 0576 - 건물당 지원 소집은 최초 1회만 (구현 완료)

## 요청

공격받았을 때 지원 요청(주변 유닛 소집)은 건물당 딱 한 번만 내리고, 그 뒤로 같은 건물이 계속
공격당해도 다시 소집 명령을 내리지 않도록.

## 구현

`Assets/Scripts/System/EnemyAIDirector.cs`

건물별 소집 여부를 기억하는 `HashSet`을 추가하고, `HandleBaseAttacked`에서 이미 소집한 건물이면
그대로 반환한다.

```diff
     private readonly Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>> baseDefenseHandlers
         = new Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>>();
+
+    // 건물별로 지원 소집을 이미 한 번 내렸는지 - 같은 건물이 계속 공격받아도 소집은 최초 1회만
+    // 내린다(doc/0576). 건물이 파괴되면 SyncBuildingDefenseHandlers에서 함께 제거한다.
+    private readonly HashSet<EnemyBuildingController> defenseRequested = new HashSet<EnemyBuildingController>();
```

```diff
     private void HandleBaseAttacked(EnemyBuildingController building, Vector3 attackerPosition, bool isEnemyAttacker)
     {
         if (isEnemyAttacker)
             return;

+        if (!defenseRequested.Add(building))
+            return; // 이 건물은 이미 한 번 소집했음
+
         float radius = homeBuildings.Contains(building) ? defenseRadius * 2f : defenseRadius;
```

`SyncBuildingDefenseHandlers`(doc/0575)가 파괴된 건물의 `baseDefenseHandlers` 항목을 정리할 때
`defenseRequested`에서도 함께 제거하도록 한 줄 추가 - 같은 건물 인스턴스가 다시 등장할 일은 없으므로
사실상 메모리 누수 방지 목적.

```diff
             if (building != null && building.GetHealthManager() != null)
                 building.GetHealthManager().OnDamaged -= baseDefenseHandlers[building];
             baseDefenseHandlers.Remove(building);
+            defenseRequested.Remove(building);
```

## 동작

- 건물 하나가 공격받아 소집이 한 번 발생하면, 그 건물이 파괴되기 전까지는 다시 맞아도 추가 소집이
  일어나지 않는다.
- 이미 반격하러 나간 유닛들의 `AttackMoveTo` 자체는 그대로 유지되며(별도 취소 로직 없음), 이번
  변경은 오직 "새로 소집 명령을 내릴지"만 막는다.
- 컴파일 확인: `uloop compile` → `Success: true, ErrorCount: 0, WarningCount: 0`.
