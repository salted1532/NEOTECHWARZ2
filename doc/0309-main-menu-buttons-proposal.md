# 0309. 메인 메뉴 Play/Option/Exit 버튼 연결 (제안)

날짜: 2026-07-30

## 요청 내용

> 지금 메인화면을 일단 테스트 버전으로 만들고 있는데 지금 MainScene에서 play버튼, option버튼, Exit
> 버튼 있는데 play버튼은 testScene으로 이동하도록 하는 버튼 연결해주고, option버튼 클릭시 설정창이
> 나오도록 하고 exit버튼 클릭시 게임이 꺼지도록 하는 코드 작성해줘

## 조사 내용

- `Assets/Scenes/MainScene.unity`의 `Canvas > Panel` 아래에 `Play`, `Option`, `Exit`라는 이름의
  `Button` GameObject가 이미 존재함(요청 원문 그대로).
- 이동할 씬은 `Assets/Scenes/TestScene.unity` (정확한 이름 `TestScene`).
- **Build Settings에 `MainScene`도 `TestScene`도 등록되어 있지 않음** - 지금은 `SampleScene`만 등록됨.
  `SceneManager.LoadScene("TestScene")`은 Build Settings에 그 씬이 들어있어야 런타임에 동작하므로,
  이 작업을 하려면 Build Settings에 두 씬을 추가해야 함 (File > Build Settings > Add Open Scenes,
  또는 스크립트로 `EditorBuildSettings.scenes` 수정).
- 옵션(설정) 패널은 MainScene에 아직 없음. 이 프로젝트엔 이미 `Assets/Scripts/UI/SoundSettingsPanel.cs`가
  있는데, 그 파일 주석에 명시된 컨벤션이 "이 스크립트는 로직만 담당한다. 실제 Canvas/슬라이더/토글
  GameObject 배치(레이아웃)는 유니티 에디터에서 직접 만들고, 인스펙터 필드에 연결해야 동작한다"임 -
  즉 이 프로젝트는 UI 레이아웃은 사람이 에디터에서 만들고, 스크립트는 그 참조를 받아 로직만 처리하는
  방식이 이미 확립된 컨벤션. 이번에도 같은 방식으로 간다: 설정 패널 GameObject 자체는 직접 만드시고,
  스크립트에는 그 패널을 켜고 끄는 로직만 넣는다 (원하면 나중에 `SoundSettingsPanel`을 그 패널의
  자식으로 붙이면 그대로 동작).
- `UIController.cs`의 기존 버튼 연결 컨벤션 확인: 인스펙터에서 OnClick()을 수동으로 등록하는 대신,
  스크립트가 `[SerializeField] private Button ...` 참조를 받아서 `Start()`/`Awake()`에서
  `button.onClick.AddListener(...)`로 직접 연결한다 (예: `squadPageButtons`). 이번 스크립트도 동일한
  패턴으로 작성.

## 제안 코드 (신규 파일)

### `Assets/Scripts/UI/MainMenuController.cs` (신규)

MainScene의 `Canvas` 또는 `Panel` GameObject에 부착. 인스펙터에서 `Play`/`Option`/`Exit` 버튼과
(직접 만들 예정인) 옵션 패널을 연결.

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메인 메뉴 화면(MainScene)의 Play/Option/Exit 버튼을 연결한다. 옵션 패널 자체(레이아웃)는
// SoundSettingsPanel.cs와 동일한 컨벤션으로 유니티 에디터에서 직접 만들고, 이 스크립트에는
// 인스펙터로 그 패널 GameObject만 연결하면 된다 - 여기선 켜고 끄는 로직만 담당한다.
public class MainMenuController : MonoBehaviour
{
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

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Application.Quit()이 동작하지 않는다
#else
        Application.Quit();
#endif
    }
}
```

## 필요한 씬/설정 변경 (코드 외)

1. **Build Settings에 `MainScene`, `TestScene` 추가** - 안 하면 `Play` 버튼을 눌러도
   `SceneManager.LoadScene("TestScene")`이 실패한다 (콘솔에 에러 발생). File > Build Settings >
   Add Open Scenes로 두 씬을 순서대로 추가하면 됨. 원하면 제가 대신 씬 파일을 열어서 추가해드릴 수
   있음.
2. **옵션 패널 GameObject 직접 제작** - 지금 MainScene엔 설정창이 없어서, 위 스크립트의
   `Options Panel` 필드에 연결할 패널을 직접 만들어야 함(빈 패널 + 원하는 UI). 다 만든 뒤 그 패널을
   `MainMenuController`의 `Options Panel` 필드에 드래그하고, `Play`/`Option`/`Exit` 버튼도 각각의
   필드에 연결하면 끝.

## 영향받는 파일

- `Assets/Scripts/UI/MainMenuController.cs` (신규)
- (승인 시) `ProjectSettings/EditorBuildSettings.asset` - MainScene/TestScene 추가

## 다음 단계

1. 위 스크립트를 이대로 생성해도 될지
2. Build Settings에 MainScene/TestScene을 지금 추가해드릴지

확인 부탁드립니다.
