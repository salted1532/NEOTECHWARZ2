# 0363 — 생산 건물 랠리 버튼(슬롯 6) + 선택 시 랠리 포인트 마커

**날짜:** 2026-08-02

## 요청

"생산 건물 랠리 버튼 만들려고해 6번 슬롯에다가 랠리 버튼을 추가해줘 이걸 누르면 M 이동명령처럼
usercontrol에서 위치 지정할수 있게 해주고 클릭시 거기로 위치가 지정되는거야(건물에서 우클릭과 같음).
그리고 건물을 선택하면 자신의 랠리 포인트 위치에 포인터가 보이도록 해줘 마커는 3초있다가 사라지는건
그대로 유지 되었으면 좋겠어 랠리 단축키는 Y야"

## 조사 — 이미 대부분 구현돼 있었음

랠리 기능 자체(위치 지정 대기 모드, 우클릭으로 확정, Y 단축키)는 이미 코드에 있었고, **버튼만 없었다**:

- `UserControl.cs`: `OrderState.Rally` 상태, 좌클릭 확정 시
  `rtsUnitController.SetRallySelectBuilding(groundPoint)` 호출 + `ShowMovePointer(groundPoint)`로
  기존 이동 포인터 표시(이미 "건물 우클릭과 동일" 동작 - `IssueRightClickMoveAt()`가 이걸 그대로 씀).
- `RTSUnitController.cs`: `EnterRallyMode()` → `userControl.SetOrderState("Rally")`.
- `BuildingController.cs`: `RallyPosition`/`SetRallyPosition()`/`GetRallyPos()`.
- **버그 발견**: `UserControl.HandlekeyBoard()`의 Y 단축키가 `if (rtsUnitController.IsUnitSelect())`로
  감싸져 있었음 — 랠리는 건물 전용 기능인데 "유닛 선택 중"일 때만 반응하는 조건이라, 실제로 건물을
  선택한 상태에서는 Y를 눌러도 아무 일도 안 일어나는 죽은 코드였음(주석엔 "건물 랠리 설정"이라고
  써있어서 의도와 조건이 안 맞음 - 복붙 실수로 보임).

## 적용

**`Assets/Scripts/UI/UIController.cs`**

- `BuildingRallySlotIndex = 6` 상수 + `LiftAndRallySlotsProtected`(Lift+Rally 슬롯 보호 집합) 추가.
  슬롯 6은 tier당 최대 유닛 수(NTA 데이터 기준 최대 3개, `<tier>k__BackingField` 값으로 확인)로는
  절대 안 채워지는 여유 슬롯이라 실제 생산 버튼과 안 겹침.
- `rallyIcon` Sprite 필드 추가(인스펙터 연결 필요 - 아래 "남은 작업" 참고).
- `ShowUnitProductionPanel()`의 보호 슬롯을 `LiftSlotOnlyProtected` → `LiftAndRallySlotsProtected`로
  변경 - 매 프레임 갱신되는 생산 패널이 슬롯 6을 지워버리지 않도록.
- `ShowBuildingRallyCommand(ButtonAction onRally)` 신규 - `ShowBuildingMoveCommand`와 동일한 패턴으로
  슬롯 6에 랠리 버튼 데이터를 채움.

**`Assets/Scripts/System/RTSUnitController.cs`**

- `RallyButtonAction()` 신규 - `ButtonAction.Simple(EnterRallyMode, "Rally", "...", KeyCode.Y)`.
  `EnterRallyMode()`는 이미 있던 메서드 그대로 재사용(신규 코드 없음, 버튼 콜백으로 연결만 함).
- `UpdateUI()`의 생산 패널 switch문에서 MainBase/Tier1/Tier2/Tier3 네 케이스 각각에
  `uIController.ShowBuildingRallyCommand(RallyButtonAction());` 추가 (Lift/Move 버튼과 동일하게
  `ShowProductionUI()` 바로 뒤).
