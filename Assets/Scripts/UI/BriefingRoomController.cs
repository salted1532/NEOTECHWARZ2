using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Briefing_Room 씬 전용. MissionSelectManager가 BriefingSelection에 담아둔 미션 번호로
// briefingEntries에서 알맞은 항목을 찾아 초상화 3장/맵 이미지/대사/미션정보 텍스트를 채운다
// (doc/0616). MissionSelectEntry와 동일하게 인스펙터 리스트로 미션↔데이터를 매핑해서
// 미션이 추가돼도 코드 수정 없이 처리한다.
//
// 인물 초상화는 미션별로 직접 들고 있지 않고 characterRoster를 키로 조회한다 - 아드리안/부관/셀레나
// 등 같은 인물이 여러 미션 브리핑에 반복 등장하므로, 이미지를 한 번만 등록하면 그 인물이 나오는
// 모든 미션에 자동 반영된다 (doc/0617). 캐릭터가 없는 슬롯은 키를 비워두면 된다.
//
// 대사는 lines 순서대로 타이프라이터로 재생된다 (doc/0619). 각 줄의 speakerSlot에 해당하는
// 인물 이미지가 아직 안 나왔으면 텍스트 타이핑과 동시에 페이드인하고, 그 미션의 첫 화자(항상
// lines[0]의 슬롯)만 대기 없이 처음부터 보이게 시작한다.
//
// 타이프라이터는 문자열을 글자 단위로 이어붙이지 않고, 완성된 리치 텍스트를 한 번에 설정한 뒤
// TMP의 maxVisibleCharacters만 늘려간다 (doc/0620) - 인물 이름/목표 접두어에 <color> 태그를 쓰는데,
// 문자열을 잘라가며 타이핑하면 태그가 중간에 잘려서 그대로 노출되는 문제가 있어 이 방식으로 피한다.
[System.Serializable]
public class BriefingCharacter
{
    public string characterKey; // "adrian", "selena", "adjutant", "scout", "detachment_leader", "rescue_leader", "defense_leader"
    public string displayName; // 인스펙터 표기용
    public Sprite portrait;
    public Color labelColor = Color.white; // 대사 로그에서 "이름:" 부분에 입히는 색 (진영 구분용)
}

[System.Serializable]
public class BriefingLine
{
    public int speakerSlot; // 1, 2, 3 - speaker1/2/3Key 중 어느 슬롯인지
    public string speakerLabelKey; // 예: "briefing.speaker.adrian" (7개 고정 키, 재사용)
    public string textKey; // 그 줄의 대사, 예: "briefing.line.1.0"
}

[System.Serializable]
public class BriefingEntry
{
    public int missionNumber;
    public bool isSubMission;
    public string speaker1Key;
    public string speaker2Key;
    public string speaker3Key;
    public Sprite mapImage;
    public List<BriefingLine> lines = new();
}

public class BriefingRoomController : MonoBehaviour
{
    [SerializeField] private List<BriefingCharacter> characterRoster = new();
    [SerializeField] private List<BriefingEntry> briefingEntries = new();

    [Header("UI 참조")]
    [SerializeField] private Image speakerPortraitImage;
    [SerializeField] private Image speakerPortraitImage2;
    [SerializeField] private Image speakerPortraitImage3;
    [SerializeField] private Image mapImage;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private ScrollRect dialogueScrollRect; // dialogueText를 담은 Content의 스크롤뷰 - 로그가 넘치면 최신 줄로 자동 스크롤
    [SerializeField] private TextMeshProUGUI missionInfoText;
    [SerializeField] private Button goBackButton;
    [SerializeField] private Button startMissionButton;

    [SerializeField] private string missionSelectSceneName = "MissionSelect";

    [Header("타이프라이터 연출")]
    [SerializeField] private float charsPerSecond = 30f;
    [SerializeField] private float pauseBetweenLines = 0.6f;
    [SerializeField] private float portraitFadeDuration = 0.25f;

    [Header("미션정보 목표 색상")]
    [SerializeField] private Color mainObjectiveColor = new(0.95f, 0.3f, 0.3f); // (주목표)
    [SerializeField] private Color subObjectiveColor = new(0.95f, 0.85f, 0.25f); // (서브)

    private readonly HashSet<int> revealedSlots = new();

    private void Awake()
    {
        BriefingEntry entry = FindSelectedEntry();
        ApplyStaticContent(entry);

        goBackButton?.onClick.AddListener(() => SceneManager.LoadScene(missionSelectSceneName));
        startMissionButton?.onClick.AddListener(StartMission);

        if (entry != null)
        {
            StartCoroutine(PlayDialogue(entry));
            StartCoroutine(TypeText(missionInfoText, BuildMissionInfoText(entry)));
        }
    }

