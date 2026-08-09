using System.Collections.Generic;
using UnityEngine;
using TMPro;

// 해상도 설정 드롭다운 - OptionPanel.prefab의 "Screen Resolution" TMP_Dropdown에 연결된다
// (doc/0501). Screen.resolutions로 실제 지원 해상도를 채우고, 선택 시 Screen.SetResolution +
// PlayerPrefs 저장. OptionPanel이 쓰이는 모든 씬(MainScene, GameManager.prefab을 통한 인게임)에서
// 자동으로 동작한다.
public class GraphicsSettingsPanel : MonoBehaviour
{
    private const string PrefWidth = "Graphics_ResolutionWidth";
    private const string PrefHeight = "Graphics_ResolutionHeight";

    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private readonly List<Resolution> options = new();

    private void Awake()
    {
        // 씬이 로드될 때 이전에 저장한 해상도가 있고 아직 적용 전이라면 맞춘다.
        if (PlayerPrefs.HasKey(PrefWidth) && PlayerPrefs.HasKey(PrefHeight))
        {
            int width = PlayerPrefs.GetInt(PrefWidth);
            int height = PlayerPrefs.GetInt(PrefHeight);
            if (width != Screen.width || height != Screen.height)
                Screen.SetResolution(width, height, Screen.fullScreenMode);
        }
    }

    private void Start()
    {
        BuildOptions();
        resolutionDropdown?.onValueChanged.AddListener(OnResolutionSelected);
    }

    // 패널이 다시 열릴 때마다(예: ESC 메뉴 재진입) 현재 해상도로 선택값을 다시 맞춘다.
    private void OnEnable()
    {
        RefreshSelectedIndex();
    }

    // Screen.resolutions는 같은 가로x세로에 주사율만 다른 항목을 중복으로 줄 수 있어 가로x세로 기준으로 합친다.
    private void BuildOptions()
    {
        var seen = new Dictionary<(int w, int h), Resolution>();
        foreach (var r in Screen.resolutions)
            seen[(r.width, r.height)] = r;

        options.Clear();
        options.AddRange(seen.Values);
        options.Sort((a, b) => (b.width * b.height) - (a.width * a.height)); // 높은 해상도부터

        var labels = new List<string>(options.Count);
        foreach (var r in options)
            labels.Add($"{r.width} x {r.height}");

        resolutionDropdown?.ClearOptions();
        resolutionDropdown?.AddOptions(labels);
        RefreshSelectedIndex();
    }

    // 씬이 막 로드된 시점엔 에디터에서 Screen.width/height가 아직 640x480(Game View 렌더 전 기본값)을
    // 반환할 수 있어(doc/0502 후속), 저장된 값이 있으면 그쪽을 우선한다. Screen.width/height는 아직
    // 한 번도 선택/저장한 적 없을 때만 폴백으로 쓴다.
    private void RefreshSelectedIndex()
    {
        if (resolutionDropdown == null) return;

        int targetWidth = PlayerPrefs.HasKey(PrefWidth) ? PlayerPrefs.GetInt(PrefWidth) : Screen.width;
        int targetHeight = PlayerPrefs.HasKey(PrefHeight) ? PlayerPrefs.GetInt(PrefHeight) : Screen.height;

        int index = options.FindIndex(r => r.width == targetWidth && r.height == targetHeight);
        if (index >= 0)
            resolutionDropdown.SetValueWithoutNotify(index);
    }

    private void OnResolutionSelected(int index)
    {
        if (index < 0 || index >= options.Count) return;

        var r = options[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode);
        PlayerPrefs.SetInt(PrefWidth, r.width);
        PlayerPrefs.SetInt(PrefHeight, r.height);
        PlayerPrefs.Save();
    }
}
