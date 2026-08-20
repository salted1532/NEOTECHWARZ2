# 0635 - 승리 화면 "다음 스테이지"가 브리핑룸을 건너뜀 (제안)

## 요청
0~5 본편 스테이지 승리 화면에서 "다음 스테이지"를 누르면 브리핑룸을 거치지 않고 바로 다음 스테이지가 시작됨 - 브리핑룸을 거치도록 수정.

## 원인
`VictoryPanelController.OnNextStageClicked()`(`Assets/Scripts/UI/VictoryPanelController.cs:84`)가 `SceneManager.LoadScene(nextStageSceneName)`으로 다음 스테이지 씬을 곧장 로드한다. `nextStageSceneName`은 미션 씬 이름 자체(예: "Mission1")를 인스펙터에 직접 넣어둔 값.

반면 `MissionSelectManager.LoadMission()`(`Assets/Scripts/UI/MissionSelectManager.cs:109`)은 미션 씬으로 바로 가지 않고, `BriefingSelection` static 필드(`MissionNumber`/`IsSubMission`/`TargetSceneName`)에 목적지를 담아둔 뒤 `Briefing_Room` 씬을 로드한다. `Briefing_Room` 씬의 `BriefingRoomController.StartMission()`이 브리핑이 끝나고 "미션 시작" 버튼을 누르면 그제서야 `BriefingSelection.TargetSceneName`으로 이동한다(doc/0616).

`VictoryPanelController`는 이 중간 단계 없이 씬을 직접 로드하도록 짜여있어 브리핑룸이 통째로 스킵됨.

## 제안 설계
`VictoryPanelController.cs`:
1. `nextStageSceneName` 인스펙터 필드를 (지금처럼 다음 스테이지의 미션 씬 이름을 그대로 담아두되) `BriefingSelection.TargetSceneName`으로 넘겨주는 용도로 재사용.
2. `briefingRoomSceneName` 필드 추가 (`MissionSelectManager`와 동일 컨벤션, 기본값 `"Briefing_Room"`).
3. `OnNextStageClicked()`를 아래로 교체:
```csharp
private void OnNextStageClicked()
{
    if (string.IsNullOrEmpty(nextStageSceneName))
        return;

    Time.timeScale = 1f;
    UserControl.IsPaused = false;

    BriefingSelection.MissionNumber = missionNumber + 1;
    BriefingSelection.IsSubMission = false; // 본편 승리 → 항상 다음 "본편" 스테이지 브리핑
    BriefingSelection.TargetSceneName = nextStageSceneName;
    SceneManager.LoadScene(briefingRoomSceneName);
}
```
- `missionNumber`는 이미 있는 필드(이 씬이 몇 번 미션인지) 그대로 사용 - 다음 미션 번호는 `missionNumber + 1`.
- `isSubMission`은 항상 `false`: 승리 화면의 "다음 스테이지"는 본편 0→1→2...로만 이어지고, 서브미션 브리핑으로는 가지 않음(서브미션은 MissionSelect에서 별도 진입).
- 씬 인스펙터에서 `nextStageSceneName`을 바꿀 필요 없음 - 값이 여전히 다음 미션 씬 이름이라 `BriefingSelection.TargetSceneName`에 그대로 넘기면 됨.

## 범위 밖
- 미션 5(마지막 본편) 승리 화면의 "다음 스테이지" 버튼 자체 존재 여부/엔딩 처리 - 별도 확인 필요하면 후속으로.
- 서브미션 승리 후 흐름 - 요청이 "0~5 메인 스테이지"로 한정.

## 구현 완료
- `VictoryPanelController.cs`: `briefingRoomSceneName` 필드 추가(기본값 `"Briefing_Room"`). `OnNextStageClicked()`을 제안대로 교체 - `BriefingSelection`에 다음 미션 정보를 채운 뒤 `Briefing_Room` 씬 로드. 컴파일 성공(에러 0, 경고 0).
- Mission0~5 씬의 `VictoryPanelController` 확인: `missionNumber`(0~5)와 `nextStageSceneName`(Mission1~5, Mission5는 MissionSelect)이 이미 올바르게 대응됨. 씬 파일 자체는 수정하지 않음 - 새 `briefingRoomSceneName` 필드는 인스펙터에 없어도 코드 기본값 `"Briefing_Room"`이 그대로 적용됨.
- Mission5는 `nextStageButton`이 애초에 비어있어(`fileID: 0`) `OnNextStageClicked()`가 걸리지 않음 - 마지막 본편 스테이지라 "다음 스테이지" 버튼 자체가 연결 안 된 상태였고, 이번 수정과 무관하게 영향 없음. Mission0~4는 버튼이 정상 연결되어 있어 수정이 그대로 적용됨.

## 상태
완료. Mission0~4 승리 화면의 "다음 스테이지" 버튼이 이제 다음 미션 씬으로 바로 가지 않고 `Briefing_Room`을 거친 뒤 미션이 시작됨.
