# 0519. 생산 중 이륙 시도 시 경고 문구 표시 (한/영) - 제안

**날짜:** 2026-08-11

## 요청 내용

> 유닛 생산시 착륙 불가하다고 문구 나오도록 + 한글/영어 번역 버전으로 경고문구에 알려주도록

## 조사 내용

`Assets/Scripts/Building/BuildingController.cs`의 `LiftOff()`(279행, "이륙" 버튼에 연결됨)는 생산
대기열에 뭔가 남아있으면 이륙을 막는 가드가 이미 있다:

```csharp
public void LiftOff()
{
    if (!canLift || isLifted)
        return;

    if (HasActiveProductionQueue()) // 생산 대기열에 뭔가 있으면 이륙 불가(공중에서 생산이 계속되는 것 방지)
        return;
    ...
}
```

문제는 이 가드가 **아무 피드백 없이 조용히 아무 일도 안 일어나는 것**이다 - 플레이어는 왜 이륙이 안
되는지 알 방법이 없다. (참고: 반대로 "착륙"(`Land()`) 쪽은 애초에 건물이 공중에 뜬 상태에서는 생산
패널 자체가 안 보이므로 - `RTSUnitController.cs:1994~2004` - 착륙 중에 유닛을 생산 중일 수가 없어서
가드가 필요 없다. 실제로 막혀야 하는/막혀 있는 동작은 **이륙(Lift Off)**이다.)

다른 경고들(`warning.resource`, `warning.population`, `warning.constructionfail`)과 동일한 패턴으로
`UIController.ShowWarning(LocalizationManager.GetText(key))`를 쓰면 됨 - 이미 `en.json`/`ko.json`에
쌍으로 존재하는 관례를 그대로 따른다.

## 변경 계획

### `BuildingController.cs`
`LiftOff()`를 `void` → `bool`로 바꿔서 성공 여부를 호출측에 알려준다 (호출부는 현재
`RTSUnitController.LiftSelectedBuilding()` 한 곳뿐이라 영향 범위 작음):
```diff
-    public void LiftOff()
+    public bool LiftOff()
     {
         if (!canLift || isLifted)
-            return;
+            return false;

         if (HasActiveProductionQueue())
-            return;
+            return false;
         ...
+        return true;
     }
```

### `RTSUnitController.cs`
```diff
     public void LiftSelectedBuilding()
     {
-        GetRepresentativeBuilding()?.LiftOff();
+        BuildingController building = GetRepresentativeBuilding();
+        if (building == null)
+            return;
+
+        if (!building.LiftOff())
+            uIController.ShowWarning(LocalizationManager.GetText("warning.liftoffproducing"));
     }
```

### `en.json` / `ko.json`
```json
{ "key": "warning.liftoffproducing", "value": "Cannot lift off while a unit is in production." }
{ "key": "warning.liftoffproducing", "value": "유닛 생산 중에는 이륙할 수 없습니다." }
```

## 변경 예정 파일
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Resources/Localization/en.json`, `ko.json`

---

## 적용 (사용자 승인 후)

> 이대로 진행시켜줘

제안대로 적용함.

### `BuildingController.cs`
```diff
-    public void LiftOff()
+    public bool LiftOff()
     {
         if (!canLift || isLifted)
-            return;
+            return false;

         if (HasActiveProductionQueue())
-            return;
+            return false;
         ...
         GetComponent<BuildingAudio>()?.PlayTakeoff();
+        return true;
     }
```

### `RTSUnitController.cs`
```diff
     public void LiftSelectedBuilding()
     {
-        GetRepresentativeBuilding()?.LiftOff();
+        BuildingController building = GetRepresentativeBuilding();
+        if (building == null)
+            return;
+
+        if (!building.LiftOff())
+            uIController.ShowWarning(LocalizationManager.GetText("warning.liftoffproducing"));
     }
```

### `en.json` / `ko.json`
`warning.liftoffproducing` 키 추가 (문서 상단 계획과 동일한 값).

`npx uloop-cli compile` 성공 확인 (Error 0개, 기존에 있던 무관한 obsolete API 경고만 있음).

## 변경된 파일
- `Assets/Scripts/Building/BuildingController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Resources/Localization/en.json`, `ko.json`
