# 0431. 퍼즈 중 유닛 선택/명령 입력 차단

- 날짜: 2026-08-05
- 상태: **취소됨** - 적용을 시작했으나(`UserControl.cs`, `ProductionSlot.cs`) 사용자가 바로 취소를
  요청해 두 파일 모두 원상복구했다(`git checkout`). `TestSceneMenuController.cs`는 이번 건과
  무관한(doc/0430) 기존 변경만 남아있고 이번 건으로 손댄 적 없음. 이 문서는 제안 내용 기록으로만
  남겨두고, 코드에는 아무것도 반영되지 않은 상태다.

## 요청 내용

> 게임이 퍼즈 상태로 가게 되면 유닛조종이나 게임을 조종하는 행위도 하지 못하도록 막을수 있나?
> 현재 옵션창이 켜지더라도 유닛을 선택해서 명령은 내릴수 있단말이야.(명령을 수행하러 이동하거나
> 행동하진 못하더라도 게임시간이 0이라서) 근데 그런 상황 자체가 안나오도록

[[0430]]에서 옵션 패널을 열 때 `Time.timeScale = 0f`로 게임을 멈췄지만, `Time.timeScale`은
`Update()`가 도는 것 자체는 막지 않고 `Time.deltaTime`만 0으로 만든다. 그래서 이동/공격 등
"결과"는 멈춰도, 마우스 클릭으로 유닛을 선택하거나 명령을 접수하는 입력 처리 자체는 그대로
동작하고 있었다.

## 조사 내용

게임 조작(선택/명령)이 실제로 들어오는 경로를 전부 추적했다:

1. **메인 화면 클릭/키보드** — `UserControl.cs`의 `Update()`(188~203번 줄) 하나가
   `HandleMouse()`(좌클릭 선택/드래그선택, 우클릭 이동/공격/명령확정)와
   `HandlekeyBoard()`(A/S/P/H/T 등 커맨드 단축키, 1~9 부대 단축키, Esc 등)를 전부 호출하는
   단일 진입점이다.