- `SelectBuilding()`: 생산 건물(`IsProductionBuildingState()` - MainBase/Tier1/Tier2/Tier3)을
  선택하는 순간 `userControl.ShowMovePointerAt(building.GetRallyPos())` 호출 - 기존 3초 자동 소멸
  이동 포인터를 그대로 재사용해서 랠리 포인트 위치에 표시.

**`Assets/Scripts/UserControl/UserControl.cs`**

- `ShowMovePointerAt(Vector3)` 공개 진입점 추가 - 기존 private `ShowMovePointer()`를 외부
  (RTSUnitController)에서 부를 수 있게 얇게 감싼 것뿐, 동작(3초 자동 사라짐 포함)은 완전히 동일.
- `HandlekeyBoard()`에서 버그 있던 Y 키 수동 처리 블록 삭제 - 이제 랠리 버튼(`ProductionSlot`)이
  자기 단축키(Y)를 스스로 감지해서 클릭을 시뮬레이션하므로(기존 Move/Attack/Build 버튼들과 동일한
  기존 관례) 더 이상 필요 없음.

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).

## 남은 작업 (사용자가 직접)

- `UIController` 인스펙터에 신규 `rallyIcon` 스프라이트 필드가 비어있음 — 랠리 버튼 아이콘 이미지를
  직접 연결해야 버튼에 그림이 보임(연결 전엔 빈 아이콘으로 표시됨, 클릭/단축키 자체는 정상 동작).

## 확인 필요 사항

- 이번엔 사전 제안 없이 바로 구현했습니다 - 랠리 기능 자체(모드 전환/우클릭 확정/Y 단축키)가 이미 다
  구현돼 있어서 "버튼 하나 추가해 기존 진입점에 연결"이 명확했기 때문입니다. 혹시 원하신 동작과
  다른 부분(예: 마커를 "선택할 때마다" 대신 "랠리를 새로 지정할 때만" 보여주고 싶다거나)이 있으면
  말씀해주세요.

## 버그 조사 (2026-08-02) — 슬롯 6이 게임 시작 직후 반투명하게 보임

**증상**: "게임 시작 후 바로 건물을 선택하면 슬롯 6(랠리) 아이콘이 반투명/어두운 색으로 보임. 대신
일꾼을 먼저 선택해서 슬롯 6의 건설 버튼이 정상 작동하는 걸 한 번 본 뒤 건물을 선택하면 랠리 버튼이
정상 색으로 보임."

### 조사 과정

1. 인스펙터 수치(`interactable`, `ColorBlock`)는 전부 정상이라는 사용자 보고를 Play Mode에서
   `execute-dynamic-code`로 직접 검증 — `Slot0`(정상 작동 중인 유닛 생산 버튼)과 `Slot6`(랠리)의
   `Button.colors`(Normal/Highlighted/Pressed/Disabled 색상)가 완전히 동일함을 확인. 클릭 시뮬레이션도
   정상 작동(`UsercurrentState`가 `Rally`로 정확히 바뀜) - 기능 자체는 문제없음을 재확인.
2. 사용자가 짚어준 재현 조건("일꾼 선택 후 건설 버튼을 한 번 정상적으로 본 뒤에는 문제없음")을 실마리로,
   **슬롯 6이 유닛 스킬(패시브 스킬 시 `Interactable=false`로 어둡게 표시)과 건물 랠리 두 용도로
   겸용된다**는 점에 주목.

### 원인 (추정)

