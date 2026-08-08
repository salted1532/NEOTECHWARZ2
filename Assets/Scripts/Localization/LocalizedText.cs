using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 스크립트가 텍스트를 직접 갱신하지 않는 정적 UI 라벨(버튼 캡션, 패널 헤더 등)에 붙여서 현재 언어로
// 표시한다. LocalizationManager.OnLanguageChanged를 구독해 언어가 바뀌면 즉시 다시 그린다 (doc/0481).
// TextMeshProUGUI/레거시 UI.Text 둘 다 지원 - OptionPanel처럼 레거시 Text를 쓰는 라벨도 있어서(doc/0482).
//
// OptionPanel 설정 라벨 키 컨벤션: "settings.<카테고리>.<항목>" (예: 오디오 설정은
// settings.audio.bgm/voice/master/sfx). 나중에 그래픽/게임 설정 같은 새 카테고리를 추가할 땐
// settings.graphics.*, settings.game.* 식으로 이 패턴만 따르면 됨 - 새 라벨 오브젝트에 이 컴포넌트를
// 붙이고 key만 채운 뒤, en.json/ko.json에 같은 키로 값 한 줄씩 추가하면 끝 (doc/0483, 코드 수정 불필요).
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI target;
    [SerializeField] private Text legacyTarget;
    [SerializeField] private string key;

    private void Reset()
    {
        target = GetComponent<TextMeshProUGUI>();
        if (target == null)
            legacyTarget = GetComponent<Text>();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Apply;
        Apply();
    }

    // 씬 로드 시점의 최초 적용은 Start()에서 한 번 더 한다 - OnEnable은 씬의 다른 오브젝트보다 먼저
    // 실행될 수 있어(Awake/OnEnable 순서 불보장) LocalizationManager.Instance가 아직 null인 채로
    // Apply()가 조용히 아무 것도 안 하고 끝날 수 있다. Start는 씬의 모든 Awake가 끝난 뒤에 호출되는
    // 게 보장되므로 이 시점엔 Instance가 항상 준비돼 있다(doc/0485 - 이게 없으면 씬을 다시 불러올 때
    // 저장된 언어 대신 에디터에 박혀있던 원본 텍스트가 그대로 보이는 버그가 있었다).
    private void Start()
    {
        Apply();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= Apply;
    }

    private void Apply()
    {
        if (LocalizationManager.Instance == null)
            return;

        string text = LocalizationManager.Instance.Get(key);

        if (target != null)
            target.text = text;
        if (legacyTarget != null)
            legacyTarget.text = text;
    }
}
