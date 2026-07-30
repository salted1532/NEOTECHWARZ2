# 0313. TestScene 옵션 패널 + 메인화면 나가기 컨트롤러 (제안)

날짜: 2026-07-30

## 요청 내용

> 이제 옵션패널에 사운드 매니저에서 조절 가능한 마스터,bgm,sfx,voice 슬라이더를 만들었어 이걸
> 연결할수 있도록 soundmananger 스크립트 수정해줘 이걸 메인화면에도 soundmanager가 있을건데 여기
> 씬에서 조정된 내용이 testscene에도 적용 되었으면 좋겠고 반대로도 작동해야해 testscene에도
> 옵션패널 하나 만들어서 거기서 사운드 설정하고 메인화면으로 나갈수 있도록 하려고

## 조사 내용 - SoundManager는 수정 불필요

- `Assets/Scripts/Audio/SoundManager.cs`에 이미 `SetMasterVolume`/`SetBGMVolume`/`SetSFXVolume`/
  `SetVoiceVolume`(+`Get`) 공개 메서드가 다 있고, 슬라이더 4개(+토글 3개, 옵션)를 이 메서드들에 연결하는
  `Assets/Scripts/UI/SoundSettingsPanel.cs`도 이미 존재함(`doc/0255`). 새로 만든 옵션 패널에 이
  컴포넌트를 붙이고 슬라이더 4개를 인스펙터에 연결하면 끝 - 토글은 안 만들었다면 그 필드는 비워둬도
  전부 `?.` null 체크가 되어 있어서 안전함.
- **씬 간 양방향 동기화도 이미 동작함**: `SetXxxVolume()` 호출마다 즉시 `PlayerPrefs`(디스크)에 저장되고,
  `SoundManager.Awake()`가 씬이 새로 로드될 때마다 `LoadVolumePrefs()`로 그 값을 다시 읽어옴. 즉
  MainScene에서 조정 → TestScene 이동 시 자동 반영, 반대 방향도 동일 - 코드 변경 없이 씬마다
  SoundManager를 하나씩 배치하기만 하면 됨.
- `MainScene.unity`/`TestScene.unity`/`SampleScene.unity` 전부 확인했지만 어느 씬에도 아직
  `SoundManager`가 배치되어 있지 않음 - 두 씬에 하나씩 배치하는 건 사람이 직접 할 씬 작업.
- `TestScene.unity`에는 아직 Canvas/UI 자체가 없음(유닛 배치만 있는 테스트 씬) - 옵션 버튼/패널은
  아직 안 만들어진 상태. 이번 요청은 그 패널을 열고 닫고, "메인화면으로 나가기"를 처리할 스크립트를
  미리 준비해두는 것.
- `MainMenuController.cs`(`doc/0309`~`0312`)의 컨벤션을 그대로 따름: 버튼은
  `[SerializeField] private Button` 참조 + `Awake()`에서 `onClick.AddListener(...)`로 스크립트가
  직접 연결.

## 제안 코드 (신규 파일)

### `Assets/Scripts/UI/TestSceneMenuController.cs` (신규)

TestScene에 Canvas/옵션 버튼/옵션 패널을 만든 뒤, 이 스크립트를 Canvas(또는 항상 켜져 있는
오브젝트)에 붙이고 필드를 연결.

```csharp
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
```

## 요약

- `SoundManager.cs`는 수정하지 않음 (이미 필요한 API + 씬 간 자동 동기화가 다 구현되어 있음).
- 새 스크립트 `TestSceneMenuController.cs`가 TestScene의 옵션 패널 열기/닫기와 "메인화면으로
  나가기"(`MainScene`으로 `LoadScene`)를 처리.
- 사운드 슬라이더 연결은 기존 `SoundSettingsPanel.cs`를 옵션 패널에 붙이고 슬라이더 4개를
  연결하면 됨(신규 작업 아님).

## 필요한 씬 작업 (코드 외)

1. `MainScene`, `TestScene` 각각에 `SoundManager` 배치(`bgmSource` 등 인스펙터 필드 연결).
2. 양쪽 씬의 옵션 패널에 `SoundSettingsPanel` 컴포넌트를 붙이고 슬라이더 4개 연결.
3. `TestScene`에 Canvas + 옵션 버튼 + 옵션 패널(닫기 X버튼 포함) + "메인화면으로 나가기" 버튼을
   만들고, `TestSceneMenuController`를 붙여서 4개 필드 연결.

## 영향받는 파일

- `Assets/Scripts/UI/TestSceneMenuController.cs` (신규)

## 다음 단계

이대로 생성해도 될지 확인 부탁드립니다.
