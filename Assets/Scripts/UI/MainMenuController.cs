using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메인 메뉴 화면(MainScene)의 Play/Option/Exit 버튼을 연결한다. 옵션 패널 자체(레이아웃)는
// SoundSettingsPanel.cs와 동일한 컨벤션으로 유니티 에디터에서 직접 만들고, 이 스크립트에는
// 인스펙터로 그 패널 GameObject만 연결하면 된다 - 여기선 켜고 끄는 로직만 담당한다.
public class MainMenuController : MonoBehaviour
{
    [Header("버튼 연결")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button optionCloseButton; // 옵션 패널의 X(닫기) 버튼

    [Header("씬 이동")]
    [SerializeField] private string testSceneName = "TestScene";

    [Header("패널 (레이아웃은 직접 제작 후 연결)")]
    [SerializeField] private GameObject mainMenuPanel; // 비워두면 이 스크립트가 붙은 오브젝트 자신을 사용
    [SerializeField] private GameObject optionsPanel;

    // TestScene/SampleScene은 UserControl이 매 프레임 이 텍스처로 커서를 되돌리는데(호버 대상이 없으면
    // 항상 기본 커서), MainScene은 RTS 게임플레이가 없어 UserControl 자체가 없다 보니 OS 기본 화살표가
    // 그대로 남아있었다. 메뉴는 버튼 외엔 "호버 대상"이라는 개념이 없어 매 프레임 갱신할 필요 없이
    // 한 번만 설정하면 다른 씬과 동일하게 보인다.
    [Header("마우스 커서 (다른 씬과 동일하게)")]
    [SerializeField] private Texture2D cursorTexture; // 비워두면 OS 기본 화살표 사용
    [SerializeField] private Texture2D cursorHoverTexture; // 클릭 가능한 버튼 위에 있을 때 (비워두면 호버해도 커서 안 바뀜)
    [SerializeField] private Vector2 cursorHotspot = Vector2.zero;
    [SerializeField] private Camera uiCamera; // Canvas RenderMode가 Overlay면 비워둔다 (TooltipUI와 동일한 컨벤션)

    private Button[] hoverableButtons;
    private bool isHoveringButton;

    private void Awake()
    {
        if (mainMenuPanel == null)
            mainMenuPanel = gameObject;

        playButton?.onClick.AddListener(OnPlayClicked);
        optionButton?.onClick.AddListener(OnOptionClicked);
        exitButton?.onClick.AddListener(OnExitClicked);
        optionCloseButton?.onClick.AddListener(CloseOptionsPanel);

        mainMenuPanel?.SetActive(true);  // 꺼진 채로 저장돼있어도 시작하면 항상 켜지도록
        optionsPanel?.SetActive(false);

        hoverableButtons = new[] { playButton, optionButton, exitButton, optionCloseButton };

        if (cursorTexture != null)
            Cursor.SetCursor(cursorTexture, cursorHotspot, CursorMode.Auto);
    }

    // 클릭 가능한(interactable + 활성화된) 버튼 위에 마우스가 있는지 매 프레임 확인해서 커서를 바꾼다.
    // TooltipUI.IsPointerOverTarget()과 동일한 방식(RectTransformUtility) - 버튼마다 별도 컴포넌트를
    // 붙일 필요 없이 이미 갖고 있는 참조만으로 처리한다.
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

    private void OnPlayClicked() => SceneManager.LoadScene(testSceneName);

    private void OnOptionClicked()
    {
        optionsPanel?.SetActive(true);
        mainMenuPanel?.SetActive(false);
    }

    // 옵션 패널의 X 버튼에 연결된다(Awake에서 자동 연결).
    public void CloseOptionsPanel()
    {
        optionsPanel?.SetActive(false);
        mainMenuPanel?.SetActive(true);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // 에디터에서는 Application.Quit()이 동작하지 않는다
#else
        Application.Quit();
#endif
    }
}
