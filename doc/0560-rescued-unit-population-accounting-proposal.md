# 0560 - 구조된 유닛 인구수 반영 제안

## 요청

> 구조된 유닛도 구조 되는 순간 인구수에 추가시키기. 해당 인구수는 NTA 유닛과 같은 데이터로 OC에게 입혀서 적용시켜줘.

## 현재 상태 (코드 조사 결과)

- `Assets/Scripts/ScriptableObject/UnitDataSO.cs`의 `UnitData` 클래스는 NTA/OC가 완전히 같은 스키마를 쓴다.
  `population` 필드(line 124-125)가 이미 OC 쪽 데이터 에셋(`OC Unit Data SO.asset`)에도 그대로 존재한다.
  → "NTA 유닛과 같은 데이터"는 이미 만족되어 있음. 별도 필드 추가는 불필요.
- `UnitController.cs:370-373` (`Start()`): 구조 가능한 OC 유닛은 `unitID == 0`, `enemyDataUnitID > 0`으로 만들어져
  있고, 스탯도 `rtsController.GetEnemyUnitData(enemyDataUnitID)`(OC 테이블)에서 가져온다.
- `UnitController.cs:384-385` (`Start()`): 인구수 반영은 `rtsController.AddPopulationForExistingUnit(unitID)`를
  호출하는데, 이건 **항상 `unitID`(NTA 테이블 키)** 로 조회한다. 구조 유닛은 `unitID == 0`이라 NTA 테이블에
  ID 0인 항목이 없어 조용히 no-op된다 → **구조 전에는 인구수가 안 붙는다 (의도대로 정상)**.
- `UnitController.cs:2055-2084` (`Rescue()`): `isRescueUnit = false`로 바꾸고 마커/시야/사운드만 처리할 뿐,
  **인구수 추가 호출이 아예 없다** → 구조해도 영구히 인구수 미반영. 이게 요청의 핵심 버그.
- `UnitController.cs:2016` (`Die()`): 죽을 때 `rtsController?.ReleaseUnitPopulation(unitID)`도 마찬가지로
  `unitID`만 본다. 구조된 유닛이 죽어도 반환 대상이 없음(애초에 추가된 적이 없으니 지금은 결과적으로 맞음).
- `RTSUnitController.cs:1272-1277` (`AddPopulationForExistingUnit`) / `1263-1268` (`ReleaseUnitPopulation`):
  둘 다 `unitDatabase`(NTA)만 조회하는 구조라 OC 테이블 경로가 없음.

## 원인

"구조되는 순간" 인구수를 반영하는 호출 자체가 존재하지 않는다. `Start()`의 기존 인구수 훅은 NTA `unitID`
전용이라 구조 유닛(`unitID == 0`)에는 애초에 안 맞고, `Rescue()`에는 그 훅이 아예 없다.

## 제안하는 수정 (최소 diff, 기존 패턴 그대로 재사용)

1. **`RTSUnitController.cs`** — 기존 `AddPopulationForExistingUnit`/`ReleaseUnitPopulation`과 동일한 모양으로,
   조회 테이블만 OC로 바꾼 짝 메서드 2개 추가 (이미 있는 `GetEnemyUnitData()`를 그대로 재사용):

   ```csharp
   // 구조된 OC 유닛(enemyDataUnitID로 OC 테이블 조회)의 인구수를 현재 사용량에 반영한다.
   // UnitController.Rescue()가 구조되는 순간 호출한다 - population 필드는 NTA와 동일한 UnitData 스키마를 그대로 쓴다.
   public void AddPopulationForRescuedUnit(int enemyDataUnitID)
   {
       UnitData data = GetEnemyUnitData(enemyDataUnitID);
       if (data != null)
           resourceManager.AddPopulationDirect(data.population);
   }

   // 구조됐던 OC 유닛이 죽었을 때 그만큼의 인구수를 반환한다 (enemyDataUnitID로 OC 테이블 조회).
   public void ReleasePopulationForRescuedUnit(int enemyDataUnitID)
   {
       UnitData data = GetEnemyUnitData(enemyDataUnitID);
       if (data != null)
           resourceManager.ReleasePopulation(data.population);
   }
   ```

2. **`UnitController.cs` `Rescue()`** — `isRescueUnit = false;` 직후에 추가:

   ```csharp
   rtsController?.AddPopulationForRescuedUnit(enemyDataUnitID);
   ```

