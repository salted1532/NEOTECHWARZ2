# 0643 - 옵션 패널에 "미션 재시작" 버튼 추가 (제안)

## 요청
옵션 패널의 "메인화면으로 나가기" 오른쪽(옛 "다음 스테이지" 버튼 자리)에 현재 미션을 재시작하는
버튼 추가. doc/0639(미션 선택 버튼)과 같은 방식으로. 번역도 진행.

## 조사
- doc/0622에서 제거되기 전 "다음 스테이지" 버튼(`GoToNextStage`, `OptionPanel` 직계 자식)은
  `anchorMin/Max (0.5,0)`, `anchoredPosition (300, 140)`, 크기 `200x100`이었음(git 이력 확인) —
  "메인화면으로 나가기"(0,140) 기준 오른쪽 대칭 위치. doc/0639에서 왼쪽에 만든 "미션 선택" 버튼
  (`-300, 140`)과 좌우 대칭.
- "미션 재시작"은 스테이지 이동(다음/이전 미션)과 달리 씬 이름을 지정할 필요 없이 **현재 활성 씬을
  다시 로드**하면 됨 — `SceneManager.LoadScene(SceneManager.GetActiveScene().name)`. 모든 미션
  씬에서 별도 인스펙터 설정 없이 그대로 동작.
- 로컬라이징 키 컨벤션에 맞춰 새 키 `ui.restartmission` 추가.

## 변경안

### 1) `Assets/Scripts/UI/SceneMenuController.cs`
```diff
     [SerializeField] private Button mainMenuButton;      // "메인화면으로 나가기"
     [SerializeField] private Button missionSelectButton; // "미션 선택 화면으로"
+    [SerializeField] private Button restartButton;       // "미션 재시작"
     ...
         mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
         missionSelectButton?.onClick.AddListener(OnMissionSelectClicked);
+        restartButton?.onClick.AddListener(OnRestartClicked);
     ...
     private void OnMissionSelectClicked()
     {
         Time.timeScale = 1f;
         UserControl.IsPaused = false;
         SceneManager.LoadScene(missionSelectSceneName);
     }
+
+    private void OnRestartClicked()
+    {
+        Time.timeScale = 1f;
+        UserControl.IsPaused = false;
+        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
+    }
```
(씬 이름 필드 불필요 - 현재 씬을 그대로 재로드)

### 2) `Assets/prefabs/Game/GameManager.prefab`
- `OptionPanel/BackToMainMenu`를 복제해 `OptionPanel/RestartMission` 생성 (doc/0639과 동일 방식,
  `PrefabUtility.EditPrefabContentsScope`).
- `anchoredPosition (300, 140)` (메인화면 버튼 오른쪽, 옛 "다음 스테이지" 자리).
- 자식 `LocalizedText.Key`를 `ui.backtomainmenu` → `ui.restartmission`으로 변경.
- `UIController.SceneMenuController.restartButton` 필드에 연결.

### 3) `ko.json`/`en.json`
```diff
     { "key": "ui.gotomissionselect", "value": "미션 선택 화면으로 돌아가기" },
+    { "key": "ui.restartmission", "value": "미션 재시작" },
```
```diff
     { "key": "ui.gotomissionselect", "value": "Back to Mission Select" },
+    { "key": "ui.restartmission", "value": "Restart Mission" },
```

## 상태
완료.

## 구현/검증
- `SceneMenuController.cs`: `restartButton` 필드, `OnRestartClicked()`(`SceneManager.LoadScene(SceneManager.GetActiveScene().name)`) 추가.
- `Assets/prefabs/Game/GameManager.prefab`: `OptionPanel/BackToMainMenu`를 복제해 `OptionPanel/RestartMission` 생성(`anchoredPosition (300, 140)`, `LocalizedText.key = ui.restartmission`), `UIController.SceneMenuController.restartButton`에 연결. `PrefabUtility.EditPrefabContentsScope`로 편집, 중복 생성 없음 확인(`grep -c "m_Name: RestartMission"` = 1).
- `ko.json`/`en.json`에 `ui.restartmission` 키 추가("미션 재시작" / "Restart Mission").
- 컴파일 통과(에러 0). `Mission0.unity`에서 Play Mode로 옵션 패널을 열어 스크린샷 확인 — 미션 선택/메인화면/미션 재시작 3버튼이 좌-중-우로 정상 배치, 한국어("미션 선택 화면으로 돌아가기" / "메인화면으로 돌아가기" / "미션 재시작")·영어("Back to Mission Select" / "Back to Main Menu" / "Restart Mission") 전환 모두 정상. 재시작 버튼 클릭 시 씬이 `Mission0`으로(즉 현재 씬 그대로) 재로드되는 것 확인.
