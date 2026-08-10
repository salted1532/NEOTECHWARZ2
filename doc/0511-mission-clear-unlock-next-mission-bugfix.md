# 0511. 미션 클리어 후 다음 미션 미해금 버그 수정 제안

**날짜:** 2026-08-10

## 요청 내용

> 미션 선택창에서 1미션을 클리어 해도 2미션이 해금이 안돼. 게임 클리어 이후 다음스테이지 가는게
> 아니라 메인메뉴를 나갔다가 미션선택창으로 오면 2미션이 해금이 되어야하는데 제대로 작동을 안해

## 조사 내용

- `MissionSelectManager.ApplyLockState()`(`Assets/Scripts/UI/MissionSelectManager.cs:117-128`)는
  `PlayerPrefs.GetInt("HighestUnlockedMission", 1)` 값을 읽어서
  `entry.button.interactable = entry.missionNumber <= highestUnlocked`로 잠금/해금을 결정한다.
- 이 `"HighestUnlockedMission"` 키를 **쓰는** 곳은 코드 전체에서 `UnlockAllMissions()`
  (개발자용 "Unlock All Mission" 버튼, 88-140줄) **한 곳뿐**이다. 실제 미션을 클리어했을 때
  이 값을 갱신하는 코드는 어디에도 없다.
- 이는 이미 알려진 미완성 상태였다 - `doc/0472`에 다음과 같이 명시되어 있다:
  > "미션1 클리어 시 다음 스테이지가 열리는 방식으로 할려고해"는 앞으로의 계획 설명이라 이번
  > 작업 범위에는 "각 미션 씬에서 클리어를 감지해 실제로 해금을 갱신하는 연결"까지는 포함하지
  > 않음 ... 나중에 미션 씬 쪽(`StageManager.OnVictory` 등)에서 이 값을 갱신하도록 이어붙이면 됨.
  즉 그 "이어붙이기"가 아직 한 번도 연결되지 않은 상태 - 이번에 보고된 버그의 근본 원인이다.
- 실제 클리어 흐름 확인:
  - `StageManager.ReportVictory()`(`Assets/Scripts/System/StageManager.cs:62-67`)가
    `OnVictory` 이벤트를 발생시킨다.
  - `VictoryPanelController`(`Assets/Scripts/UI/VictoryPanelController.cs`)가 이 이벤트를
    구독해서(`Start()`, 36-37줄) 승리 패널을 띄운다(`HandleVictory` → `ShowVictoryPanelAfterDelay`).
  - "메인화면으로" 버튼(`OnMainMenuClicked`, 56-61줄)은 `SceneManager.LoadScene(mainSceneName)`만
    호출할 뿐, `PlayerPrefs`는 전혀 건드리지 않는다.
  - 결과: Mission1을 깨고 메인 메뉴로 나가도 `HighestUnlockedMission`은 여전히 기본값 1 그대로라
    `MissionSelect`로 돌아와도 Mission2는 계속 잠겨 있다. (보고된 증상과 정확히 일치)
- `VictoryPanelController`는 `Mission0.unity` ~ `Mission5.unity` 6개 미션 씬 전부에 각각
  배치되어 있음(씬별 개별 인스턴스, 싱글턴 아님) - `MissionSelectManager`의 `missionNumber`
  필드와 동일한 컨벤션으로 씬별 값을 인스펙터에 넣을 수 있다.

## 계획된 코드 변경

파일: `Assets/Scripts/UI/VictoryPanelController.cs`

- `[SerializeField] private int missionNumber;` 필드 추가 - `MissionSelectManager.
  MissionSelectEntry.missionNumber`와 동일한 의미(이 씬이 몇 번 미션인지). 각 미션 씬
  (`Mission0`~`Mission5`)의 인스펙터에서 0~5로 직접 설정해야 함(씬 파일 변경 수반).
- `HandleVictory()`에서 패널을 띄우기 전에 해금 갱신을 먼저 처리:
  ```csharp
  private void HandleVictory()
  {
      UnlockNextMission();
      StartCoroutine(ShowVictoryPanelAfterDelay());
  }

  private void UnlockNextMission()
  {
      int highest = PlayerPrefs.GetInt(HighestUnlockedMissionKey, 1);
      if (missionNumber + 1 <= highest)
          return;

      PlayerPrefs.SetInt(HighestUnlockedMissionKey, missionNumber + 1);
      PlayerPrefs.Save();
  }
  ```
- 키 이름 `"HighestUnlockedMission"`을 `MissionSelectManager.cs`와 동일한 문자열 리터럴로
  `private const string HighestUnlockedMissionKey = "HighestUnlockedMission";`로 이 클래스에도
  둔다. 두 클래스가 문자열을 각자 하드코딩하는 형태라 오타 위험은 있지만, `MissionSelectManager`
  자체도 이미 이 방식(자체 `private const`)을 쓰고 있어 기존 컨벤션과 맞춘 최소 변경으로 제안함.
  (원하면 공유 상수 클래스로 옮기는 방법도 있음 - 필요하면 말씀해주세요.)
- Mission5(마지막 미션)를 깨면 `missionNumber + 1 = 6`이 저장되지만, `MissionSelectManager`의
  `missions` 리스트에는 `missionNumber <= 6`을 만족하는 미션이 어차피 다 포함되므로(0~5 전부)
  해가 되지 않음 - 별도 상한 처리 불필요.

## 이번 범위에 포함하지 않는 것

- "다음 스테이지" 버튼(`OnNextStageClicked`)은 현재 `nextStageSceneName`이 전부
  `"SampleScene"`(플레이스홀더)로 되어 있어 다음 미션 씬으로 직접 이어지는 기능이 아직 없음 -
  이번 버그(메인 메뉴를 거쳐 미션 선택창에서 해금되는 흐름)와는 별개라 손대지 않음.

