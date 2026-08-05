# 0435. TestSceneMenuController → SceneMenuController 정식 편입 + "이전 스테이지" 버튼 추가 (제안)

**날짜:** 2026-08-05

## 요청 내용
> TestSceneMenuController 를 정규 스크립트로 편입 시키려고해 Test라는 이름은 빼주고 Next StageButton
> 말고도 Previous 이전 스테이지 이동 버튼도 추가해줘 씬 이름은 내가 직접 입력할게

## 현재 구조

`Assets/Scripts/UI/TestSceneMenuController.cs` — 옵션 패널 열기/닫기, 메인화면 나가기,
다음 스테이지 이동을 담당. `nextStageButton` → `nextStageSceneName`("SampleScene" 기본값) →
`SceneManager.LoadScene()` 패턴이 이미 있음.

씬(`Assets/Scenes/SampleScene.unity`)에 이 스크립트가 GameObject `fileID: 1165283468`에 붙어 있고,
스크립트 guid는 `0390e5b057117274d9eb4e070525c8ff`. **파일을 rename할 때 `.meta`를 함께 옮겨서
guid를 보존**해야 씬의 컴포넌트 연결이 끊어지지 않음.

다른 두 파일이 주석에서 이름을 참조 중:
- `Assets/Scripts/UI/VictoryPanelController.cs:8` — "TestSceneMenuController와 동일한 컨벤션"
- `Assets/Scripts/UserControl/UserControl.cs:87` — "TestSceneMenuController/VictoryPanelController가"

## 제안하는 변경

1. **클래스/파일명 변경 (Test 제거)**
   - `Assets/Scripts/UI/TestSceneMenuController.cs` → `Assets/Scripts/UI/SceneMenuController.cs`
     (`git mv`로 `.cs`와 `.cs.meta` 함께 이동해 guid 보존 → 씬 연결 안 끊어짐)
   - 클래스명 `TestSceneMenuController` → `SceneMenuController`
   - 최상단 주석에서 "TestScene(게임플레이 씬)" 표현도 일반화

2. **"이전 스테이지" 버튼 추가** — `nextStageButton`/`nextStageSceneName`과 완전히 동일한 패턴:
   - `[SerializeField] private Button previousStageButton;`
   - `[SerializeField] private string previousStageSceneName = "";` (씬 이름은 인스펙터에서 직접 입력하실 것이므로 기본값은 빈 문자열)
   - `Awake()`에 `previousStageButton?.onClick.AddListener(OnPreviousStageClicked);`
   - `OnNextStageClicked()` 아래에 `OnPreviousStageClicked()` 추가 (동일하게 `Time.timeScale`/`UserControl.IsPaused` 복구 후 `SceneManager.LoadScene(previousStageSceneName)`)

3. **주석 참조 갱신** — `VictoryPanelController.cs:8`, `UserControl.cs:87`의
   "TestSceneMenuController" 텍스트를 "SceneMenuController"로 변경 (동작에는 영향 없음, 정확성만).

버튼 자체(UI 오브젝트 생성/인스펙터 연결, `previousStageSceneName` 값 입력)는 요청대로 직접 하실
것이므로 씬/프리팹 파일은 스크립트 참조 갱신 목적 외에는 건드리지 않음.

## 구현 (승인 후 적용됨)

**Before (`TestSceneMenuController.cs`):**
```csharp
// TestScene(게임플레이 씬)의 옵션 패널을 열고 닫고, "메인화면으로 나가기"를 처리한다. 사운드 슬라이더
// 연결은 SoundSettingsPanel.cs가 이미 담당하므로 이 스크립트는 패널 표시/씬 전환만 담당한다.
public class TestSceneMenuController : MonoBehaviour
{
    [Header("버튼 연결")]
    [SerializeField] private Button optionButton;       // 옵션 패널 열기
    [SerializeField] private Button optionCloseButton;   // 옵션 패널의 X(닫기) 버튼
    [SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"
    [SerializeField] private Button nextStageButton;     // "다음 스테이지로 이동"

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string nextStageSceneName = "SampleScene";
    ...
    private void Awake()
    {
        optionButton?.onClick.AddListener(OpenOptionsPanel);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);

        optionsPanel?.SetActive(false);
    }
    ...
    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(nextStageSceneName);
    }
}
```

**After (`SceneMenuController.cs`):**
```csharp
// 게임플레이 씬의 옵션 패널을 열고 닫고, "메인화면으로 나가기"를 처리한다. 사운드 슬라이더
// 연결은 SoundSettingsPanel.cs가 이미 담당하므로 이 스크립트는 패널 표시/씬 전환만 담당한다.
public class SceneMenuController : MonoBehaviour
{
    [Header("버튼 연결")]
    [SerializeField] private Button optionButton;       // 옵션 패널 열기
    [SerializeField] private Button optionCloseButton;   // 옵션 패널의 X(닫기) 버튼
    [SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"
    [SerializeField] private Button nextStageButton;     // "다음 스테이지로 이동"
    [SerializeField] private Button previousStageButton; // "이전 스테이지로 이동"

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string nextStageSceneName = "SampleScene";
    [SerializeField] private string previousStageSceneName = "";
    ...
    private void Awake()
    {
        optionButton?.onClick.AddListener(OpenOptionsPanel);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);
        previousStageButton?.onClick.AddListener(OnPreviousStageClicked);

        optionsPanel?.SetActive(false);
    }
    ...
    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(nextStageSceneName);
    }

    private void OnPreviousStageClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(previousStageSceneName);
    }
}
```

## 검증

- `git mv Assets/Scripts/UI/TestSceneMenuController.cs Assets/Scripts/UI/SceneMenuController.cs`
  (+ `.meta`)로 guid(`0390e5b057117274d9eb4e070525c8ff`) 보존 확인 — `git status`에 `R` (rename)으로 표시됨.
- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`, `WarningCount: 34`(전부 기존에도 있던 무관한
  경고, 새로 추가된 경고 없음).
- `Assets/Scenes/SampleScene.unity`의 `m_EditorClassIdentifier: Assembly-CSharp::TestSceneMenuController`는
  guid 기반 참조이므로 실제 연결은 안 끊기지만, 에디터에서 씬을 열고 저장하면 자동으로
  `SceneMenuController`로 갱신됨 (수동 텍스트 치환은 하지 않음 — 에디터가 정본).
- 새 `previousStageButton`/`previousStageSceneName` 인스펙터 필드는 요청대로 씬/프리팹에서 직접
  연결·입력하실 것이므로 이 세션에서는 스크립트만 변경.

## 영향받는 파일

- `Assets/Scripts/UI/TestSceneMenuController.cs` → `Assets/Scripts/UI/SceneMenuController.cs` (rename + 내용 변경)
- `Assets/Scripts/UI/VictoryPanelController.cs` (주석 텍스트만)
- `Assets/Scripts/UserControl/UserControl.cs` (주석 텍스트만)
