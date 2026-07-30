# 0311. Option 패널 열림/닫힘에 따라 메인 메뉴 패널 토글 (제안)

날짜: 2026-07-30

## 요청 내용

> Optionpanel을 열면 mainmenupanel은 꺼지도록 해주고 optionpanel에서 X버튼을 눌러야 다시
> 옵션패널은 꺼주고 메인패널이 켜지도록 해줘

## 조사 내용

- `MainScene.unity`에서 버튼들은 `Canvas > Panel` 아래 `Play`/`Option`/`Exit`로 존재 (`doc/0309`에서
  확인한 그대로) - 이 `Panel`이 사용자가 말하는 "mainmenupanel"에 해당함.
- `Assets/Scripts/UI/MainMenuController.cs`(`doc/0309`)에 이미 `optionsPanel` 필드와
  `OnOptionClicked()`/`CloseOptionsPanel()`이 있음 - 지금은 옵션 패널을 켜고 끄기만 하고, 메인 메뉴
  패널(Panel) 쪽은 건드리지 않음.
- 지금까지 이 스크립트는 버튼을 `[SerializeField] private Button ...` + `Awake()`에서
  `onClick.AddListener(...)`로 직접 연결하는 방식을 써왔음(`squadPageButtons` 패턴과 동일) - 옵션
  패널의 "X(닫기)" 버튼도 같은 방식으로 스크립트에서 직접 연결하는 게 일관적임(인스펙터에서 수동으로
  `OnClick()`을 등록하는 대신).

## 코드 변경

### `Assets/Scripts/UI/MainMenuController.cs`

**기존 코드**:
```csharp
    [Header("버튼 연결")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionButton;
    [SerializeField] private Button exitButton;

    [Header("씬 이동")]
    [SerializeField] private string testSceneName = "TestScene";

    [Header("옵션 패널 (레이아웃은 직접 제작 후 연결)")]
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        playButton?.onClick.AddListener(OnPlayClicked);
        optionButton?.onClick.AddListener(OnOptionClicked);
        exitButton?.onClick.AddListener(OnExitClicked);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void OnPlayClicked() => SceneManager.LoadScene(testSceneName);

    private void OnOptionClicked() => optionsPanel?.SetActive(true);

    // 옵션 패널에 닫기 버튼을 만들 때 이 메서드를 OnClick()에 연결하면 된다.
    public void CloseOptionsPanel() => optionsPanel?.SetActive(false);
```

**변경 코드**:
```csharp
    [Header("버튼 연결")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button optionCloseButton; // 옵션 패널의 X(닫기) 버튼

    [Header("씬 이동")]
    [SerializeField] private string testSceneName = "TestScene";

    [Header("패널 (레이아웃은 직접 제작 후 연결)")]
    [SerializeField] private GameObject mainMenuPanel; // 비워두면 이 스크립트가 붙은 오브젝트 자신을 사용
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        if (mainMenuPanel == null)
            mainMenuPanel = gameObject;

        playButton?.onClick.AddListener(OnPlayClicked);
        optionButton?.onClick.AddListener(OnOptionClicked);
        exitButton?.onClick.AddListener(OnExitClicked);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    private void OnPlayClicked() => SceneManager.LoadScene(testSceneName);

    private void OnOptionClicked()
    {
        optionsPanel?.SetActive(true);
        mainMenuPanel?.SetActive(false);
    }

    // 옵션 패널의 X 버튼에 연결된다(Awake에서 자동 연결).
    public void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
    }
```

## 요약

- `mainMenuPanel` 필드 추가 - 비워두면(이 스크립트를 `Panel` 자체에 붙인 경우) 자기 자신을 자동으로
  쓰므로 별도 연결 없이도 동작.
- Option 클릭 → 옵션 패널 켜짐 + 메인 메뉴 패널 꺼짐.
- 옵션 패널의 X 버튼(`optionCloseButton` 필드에 연결) 클릭 → 옵션 패널 꺼짐 + 메인 메뉴 패널 켜짐.
- X 버튼은 인스펙터에서 수동으로 `OnClick()`을 등록할 필요 없이, `optionCloseButton` 필드에 버튼만
  연결하면 스크립트가 알아서 `CloseOptionsPanel()`에 연결한다(다른 버튼들과 동일한 방식).

## 필요한 작업 (코드 외)

- 옵션 패널에 X(닫기) 버튼을 아직 안 만들었다면 만들고, `MainMenuController`의
  `Option Close Button` 필드에 연결.
- `mainMenuPanel` 필드는 이 스크립트가 `Panel`에 붙어있다면 비워둬도 되지만, 다른 오브젝트에
  붙어있다면 `Panel`을 직접 연결.

## 영향받는 파일

- `Assets/Scripts/UI/MainMenuController.cs` (수정)

## 다음 단계

이대로 수정해도 될지 확인 부탁드립니다.