## 변경 예정 파일

- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scenes/Missions/Mission0.unity` ~ `Mission5.unity` (각 씬의 `VictoryPanelController`
  인스펙터에 `missionNumber` 값 0~5 설정)

---

## 적용 (사용자 승인 후)

> 이대로 진행시켜줘

제안대로 적용함.

### `VictoryPanelController.cs`

```diff
 public class VictoryPanelController : MonoBehaviour
 {
+    // MissionSelectManager.HighestUnlockedMissionKey와 동일한 문자열(doc/0511) - 클리어 시
+    // 다음 미션 번호로 갱신해서 MissionSelect 화면에서 잠금이 풀리게 한다.
+    private const string HighestUnlockedMissionKey = "HighestUnlockedMission";
+
     [Header("승리 패널 (레이아웃은 직접 제작 후 연결)")]
     [SerializeField] private GameObject victoryPanel;
 
+    [Header("해금 진행 (doc/0511)")]
+    [SerializeField] private int missionNumber; // 이 씬이 몇 번 미션인지
+
     [Header("버튼 연결")]
     ...
-    private void HandleVictory() => StartCoroutine(ShowVictoryPanelAfterDelay());
+    private void HandleVictory()
+    {
+        UnlockNextMission();
+        StartCoroutine(ShowVictoryPanelAfterDelay());
+    }
+
+    private void UnlockNextMission()
+    {
+        int highest = PlayerPrefs.GetInt(HighestUnlockedMissionKey, 1);
+        if (missionNumber + 1 <= highest)
+            return;
+
+        PlayerPrefs.SetInt(HighestUnlockedMissionKey, missionNumber + 1);
+        PlayerPrefs.Save();
+    }
```

### 씬 파일 (`Mission0.unity` ~ `Mission5.unity`)

각 씬의 `VictoryPanelController` 컴포넌트 블록에 `missionNumber` 필드를 씬 번호와 맞춰 한 줄씩 추가:

```yaml
  m_EditorClassIdentifier: Assembly-CSharp::VictoryPanelController
  missionNumber: 0   # Mission0.unity는 0, Mission1.unity는 1, ... Mission5.unity는 5
  victoryPanel: {fileID: ...}
```

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0` (기존에 있던 37개 경고는 이번 변경과
  무관한 프로젝트 전역의 obsolete API 경고 - 그대로 유지됨).
- 각 미션 씬의 `nextStageSceneName`을 확인하는 과정에서, `doc/0472`에 "다음 스테이지 씬이 아직
  없어 SampleScene 플레이스홀더"라고 적혀 있던 부분이 이미 이후 작업으로 갱신되어 있음을 확인:
  `Mission0→Mission1`, `Mission1→Mission2`, ... `Mission4→Mission5`로 정상 연결(단, `Mission5`만
  `Mission0`로 순환 - 마지막 미션이라 의도적인 것으로 보이나 확인 필요하면 말씀해주세요).
  `UnlockNextMission()`을 "다음 스테이지" 버튼이 아니라 `HandleVictory()`(승리 이벤트 발생 시점)에
  붙였기 때문에, 어느 버튼을 누르든(메인 메뉴로 나가기/다음 스테이지 바로 가기 둘 다) 해금은
  이미 처리된 상태 - 원래 보고된 "메인 메뉴 경유" 경로와 "다음 스테이지 직행" 경로 둘 다 커버됨.

### ⚠ 확인 필요 - `Mission1.unity`에 예상보다 큰 diff 발생

`git diff --stat` 결과 다른 미션 씬은 전부 `missionNumber` 한 줄만 늘었는데, `Mission1.unity`만
853줄이 바뀜. 내용을 확인해보니 제가 건드리지 않은 부분들 - 예: 저장 안 된 "Guardian Drone (1)"
프리팹 인스턴스 추가/좌표 변경, 사용하지 않는 `Universal Render Pipeline/Unlit` 머티리얼 블록
제거 등 - 이 함께 파일에 반영되어 있음.

**원인 추정:** Unity 에디터가 `Mission1` 씬을 열어둔 채로 저장하지 않은 변경사항(사용자가
에디터에서 작업 중이던 것으로 보임)이 있는 상태였는데, 제가 씬 파일을 직접 텍스트 편집하자
에디터가 외부 변경을 감지 → 이후 `compile` 호출 등으로 에디터가 씬을 다시 저장하면서 그
미저장 변경사항까지 같이 디스크에 flush된 것으로 보임. 즉 **제가 지운/추가한 내용이 아니라
에디터에 남아있던 사용자의 작업 내용**일 가능성이 높음 - 하지만 자동으로 벌어진 일이라 확실히
의도한 저장인지는 확인이 필요함. 커밋 전에 `Mission1.unity` diff를 직접 한 번 확인해주세요
(`git diff "Assets/Scenes/Missions/Mission1.unity"`). 필요하면 `missionNumber: 1` 한 줄만 남기고
나머지는 되돌릴 수 있습니다.

- `Assets/AssetFolder/LowPolyWater_Pack/Plane Meshes/Ocean50x50W750H750.asset` 변경도 같이
  잡혔는데, 이건 `doc/0509`/`doc/0510`에서도 언급된 기존에 알려진 "물 메쉬 노이즈"(에디터가 씬을
  다시 저장할 때마다 부동소수점 오차로 계속 갱신되는 것)라 이번 작업과 무관 - 무시해도 됨.

## 변경된 파일

- `Assets/Scripts/UI/VictoryPanelController.cs`
- `Assets/Scenes/Missions/Mission0.unity` ~ `Mission5.unity` (`missionNumber` 필드 추가 - 단
  `Mission1.unity`는 위 확인 필요 항목 참고)