3. **`UnitController.cs` `Die()`** — 기존 한 줄을 분기 처리 (구조된 뒤 죽었을 때만 OC 테이블로 반환하고,
   구조되기 전에 죽으면 애초에 추가된 적이 없으므로 반환하지 않음 - 그렇지 않으면 인구수가 음수로 새어나감):

   ```csharp
   if (enemyDataUnitID > 0)
   {
       if (!isRescueUnit) // 구조된 뒤 죽었을 때만 반환 - 구조 전이면 애초에 추가된 적이 없음
           rtsController?.ReleasePopulationForRescuedUnit(enemyDataUnitID);
   }
   else
   {
       rtsController?.ReleaseUnitPopulation(unitID);
   }
   ```

## 영향 범위

- 일반 NTA 유닛 생산/사망 경로(`unitID` 기반)는 전혀 안 건드림.
- 일반 OC/적/아군 유닛(`EnemyUnitController`/`AllyController` 경로)은 여전히 플레이어 인구수와 무관 (doc/0447
  그대로 유지) - 이번 변경은 **"구조 가능한 OC 유닛"(`isRescueUnit`) 한정**.
- `AddPopulationDirect`는 기존에도 인구수 한도(cap) 체크 없이 바로 더하는 함수라(씬 배치 시작 유닛과 동일 취급),
  구조 유닛도 같은 방식 - 인구수 초과로 구조가 막히는 일은 없음(NTA 시작 유닛과 동일한 기존 동작 그대로).

## 확인 요청

이 방향(위 3곳, diff 최소)으로 구현해도 될지 확인 부탁드립니다.

## 구현 결과 (사용자 승인 후)

제안대로 3곳 그대로 적용, 컴파일 성공(0 errors).

**`Assets/Scripts/System/RTSUnitController.cs`** - `AddPopulationForExistingUnit` 바로 뒤에 짝 메서드 2개 추가:

```diff
     public void AddPopulationForExistingUnit(int unitID)
     {
         UnitData data = unitDatabase.unitData.Find(d => d.ID == unitID);
         if (data != null)
             resourceManager.AddPopulationDirect(data.population);
     }
+
+    // 구조된 OC 유닛(enemyDataUnitID로 OC 테이블 조회)의 인구수를 현재 사용량에 반영한다.
+    // UnitController.Rescue()가 구조되는 순간 호출한다 - population 필드는 NTA와 동일한 UnitData 스키마를 그대로 쓴다.
+    public void AddPopulationForRescuedUnit(int enemyDataUnitID)
+    {
+        UnitData data = GetEnemyUnitData(enemyDataUnitID);
+        if (data != null)
+            resourceManager.AddPopulationDirect(data.population);
+    }
+
+    // 구조됐던 OC 유닛이 죽었을 때 그만큼의 인구수를 반환한다 (enemyDataUnitID로 OC 테이블 조회).
+    public void ReleasePopulationForRescuedUnit(int enemyDataUnitID)
+    {
+        UnitData data = GetEnemyUnitData(enemyDataUnitID);
+        if (data != null)
+            resourceManager.ReleasePopulation(data.population);
+    }
```

**`Assets/Scripts/Unit/UnitController.cs`** - `Rescue()`에서 구조되는 순간 인구수 추가:

```diff
         isRescueUnit = false;

+        rtsController?.AddPopulationForRescuedUnit(enemyDataUnitID); // NTA 유닛과 같은 UnitData.population 필드를 OC 테이블에서 가져와 반영
+
         if (preRescueMarker != null)
```

**`Assets/Scripts/Unit/UnitController.cs`** - `Die()`에서 구조된 유닛은 OC 테이블 기준으로, 구조 전 사망은 반환하지 않도록 분기:

```diff
-        rtsController?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환
+        // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환. 구조 가능한 OC 유닛(enemyDataUnitID>0)은 구조된
+        // 뒤(!isRescueUnit)에만 Rescue()에서 추가됐었으므로 그때만 반환 - 구조 전에 죽으면 애초에 추가된 적이
+        // 없어 반환하면 인구수가 음수로 샌다.
+        if (enemyDataUnitID > 0)
+        {
+            if (!isRescueUnit)
+                rtsController?.ReleasePopulationForRescuedUnit(enemyDataUnitID);
+        }
+        else
+        {
+            rtsController?.ReleaseUnitPopulation(unitID);
+        }
```

컴파일 확인: `npx uloop-cli compile --wait-for-domain-reload true` → `Success: true, ErrorCount: 0` (기존 경고 40개만 그대로, 이번 변경으로 새로 생긴 경고/에러 없음).
