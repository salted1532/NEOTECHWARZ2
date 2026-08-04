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
    [SerializeField] private Button nextStageButton;     // "다음 스테이지로 이동"

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string nextStageSceneName = "SampleScene";

    [Header("옵션 패널 (레이아웃/사운드 슬라이더는 직접 제작 후 연결)")]
    [SerializeField] private GameObject optionsPanel;

    private void Awake()
    {
        optionButton?.onClick.AddListener(OpenOptionsPanel);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);

        optionsPanel?.SetActive(false);
    }

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
}