2. **미니맵 클릭** — `MinimapController.cs`는 `UserControl.Update()`를 거치지 않고, 우클릭 시
   `userControl.IssueRightClickMoveAt()`를, 좌클릭(대기 중인 명령이 있을 때)엔
   `userControl.ConfirmPendingOrderAt()`를 **직접** 호출한다(doc/0349 - "메인 화면 클릭과
   미니맵 클릭이 공유"). 즉 `Update()`만 막으면 미니맵으로는 여전히 명령을 내릴 수 있다.
3. **커맨드 패널/생산/스킬/연구 버튼** — `ProductionSlot.cs`가 공격/정지/순찰/홀드/생산/스킬/
   연구/리프트/랠리 등 모든 커맨드 버튼의 공용 슬롯 컴포넌트다(`OnClick()`이 마우스 클릭을,
   `Update()`의 `Input.GetKeyDown(shortcut)`이 그 버튼의 키보드 단축키를 처리 - 둘 다 같은
   콜백을 호출).

이 세 곳(①`UserControl.Update()` ②`UserControl`의 `ConfirmPendingOrderAt`/
`IssueRightClickMoveAt`(미니맵이 직접 호출하는 지점 - 여기서 막으면 `MinimapController.cs`는
전혀 손댈 필요 없음, "호출하는 모든 곳"이 아니라 "공유되는 지점 하나"를 막는 방식) ③
`ProductionSlot.OnClick()`/단축키 체크)만 막으면, 사실상 모든 "명령을 내리는" 경로가 막힌다.
(부대선택 버튼(`ControlGroupPanel`)이나 여러 유닛 선택 시 나오는 Squad_panel 아이콘 클릭은
선택만 바꿀 뿐 게임 상태에 아무 영향이 없어 그대로 둬도 무방 - 그대로 둔다.)

퍼즈 여부를 판단할 공용 플래그가 아직 없어서, `UserControl`에 `public static bool IsPaused`
하나를 추가하고 [[0430]]에서 만든 `TestSceneMenuController`의 Open/Close 지점에서 이 값을
`Time.timeScale`과 함께 같이 갱신한다(새 시스템을 따로 만들지 않고 기존 On/Off 지점 재사용).

## 코드 변경 (제안)

**`Assets/Scripts/UserControl/UserControl.cs`**

```csharp
// 추가 - 클래스 최상단 필드 근처
    // 옵션 패널이 열려 게임이 퍼즈된 동안 true. TestSceneMenuController가 Open/CloseOptionsPanel에서
    // Time.timeScale과 함께 갱신한다 - Time.timeScale=0은 Update() 자체를 막지 않으므로(deltaTime만
    // 0이 됨) 입력 처리는 이 플래그로 별도로 막아야 한다.
    public static bool IsPaused;
```

```csharp
// 기존 (188~203번 줄)
    private void Update()
    {
        //마우스 입력 관리
        HandleMouse();
        //키보드 입력 관리
        HandlekeyBoard();

        // 입력 상황에 따라 포인터 생성
        UpdatePointer();

        // 명령 확정 후 일정 시간이 지난 마커를 자동으로 숨김
        UpdatePointerAutoHide();

        // 입력 상황에 따라 마우스 커서 아이콘 갱신
        UpdateCursor();
    }
```
```csharp
// 변경
    private void Update()
    {
        if (IsPaused)
            return; // 퍼즈 중엔 선택/명령 입력을 아예 처리하지 않는다

        //마우스 입력 관리
        HandleMouse();
        //키보드 입력 관리
        HandlekeyBoard();

        // 입력 상황에 따라 포인터 생성
        UpdatePointer();

        // 명령 확정 후 일정 시간이 지난 마커를 자동으로 숨김
        UpdatePointerAutoHide();

        // 입력 상황에 따라 마우스 커서 아이콘 갱신
        UpdateCursor();
    }
```

```csharp
// 기존 (664번 줄, 미니맵 좌클릭 명령 확정도 공유하는 지점)
    public void ConfirmPendingOrderAt(Vector3 groundPoint)
    {
        if (UsercurrentState == OrderState.SkillGround)
```
```csharp
// 변경
    public void ConfirmPendingOrderAt(Vector3 groundPoint)
    {
        if (IsPaused)
            return; // 미니맵 클릭은 Update()를 거치지 않고 이 메서드를 직접 호출하므로 여기서도 막아야 함

        if (UsercurrentState == OrderState.SkillGround)
```

```csharp
// 기존 (717번 줄, 미니맵 우클릭 이동/랠리도 공유하는 지점)
    public void IssueRightClickMoveAt(Vector3 groundPoint)
    {
        if (rtsUnitController.IsUnitSelect())
```
```csharp
// 변경
    public void IssueRightClickMoveAt(Vector3 groundPoint)
    {
        if (IsPaused)
            return;

        if (rtsUnitController.IsUnitSelect())
```

**`Assets/Scripts/UI/ProductionSlot.cs`**

```csharp
// 기존 (130~148번 줄)
    private void OnClick()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
```
```csharp
// 변경
    private void OnClick()
    {
        if (UserControl.IsPaused)
            return; // 퍼즈 중엔 공격/생산/스킬 등 어떤 커맨드 버튼도 실행하지 않는다

        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
```

```csharp
// 기존 (152~162번 줄, 버튼의 키보드 단축키 처리)
    private void Update()
    {
        if (isHovered)
            RefreshTooltip(); // 쿨다운 잔여시간처럼 매 프레임 바뀌는 설명 텍스트를 호버 중에도 실시간으로 반영

        if (!hasData || shortcut == KeyCode.None || button == null || !button.interactable)
            return;

        if (Input.GetKeyDown(shortcut))
            StartCoroutine(SimulateClickRoutine());
    }
```
```csharp
// 변경
    private void Update()
    {
        if (isHovered)
            RefreshTooltip(); // 쿨다운 잔여시간처럼 매 프레임 바뀌는 설명 텍스트를 호버 중에도 실시간으로 반영

        if (UserControl.IsPaused || !hasData || shortcut == KeyCode.None || button == null || !button.interactable)
            return;

        if (Input.GetKeyDown(shortcut))
            StartCoroutine(SimulateClickRoutine());
    }
```
(툴팁 호버 표시는 퍼즈와 무관하게 그대로 둬도 되는 정보 표시일 뿐이라 `RefreshTooltip()`은 막지
않는다 - 단축키 실행만 막는다)

**`Assets/Scripts/UI/TestSceneMenuController.cs`**

```csharp
// 기존
    public void OpenOptionsPanel()
    {
        optionsPanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    public void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f; // 옵션(퍼즈) 상태로 나가면 다음 씬까지 멈춰있지 않도록 안전하게 복구
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }
```
```csharp
// 변경
    public void OpenOptionsPanel()
    {
        optionsPanel?.SetActive(true);
        Time.timeScale = 0f;
        UserControl.IsPaused = true;
    }

    public void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f; // 옵션(퍼즈) 상태로 나가면 다음 씬까지 멈춰있지 않도록 안전하게 복구
        UserControl.IsPaused = false;
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(nextStageSceneName);
    }
```

## 요약 / 영향받는 파일

- 옵션 패널이 열려 있는 동안엔: 메인 화면 클릭(선택/드래그선택/이동/공격 등 명령), 미니맵
  클릭(이동/명령확정), 커맨드 패널/생산/스킬/연구 버튼(마우스 클릭 + 키보드 단축키 전부) 모두
  아무 동작도 하지 않는다.
- 부대선택 버튼/Squad_panel 아이콘 클릭 등 "선택만 바꾸는" 조작은 게임 상태에 영향이 없어 그대로
  둔다(원하면 이것도 막을 수 있음 - 필요하면 알려달라).
- 옵션 패널의 볼륨 슬라이더/닫기 버튼 등 옵션 패널 자체의 UI는 당연히 영향받지 않는다(별도
  경로라 이번 변경과 무관).
- 영향받는 파일: `Assets/Scripts/UserControl/UserControl.cs`, `Assets/Scripts/UI/ProductionSlot.cs`,
  `Assets/Scripts/UI/TestSceneMenuController.cs` (코드 변경만, 씬/프리팹 변경 없음)
- 아직 프로젝트 파일에는 적용하지 않음 (제안 단계).
