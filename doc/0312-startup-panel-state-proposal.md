# 0312. 게임 시작 시 MainMenuPanel 켜짐/OptionPanel 꺼짐 보장 (제안)

날짜: 2026-07-30

## 요청 내용

> 게임 실행시 MainMenuPanel이 꺼져있으면 켜주도록 해줄래 옵션패널은 꺼주고

## 조사 내용

- `MainMenuController.Awake()`는 지금 `optionsPanel`만 명시적으로 꺼주고(`doc/0309`),
  `mainMenuPanel`은 시작 시 상태를 강제하지 않음(`doc/0311`에서 옵션 열고 닫을 때만 토글).
- 에디터에서 옵션 패널 작업을 하다가(Panel을 꺼둔 채로) 씬을 저장해버리는 경우 등, 시작 시
  `mainMenuPanel`이 꺼진 채로 플레이가 시작될 수 있어서 이번 요청대로 시작 시 강제로 켜주는 로직 추가.

## 코드 변경

### `Assets/Scripts/UI/MainMenuController.cs`

**기존 코드**:
```csharp
        playButton?.onClick.AddListener(OnPlayClicked);
        optionButton?.onClick.AddListener(OnOptionClicked);
        exitButton?.onClick.AddListener(OnExitClicked);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }
```

**변경 코드**:
```csharp
        playButton?.onClick.AddListener(OnPlayClicked);
        optionButton?.onClick.AddListener(OnOptionClicked);
        exitButton?.onClick.AddListener(OnExitClicked);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);

        mainMenuPanel?.SetActive(true);  // 꺼진 채로 저장돼있어도 시작하면 항상 켜지도록
        optionsPanel?.SetActive(false);
    }
```

## 요약

- 게임 시작 시 `mainMenuPanel`이 꺼져 있어도 무조건 켜지고, `optionsPanel`은 무조건 꺼진 채로
  시작한다.
- 단, 이 스크립트가 `mainMenuPanel` 자신에게 붙어 있으면(필드를 비워서 자기 자신을 쓰는 경우) 그
  오브젝트가 씬 저장 시점에 이미 꺼져 있으면 `Awake()` 자체가 실행되지 않아 이 로직이 발동하지
  않는다 - 이 기능이 확실히 동작하려면 `MainMenuController`를 (Panel이 아니라) 항상 켜져 있는
  `Canvas` 같은 오브젝트에 붙이고 `mainMenuPanel` 필드에 Panel을 직접 연결해두는 걸 권장.

## 영향받는 파일

- `Assets/Scripts/UI/MainMenuController.cs` (수정)

## 다음 단계

이대로 수정해도 될지 확인 부탁드립니다.
