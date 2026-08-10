using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// StageManager.OnVictory를 구독해서 승리 패널을 띄우고, 패널 안의 "메인화면으로" 버튼을 처리한다.
// 패널 레이아웃(배경/문구/연출)은 유니티 에디터에서 직접 만들고 이 스크립트에는 그 패널 GameObject와
// 버튼만 연결하면 된다 - SceneMenuController와 동일한 컨벤션(패널 표시/씬 전환만 담당).
public class VictoryPanelController : MonoBehaviour
{
    // MissionSelectManager.HighestUnlockedMissionKey와 동일한 문자열(doc/0511) - 클리어 시
    // 다음 미션 번호로 갱신해서 MissionSelect 화면에서 잠금이 풀리게 한다.
    private const string HighestUnlockedMissionKey = "HighestUnlockedMission";

    [Header("승리 패널 (레이아웃은 직접 제작 후 연결)")]
    [SerializeField] private GameObject victoryPanel;

    [Header("해금 진행 (doc/0511)")]
    [SerializeField] private int missionNumber; // 이 씬이 몇 번 미션인지 - MissionSelectEntry.missionNumber와 동일 컨벤션

    [Header("버튼 연결")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button nextStageButton;
    [SerializeField] private Button returnToGameButton;

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string nextStageSceneName = "SampleScene"; // 다음 스테이지 씬이 아직 없어 일단 SampleScene으로 연결

    [Header("연출")]
    [SerializeField] private float victoryDelay = 3f;

    private void Awake()
    {
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);
        returnToGameButton?.onClick.AddListener(OnReturnToGameClicked);
        victoryPanel?.SetActive(false);
    }

    private void Start()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnVictory += HandleVictory;
    }

    private void OnDestroy()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnVictory -= HandleVictory;
    }

    private void HandleVictory()
    {
        UnlockNextMission();
        StartCoroutine(ShowVictoryPanelAfterDelay());
    }

    private void UnlockNextMission()
    {
        int highest = PlayerPrefs.GetInt(HighestUnlockedMissionKey, 1);
        if (missionNumber + 1 <= highest)
            return;

        PlayerPrefs.SetInt(HighestUnlockedMissionKey, missionNumber + 1);
        PlayerPrefs.Save();
    }

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
        if (string.IsNullOrEmpty(nextStageSceneName))
            return;

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
}
