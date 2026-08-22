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
    [SerializeField] private string nextStageSceneName = "SampleScene"; // 다음 스테이지 씬 이름 - BriefingSelection.TargetSceneName으로 넘겨서 브리핑룸을 거친 뒤 이 씬으로 이동한다
    [SerializeField] private string briefingRoomSceneName = "Briefing_Room"; // MissionSelectManager와 동일 컨벤션 (doc/0616)
    [SerializeField] private string missionSelectSceneName = "MissionSelect"; // BriefingRoomController.goBackButton과 동일 컨벤션 - 서브미션은 nextStageSceneName이 이 값이라 브리핑룸을 건너뛴다 (doc/0670)

    [Header("연출")]
    [SerializeField] private float victoryDelay = 3f;

    private void Awake()
    {
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);
        returnToGameButton?.onClick.AddListener(OnReturnToGameClicked);

        // 버튼 클릭 공통 사운드(doc/0648)
        mainMenuButton?.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick());
        nextStageButton?.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick());
        returnToGameButton?.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick());

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
        SoundManager.Instance?.PlayVictoryScreenVoice(); // 승리화면 표시 사운드(doc/0645)
        Time.timeScale = 0f;
        UserControl.IsPaused = true;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
        SceneManager.LoadScene(mainSceneName);
    }

    // 씬을 바로 로드하지 않고 MissionSelectManager.LoadMission()과 같은 경로(BriefingSelection →
    // Briefing_Room)를 태워 다음 스테이지 브리핑을 거치게 한다 (doc/0635) - 이전엔 씬을 직접 로드해서
    // 브리핑룸이 스킵됐음.
    private void OnNextStageClicked()
    {
        if (string.IsNullOrEmpty(nextStageSceneName))
            return;

        Time.timeScale = 1f;
        UserControl.IsPaused = false;

        // 서브미션의 다음 목적지는 "다음 미션"이 아니라 미션 선택 화면이다 - 브리핑룸(다음 미션
        // 브리핑 전용)을 거치지 않고 바로 이동한다 (doc/0670).
        if (nextStageSceneName == missionSelectSceneName)
        {
            SceneManager.LoadScene(nextStageSceneName);
            return;
        }

        BriefingSelection.MissionNumber = missionNumber + 1;
        BriefingSelection.IsSubMission = false;
        BriefingSelection.TargetSceneName = nextStageSceneName;
        SceneManager.LoadScene(briefingRoomSceneName);
    }

    private void OnReturnToGameClicked()
    {
        victoryPanel?.SetActive(false);
        Time.timeScale = 1f;
        UserControl.IsPaused = false;
    }
}
