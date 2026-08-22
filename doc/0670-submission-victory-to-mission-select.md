# 0670 - 서브미션 클리어 후 브리핑룸 대신 미션 선택 화면으로

## 요청
> 현재 서브 미션들이 승리시 미션 선택이 아니라 브리핑룸으로 연결된거 같아 서브미션들은 클리어
> 이후 미션선택창으로 이동하도록 해줘

## 원인
`VictoryPanelController.OnNextStageClicked()`(doc/0635에서 "다음 스테이지도 브리핑룸을 거치게"로
바뀜)가 `nextStageSceneName` 값과 무관하게 항상 `Briefing_Room`부터 로드한다:
```csharp
private void OnNextStageClicked()
{
    ...
    BriefingSelection.MissionNumber = missionNumber + 1;
    BriefingSelection.IsSubMission = false;
    BriefingSelection.TargetSceneName = nextStageSceneName;
    SceneManager.LoadScene(briefingRoomSceneName); // 항상 브리핑룸
}
```
그런데 서브미션 씬(`Sub_Mission1~4.unity`)의 `VictoryPanelController` 직렬화 값을 확인해보니
`nextStageSceneName`이 이미 `"MissionSelect"`로 올바르게 설정돼 있었다(메인 미션은 `Mission1`처럼
실제 다음 미션 씬 이름). 즉 "다음 목적지가 미션 선택 화면"이라는 정보는 이미 씬에 있었는데, 코드가
그 값을 무시하고 무조건 브리핑룸을 거치도록 되어 있어서 서브미션도 (다음 미션 브리핑 UI인)
브리핑룸으로 잘못 연결된 것.

## 수정
`VictoryPanelController.cs`에 `missionSelectSceneName` 필드(BriefingRoomController.goBackButton과
동일 컨벤션, 기본값 "MissionSelect") 추가, `OnNextStageClicked()`에서 `nextStageSceneName`이 이
값과 같으면 브리핑룸을 건너뛰고 바로 로드:
```diff
+    [SerializeField] private string missionSelectSceneName = "MissionSelect";

     private void OnNextStageClicked()
     {
         if (string.IsNullOrEmpty(nextStageSceneName))
             return;

         Time.timeScale = 1f;
         UserControl.IsPaused = false;

+        if (nextStageSceneName == missionSelectSceneName)
+        {
+            SceneManager.LoadScene(nextStageSceneName);
+            return;
+        }
+
         BriefingSelection.MissionNumber = missionNumber + 1;
         BriefingSelection.IsSubMission = false;
         BriefingSelection.TargetSceneName = nextStageSceneName;
         SceneManager.LoadScene(briefingRoomSceneName);
     }
```
새 필드는 기본값이 기존 관례("MissionSelect")와 같아서 씬/프리팹 쪽 직렬화 데이터를 전혀 건드리지
않아도 된다(4개 서브미션 씬 모두 `nextStageSceneName: MissionSelect`로 이미 일치).

## 결과
- 서브미션 클리어 → "다음으로" 버튼 → 브리핑룸을 거치지 않고 바로 미션 선택 화면(`MissionSelect`)으로 이동.
- 메인 미션(다음 미션 씬 이름이 지정된 경우)은 기존 그대로 브리핑룸을 거쳐 다음 미션으로 진행.
- 컴파일 확인: Unity CLI Loop 서버가 꺼져있어 이번 세션에서는 자동 확인 못 함 - 에디터에서
  Window > Unity CLI Loop > Server를 켠 뒤 재확인 필요.
