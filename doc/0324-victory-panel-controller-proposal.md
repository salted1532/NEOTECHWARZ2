# 0324. 승리 패널 연결 (메인화면 버튼 포함)

**날짜:** 2026-07-31

## 요청 내용

> 이제 승리시 뜨는 panel을 연결할수 있게 해주고 panel에는 메인화면으로 돌아가는 버튼이 있을거야

(참고: 작업 중 `doc/0322`가 다른 세션에서 동시에 다른 내용으로 쓰여 번호가 겹쳐서, 그 파일을 `doc/0323-advanced-unit-active-passive-skill-effects-design.md`로 옮기고 이번 요청은 `0324`로 기록함.)

## 조사 내용

- [[stage-manager-skeleton|doc/0321]]에서 만든 `StageManager`가 이미 `OnVictory`(승리) / `OnDefeat`(패배) 이벤트를 노출하고 있음(`System/StageManager.cs`) — 새 이벤트를 만들 필요 없이 그대로 구독하면 됨.
- 같은 씬 전환/버튼 연결 패턴이 이미 두 군데 있음 — 그대로 재사용:
  - `UI/TestSceneMenuController.cs`: "메인화면으로 나가기" 버튼 → `SceneManager.LoadScene(mainSceneName)` (`mainSceneName` 인스펙터 필드, 기본값 `"MainScene"`)
  - `UI/MainMenuController.cs`: 패널 GameObject를 인스펙터로 연결하고 `SetActive`로 켜고 끄는 컨벤션
- 즉 이번 요청("승리 패널 연결" + "메인화면 버튼")은 이 두 기존 패턴을 그대로 합친 것과 동일 — 새 씬 전환 로직이나 패널 프레임워크를 만들 필요 없음.

## 설계안

### 신규 파일: `Assets/Scripts/UI/VictoryPanelController.cs`
```csharp
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// StageManager.OnVictory를 구독해서 승리 패널을 띄우고, 패널 안의 "메인화면으로" 버튼을 처리한다.
// 패널 레이아웃(배경/문구/연출)은 유니티 에디터에서 직접 만들고 이 스크립트에는 그 패널 GameObject와
// 버튼만 연결하면 된다 - TestSceneMenuController와 동일한 컨벤션(패널 표시/씬 전환만 담당).
public class VictoryPanelController : MonoBehaviour
{
    [Header("승리 패널 (레이아웃은 직접 제작 후 연결)")]
    [SerializeField] private GameObject victoryPanel;

    [Header("버튼 연결")]
    [SerializeField] private Button mainMenuButton;

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";

    private void Awake()
    {
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        victoryPanel?.SetActive(false);
    }

    private void Start()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnVictory += ShowVictoryPanel;
    }

    private void OnDestroy()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnVictory -= ShowVictoryPanel;
    }

    private void ShowVictoryPanel() => victoryPanel?.SetActive(true);

    private void OnMainMenuClicked() => SceneManager.LoadScene(mainSceneName);
}
```

### 이번에 포함하지 않는 것 (요청에 없음, 스코프 밖)
- 승리 시 `Time.timeScale = 0` 등 게임 일시정지 — 필요하면 별도로 알려주세요
- 패배 패널(`OnDefeat` 구독) — 이번엔 승리 패널만 요청받음. 필요하면 같은 패턴으로 별도 컴포넌트(또는 이 컴포넌트에 필드 추가)로 바로 확장 가능
- 패널 레이아웃(배경/문구/애니메이션) 제작 — 씬에서 직접 제작 필요

### 씬 작업 (스크립트 생성 후 별도로 필요)
- 승리 패널 UI(배경 + 문구 + 버튼)를 씬에 제작
- `Stage0Objectives`가 있는 씬(예: TestScene)의 아무 GameObject에 `VictoryPanelController` 부착
- `victoryPanel`에 승리 패널 GameObject 연결, `mainMenuButton`에 그 패널 안의 버튼 연결

## 검증

- 사용자 확인 후 위 설계안 그대로 `Assets/Scripts/UI/VictoryPanelController.cs` 생성
- `uloop compile`: `Success: true, ErrorCount: 0` (VictoryPanelController.cs 관련 에러/경고 없음)
- 승리 패널 UI 제작, 씬에 컴포넌트 부착, `victoryPanel`/`mainMenuButton` 인스펙터 연결은 다음 작업으로 남김(코드로 할 수 없는 씬 편집)

## 영향받는 파일

- `Assets/Scripts/UI/VictoryPanelController.cs` (신규, 생성 완료)
