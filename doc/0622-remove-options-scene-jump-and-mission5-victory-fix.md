# 0622 - 옵션 패널 씬 이동 기능 제거 + 미션5 승리화면 다음씬 수정

## 배경
사용자가 두 가지를 계획 중이라 언급, 직접 처리 가능한지 확인 요청:
1. 옵션 패널에서 다른 씬으로 이동하는 기능 제거
2. 승리화면 - 본편 미션은 다음 미션으로, 서브미션은 미션선택 화면으로

## 조사 결과
- **옵션 패널 씬 이동**: `SceneMenuController.cs`(모든 미션 씬이 공유하는 `Assets/prefabs/Game/GameManager.prefab`에 부착)에 `nextStageButton`/`previousStageButton`과 `nextStageSceneName`/`previousStageSceneName`이 있었음. 프리팹 기본값은 비어있고 일부 씬(Mission0 등)에서만 `nextStageSceneName: Mission1` 식으로 오버라이드됨.
- **승리화면 분기**: `VictoryPanelController.cs`가 씬마다 `nextStageSceneName`을 인스펙터로 따로 지정하는 구조라, 실제 씬 데이터를 확인해보니 **서브미션(Sub_Mission1~4)은 이미 전부 `MissionSelect`로 설정되어 있어 추가 작업 불필요**. 본편은 Mission0→1→2→3→4→5로 체인되어 있었는데 Mission5(마지막)만 `Mission0`으로 돌아가게 되어 있었음 - 사용자 확인 결과 임시값이었음.

## 사용자 결정
- Mission5의 "다음 스테이지" → `MissionSelect`로 이동.
- 옵션 패널의 "메인화면으로 나가기"(mainMenuButton)는 유지.

## 구현
- `SceneMenuController.cs`: `nextStageButton`/`previousStageButton`, `nextStageSceneName`/`previousStageSceneName` 필드와 `OnNextStageClicked`/`OnPreviousStageClicked` 메서드 제거. `optionButton`/`optionCloseButton`/`mainMenuButton`은 그대로 유지.
- `Assets/prefabs/Game/GameManager.prefab`: 옵션 패널의 "GoToNextStage"/"GoToPreviousStage" 버튼 오브젝트를 `PrefabUtility.EditPrefabContentsScope`로 직접 삭제 (프리팹 하나만 고치면 이 프리팹을 쓰는 모든 미션 씬에 반영됨).
- `Mission5.unity`: `VictoryPanelController.nextStageSceneName`을 `Mission0` → `MissionSelect`로 변경.

## 검증
컴파일 성공(에러 0). Play Mode로 Mission5에서 옵션 패널을 직접 열어(`SceneMenuController.OpenOptionsPanel()` 호출) 스크린샷 확인 - 사운드 슬라이더/해상도/뒤로가기/메인화면으로 돌아가기만 남고 씬 이동 버튼은 완전히 사라짐, 레이아웃 정상.

## 버그 정정 (doc/0627)
`GameManager.prefab`에서 이름("GoToNextStage")만으로 지운 탓에, 같은 이름을 쓰던 승리화면(`VictoryPanelController`)의 "다음 미션으로" 버튼까지 같이 삭제되는 사고가 있었음 - doc/0627에서 프리팹을 복원하고 부모 오브젝트("OptionPanel")로 정확히 골라 다시 제거해서 수정함. 이 문서의 "검증"에 적힌 스크린샷 확인은 옵션 패널만 봤을 뿐 승리화면까지 확인하지 않아 그 사고를 못 잡았음 - 다음부터는 이름이 겹칠 수 있는 UI 정리 작업 후 관련된 다른 화면도 같이 확인할 것.

## 상태
완료 (승리화면 버튼 삭제 사고는 doc/0627에서 수정).
