using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// MissionSelect 씬의 미션 버튼들을 연결한다. 버튼→씬 매핑은 코드에 하드코딩하지 않고 인스펙터의
// missions 리스트로 노출해서, 미션이 추가되거나 순서가 바뀌어도 코드 수정 없이 처리할 수 있게 한다
// (doc/0470). 호버 시 툴팁은 기존 TooltipUI 싱글턴을 그대로 재사용한다 - 이 씬 전용 매니저라
// 싱글턴은 아니다.
[System.Serializable]
public class MissionSelectEntry
{
    public Button button;
    public int missionNumber; // 서브미션은 같이 병행되는 본편 미션 번호를 그대로 써서 해금 상태를 공유한다
    public string missionName;
    public string sceneName; // SceneManager.LoadScene에 넘길 씬 이름 (예: "Mission0")
    public bool isSubMission; // true면 이름/툴팁 부제 조회 시 "missionselect.name.sub{missionNumber}" 등 서브미션 전용 키를 쓴다
}

public class MissionSelectManager : MonoBehaviour
{
    // 미션 0/1은 항상 열려있고, 그 이후는 여기 저장된 "마지막으로 해금된 미션 번호" 이하만 열린다
    // (doc/0472). 미션을 클리어하면 다음 미션이 열리는 연출은 각 미션 씬의 StageManager.OnVictory
    // 등에서 이 키를 갱신하도록 나중에 이어붙이면 됨 - 이번 작업에는 그 연결까진 포함하지 않음.
    private const string HighestUnlockedMissionKey = "HighestUnlockedMission";
    private const int DefaultHighestUnlockedMission = 1;

    [SerializeField] private List<MissionSelectEntry> missions = new();

    [Header("메인 메뉴로 돌아가기")]
    [SerializeField] private Button backToMainMenuButton; // 씬의 "Close" 버튼
    [SerializeField] private string mainMenuSceneName = "MainScene"; // SceneMenuController.mainSceneName과 동일한 컨벤션

    [Header("개발자용 - 정식 버전 출시 전 이 버튼과 연결을 제거할 것")]
    [SerializeField] private Button unlockAllMissionButton;

    [Header("브리핑룸")]
    [SerializeField] private string briefingRoomSceneName = "Briefing_Room"; // 미션 씬으로 바로 가지 않고 이 씬을 거친다 (doc/0616)

    // MainScene(MainMenuController)이나 미션 플레이(UserControl)에서 바꾼 커서가 Cursor.SetCursor의
    // 전역 상태 때문에 씬을 넘어와도 남아있던 문제(doc/0473) - MainMenuController와 동일한 패턴으로
    // 이 씬 진입 시 기본 커서로 되돌리고, 버튼 호버 시에만 다시 바꾼다.
    [Header("마우스 커서 (MainMenuController와 동일한 패턴)")]
    [SerializeField] private Texture2D cursorTexture; // 비워두면 OS 기본 화살표 사용
    [SerializeField] private Texture2D cursorHoverTexture; // 비워두면 호버해도 커서 안 바뀜
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;
    [SerializeField] private Camera uiCamera; // Canvas RenderMode가 Overlay면 비워둔다

    private Button[] hoverableButtons;
    private bool isHoveringButton;

    private void Awake()
    {
        foreach (MissionSelectEntry entry in missions)
        {
            if (entry.button == null)
                continue;

            entry.button.onClick.AddListener(() => LoadMission(entry));
            entry.button.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick()); // 버튼 클릭 공통 사운드(doc/0648)
            SetupHoverTooltip(entry);
        }

        ApplyLockState();

        backToMainMenuButton?.onClick.AddListener(BackToMainMenu);
        unlockAllMissionButton?.onClick.AddListener(UnlockAllMissions);

        // 버튼 클릭 공통 사운드(doc/0648)
        backToMainMenuButton?.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick());
        unlockAllMissionButton?.onClick.AddListener(() => SoundManager.Instance?.PlayUIClick());

        hoverableButtons = missions.Select(entry => entry.button)
            .Append(backToMainMenuButton)
            .Append(unlockAllMissionButton)
            .ToArray();

        if (cursorTexture != null)
            Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
    }

    private void Update()
    {
        if (cursorHoverTexture == null)
            return;

        bool hovering = IsHoveringClickableButton();

        if (hovering == isHoveringButton)
            return;

        isHoveringButton = hovering;
        Cursor.SetCursor(hovering ? cursorHoverTexture : cursorTexture, cursorHotspot, CursorMode.Auto);
    }

    private bool IsHoveringClickableButton()
    {
        foreach (Button button in hoverableButtons)
        {
            if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                continue;

            RectTransform rect = button.transform as RectTransform;

            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera))
                return true;
        }

        return false;
    }

    private void LoadMission(MissionSelectEntry entry)
    {
        if (string.IsNullOrEmpty(entry.sceneName))
            return;

        BriefingSelection.MissionNumber = entry.missionNumber;
        BriefingSelection.IsSubMission = entry.isSubMission;
        BriefingSelection.TargetSceneName = entry.sceneName;
        SceneManager.LoadScene(briefingRoomSceneName);
    }

    private void BackToMainMenu()
    {
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ApplyLockState()
    {
        int highestUnlocked = PlayerPrefs.GetInt(HighestUnlockedMissionKey, DefaultHighestUnlockedMission);

        foreach (MissionSelectEntry entry in missions)
        {
            if (entry.button == null)
                continue;

            entry.button.interactable = entry.missionNumber <= highestUnlocked;
        }
    }

    // 개발자용 - 모든 미션을 즉시 해금한다. 정식 버전 출시 전 이 버튼/메소드를 UnlockMission
    // 버튼과 함께 제거할 것.
    private void UnlockAllMissions()
    {
        int maxMissionNumber = DefaultHighestUnlockedMission;
        foreach (MissionSelectEntry entry in missions)
            maxMissionNumber = Mathf.Max(maxMissionNumber, entry.missionNumber);

        PlayerPrefs.SetInt(HighestUnlockedMissionKey, maxMissionNumber);
        ApplyLockState();
    }

    // UIController.AddStatHoverTooltip()과 동일한 EventTrigger 패턴(doc/0470).
    private void SetupHoverTooltip(MissionSelectEntry entry)
    {
        RectTransform rect = entry.button.transform as RectTransform;

        EventTrigger trigger = entry.button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = entry.button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ =>
        {
            // &lt;/&gt;는 이 프로젝트의 TMP 설정에서 디코딩되지 않고 글자 그대로 찍혀서(doc/0490
            // 확인) 리터럴 "<"/">"로 되돌림 - "Boot Camp>" 같은 유효하지 않은 태그는 TMP가 그냥
            // 일반 텍스트로 그려주므로 실제로는 이스케이프 없이도 안전하다.
            string nameKey = entry.isSubMission ? $"missionselect.name.sub{entry.missionNumber}" : $"missionselect.name.{entry.missionNumber}";
            string subtitleKey = entry.isSubMission ? "missionselect.tooltip.subtitle.sub" : "missionselect.tooltip.subtitle";
            string missionName = LocalizationManager.GetTextOrFallback(nameKey, entry.missionName);
            string subtitle = LocalizationManager.GetText(subtitleKey, entry.missionNumber);
            string planetName = LocalizationManager.GetText($"missionselect.planet.{entry.missionNumber}");
            TooltipUI.Instance?.Show(rect, $"<{missionName}>", $"{subtitle} · {planetName}");
        });
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => TooltipUI.Instance?.Hide());
        trigger.triggers.Add(exitEntry);
    }
}
