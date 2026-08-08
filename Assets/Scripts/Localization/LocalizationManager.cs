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

    // 언어가 바뀌면 이미 떠 있는 정적 라벨(LocalizedText)이 다시 그릴 수 있도록 발행한다.
    public event System.Action OnLanguageChanged;

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
