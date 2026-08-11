# 0526. 건설/착륙 배치 모드 중 유닛 선택(클릭+드래그) 차단 - 제안

**날짜:** 2026-08-11

## 요청 내용

> 건물 착륙 모드일시 다른 유닛 선택 되는거 막아줘
> (이어서) 일꾼이 건설 모드중일때도 드래그도 안되도록해줘 착륙도 같게

## 조사 내용

건물 배치(`PlacementSystem.StartPlacement`, 일꾼 건설 모드)와 착륙 위치 선택
(`PlacementSystem.StartBuildingRelocation`)은 둘 다 마우스 클릭을 `InputManager.OnClicked` 이벤트로
받아서 처리하는데, 유닛 선택을 담당하는 `UserControl.HandleMouse()`는 완전히 별개의 컴포넌트라 이
배치 모드 상태를 전혀 모른다 - 그래서 같은 클릭에 대해 "배치 확정 시도"와 "유닛 클릭 선택"이 동시에
일어난다. 실제로:

- 단일 클릭 선택: `UserControl.HandleLeftClick()`(278행)이 마우스 다운 시점에 유닛 레이어를 직접
  레이캐스트해서 `pendingLeftClickSelect`를 채워두고, 마우스 업 때(`SelectObject()`, 969행) 실행함.
  배치 모드 여부를 전혀 확인하지 않음.
- 드래그 선택: `SelectObject()`가 마우스 업 시점에 드래그 범위(`dragRect`) 안에 든 유닛을 전부
  선택함(972~999행). 이것도 배치 모드 여부를 확인하지 않음.

두 모드(건설 배치/착륙 위치 선택) 다 `PlacementSystem`의 같은 필드 `selectedObjectIndex`가 -1이
아닐 때 활성 상태다(`StartPlacement`/`StartBuildingRelocation`이 채우고, `StopPlacement`가 -1로
되돌림) - 즉 이 필드 하나로 "지금 배치 모드 중인지(건설이든 착륙이든)"를 정확히 판정할 수 있다.

`RTSUnitController`가 이미 `PlacementSystem` 참조를 들고 있고(`RTSUnitController.cs:60`),
`UserControl`도 이미 `rtsUnitController` 참조를 들고 있으므로(에디터에서 새로 연결할 필드 없이) 이
경로로 상태를 물어보면 된다.

## 변경 계획

### `PlacementSystem.cs`
```diff
     private int selectedObjectIndex = -1;
+
+    // 건설/착륙 배치 모드가 활성 상태인지 - UserControl이 배치 모드 중 유닛 선택(클릭/드래그)을
+    // 막을 때 조회한다 (doc/0526). StartPlacement/StartBuildingRelocation이 켜고 StopPlacement가 끈다.
+    public bool IsPlacementModeActive => selectedObjectIndex >= 0;
```

### `RTSUnitController.cs`
```diff
     [SerializeField]
     private PlacementSystem PlacementSystem;
+
+    // 건설/착륙 배치 모드 중엔 유닛 선택을 막기 위해 UserControl이 조회한다 (doc/0526).
+    public bool IsPlacementModeActive => PlacementSystem != null && PlacementSystem.IsPlacementModeActive;
```

### `UserControl.cs`
```diff
     private void HandleLeftClick()
     {
+        if (rtsUnitController != null && rtsUnitController.IsPlacementModeActive)
+            return; // 건설/착륙 배치 모드 중엔 클릭이 PlacementSystem 전담(doc/0526) - 유닛 클릭 선택 안 함
+
         Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
         ...
```
```diff
     private void SelectObject()
     {
+        if (rtsUnitController != null && rtsUnitController.IsPlacementModeActive)
+            return; // 건설/착륙 배치 모드 중엔 드래그 선택도 막는다 (doc/0526)
+
         //드래그 범위 안에 들어오는 유닛부터 먼저 계산
         ...
```

## 영향 범위
- 건설 배치 모드(건물 버튼 클릭 후 고스트 프리뷰가 마우스를 따라다니는 중)와 착륙 위치 선택 모드
  둘 다 동일하게 적용됨 (`selectedObjectIndex` 하나로 두 모드를 같이 커버).
- 배치 모드 중엔 클릭/드래그로 유닛을 선택할 수 없게 됨 - 기존에 선택돼 있던 유닛/건물은 그대로
  유지됨(선택 해제 로직을 건드리지 않음).
- 배치 모드는 `InputManager.OnClicked`/`OnExit`으로 클릭/ESC를 그대로 받으므로 배치 자체(클릭 확정,
  ESC 취소)는 이번 변경과 무관하게 정상 동작함.
- 우클릭 명령(`HandleRightClick`)은 이번 요청 범위 밖이라 그대로 둠 - 필요하면 별도로 요청.

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

---

## 적용 (사용자 승인 후)

> 진행 (Recommended)

제안대로 3개 파일 전부 위 diff 그대로 적용함. `npx uloop-cli compile` 성공 확인 (Error 0개, Warning
37개는 전부 이번 변경과 무관한 기존 `FindFirstObjectByType` obsolete API 경고).

## 변경된 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
