# 0519. 구조된 유닛 - 스쿼드 패널 이름 표기 및 Ctrl+Click 그룹핑 버그 수정 제안

**날짜:** 2026-08-11

## 요청 내용

> 구조된 유닛 스쿼드 창에서 이름이 유닛으로 표기 됨
> 구조된 유닛 스쿼드 창에서 컨트롤 클릭시 헤비어썰트 탱크랑 사이보그 솔져 모두 선택됨

## 배경 (doc/0458)

미션에 배치되는 "구조 가능한 OC 유닛"은 겉모습은 OC 프리팹이지만 실제로는 플레이어가 조종하는
`UnitController`다. 이런 유닛은 `unitID`(NTA 테이블 조회용)를 **0으로 그대로 두고**, 대신
`enemyDataUnitID`(OC 테이블 조회용)로 실제 종류(사이보그 솔저/헤비어썰트 탱크 등)와 스탯을 가져온다
(`UnitController.cs:363-370`). 이름도 이 경로를 타서 `heroName`에 OC 로컬라이즈 이름이 자동으로 채워진다
(`UnitController.cs:376-377`).

## 원인 1 - 이름이 "Unit"으로 표기됨

`UIController.GetUnitDisplayName()`(`Assets/Scripts/UI/UIController.cs:992-1002`, 스쿼드 패널 툴팁
제목 조회용)이 `heroName`을 전혀 안 보고 **NTA `database`만** 조회한다:

```csharp
private string GetUnitDisplayName(UnitController unit)
{
    if (database != null)
    {
        UnitData data = database.unitData.Find(d => d.ID == unit.GetUnitID());
        if (data != null && !string.IsNullOrEmpty(data.unitName))
            return LocalizationManager.GetTextOrFallback($"unit.nta.{data.ID}.name", data.unitName.Trim());
    }

    return LocalizationManager.GetText("squad.unitfallback"); // "Unit"/"유닛"
}
```

구조된 유닛은 `unitID`가 0이라 NTA `database`에서 못 찾고(0번 ID가 없음), 곧장 `"squad.unitfallback"`
문구("유닛")로 떨어진다. 반면 **Info Panel(단일 선택 시)은 같은 상황을 이미 올바르게 처리**하고 있다
(`RTSUnitController.cs:1959`):
```csharp
string displayName = string.IsNullOrEmpty(unit.GetHeroName()) ? GetUnitName(unit.GetUnitID()) : unit.GetHeroName();
```
즉 Info Panel은 `heroName`을 먼저 확인하는데, 스쿼드 패널 쪽만 이 로직이 빠져 있던 것 - 두 곳의 이름 조회
로직이 서로 다르게 구현되면서 생긴 불일치.

## 원인 2 - Ctrl+Click 시 서로 다른 종류가 함께 선택 유지됨

`RTSUnitController.KeepOnlySameUnitTypeInSelection()`(`Assets/Scripts/System/RTSUnitController.cs:301-315`,
스쿼드 패널 Ctrl+Click = "같은 종류만 남기고 나머지 선택 해제")도 `unitID`만으로 종류를 비교한다:

```csharp
public void KeepOnlySameUnitTypeInSelection(UnitController unit)
{
    int unitID = unit.GetUnitID();

    for (int i = selectedUnitList.Count - 1; i >= 0; i--)
    {
        UnitController other = selectedUnitList[i];
        if (other != null && other.GetUnitID() != unitID)
            DeselectUnit(other);
    }
}
```

구조된 유닛은 전부 `unitID == 0`이므로(실제 종류는 `enemyDataUnitID`에만 담겨 있음), 사이보그 솔저와
헤비어썰트 탱크가 **둘 다 "unitID 0"으로 같은 종류 취급**되어 Ctrl+Click을 해도 서로를 걸러내지 못하고
둘 다 선택된 채로 남는다.

두 버그 모두 "구조된 유닛은 `unitID` 대신 `enemyDataUnitID`로 실제 종류가 구분된다"는 doc/0458의 설계를
빠뜨린 동일 계열의 실수다.

## 계획된 수정

**`UIController.GetUnitDisplayName()`** - `heroName`을 먼저 확인 (Info Panel과 동일한 우선순위):
```csharp
private string GetUnitDisplayName(UnitController unit)
{
    if (!string.IsNullOrEmpty(unit.GetHeroName()))
        return unit.GetHeroName();

    if (database != null)
    {
        UnitData data = database.unitData.Find(d => d.ID == unit.GetUnitID());
        if (data != null && !string.IsNullOrEmpty(data.unitName))
            return LocalizationManager.GetTextOrFallback($"unit.nta.{data.ID}.name", data.unitName.Trim());
    }

    return LocalizationManager.GetText("squad.unitfallback");
}
```

**`RTSUnitController.KeepOnlySameUnitTypeInSelection()`** - `unitID`와 `enemyDataUnitID`를 함께 비교해서
"같은 종류"를 판정:
```csharp
public void KeepOnlySameUnitTypeInSelection(UnitController unit)
{
    if (unit == null)
        return;

    int unitID = unit.GetUnitID();
    int enemyDataUnitID = unit.GetEnemyDataUnitID();

    for (int i = selectedUnitList.Count - 1; i >= 0; i--)
    {
        UnitController other = selectedUnitList[i];
        if (other != null && (other.GetUnitID() != unitID || other.GetEnemyDataUnitID() != enemyDataUnitID))
            DeselectUnit(other);
    }
}
```
일반 NTA 생산 유닛은 `enemyDataUnitID`가 항상 0이라 기존 동작(unitID만으로 비교)과 완전히 동일하게
유지됨 - 구조된 유닛(enemyDataUnitID > 0)에서만 추가 구분이 걸림.

## 변경 예정 파일

- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`

---

## 적용 (사용자 승인 후)

> 네, 진행

제안대로 적용함.

### `UIController.cs`

```diff
     private string GetUnitDisplayName(UnitController unit)
     {
+        if (!string.IsNullOrEmpty(unit.GetHeroName()))
+            return unit.GetHeroName();
+
         if (database != null)
         {
```

### `RTSUnitController.cs`

```diff
     public void KeepOnlySameUnitTypeInSelection(UnitController unit)
     {
         if (unit == null)
             return;

         int unitID = unit.GetUnitID();
+        int enemyDataUnitID = unit.GetEnemyDataUnitID();

         for (int i = selectedUnitList.Count - 1; i >= 0; i--)
         {
             UnitController other = selectedUnitList[i];
-            if (other != null && other.GetUnitID() != unitID)
+            if (other != null && (other.GetUnitID() != unitID || other.GetEnemyDataUnitID() != enemyDataUnitID))
                 DeselectUnit(other);
         }
     }
```

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0` (기존과 동일한 37개 obsolete API 경고만 있음,
  이번 변경과 무관).

## 변경된 파일

- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
