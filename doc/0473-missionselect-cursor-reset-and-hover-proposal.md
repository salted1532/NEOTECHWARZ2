# 0473 - MissionSelect 씬 커서 초기화 + 버튼 호버 커서 (제안)

## 질문
"MissionSelect 씬에서 메인씬에서 마우스 호버시 변경된 아이콘으로 계속 유지되는데 미션선택씬에서
원래대로 마우스 돌아오고 각 버튼 호버시 바뀌도록 해줘"

## 원인
`Cursor.SetCursor`는 OS 레벨 전역 상태라 씬을 넘어가도 유지된다. `MainMenuController.cs`
(MainScene)는 Awake에서 커서를 지정 텍스처로 바꾸고, 버튼 호버 시 `cursorHoverTexture`로
바꾸는 로직을 갖고 있다(`Assets/Scripts/UI/MainMenuController.cs:56-91`). 반면 MissionSelect
씬을 담당하는 `MissionSelectManager.cs`에는 커서 관련 로직이 전혀 없다 - 그래서 MainScene에서
마지막으로 세팅된 커서 텍스처가 MissionSelect 씬에서도 그대로 남는다.

## 해결 방향
`MainMenuController.cs`에 이미 있는 패턴(Awake에서 기본 커서 세팅 + Update에서 클릭 가능한
버튼 위에 있는지 매 프레임 확인해서 호버 커서로 교체)을 `MissionSelectManager.cs`에 동일하게
적용한다. 대상 버튼은 `missions` 리스트의 각 버튼 + `backToMainMenuButton` +
`unlockAllMissionButton`.

인스펙터에 `cursorTexture`/`cursorHoverTexture`를 비워두면 기존과 동일하게 아무 것도 안 바뀐다
(MainMenuController와 동일 컨벤션 - `cursorHoverTexture == null`이면 Update가 조기 return).
즉 이 변경만으로는 아직 아무 동작 변화가 없고, 인스펙터에 두 텍스처를 연결해야 실제로 커서가
바뀐다. MainScene에서 쓰는 것과 같은 텍스처를 재사용하면 됨.

## 변경 파일
`Assets/Scripts/UI/MissionSelectManager.cs`

### Before
```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

...

public class MissionSelectManager : MonoBehaviour
{
    private const string HighestUnlockedMissionKey = "HighestUnlockedMission";
    private const int DefaultHighestUnlockedMission = 1;

    [SerializeField] private List<MissionSelectEntry> missions = new();

    [Header("메인 메뉴로 돌아가기")]
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainScene";

    [Header("개발자용 - 정식 버전 출시 전 이 버튼과 연결을 제거할 것")]
    [SerializeField] private Button unlockAllMissionButton;

    private void Awake()
    {
        foreach (MissionSelectEntry entry in missions)
        {
            if (entry.button == null)
                continue;

            entry.button.onClick.AddListener(() => LoadMission(entry));
            SetupHoverTooltip(entry);
        }

        ApplyLockState();

        backToMainMenuButton?.onClick.AddListener(BackToMainMenu);
        unlockAllMissionButton?.onClick.AddListener(UnlockAllMissions);
    }

    ...
}
```

### After (제안)
```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

...

public class MissionSelectManager : MonoBehaviour
{
    private const string HighestUnlockedMissionKey = "HighestUnlockedMission";
    private const int DefaultHighestUnlockedMission = 1;

    [SerializeField] private List<MissionSelectEntry> missions = new();

    [Header("메인 메뉴로 돌아가기")]
    [SerializeField] private Button backToMainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainScene";

    [Header("개발자용 - 정식 버전 출시 전 이 버튼과 연결을 제거할 것")]
    [SerializeField] private Button unlockAllMissionButton;

    // MainScene(MainMenuController)이나 미션 플레이(UserControl)에서 바꾼 커서가 Cursor.SetCursor의
    // 전역 상태 때문에 씬을 넘어와도 남아있던 문제(doc/0473) - MainMenuController와 동일한 패턴으로
    // 이 씬 진입 시 기본 커서로 되돌리고, 버튼 호버 시에만 다시 바꾼다.
    [Header("마우스 커서 (MainMenuController와 동일한 패턴)")]
    [SerializeField] private Texture2D cursorTexture; // 비워두면 OS 기본 화살표 사용
    [SerializeField] private Texture2D cursorHoverTexture; // 비워두면 호버해도 커서 안 바뀜
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;
    [SerializeField] private Camera uiCamera; // Canvas RenderMode가 Overlay면 비워둔다

    private Button[] hoverableButtons;
    private bool isHoveringButton;

    private void Awake()
    {
        foreach (MissionSelectEntry entry in missions)
        {
            if (entry.button == null)
                continue;

            entry.button.onClick.AddListener(() => LoadMission(entry));
            SetupHoverTooltip(entry);
        }

        ApplyLockState();

        backToMainMenuButton?.onClick.AddListener(BackToMainMenu);
        unlockAllMissionButton?.onClick.AddListener(UnlockAllMissions);

        hoverableButtons = missions.Select(entry => entry.button)
            .Append(backToMainMenuButton)
            .Append(unlockAllMissionButton)
            .ToArray();

        if (cursorTexture != null)
            Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
    }

    private void Update()
    {
        if (cursorHoverTexture == null)
            return;

        bool hovering = IsHoveringClickableButton();

        if (hovering == isHoveringButton)
            return;

        isHoveringButton = hovering;
        Cursor.SetCursor(hovering ? cursorHoverTexture : cursorTexture, cursorHotspot, CursorMode.Auto);
    }

    private bool IsHoveringClickableButton()
    {
        foreach (Button button in hoverableButtons)
        {
            if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                continue;

            RectTransform rect = button.transform as RectTransform;

            if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, uiCamera))
                return true;
        }

        return false;
    }

    ...
}
```

## 참고
- 인스펙터 연결 필요: MissionSelect 씬 오브젝트의 `MissionSelectManager` 컴포넌트에
  `cursorTexture`/`cursorHoverTexture`를 MainScene과 동일한 텍스처로 채워야 실제 효과가 있음.
  둘 다 비워두면 지금과 동일하게 아무 것도 안 바뀜 (기본 OS 커서 유지).
- `unlockAllMissionButton`이 비활성화/미할당이어도 `hoverableButtons` 배열에 null이 들어갈 수
  있는데, `IsHoveringClickableButton`이 null 체크로 걸러내므로 문제없음.
