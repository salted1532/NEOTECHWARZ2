# 0359 — 제안: 이동/공격 등 이펙트도 Fog of War로 가려진 곳에서는 안 보이게

**날짜:** 2026-08-02

## 요청

"안개 속에서 이동 이펙트, 공격이펙트 등 이펙트들도 안보여야 할거 같아" — `doc/0358`(체력바/점령 타이머)의
연장선.

## 조사

이펙트는 전부 `Assets/Scripts/Effects/EffectPlayer.cs`(정적 유틸)를 거쳐 스폰된다.

- **발사 후 잊기(fire-and-forget)**: 총구/피격/사망/파괴/이륙/착륙/건설완료 이펙트 — `UnitEffects`,
  `BuildingEffects`, `ConstructionEffects` 전부 `EffectPlayer.SpawnAtPoints()` 또는
  `EffectPlayer.PlayHit()`를 거치는데, 이 둘 다 결국 내부적으로 `EffectPlayer.Spawn()` 한 곳으로
  모인다.
- **지속형(persistent)**: 이동 트레일(`UnitEffects.SetMoveTrail`, 이동 중인 동안 계속 유지)과 건설 중
  루프 이펙트(`ConstructionEffects.StartLoop`)는 `SpawnPersistentAt()`을 거치는데, `Spawn()`과
  별도 경로라 한 번 스폰되면 안개가 나중에 다시 덮여도 스스로 안 꺼진다 - 계속 켜져 있는 상태를
  매 프레임 다시 확인해야 한다.

## 제안

### 1. `EffectPlayer.Spawn()` 한 곳에서 발사 후 잊기 이펙트 전부 게이트 (신규 변경 없이 자동 전파)

```diff
+    private static csFogWar fogWar;
+    private static bool fogWarChecked;
+
+    private static csFogWar GetFogWar()
+    {
+        if (!fogWarChecked)
+        {
+            fogWar = Object.FindFirstObjectByType<csFogWar>();
+            fogWarChecked = true;
+        }
+        return fogWar;
+    }
+
     public static GameObject Spawn(GameObject effectPrefab, Vector3 pos, Quaternion rot, Transform parent = null)
     {
         if (effectPrefab == null)
             return null;
+
+        if (!FogVisibility.IsRevealed(GetFogWar(), pos))
+            return null; // 안개에 가려진 위치에서는 이펙트를 아예 스폰하지 않는다 (doc/0359)

         GameObject instance = Object.Instantiate(effectPrefab, pos, rot, parent);
         ...
```

`Spawn()`은 이미 `effectPrefab == null`이면 `null`을 반환하고, 모든 호출부(`SpawnAtPoints`,
`PlayHit`)가 그 `null`을 이미 안전하게 처리하고 있다(리스트에 안 넣거나 그냥 무시) — 그래서 이 한 줄
추가만으로 `UnitEffects`/`BuildingEffects`/`ConstructionEffects`/`LaserBeamAttack` 등 전부 자동으로
적용되고, 개별 파일을 손댈 필요가 없다.

### 2. 이동 트레일은 지속형이라 별도로 매 프레임 재확인 필요

`UnitEffects.Update()`가 이미 매 프레임 "이동 중인가"를 폴링해서 트레일을 켜고 끄고 있으므로
(`SetMoveTrail(moving)`), 여기에 안개 조건만 더한다 — 이동 중이던 유닛이 안개 속으로 들어가면 트레일이
꺼지고, 다시 나오면(계속 이동 중이라면) 자동으로 다시 켜진다.

```diff
+    private csFogWar fogWar;
+
+    private void Awake()
+    {
+        ...
+        fogWar = FindFirstObjectByType<csFogWar>();
+    }
+
     private void Update()
     {
         bool moving = (unitController != null && unitController.IsCurrentlyMoving())
             || (enemyUnitController != null && enemyUnitController.IsCurrentlyMoving());
-        SetMoveTrail(moving);
+        SetMoveTrail(moving && FogVisibility.IsRevealed(fogWar, transform.position));
     }
```

### 3. 건설 중 루프 이펙트(`ConstructionEffects.StartLoop`)는 범위 밖

건설 시스템은 플레이어(아군) 전용이라 항상 자기 시야로 보이는 대상만 대상으로 한다
(`EnemyBuildingController`는 생산/건설 큐 자체가 없음, doc/0243/0245) — 안개에 가려질 일이 실질적으로
없으므로 굳이 손 안 댐(YAGNI).

## 확인 필요 사항

- `EffectPlayer.Spawn()` 한 곳에서 게이트하는 중앙집중 방식으로 발사 후 잊기 이펙트 전체를 처리하고,
  이동 트레일만 별도로 `UnitEffects`에서 매 프레임 재확인하는 이 구성으로 진행해도 되는지
- 건설 중 루프 이펙트는 범위 밖으로 뒀는데 맞는지

## 적용 (2026-08-02)

"이대로 진행해줘" — 위 설계 그대로 적용.

- **`Assets/Scripts/Effects/EffectPlayer.cs`**: 정적 `fogWar`/`fogWarChecked` 캐시 + `GetFogWar()`
  추가, `Spawn()` 맨 앞에서 `FogVisibility.IsRevealed(GetFogWar(), pos)`가 거짓이면 스폰하지 않고
  `null` 반환. `SpawnAtPoints`/`PlayHit`가 전부 이 메서드를 거치므로 `UnitEffects`/`BuildingEffects`/
  `ConstructionEffects`/`LaserBeamAttack`의 발사 후 잊기 이펙트(총구/피격/사망/파괴/이륙/착륙/건설완료)가
  전부 자동으로 적용됨 - 해당 파일들은 변경 없음.
- **`Assets/Scripts/Effects/UnitEffects.cs`**: `fogWar` 필드 추가(`Awake()`에서 캐싱), `Update()`의
  `SetMoveTrail(moving)` 호출을 `SetMoveTrail(moving && FogVisibility.IsRevealed(fogWar,
  transform.position))`로 변경 - 이동 중 안개 속으로 들어가면 트레일이 꺼지고 나오면 다시 켜짐.

`npx uloop-cli compile` 통과 (에러 0, 경고 32개 — 신규 2건은 `EffectPlayer`/`UnitEffects`에 추가된
`FindFirstObjectByType` obsolete 경고로, 프로젝트 전역에 이미 있던 동일 패턴 - 새로운 문제 아님).
