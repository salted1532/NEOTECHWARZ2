# 0430. 인게임 옵션 버튼으로 게임 일시정지/재개

- 날짜: 2026-08-05
- 상태: **적용 완료** (컴파일 확인: 에러 0개)

## 요청 내용

> 인게임 씬에서 option 버튼을 누르면 게임이 퍼즈 되도록 했으면 좋겠어 cancel버튼을 누르면 다시
> 재개되도록 해줘

## 조사 내용

인게임(게임플레이) 씬의 옵션 버튼은 `Assets/Scripts/UI/TestSceneMenuController.cs`가 담당하고
있고, `Assets/prefabs/Game/GameManager.prefab`에 붙어 있다(`TestSceneMenuController`를 참조하는
유일한 prefab). 이 스크립트엔 "Cancel"이라는 이름의 버튼은 따로 없고, 옵션 패널을 여는
`optionButton`과 그 패널을 닫는 `optionCloseButton`(패널의 X/닫기 버튼) 한 쌍만 있다 — 요청하신
"cancel버튼"은 이 `optionCloseButton`(옵션 패널을 취소/닫는 버튼)을 가리키는 것으로 이해했다.

**기존 코드** (`TestSceneMenuController.cs`):
```csharp
    public void OpenOptionsPanel() => optionsPanel?.SetActive(true);

    public void CloseOptionsPanel() => optionsPanel?.SetActive(false);
```
지금은 패널 표시만 토글할 뿐 `Time.timeScale`은 건드리지 않는다.

일시정지 처리는 이미 같은 프로젝트의 `Assets/Scripts/UI/VictoryPanelController.cs`(승리 패널)에서
쓰고 있는 패턴이 있다 - 패널을 켤 때 `Time.timeScale = 0f`, 끄거나 다른 씬으로 나갈 때
`Time.timeScale = 1f`로 되돌린다. 옵션 패널도 같은 패턴을 그대로 따르면 된다(프로젝트에 이미
있는 관례를 재사용 - 새 일시정지 시스템을 따로 만들 필요 없음).

`Time.timeScale = 0`이 되면 `Time.deltaTime` 기반으로 동작하는 모든 유닛 이동/공격 타이머/생산
큐 등은 자연히 멈추고(이미 프로젝트 전역이 `Time.deltaTime` 기반), UI 클릭/버튼 이벤트는
`Time.timeScale`과 무관하게 정상 동작하므로 옵션 패널 조작 자체는 멈추지 않는다 - 승리 패널이
이미 같은 방식으로 잘 동작하고 있어 검증된 패턴이다.

**추가로 확인한 것**: `mainMenuButton`("메인화면으로 나가기")과 `nextStageButton`("다음
스테이지로 이동")을 옵션 패널이 열려 있는(퍼즈된) 상태에서 눌러 다른 씬으로 이동하면,
`Time.timeScale`이 0인 채로 새 씬이 로드돼버려서 새 씬도 멈춰있는 것처럼 보이는 문제가 생길 수
있다. `VictoryPanelController`의 `OnMainMenuClicked`/`OnNextStageClicked`도 씬 이동 직전에
`Time.timeScale = 1f`로 되돌리는 걸 보면 이미 알려진 위험이라 안전장치를 넣어둔 것으로 보인다 -
`TestSceneMenuController`에도 동일하게 추가한다.

## 코드 변경 (제안)

**기존 코드**:
```csharp
    public void OpenOptionsPanel() => optionsPanel?.SetActive(true);

    public void CloseOptionsPanel() => optionsPanel?.SetActive(false);

    private void OnMainMenuClicked() => SceneManager.LoadScene(mainSceneName);

    private void OnNextStageClicked() => SceneManager.LoadScene(nextStageSceneName);
```

**변경 코드**:
```csharp
    public void OpenOptionsPanel()
    {
        optionsPanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    public void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
        Time.timeScale = 1f;
    }

    private void OnMainMenuClicked()
    {
        Time.timeScale = 1f; // 옵션(퍼즈) 상태로 나가면 다음 씬까지 멈춰있지 않도록 안전하게 복구
        SceneManager.LoadScene(mainSceneName);
    }

    private void OnNextStageClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextStageSceneName);
    }
```

## 요약 / 영향받는 파일

- 옵션 버튼(`optionButton`) 클릭 → 패널이 열리며 `Time.timeScale = 0f`로 게임 일시정지.
- 옵션 패널의 닫기/취소 버튼(`optionCloseButton`) 클릭 → 패널이 닫히며 `Time.timeScale = 1f`로
  게임 재개.
- 옵션이 열린 채로 "메인화면으로"/"다음 스테이지로" 버튼을 눌러도 씬 이동 전에 타임스케일을
  되돌려 다음 씬이 멈춘 채로 시작되는 것을 방지.
- 영향받는 파일: `Assets/Scripts/UI/TestSceneMenuController.cs` (코드 변경만, 씬/프리팹 설정
  변경 없음)
- 아직 프로젝트 파일에는 적용하지 않음 (제안 단계).
