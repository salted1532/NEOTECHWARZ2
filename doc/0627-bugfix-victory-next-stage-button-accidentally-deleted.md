# 0627 - 버그수정: 승리화면 "다음 스테이지" 버튼이 옵션 패널 정리 때 같이 삭제됨

## 문제
doc/0622에서 옵션 패널의 씬 이동 기능(`SceneMenuController.nextStageButton`/`previousStageButton`)을 제거하면서, `Assets/prefabs/Game/GameManager.prefab`에서 이름이 "GoToNextStage"/"GoToPreviousStage"인 오브젝트를 이름만으로 찾아 전부 삭제했음. 그런데 이 프리팹 안에는 **이름이 같은 "GoToNextStage" 오브젝트가 두 개** 있었음:
- `OptionPanel`의 자식 - `SceneMenuController.nextStageButton`용 (제거 대상 맞음)
- `VictoryPanel`의 자식 - `VictoryPanelController.nextStageButton`용 (전혀 다른 버튼, 승리화면의 "다음 미션으로" 버튼)

이름 매칭만으로 지운 탓에 승리화면 버튼까지 같이 삭제됨 - 사용자가 승리화면에서 버튼이 사라진 걸 확인하고 복구 요청.

## 조사
- `git diff`로 삭제된 오브젝트 목록을 확인하던 중 "SkillSelect"(전혀 무관한 커맨드패널 스킬선택 UI)도 삭제된 것처럼 보였으나, 이건 `PrefabUtility.EditPrefabContentsScope` 저장 시 프리팹 파일이 재직렬화되며 생기는 diff 노이즈였고 실제로는 삭제되지 않았음(fileID 그대로 존재, `UIController.skillSelectPanel` 참조도 정상) - 오탐으로 확인.
- Mission0.unity에서 `VictoryPanelController.nextStageButton: {fileID: 373655230}`가 `stripped` MonoBehaviour로, `m_CorrespondingSourceObject`가 GameManager.prefab의 fileID 3248576223042666206(=VictoryPanel 밑 GoToNextStage의 Button 컴포넌트)를 가리키는 것으로 확정 - 승리화면 버튼이 실제로 GameManager.prefab 안에 있었다는 것과, 그게 지워진 게 원인임을 확인.

## 수정
1. `git checkout -- Assets/prefabs/Game/GameManager.prefab`로 프리팹을 doc/0622 이전 상태(HEAD)로 복원.
2. 이번엔 이름이 아니라 **부모 오브젝트 이름("OptionPanel")** 으로 정확히 걸러서 그 두 버튼만 삭제 (`t.parent.name == "OptionPanel"` 조건 추가) - VictoryPanel 쪽은 그대로 보존됨.
3. `git diff`로 이번엔 정확히 옵션 패널 버튼 2개(+그 라벨 텍스트)만 삭제됐고 다른 오브젝트는 안 건드렸음을 재확인.

## 검증
컴파일 성공. Mission0을 Play Mode로 실행 후 `StageManager`/`VictoryPanelController`에 리플렉션으로 승리를 강제 트리거해 승리화면을 스크린샷으로 확인 - "메인화면으로 돌아가기" / "돌아가기" / "다음 미션으로" 3버튼 모두 정상 표시. 각 미션 씬의 `nextStageSceneName` 데이터(본편 Mission0→1→2→3→4→5→MissionSelect, 서브미션 전부→MissionSelect)는 애초에 씬 파일 자체에 저장된 값이라 이번 사고와 무관하게 그대로 유지되어 있었음 - 확인 완료.

## 교훈
Unity 프리팹/씬에서 이름으로 오브젝트를 찾아 지울 때, 이름이 겹치는 다른 UI가 있을 수 있으므로 어떤 스크립트의 어떤 필드가 그 오브젝트를 참조하는지(또는 부모 컨테이너가 무엇인지)까지 같이 확인하고 지워야 함 - `[[remove_options_scene_jump]]` 류 작업은 이름 매칭 단독으로 하지 말 것.

## 상태
완료.
