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

        mainMenuPanel?.SetActive(true);  // 꺼진 채로 저장돼있어도 시작하면 항상 켜지도록
        optionsPanel?.SetActive(false);
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

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Application.Quit()이 동작하지 않는다
#else
        Application.Quit();
#endif
    }
}
