using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TestScene(게임플레이 씬)의 옵션 패널을 열고 닫고, "메인화면으로 나가기"를 처리한다. 사운드 슬라이더
// 연결은 SoundSettingsPanel.cs가 이미 담당하므로 이 스크립트는 패널 표시/씬 전환만 담당한다.
public class TestSceneMenuController : MonoBehaviour
{
    [Header("버튼 연결")]
    [SerializeField] private Button optionButton;       // 옵션 패널 열기
    [SerializeField] private Button optionCloseButton;   // 옵션 패널의 X(닫기) 버튼
    [SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";

    [Header("옵션 패널 (레이아웃/사운드 슬라이더는 직접 제작 후 연결)")]
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        optionButton?.onClick.AddListener(OpenOptionsPanel);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);

        optionsPanel?.SetActive(false);
    }

    public void OpenOptionsPanel() => optionsPanel?.SetActive(true);

    public void CloseOptionsPanel() => optionsPanel?.SetActive(false);

    private void OnMainMenuClicked() => SceneManager.LoadScene(mainSceneName);
}