    // 미션정보 패널은 별도 텍스트를 새로 안 쓰고, 실제 미션의 목표 표시에 쓰이는
    // objective.stage{n}/substage{n}.main{i}/sub{i} 키를 그대로 재사용한다 - 이미 "(주목표)"/"(서브)"
    // 접두어가 붙은 실제 목표 문구라 브리핑과 인게임 목표가 항상 같은 내용으로 유지된다.
    private string BuildMissionInfoText(BriefingEntry entry)
    {
        string stagePrefix = entry.isSubMission ? $"substage{entry.missionNumber}" : $"stage{entry.missionNumber}";

        var lines = new List<string>();
        AppendObjectiveLines(lines, stagePrefix, "main");
        AppendObjectiveLines(lines, stagePrefix, "sub");
        return string.Join("\n", lines);
    }

    private void AppendObjectiveLines(List<string> lines, string stagePrefix, string kind)
    {
        Color color = kind == "main" ? mainObjectiveColor : subObjectiveColor;

        for (int i = 1; i <= 8; i++)
        {
            string key = $"objective.{stagePrefix}.{kind}{i}";
            string value = LocalizationManager.GetText(key);
            if (value == key) // 키가 없으면 GetText가 키 자체를 돌려준다 - 그 지점에서 목록이 끝난 것
                break;

            lines.Add(ColorizeBracketPrefix(value, color));
        }
    }

    // "(주목표) 내용" / "(Main) content"처럼 괄호로 감싼 접두어만 색칠한다. 언어별로 괄호 안 문구가
    // 달라도 첫 ')'까지만 잘라 태그를 씌우면 되므로 언어별 분기가 필요 없다.
    private string ColorizeBracketPrefix(string text, Color color)
    {
        int closeIndex = text.IndexOf(')');
        if (closeIndex < 0)
            return text;

        string prefix = text.Substring(0, closeIndex + 1);
        string rest = text.Substring(closeIndex + 1);
        return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{prefix}</color>{rest}";
    }

    private BriefingEntry FindSelectedEntry()
    {
        int missionNumber = BriefingSelection.MissionNumber;
        bool isSubMission = BriefingSelection.IsSubMission;

        BriefingEntry entry = briefingEntries.FirstOrDefault(e => e.missionNumber == missionNumber && e.isSubMission == isSubMission);
        if (entry == null)
            Debug.LogWarning($"BriefingEntry not found for mission {missionNumber} (sub={isSubMission})");

        return entry;
    }

    private void ApplyStaticContent(BriefingEntry entry)
    {
        SetPortraitAlpha(speakerPortraitImage, 0f);
        SetPortraitAlpha(speakerPortraitImage2, 0f);
        SetPortraitAlpha(speakerPortraitImage3, 0f);

        if (dialogueText != null) dialogueText.text = string.Empty;
        if (missionInfoText != null) missionInfoText.text = string.Empty;

        if (entry == null)
            return;

        if (speakerPortraitImage != null) speakerPortraitImage.sprite = FindPortrait(entry.speaker1Key);
        if (speakerPortraitImage2 != null) speakerPortraitImage2.sprite = FindPortrait(entry.speaker2Key);
        if (speakerPortraitImage3 != null) speakerPortraitImage3.sprite = FindPortrait(entry.speaker3Key);
        if (mapImage != null) mapImage.sprite = entry.mapImage;

        // 그 미션의 첫 화자는 대기 없이 바로 보이게 시작한다.
        if (entry.lines.Count > 0)
        {
            int firstSlot = entry.lines[0].speakerSlot;
            SetPortraitAlpha(GetSlotImage(firstSlot), 1f);
            revealedSlots.Add(firstSlot);
        }
    }

