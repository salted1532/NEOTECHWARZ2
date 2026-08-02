# 0367. TestSceneMenuController에 "다음 스테이지" 버튼 인스펙터 필드 추가 (제안)

**날짜:** 2026-08-02

## 요청 내용
> test scene menu controller에서 다음 스테이지로 이동하는 버튼을 추가하려고해 인스펙터 필드를 만들어주고
> 내가 직접 연결할게 다음 스테이지는 SampleScene이야

## 현재 구조

`Assets/Scripts/UI/TestSceneMenuController.cs`에 이미 `mainMenuButton` → `mainSceneName`
("MainScene") → `SceneManager.LoadScene(mainSceneName)` 패턴이 있음(`OnMainMenuClicked()`).
"다음 스테이지" 버튼도 정확히 같은 구조로, 대상 씬만 `"SampleScene"`으로 하드코딩된 기본값을 갖는
새 필드를 추가하면 됨.

## 제안하는 변경

`Assets/Scripts/UI/TestSceneMenuController.cs`:
- `[Header("버튼 연결")]`에 `[SerializeField] private Button nextStageButton;` 추가.
- `[Header("씬 이동")]`에 `[SerializeField] private string nextStageSceneName = "SampleScene";` 추가.
- `Awake()`에 `nextStageButton?.onClick.AddListener(OnNextStageClicked);` 추가.
- `OnMainMenuClicked()` 바로 아래에 `private void OnNextStageClicked() => SceneManager.LoadScene(nextStageSceneName);` 추가.

버튼 자체(UI 오브젝트/인스펙터 연결)는 요청대로 직접 연결하실 것이므로 씬/프리팹 파일은 건드리지 않음
— 스크립트에 필드와 로직만 추가.

## 구현 (승인 후 적용됨)

**Before:**
```csharp
[Header("버튼 연결")]
[SerializeField] private Button optionButton;       // 옵션 패널 열기
[SerializeField] private Button optionCloseButton;   // 옵션 패널의 X(닫기) 버튼
[SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"

[Header("씬 이동")]
[SerializeField] private string mainSceneName = "MainScene";
...
private void Awake()
{
    optionButton?.onClick.AddListener(OpenOptionsPanel);
    optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
    mainMenuButton?.onClick.AddListener(OnMainMenuClicked);

    optionsPanel?.SetActive(false);
}
...
private void OnMainMenuClicked() => SceneManager.LoadScene(mainSceneName);
```

**After:**
```csharp
[Header("버튼 연결")]
[SerializeField] private Button optionButton;       // 옵션 패널 열기
[SerializeField] private Button optionCloseButton;   // 옵션 패널의 X(닫기) 버튼
[SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"
[SerializeField] private Button nextStageButton;     // "다음 스테이지로 이동"

[Header("씬 이동")]
[SerializeField] private string mainSceneName = "MainScene";
[SerializeField] private string nextStageSceneName = "SampleScene";
...
private void Awake()
{
    optionButton?.onClick.AddListener(OpenOptionsPanel);
    optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
    mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
    nextStageButton?.onClick.AddListener(OnNextStageClicked);

    optionsPanel?.SetActive(false);
}
...
private void OnMainMenuClicked() => SceneManager.LoadScene(mainSceneName);

private void OnNextStageClicked() => SceneManager.LoadScene(nextStageSceneName);
```

## 검증

- `npx uloop-cli compile`: 에러 0개(기존에도 있던 무관한 경고 33개만 남음).
- 버튼 자체는 사용자가 인스펙터에서 직접 연결 예정이므로 씬/프리팹 파일은 변경하지 않음.

## 영향받는 파일

- `Assets/Scripts/UI/TestSceneMenuController.cs`
