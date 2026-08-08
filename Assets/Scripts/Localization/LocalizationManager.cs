using System.Collections.Generic;
using UnityEngine;

// 현재 언어(PlayerPrefs "Language", 기본 "en")에 맞는 JSON을 Resources/Localization에서 읽어와
// 텍스트 조회를 제공하는 싱글턴. SoundManager/TooltipUI와 동일한 패턴 - DontDestroyOnLoad 없이
// PlayerPrefs로 씬을 넘어가도 선택한 언어가 이어진다 (doc/0481).
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    private const string LanguagePrefsKey = "Language";
    private const string DefaultLanguage = "en";

    // static인 이유: 인스턴스 이벤트로 두면 LocalizedText.OnEnable이 이 매니저의 Awake보다 먼저 실행될
    // 경우(씬의 다른 오브젝트와의 Awake/OnEnable 순서는 보장되지 않음) Instance가 아직 null이라
    // 구독 자체가 조용히 스킵되고, 그 뒤로는 오브젝트를 껐다 켜서 OnEnable을 다시 태우기 전까진 언어
    // 변경 버튼을 눌러도 갱신되지 않는 버그가 있었다(doc/0485). static 이벤트는 Instance 존재 여부와
    // 무관하게 항상 구독에 성공한다.
    public static event System.Action OnLanguageChanged;

    private readonly Dictionary<string, string> strings = new Dictionary<string, string>();

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    private void Awake()
    {
        Instance = this;
        LoadLanguage(PlayerPrefs.GetString(LanguagePrefsKey, DefaultLanguage));
    }

    // 메인화면 언어 전환 버튼(KR/EN)의 OnClick()에 인스펙터에서 바로 연결한다.
    public void SetLanguage(string languageCode)
    {
        if (languageCode == CurrentLanguage)
            return;

        LoadLanguage(languageCode);
        PlayerPrefs.SetString(LanguagePrefsKey, languageCode);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke();
    }

    // JsonUtility는 Dictionary를 직접 못 읽어서 {"entries":[{key,value}]} 리스트 래퍼로 감싸서 읽는다
    // (Newtonsoft 같은 별도 JSON 패키지가 프로젝트에 없음).
    private void LoadLanguage(string languageCode)
    {
        strings.Clear();
        CurrentLanguage = languageCode;

        TextAsset json = Resources.Load<TextAsset>($"Localization/{languageCode}");
        if (json == null)
        {
            Debug.LogWarning($"Localization file not found: {languageCode}");
            return;
        }

        LocalizationFile file = JsonUtility.FromJson<LocalizationFile>(json.text);
        if (file?.entries == null)
            return;

        foreach (LocalizationEntry entry in file.entries)
            strings[entry.key] = entry.value;
    }

    // 키가 없으면 키 자체를 그대로 보여준다 - 번역 누락을 화면에서 바로 알아챌 수 있게.
    public string Get(string key) => strings.TryGetValue(key, out string value) ? value : key;
    public string Get(string key, params object[] args) => string.Format(Get(key), args);

    // Instance가 아직 없어도(씬에 매니저가 없는 테스트 등) 매 호출부에서 null 체크를 반복하지 않도록 하는
    // 정적 패스스루 - 이 경우에도 키 자체를 보여줘서 번역 누락과 동일하게 눈에 띈다.
    public static string GetText(string key) => Instance != null ? Instance.Get(key) : key;
    public static string GetText(string key, params object[] args) => Instance != null ? Instance.Get(key, args) : key;

    // 유닛/건물 ScriptableObject 이름·설명처럼, 키가 없거나 매니저가 없거나 조회 중 예외가 나더라도
    // "키 문자열"이 아니라 원래 SO에 적혀있던 원문을 그대로 보여줘야 하는 곳에서 쓴다 (doc/0487).
    public string GetOrFallback(string key, string fallback) => strings.TryGetValue(key, out string value) ? value : fallback;

    public static string GetTextOrFallback(string key, string fallback)
    {
        try
        {
            return Instance != null ? Instance.GetOrFallback(key, fallback) : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}

[System.Serializable]
public class LocalizationEntry
{
    public string key;
    public string value;
}

[System.Serializable]
public class LocalizationFile
{
    public List<LocalizationEntry> entries;
}
