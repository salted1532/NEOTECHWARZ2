# 0400 - 승리 패널 3초 딜레이 + 표시 시 게임 일시정지 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 스테이지 클리어 시 3초 있다가 승리 화면 뜨고 승리 화면 떴을때 게임이 퍼즈 걸리도록 해줘

## 현재 동작

`StageManager.ReportVictory()` (`Assets/Scripts/System/StageManager.cs:30~35`)가 호출되면
`OnVictory` 이벤트가 즉시 발생하고, `VictoryPanelController.ShowVictoryPanel()`
(`Assets/Scripts/UI/VictoryPanelController.cs:40`)이 그 자리에서 바로 패널을 켠다. 딜레이도
없고 `Time.timeScale`을 건드리는 코드는 프로젝트 전체에 없다(WarFX 데모 에셋 제외 - 게임
로직과 무관). doc/0324에서도 "승리 시 일시정지는 필요하면 별도로 알려달라"고 남겨둔 부분.

## 제안 변경

`VictoryPanelController`만 수정:

1. `ShowVictoryPanel`을 코루틴으로 바꿔 `WaitForSecondsRealtime(3f)`으로 3초 대기 후 패널을
   켜고, 그 직후 `Time.timeScale = 0f`로 게임을 멈춘다. (`Realtime`을 쓰는 이유: 패널이 뜨는
   시점에 타임스케일을 0으로 만들 것이므로, 대기 자체는 타임스케일 영향을 안 받아야 정확히
   3초 뒤에 뜬다.)
2. 메인화면/다음 스테이지 버튼 클릭 시(`OnMainMenuClicked`, `OnNextStageClicked`) 씬을
   전환하기 직전에 `Time.timeScale = 1f`로 되돌린다. `Time.timeScale`은 `SceneManager.LoadScene`
   으로 씬이 바뀌어도 초기화되지 않고 그대로 유지되는 값이라, 여기서 복원하지 않으면 다음
   씬(메인 메뉴 등)도 멈춘 채로 로드된다.

### `Assets/Scripts/UI/VictoryPanelController.cs`

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class VictoryPanelController : MonoBehaviour
{
    [Header("승리 패널 (레이아웃은 직접 제작 후 연결)")]
    [SerializeField] private GameObject victoryPanel;

    [Header("버튼 연결")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button nextStageButton;

    [Header("씬 이동")]
    [SerializeField] private string mainSceneName = "MainScene";
    [SerializeField] private string nextStageSceneName = "SampleScene";

    [Header("연출")]
    [SerializeField] private float victoryDelay = 3f;

    private void Awake()
    {
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);
        victoryPanel?.SetActive(false);
    }

    private void Start()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnVictory += HandleVictory;
    }

    private void OnDestroy()
    {
        if (StageManager.Instance != null)
            StageManager.Instance.OnVictory -= HandleVictory;
    }

    private void HandleVictory() => StartCoroutine(ShowVictoryPanelAfterDelay());

    private IEnumerator ShowVictoryPanelAfterDelay()
    {
        yield return new WaitForSecondsRealtime(victoryDelay);
        victoryPanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }
}
```

## 확인 필요

- 3초 딜레이 값(`victoryDelay`) 인스펙터 노출로 충분한지, 아니면 하드코딩 3초 고정이 나은지.
- 승리 판정 시점(`ReportVictory` 호출) 이후에도 유닛들이 3초간 계속 움직이는 것이 의도인지
  (제안대로면 패널이 뜨기 전까지는 게임이 그대로 진행됨 - "3초 있다가 뜬다"는 요청과 일치).

## 영향받는 파일

- `Assets/Scripts/UI/VictoryPanelController.cs`
