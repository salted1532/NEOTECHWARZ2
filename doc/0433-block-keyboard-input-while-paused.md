# 0433. 퍼즈 중 키보드 입력 차단 (옵션창/승리화면)

- 날짜: 2026-08-05
- 상태: **적용 완료** (컴파일 확인: 에러 0개)

## 요청 내용

> 게임이 퍼즈 되었을때 옵션창, 승리화면 이 두가지경우에 퍼즈를 걸도록 했는데 퍼즈가 걸렸을때
> 입력을 막는건 UI 패널을 이용해서 게임화면을 덮어서 막았는데 키보드 입력은 못막았네 키보드
> 입력을 좀 막아줄래 근데 키보드 입력이 옵션창에서 사용되는 경우는 막으면 안돼고 부대지정이나
> 부대 선택 키 1~0사이 넘버버튼이랑 유닛 명령 내리는거 정도인거 같은데 키보드 입력에서 막을것들을
> 찾고 막아줘

마우스는 게임화면을 덮는 UI 패널(레이캐스트 차단)로 이미 막혀 있지만, 그 패널은 `Input.GetKey`류
호출 자체를 막지 못하므로 키보드 단축키는 여전히 그대로 동작한다.

## 조사 내용

`Assets/Scripts` 전체에서 `Input.GetKey`(`Down`/`Up` 포함)를 쓰는 곳은 딱 4개 파일뿐이다:

1. **`UserControl.cs`의 `HandlekeyBoard()`** — Esc(건설모드 취소/생산대기열 취소/명령대기 취소) +
   `HandleControlGroupInput()`(부대 지정 Ctrl+1~0, 병합 Shift+1~0, 선택 1~0) 전부를 호출하는
   단일 진입점. 요청하신 "부대지정이나 부대 선택 키 1~0"이 정확히 이 안에 있다.
2. **`ProductionSlot.cs`의 `Update()`** — 공격/이동/정지/순찰/홀드/생산/스킬/연구/리프트/랠리 등
   모든 커맨드 버튼의 키보드 단축키(`Input.GetKeyDown(shortcut)`)를 처리하는 공용 슬롯. 주석에도
   "유닛 명령... 각 버튼(ProductionSlot)이 자기 단축키를 직접 감지"라고 돼 있다 - 요청하신
   "유닛 명령 내리는거"가 여기 있다.
3. **`Assets/Scripts/BuildSystem/InputManager.cs`** — 건설 배치 모드 전용. Esc로 배치를 취소하는
   `OnExit` 이벤트를 발행한다(좌클릭 배치 확정은 `IsPointerOverUI()`로 이미 UI 위 클릭을 걸러내고
   있어 마우스 쪽은 기존 UI 차단 패널로 충분함 - 다만 Esc는 키보드라 안 막힘). 건물을 배치하던
   도중에 옵션/승리 화면이 뜨는 경우를 대비해 이 Esc도 같이 막는다.
4. **`Assets/Scripts/Camera/CameraControl.cs`** — 방향키 이동/Space(본진 복귀)/Q,E(카메라 회전).
   전부 "카메라 시점 조작"일 뿐 유닛/게임 상태에 영향을 주지 않아서, 요청하신 "유닛조종/게임조종"
   범주에 안 들어간다고 판단해 그대로 둔다(퍼즈 중에도 화면을 둘러보는 건 자연스러움).

옵션 패널 쪽 스크립트(`SoundSettingsPanel` 등)는 `Input.GetKey`를 전혀 쓰지 않는다(볼륨 슬라이더는
유니티 기본 `Slider`/`EventSystem` 처리라 이 커스텀 키 입력 코드와 무관) - 그래서 "옵션창에서
쓰는 키 입력은 막으면 안 된다"는 조건은 위 세 곳을 막아도 저절로 지켜진다(애초에 겹치는 게 없음).

## 코드 변경 (제안)

퍼즈 여부를 나타내는 공용 플래그가 필요하다. [[0430]]에서 만든 옵션창 On/Off 지점과, 기존부터
있던 승리화면 On/Off 지점(`VictoryPanelController.cs`) 양쪽에서 같이 갱신한다.

