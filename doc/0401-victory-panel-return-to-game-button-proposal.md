# 0401 - 승리 패널 "Return to Game" 버튼 연결 (제안)

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 0개.

## 요청 내용

> 승리 패널에서 return to game 이라는 버튼을 추가했는데 버튼을 누르면 승리 패널이 꺼지고
> 게임 퍼즈도 풀리도록 해줘

## 현재 동작

`VictoryPanelController` (`Assets/Scripts/UI/VictoryPanelController.cs`)는 `mainMenuButton`,
`nextStageButton` 두 버튼만 연결되어 있고, 각각 씬 전환 전 `Time.timeScale = 1f`로 복원한다.
에디터에서 새로 추가한 "Return to Game" 버튼은 아직 스크립트에 연결할 필드/핸들러가 없다.

## 제안 변경

`mainMenuButton`/`nextStageButton`과 같은 자리에 `returnToGameButton` 필드를 추가하고,
클릭 시 씬 전환 없이 패널만 끄고 타임스케일만 복원하는 핸들러를 연결한다.

### `Assets/Scripts/UI/VictoryPanelController.cs`

```csharp
    [Header("버튼 연결")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button nextStageButton;
    [SerializeField] private Button returnToGameButton;
```

```csharp
    private void Awake()
    {
        mainMenuButton?.onClick.AddListener(OnMainMenuClicked);
        nextStageButton?.onClick.AddListener(OnNextStageClicked);
        returnToGameButton?.onClick.AddListener(OnReturnToGameClicked);
        victoryPanel?.SetActive(false);
    }
```

```csharp
    private void OnReturnToGameClicked()
    {
        victoryPanel?.SetActive(false);
        Time.timeScale = 1f;
    }
```

## 확인 필요

- 승리 상태(`StageManager.Result == Victory`)는 그대로 유지한 채 패널만 닫고 게임을 계속
  진행시키는 것이 맞는지 (재요청 시 다시 패널이 뜨진 않음 - `ReportVictory`는 `InProgress`일
  때만 동작하므로).

## 영향받는 파일

- `Assets/Scripts/UI/VictoryPanelController.cs`
