# 0332. 승리 패널에 "다음 스테이지" 버튼 추가 (일단 SampleScene 연결)

**날짜:** 2026-07-31

## 요청

> 주목표 완려시 승리화면에서 다음 스테이지 버튼도 추가해주고 일단은 SampleScene으로 연결되도록 해줘

## 조사 내용

- `Assets/Scripts/UI/VictoryPanelController.cs`([[0324]])가 이미 `mainMenuButton`(메인화면으로) 버튼
  하나만 처리하고 있었음 — 같은 패턴으로 버튼 하나만 더 추가하면 됨.
- 씬(`TestScene.unity`, `GameManager` 프리팹 인스턴스의 `Canvas/VictoryPanel` 아래)에 이미
  `GoToNextStage`라는 버튼 오브젝트가 만들어져 있었지만(레이아웃만 있고) 아무 스크립트에도 연결돼
  있지 않았음 — 새로 만들 필요 없이 그대로 연결.
- `SampleScene`은 이미 `Assets/Scenes/SampleScene.unity`로 존재하고 Build Settings에도 등록돼 있어
  `SceneManager.LoadScene("SampleScene")`이 바로 동작함.

## 적용한 변경

### `Assets/Scripts/UI/VictoryPanelController.cs`
- `[SerializeField] private Button nextStageButton;` / `[SerializeField] private string nextStageSceneName = "SampleScene";` 추가
- `Awake()`에서 `nextStageButton?.onClick.AddListener(OnNextStageClicked);` 등록
- `OnNextStageClicked() => SceneManager.LoadScene(nextStageSceneName);` 추가 (`OnMainMenuClicked`와 동일한 패턴)

### `Assets/Scenes/TestScene.unity`
- `StageObject`의 `VictoryPanelController` 컴포넌트에 `nextStageButton`을 기존 `GoToNextStage` 버튼에,
  `nextStageSceneName`을 `SampleScene`으로 연결 (기존 `mainMenuButton`이 `GameManager` 프리팹 내부의
  `BackToMainMenu` 버튼을 참조하던 것과 동일한 "stripped 컴포넌트 참조" 방식으로 새 항목 추가).

`npx uloop-cli compile`/`get-logs`로 에러 0개 확인. `find-game-objects`로 `Next Stage Button` →
`GoToNextStage`, `Next Stage Scene Name` → `SampleScene` 연결도 재확인.

## 결과

주목표를 모두 완료해 승리 패널이 뜨면 "다음 스테이지" 버튼을 눌러 `SampleScene`으로 이동할 수 있음.
실제 다음 스테이지 씬이 만들어지면 `nextStageSceneName`만 인스펙터에서 바꾸면 됨(코드 수정 불필요).

## 영향받는 파일

- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scenes/TestScene.unity`
