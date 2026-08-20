# 0639 - 옵션 패널에 "미션 선택 화면으로" 버튼 추가 (제안)

## 요청
게임플레이 씬 옵션 패널에 "메인화면으로 나가기" 밖에 없는데, 그 왼쪽(원래 doc/0622에서 제거된
"이전 스테이지" 버튼이 있던 자리)에 미션 선택 화면(`MissionSelect` 씬)으로 이동하는 버튼을 추가.
한글/영어 번역에 맞춰 텍스트도 전환되도록.

## 조사
- `Assets/Scripts/UI/SceneMenuController.cs` (`GameManager/UIController`에 부착)가 옵션 패널
  버튼들을 담당. 현재 `mainMenuButton` 하나만 있고, `OnMainMenuClicked()`가 `Time.timeScale`/
  `UserControl.IsPaused` 복구 후 `mainSceneName`("MainScene") 씬을 로드.
- `Assets/prefabs/Game/GameManager.prefab`의 `OptionPanel/BackToMainMenu` 버튼: 앵커 (0.5, 0),
  `anchoredPosition (0, 140)`, 크기 `200x100`, 스프라이트 `button1`. 자식 `Text (TMP)`에
  `LocalizedText`(Key `ui.backtomainmenu`) 부착.
- doc/0622에서 제거되기 전 "이전 스테이지" 버튼(`GoToPreviousStage`)은 같은 부모(`OptionPanel`),
  같은 앵커, `anchoredPosition (-300, 140)`, 크기 `200x100`이었음(git 이력 확인) — 요청하신
  "그 왼쪽, 원래 이전 스테이지 버튼 자리"가 정확히 이 좌표.
- `MissionSelect` 씬 이름은 `BriefingRoomController.missionSelectSceneName`("MissionSelect")과
  동일하게 이미 프로젝트 전역에서 쓰는 값.
- 로컬라이징 키 컨벤션(`Docs/LocalizedText.md`/기존 `ui.backtomainmenu`/`ui.gotonextstage`/
  `ui.gotopreviousstage`)을 따라 새 키 `ui.gotomissionselect` 추가.

## 변경안

### 1) `Assets/Scripts/UI/SceneMenuController.cs`
```diff
     [Header("버튼 연결")]
     [SerializeField] private Button optionButton;       // 옵션 패널 열기
     [SerializeField] private Button optionCloseButton;   // 옵션 패널의 X(닫기) 버튼
     [SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"
+    [SerializeField] private Button missionSelectButton; // "미션 선택 화면으로"

     [Header("씬 이동")]
     [SerializeField] private string mainSceneName = "MainScene";
+    [SerializeField] private string missionSelectSceneName = "MissionSelect";

     private void Awake()
     {
         optionButton?.onClick.AddListener(OpenOptionsPanel);
         optionCloseButton?.onClick.AddListener(CloseOptionsPanel);
         mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
+        missionSelectButton?.onClick.AddListener(OnMissionSelectClicked);

         optionsPanel?.SetActive(false);
     }
     ...
     private void OnMainMenuClicked()
     {
         Time.timeScale = 1f;
         UserControl.IsPaused = false;
         SceneManager.LoadScene(mainSceneName);
     }
+
+    private void OnMissionSelectClicked()
+    {
+        Time.timeScale = 1f;
+        UserControl.IsPaused = false;
+        SceneManager.LoadScene(missionSelectSceneName);
+    }
```

### 2) `Assets/prefabs/Game/GameManager.prefab` (Unity 에디터 API로 편집, doc/0622와 동일한 방식)
- `OptionPanel/BackToMainMenu`를 복제해 `OptionPanel/GoToMissionSelect` 생성.
- `RectTransform.anchoredPosition`을 `(-300, 140)`로 변경(나머지 앵커/크기는 동일, `BackToMainMenu`
  왼쪽 300픽셀 — 옛 "이전 스테이지" 버튼 자리와 동일).
- 자식 `Text (TMP)`의 `LocalizedText.Key`를 `ui.backtomainmenu` → `ui.gotomissionselect`로 변경.
- `UIController` 오브젝트의 `SceneMenuController.missionSelectButton` 필드에 새 버튼 연결.

### 3) `Assets/Resources/Localization/ko.json` / `en.json`
```diff
     { "key": "ui.gotonextstage", "value": "다음 미션으로" },
     { "key": "ui.gotopreviousstage", "value": "이전 미션으로" },
     { "key": "ui.backtomainmenu", "value": "메인화면으로 돌아가기" },
+    { "key": "ui.gotomissionselect", "value": "미션 선택 화면으로 돌아가기" },
```
```diff
     { "key": "ui.gotonextstage", "value": "Next Mission" },
     { "key": "ui.gotopreviousstage", "value": "Previous Mission" },
     { "key": "ui.backtomainmenu", "value": "Back to Main Menu" },
+    { "key": "ui.gotomissionselect", "value": "Back to Mission Select" },
```

## 상태
완료. 사용자가 "메인화면으로 돌아가기 버튼을 복사해서 같은 버튼으로 만들어달라"고 확인해 위 안(복제
방식) 그대로 진행.

## 구현/검증
- `SceneMenuController.cs`: `missionSelectButton`/`missionSelectSceneName` 필드, `OnMissionSelectClicked()` 추가.
- `Assets/prefabs/Game/GameManager.prefab`: `OptionPanel/BackToMainMenu`를 복제해 `OptionPanel/GoToMissionSelect` 생성(`anchoredPosition (-300, 140)`, `LocalizedText.key = ui.gotomissionselect`), `UIController.SceneMenuController.missionSelectButton`에 연결. `PrefabUtility.EditPrefabContentsScope`로 편집(doc/0622와 동일 방식).
- `ko.json`/`en.json`에 `ui.gotomissionselect` 키 추가("미션 선택 화면으로 돌아가기" / "Back to Mission Select").
- 컴파일 통과(에러 0). `Mission0.unity`에서 Play Mode로 옵션 패널을 직접 열어(`SceneMenuController.OpenOptionsPanel()`) 스크린샷 확인 — "미션 선택 화면으로" 버튼이 "메인화면으로 나가기" 버튼 왼쪽(옛 "이전 스테이지" 버튼 자리)에 정상 배치, 한국어/영어 전환 시 텍스트 모두 정상 전환. 버튼 클릭 시 `MissionSelect` 씬으로 정상 이동 확인.
- 검증 중 발견(이번 작업과 무관, 범위 밖): `Assets/Scenes/SampleScene.unity`(Unity 기본 생성 테스트용 씬으로 추정, 실제 미션 흐름에 쓰이지 않음)의 `GameManager` 프리팹 인스턴스에서 `BackToMainMenu` 버튼의 `anchoredPosition`이 프리팹 기본값(0, 140)이 아닌 (-300, -300)으로 저장돼 있어 화면 하단(미니맵 쪽)에 가려짐 — 실제 미션 씬(Mission0 등)에는 이 문제 없음. 이번 요청과 무관한 기존 오버라이드로 보여 손대지 않았음.