**`Assets/Scripts/UserControl/UserControl.cs`**
```csharp
// 추가 - 클래스 필드
    // 옵션 패널/승리 화면이 떠서 게임이 퍼즈된 동안 true. TestSceneMenuController/VictoryPanelController가
    // 패널을 열고 닫을 때 같이 갱신한다. 마우스는 화면을 덮는 UI 패널로 이미 막혀 있으므로, 여기서는
    // Input.GetKey류를 쓰는 키보드 단축키 처리만 막는다.
    public static bool IsPaused;
```
```csharp
// 기존
    private void HandlekeyBoard()
    {
        // 유닛 명령(Attack/Move/Stop/Patrol/Hold/Return/Build)과 건물 건설/유닛 생산 단축키, 그리고
        // 이제 생산 건물의 랠리(Y) 단축키까지 각 버튼(ProductionSlot)이 자기 단축키를 직접 감지해서
        // 스스로 클릭되므로 여기서 따로 처리하지 않는다 (doc/0363).

        if (rtsUnitController.IsBuildMode())
```
```csharp
// 변경
    private void HandlekeyBoard()
    {
        // 유닛 명령(Attack/Move/Stop/Patrol/Hold/Return/Build)과 건물 건설/유닛 생산 단축키, 그리고
        // 이제 생산 건물의 랠리(Y) 단축키까지 각 버튼(ProductionSlot)이 자기 단축키를 직접 감지해서
        // 스스로 클릭되므로 여기서 따로 처리하지 않는다 (doc/0363).

        if (IsPaused)
            return; // 퍼즈 중엔 Esc 취소/부대 지정·선택(1~0) 등 키보드 명령을 처리하지 않는다

        if (rtsUnitController.IsBuildMode())
```

**`Assets/Scripts/UI/ProductionSlot.cs`** (마우스 클릭이 아니라 키보드 단축키 체크만 막음)
```csharp
// 기존 (152~162번 줄)
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

**`Assets/Scripts/BuildSystem/InputManager.cs`**
```csharp
// 기존
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnClicked?.Invoke();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnExit?.Invoke();
        }

    }
```
```csharp
// 변경
    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnClicked?.Invoke(); // 마우스는 화면을 덮는 UI 패널로 이미 막혀 있어 여기선 그대로 둠

        if (!UserControl.IsPaused && Input.GetKeyDown(KeyCode.Escape))
        {
            OnExit?.Invoke();
        }
    }
```

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

**`Assets/Scripts/UI/VictoryPanelController.cs`**
```csharp
// 기존
    private IEnumerator ShowVictoryPanelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(victoryDelay);
        victoryPanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }

    private void OnReturnToGameClicked()
    {
        victoryPanel?.SetActive(false);
        Time.timeScale = 1f;
    }
```
```csharp
// 변경
    private IEnumerator ShowVictoryPanelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(victoryDelay);
        victoryPanel?.SetActive(true);
        Time.timeScale = 0f;
        UserControl.IsPaused = true;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(nextStageSceneName);
    }

    private void OnReturnToGameClicked()
    {
        victoryPanel?.SetActive(false);
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
    }
```

## 요약 / 영향받는 파일

- 옵션창/승리화면이 떠 있는 동안: Esc 취소, 부대 지정/병합/선택(1~0), 커맨드 패널의 모든 키보드
  단축키(공격/이동/정지/순찰/홀드/생산/스킬/연구/리프트/랠리), 건설 배치 중 Esc 취소가 전부
  막힌다.
- 카메라 이동/회전(방향키, Space, Q/E)은 시점 조작일 뿐이라 그대로 둔다.
- 옵션 패널 자체는 키보드 입력을 전혀 쓰지 않아 이번 변경과 무관 - 볼륨 슬라이더 등은 평소처럼
  동작한다.
- 영향받는 파일: `Assets/Scripts/UserControl/UserControl.cs`, `Assets/Scripts/UI/ProductionSlot.cs`,
  `Assets/Scripts/BuildSystem/InputManager.cs`, `Assets/Scripts/UI/TestSceneMenuController.cs`,
  `Assets/Scripts/UI/VictoryPanelController.cs` (코드 변경만, 씬/프리팹 변경 없음)
- 아직 프로젝트 파일에는 적용하지 않음 (제안 단계).