    private IEnumerator PlayDialogue(BriefingEntry entry)
    {
        if (dialogueText == null)
            yield break;

        var log = new StringBuilder();

        foreach (BriefingLine line in entry.lines)
        {
            RevealPortraitIfNeeded(line.speakerSlot);

            string characterKey = GetSpeakerCharacterKey(entry, line.speakerSlot);
            Color labelColor = FindCharacter(characterKey)?.labelColor ?? Color.white;
            string label = LocalizationManager.GetText(line.speakerLabelKey);
            string prefix = $"<color=#{ColorUtility.ToHtmlStringRGB(labelColor)}>{label}</color>: ";
            string content = LocalizationManager.GetText(line.textKey);
            string logSoFar = log.ToString();

            dialogueText.text = logSoFar + prefix;
            dialogueText.ForceMeshUpdate();
            int prefixVisibleEnd = dialogueText.textInfo.characterCount;

            dialogueText.text = logSoFar + prefix + content;
            dialogueText.ForceMeshUpdate();
            int lineVisibleEnd = dialogueText.textInfo.characterCount;

            dialogueText.maxVisibleCharacters = prefixVisibleEnd; // 이름은 즉시 표시
            ScrollToBottom();

            for (int visible = prefixVisibleEnd + 1; visible <= lineVisibleEnd; visible++)
            {
                dialogueText.maxVisibleCharacters = visible;
                ScrollToBottom();
                yield return new WaitForSeconds(1f / charsPerSecond);
            }

            log.Append(prefix).Append(content).Append("\n\n");
            yield return new WaitForSeconds(pauseBetweenLines);
        }

        // 마지막 대사 다음에 "브리핑 끝." 안내문을 타이핑하고, 다 끝나면 인물 이미지를 페이드아웃한다
        // (doc/0621). 텍스트 로그 자체는 지우지 않고 그대로 남겨둔다.
        string endingLogSoFar = log.ToString();
        string ending = LocalizationManager.GetText("briefing.end");

        dialogueText.text = endingLogSoFar;
        dialogueText.ForceMeshUpdate();
        int beforeEndingVisible = dialogueText.textInfo.characterCount;

        dialogueText.text = endingLogSoFar + ending;
        dialogueText.ForceMeshUpdate();
        int afterEndingVisible = dialogueText.textInfo.characterCount;

        for (int visible = beforeEndingVisible + 1; visible <= afterEndingVisible; visible++)
        {
            dialogueText.maxVisibleCharacters = visible;
            ScrollToBottom();
            yield return new WaitForSeconds(1f / charsPerSecond);
        }

        yield return new WaitForSeconds(pauseBetweenLines);
        FadeOutAllPortraits();
    }

    private IEnumerator TypeText(TextMeshProUGUI target, string content)
    {
        if (target == null)
            yield break;

        target.text = content;
        target.ForceMeshUpdate();
        int totalVisible = target.textInfo.characterCount;

        target.maxVisibleCharacters = 0;
        for (int visible = 1; visible <= totalVisible; visible++)
        {
            target.maxVisibleCharacters = visible;
            yield return new WaitForSeconds(1f / charsPerSecond);
        }
    }

    private void ScrollToBottom()
    {
        if (dialogueScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        dialogueScrollRect.verticalNormalizedPosition = 0f;
    }

    private void RevealPortraitIfNeeded(int slot)
    {
        if (!revealedSlots.Add(slot))
            return;

        Image image = GetSlotImage(slot);
        if (image != null)
            StartCoroutine(FadeTo(image, 1f, portraitFadeDuration));
    }

    private void FadeOutAllPortraits()
    {
        foreach (Image image in new[] { speakerPortraitImage, speakerPortraitImage2, speakerPortraitImage3 })
        {
            if (image != null)
                StartCoroutine(FadeTo(image, 0f, portraitFadeDuration));
        }
    }

    private IEnumerator FadeTo(Image image, float targetAlpha, float duration)
    {
        float startAlpha = image.color.a;
        float elapsed = 0f;
        Color color = image.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            image.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        image.color = color;
    }

    private void SetPortraitAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private Image GetSlotImage(int slot)
    {
        return slot switch
        {
            1 => speakerPortraitImage,
            2 => speakerPortraitImage2,
            3 => speakerPortraitImage3,
            _ => null,
        };
    }

    private string GetSpeakerCharacterKey(BriefingEntry entry, int slot)
    {
        return slot switch
        {
            1 => entry.speaker1Key,
            2 => entry.speaker2Key,
            3 => entry.speaker3Key,
            _ => null,
        };
    }

    private BriefingCharacter FindCharacter(string characterKey)
    {
        if (string.IsNullOrEmpty(characterKey))
            return null;

        return characterRoster.FirstOrDefault(c => c.characterKey == characterKey);
    }

    private Sprite FindPortrait(string characterKey) => FindCharacter(characterKey)?.portrait;

    private void StartMission()
    {
        if (!string.IsNullOrEmpty(BriefingSelection.TargetSceneName))
            SceneManager.LoadScene(BriefingSelection.TargetSceneName);
    }
}