Unity `Selectable`(Button)의 색상 전환(Normal/Pressed/**Disabled**)은 `Image.color` 값과는 별개로
`CanvasRenderer`에 직접 크로스페이드로 적용된다. 슬롯 6이 "한 번도 정상적으로 SetData를 거치지 않은
상태"(게임 시작 직후 바로 건물 선택)에서는 에디터에 저장된 기본 상태 혹은 최초 `Clear()` 호출 시점의
disabled 틴트가 그대로 남아있을 수 있는 반면, 일꾼을 먼저 선택해 슬롯 6이 한 번 "정상적으로 활성화"된
적이 있으면 그 이후엔 문제가 재현되지 않는다는 사용자의 관찰과 일치한다.

### 적용

**`Assets/Scripts/UI/ProductionSlot.cs`** — `SetData()`에서 버튼이 `Interactable=true`가 될 때마다
`CanvasRenderer` 색을 강제로 완전 불투명(흰색)으로 리셋하도록 변경. `Interactable=false`인 경우(진짜
비활성 상태를 보여줘야 하는 패시브 스킬 등)는 건드리지 않아 기존 "비활성화 표시" 기능은 그대로 유지됨.

```diff
         if (button != null)
         {
-            button.interactable =
-                data.Interactable &&
-                data.Callback != null;
+            bool interactable = data.Interactable && data.Callback != null;
+            button.interactable = interactable;
+
+            // 슬롯이 다른 용도(유닛 스킬 ↔ 건물 랠리)로 재활용될 때 남아있을 수 있는 이전 상태의
+            // 크로스페이드 틴트를, 활성화되는 경우엔 항상 완전 불투명한 기본 색으로 즉시 리셋한다.
+            if (interactable && button.targetGraphic != null)
+                button.targetGraphic.canvasRenderer.SetColor(Color.white);
         }
```

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음). Play Mode에서 재현
자체는 제 환경에서 못 만들었지만(사전에도 정상으로 보였음 - 타이밍에 민감한 문제로 추정), 증상의
메커니즘에 정확히 대응하는 안전한 방어적 수정이라 적용함. **사용자 환경에서 재현되던 증상이 실제로
없어졌는지 확인 부탁드립니다.**

## 후속 버그 (2026-08-02) — 위 수정이 눌림(Pressed) 색상 전환을 매 프레임 지워버림

**증상**: "불투명한 건 확실하게 잘 적용됐고 버튼도 잘 작동하는데, 처음 건물을 선택하고 버튼을 누르면
눌렀을 때 어두워지는 게 제대로 작동을 안 함. 일꾼을 선택하고 다시 건물을 선택하면 또 정상 작동함."

### 원인

`ShowBuildingRallyCommand()`는 건물이 선택된 동안 `UpdateUI()`를 통해 **매 프레임** 호출되고, 그때마다
`ProductionSlot.SetData()`도 매 프레임 다시 실행된다. 위에서 추가한 `canvasRenderer.SetColor(Color.white)`가
`interactable`이면 조건 없이 매번 실행되도록 되어 있었는데, 이게 문제 - 버튼을 누르는 순간 Selectable이
Pressed 색으로 크로스페이드를 시작해도, 바로 다음 프레임에 `SetData()`가 또 호출되면서 즉시 흰색으로
되돌려버려 눌림 효과가 화면에 보일 틈이 없었음(매 프레임 강제 리셋 vs 매 프레임 다시 호출되는 SetData가
서로 싸우는 구도).

"일꾼 선택 후 건물 재선택 시 정상 작동"했던 것도 결국 같은 매커니즘의 우연한 결과로, 근본 원인은 여전히
"활성화될 때마다 무조건 리셋"이었음.

### 적용

**`Assets/Scripts/UI/ProductionSlot.cs`**: 리셋 조건을 "활성화되는 경우"에서 "**비활성 → 활성으로
막 전환되는 그 프레임에만**"으로 좁힘 - 처음 재활용될 때 남아있는 잔여 틴트는 여전히 지워지고, 이미
활성 상태가 유지되는 동안(매 프레임 반복 호출)은 더 이상 건드리지 않아 정상적인 눌림/호버 색상 전환이
살아남는다.

```diff
         if (button != null)
         {
             bool interactable = data.Interactable && data.Callback != null;
+            bool wasInteractable = button.interactable;
             button.interactable = interactable;

-            if (interactable && button.targetGraphic != null)
+            if (interactable && !wasInteractable && button.targetGraphic != null)
                 button.targetGraphic.canvasRenderer.SetColor(Color.white);
         }
```

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).

## 슬롯 7로 이동 + 색상 강제 코드 제거 (2026-08-02)

"랠리 버튼이랑 일꾼 건설 버튼이 도대체 무슨 차이야, 똑같은거 아니야?" — Build/Rally를 코드로 재비교한
결과 완전히 동일한 구조(`ProductionSlot.SetData()`, 같은 `ColorBlock`)임을 재확인. Play Mode에서
Build 버튼을 3초 누른 상태로 스크린샷을 찍어봐도 안 누른 것과 구분이 안 갈 정도로 미묘해서, "Build는
눌림 피드백이 보이는데 Rally만 안 보인다"는 전제 자체가 의심스러워짐 — 지난번 "매 프레임 SetData가
서로 싸운다"는 진단이 틀렸을 가능성이 높다고 판단.

사용자 지시로 변수 하나씩 제거해서 다시 원점에서 확인하기로 함:

- **`Assets/Scripts/UI/UIController.cs`**: `BuildingRallySlotIndex`를 `6` → `7`로 변경(관련 주석도
  갱신). 슬롯 6은 유닛 스킬(`UnitSkillSlotIndex`)과 물리적으로 겸용되는 자리라 그로 인한 영향 가능성을
  완전히 배제하기 위함 - 슬롯 7은 생산 패널에서 아무 용도로도 안 쓰이는 완전히 독립된 자리.
- **`Assets/Scripts/UI/ProductionSlot.cs`**: `SetData()`에 넣었던 `canvasRenderer.SetColor(Color.white)`
  강제 리셋 코드를 전부 제거하고, 원래의 단순한 `button.interactable = ...` 한 줄로 되돌림 - 이 코드
  자체가 문제를 더 꼬이게 했을 가능성을 배제하기 위함.

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).

**남은 확인 사항**: 이제 랠리 버튼은 슬롯 7에서, 색상 강제 코드 없이 뜬다. 이 상태에서 (1) 게임 시작
직후 바로 건물 선택 시 투명도 문제가 재현되는지, (2) 눌렀을 때 어두워지는지 사용자 환경에서 확인 필요.

## 진짜 원인 발견 (2026-08-02) — `UpdateUnitSkillUI()`가 매 프레임 낡은 슬롯을 지워버림

사용자 확인: "7번 슬롯에는 버튼이 정상작동하네 ... 6번슬롯에 대한 락을 거는 코드에 관한거 같아 ...
스킬 특성 이거 부분에서 6번에 대한 요청이 많았는데 확인을 한번좀 해줘" — 슬롯 7은 완전 정상이라는
결과로 문제가 "슬롯 6 자체"에 있다는 게 확정됐고, 그 실마리로 스킬 트레이트 시스템(doc/0228/0251,
`UnitSkillSlotIndex = 6`)을 다시 조사해서 **진짜 원인을 찾음**.

### 원인

`RTSUnitController.UpdateUnitSkillUI()`는 선택 종류와 무관하게 **매 프레임 무조건 호출**된다(주석에
명시: "switch보다 먼저, 매 프레임 무조건 호출"). 유닛이 하나도 선택 안 돼 있으면(건물 선택 포함)
이렇게 동작했다:

```csharp
private void UpdateUnitSkillUI()
{
    bool useFallbackSlot = UnitSelectState == UnitState.Worker;   // ← 낡은 값

    if (selectedUnitList.Count == 0)
    {
        uIController.HideSkillSelectPanel();
        uIController.ClearUnitSkillSlot(useFallbackSlot);   // 매 프레임 실행
        return;
    }
    ...
```

`UnitSelectState`는 **유닛을 선택했을 때만 갱신**되는 필드라서, 건물을 선택 중일 땐 "마지막으로
선택했던 유닛이 뭐였는지"의 낡은 값이 그대로 남아있다:

- **게임 시작 직후 바로 건물 선택**: 유닛을 한 번도 선택한 적 없어 `UnitSelectState`가 기본값(`None`)
  → `useFallbackSlot = false` → 매 프레임 **슬롯 6을 `Clear()`**. 그 직후(같은 프레임)
  `ShowBuildingRallyCommand()`가 다시 슬롯 6에 랠리 데이터를 `SetData()`로 채움 → 매 프레임
  Clear→SetData가 반복되며 `gameObject.SetActive(false)`→`true`가 반복돼 반투명하게 보이고
  Selectable의 눌림 색상 전환도 매 프레임 끊겨서 안 보임.
- **일꾼 먼저 선택**: `UnitSelectState = Worker` → `useFallbackSlot = true` → 매 프레임 **슬롯 7**을
  `Clear()`(슬롯 6은 안 건드림) → 이후 건물 선택 시 슬롯 6은 아무도 안 건드려서 랠리 버튼 정상.
- **슬롯 7로 옮긴 뒤**: 신선한 게임에선 `useFallbackSlot=false`라 슬롯 6만 지워지고 슬롯 7은 손
  안 대서 항상 정상 — 사용자가 확인한 결과와 정확히 일치.

즉 "슬롯 6에 대한 락"이 아니라, **"지금 선택된 유닛이 없으니 스킬 슬롯을 지워라"는 로직이 매 프레임
반복 실행되면서, 그 대상 슬롯을 정하는 기준(`UnitSelectState`)이 낡은 값이라 엉뚱한(또는 마침 다른
용도로 쓰이는) 슬롯을 계속 건드린 버그**였다.

### 적용

**`Assets/Scripts/System/RTSUnitController.cs`**: `ClearUnitSkillSlot`을 "유닛이 없을 때마다 매
프레임" 호출하는 대신, "스킬 슬롯에 실제로 뭔가 표시됐던 경우에만, 표시가 끝나는 시점에 딱 한 번" 그
슬롯만 정리하도록 변경. `skillSlotShown`/`skillSlotUsedFallback` 필드로 "마지막으로 실제 표시했을 때
어느 슬롯을 썼는지"를 기억해서, 스킬을 보여준 적 없는 컨텍스트(건물 선택 등)는 아예 건드리지 않는다.

```diff
+    private bool skillSlotShown;
+    private bool skillSlotUsedFallback;
+
+    private void ClearSkillSlotIfShown()
+    {
+        if (!skillSlotShown) return;
+        uIController.ClearUnitSkillSlot(skillSlotUsedFallback);
+        skillSlotShown = false;
+    }
+
     private void UpdateUnitSkillUI()
     {
         bool useFallbackSlot = UnitSelectState == UnitState.Worker;

         if (selectedUnitList.Count == 0)
         {
             uIController.HideSkillSelectPanel();
-            uIController.ClearUnitSkillSlot(useFallbackSlot);
+            ClearSkillSlotIfShown();
             return;
         }
         ... (data == null 분기, chosen == None 분기도 동일하게 교체) ...

         uIController.ShowUnitSkillSlot(new CommandButtonData(...), useFallbackSlot);
+        skillSlotShown = true;
+        skillSlotUsedFallback = useFallbackSlot;
```

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일). Play Mode에서 재검증: 게임 시작 직후
바로 건물(MainBase) 선택 후 `skillSlotShown` 필드를 리플렉션으로 확인 → `False`(스킬을 한 번도 안
보여줬으므로 `ClearUnitSkillSlot` 자체가 호출 안 됨) — 슬롯 6이 더 이상 매 프레임 껐다 켜졌다 하지
않음을 확인.

**참고**: 근본 원인이 고쳐졌으므로 랠리 버튼을 다시 슬롯 6으로 옮겨도 이제 안전하다. 다만 굳이 옮길
필요는 없어서(슬롯 7이 이미 잘 동작 중) 일단 슬롯 7 그대로 둠 — 사용자가 원하면 6으로 되돌릴 수 있음.

## 슬롯 6으로 복귀 (2026-08-02)

"6번으로 옮겨줘" — `BuildingRallySlotIndex`를 `7` → `6`으로 되돌리고, `UIController.cs`/
`RTSUnitController.cs`의 관련 주석도 갱신(`rallyIcon` 필드 주석, `RallyButtonAction()` 주석,
`BuildingRallySlotIndex` 선언부 주석 - 근본 원인이 이미 고쳐졌으므로 슬롯 6으로 되돌려도 안전하다는
내용으로). `npx uloop-cli compile` 통과 (에러 0, 경고 33개 - 기존과 동일, 신규 경고 없음).
