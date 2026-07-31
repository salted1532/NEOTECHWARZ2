using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 커맨드 패널/생산 대기열의 버튼 슬롯 하나를 표현.
// 아이콘 표시, 클릭 콜백 연결, 비활성/초기화 처리, 호버 시 툴팁 표시를 담당하는 재사용 가능한 UI 슬롯 컴포넌트.
public class ProductionSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    // 쿨다운 중인 스킬 버튼 위에 겹쳐서 시계처럼 줄어드는 원형 오버레이 (doc/0323 후속) - 비워두면 아무 효과도 없음.
    // 인스펙터에서 Image Type을 Filled/Fill Method를 Radial 360으로 설정해두면 SetCooldownFill()이 fillAmount만 갱신한다.
    [SerializeField] private Image cooldownOverlayImage;

    private RectTransform rectTransform;
    private Action callback;
    private UIController.CommandButtonData data;
    private bool hasData;
    private KeyCode shortcut = KeyCode.None; // 이 슬롯을 대신 "누르는" 키보드 단축키 (없으면 KeyCode.None)
    private bool isHovered; // 호버 중엔 Update()가 매 프레임 툴팁을 새로 갱신해서 쿨다운 잔여시간처럼 계속 바뀌는 설명도 실시간으로 보이게 한다

    // 마지막으로 TooltipUI에 실제로 표시한 내용. 호버 중엔 매 프레임 확인하되, 내용이 지난 프레임과 완전히
    // 같으면 TooltipUI.Show()(내부적으로 강제 레이아웃 리빌드 포함)를 다시 부르지 않는다 - 표시 결과는 동일함.
    private bool hasShownTooltip;
    private string lastTooltipTitle;
    private string lastTooltipDescription;
    private bool lastTooltipHasCost;
    private int lastTooltipOre;
    private int lastTooltipGas;
    private int lastTooltipPopulation;

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);

        // 오버레이가 클릭을 가로채 버튼이 안 눌리는 사고를 막기 위한 방어 처리 (인스펙터에서 깜빡 안 꺼도 항상 안전).
        if (cooldownOverlayImage != null)
            cooldownOverlayImage.raycastTarget = false;
    }

    /// <summary>
    /// 슬롯에 데이터를 표시한다 (아이콘/활성화 여부/콜백/툴팁 정보 설정 후 슬롯을 켠다).
    /// </summary>
    public void SetData(UIController.CommandButtonData data)
    {
        gameObject.SetActive(true);

        this.data = data;
        hasData = true;
        callback = data.Callback;
        shortcut = data.Shortcut;

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (button != null)
        {
            button.interactable =
                data.Interactable &&
                data.Callback != null;
        }
    }

    /// <summary>
    /// 슬롯을 초기화하고 비활성화한다 (아이콘 제거, 콜백 해제, 게임오브젝트 비활성화).
    /// </summary>
    public void Clear()
    {
        callback = null;
        hasData = false;
        shortcut = KeyCode.None;

        // 비활성화(SetActive(false))로는 OnPointerExit이 호출되지 않아 isHovered가 true로 남는다.
        // 이 슬롯이 나중에 다른 커맨드로 재활용(SetData)되면, 마우스가 실제로 그 위에 없는데도
        // Update()가 곧장 RefreshTooltip()을 불러 엉뚱한 내용의 툴팁이 잠깐 깜빡이는 원인이 된다.
        if (isHovered)
        {
            isHovered = false;
            hasShownTooltip = false;
            TooltipUI.Instance?.Hide();
        }

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (button != null)
            button.interactable = false;

        SetCooldownFill(0f); // 다른 종류의 버튼으로 재사용될 때 이전 스킬의 쿨다운 표시가 남아있지 않도록

        gameObject.SetActive(false);
    }

    /// <summary>
    /// 쿨다운 원형 오버레이를 갱신한다. 1=방금 사용(꽉 참) → 0=사용 가능(안 보임)으로 매 프레임 호출해서 줄어든다.
    /// cooldownOverlayImage가 연결 안 돼 있으면 조용히 무시된다(doc/0323 후속 - 스킬 슬롯 전용, 선택적 기능).
    /// </summary>
    public void SetCooldownFill(float normalizedRemaining)
    {
        if (cooldownOverlayImage == null)
            return;

        normalizedRemaining = Mathf.Clamp01(normalizedRemaining);
        cooldownOverlayImage.fillAmount = normalizedRemaining;
        cooldownOverlayImage.gameObject.SetActive(normalizedRemaining > 0f);
    }

    private void OnClick()
    {
        bool ctrlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (ctrlHeld && data.CtrlClickCallback != null)
        {
            data.CtrlClickCallback.Invoke();
            return;
        }

        if (shiftHeld && data.ShiftClickCallback != null)
        {
            data.ShiftClickCallback.Invoke();
            return;
        }

        callback?.Invoke();
    }

    // 이 슬롯이 활성화(SetData)돼 있는 동안에만 실행됨 - Clear()되면 gameObject 자체가 비활성화라
    // Update()가 아예 호출되지 않으므로, "지금 이 버튼이 안 보이면 단축키도 죽어있다"가 자동으로 성립한다.
    private void Update()
    {
        if (isHovered)
            RefreshTooltip(); // 쿨다운 잔여시간처럼 매 프레임 바뀌는 설명 텍스트를 호버 중에도 실시간으로 반영

        if (!hasData || shortcut == KeyCode.None || button == null || !button.interactable)
            return;

        if (Input.GetKeyDown(shortcut))
            StartCoroutine(SimulateClickRoutine());
    }

    // 실제 마우스 클릭과 동일한 PointerDown → (짧은 대기) → PointerUp/PointerClick 이벤트를 그대로 재현한다.
    // 버튼에 이미 설정된 눌림 색상/스프라이트 Transition이 그대로 재생되고, PointerClick이 onClick을 호출한다.
    private IEnumerator SimulateClickRoutine()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);

        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSeconds(0.08f);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(gameObject, eventData, ExecuteEvents.pointerClickHandler);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        hasShownTooltip = false; // 실제로 숨겼으므로, 다음 호버 시엔 내용이 지난번과 같아도 다시 Show()해야 함

        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide();
    }

    private void RefreshTooltip()
    {
        if (!hasData || string.IsNullOrEmpty(data.Title) || TooltipUI.Instance == null)
            return;

        bool unchanged = hasShownTooltip &&
            lastTooltipTitle == data.Title &&
            lastTooltipDescription == data.Description &&
            lastTooltipHasCost == data.HasCost &&
            lastTooltipOre == data.Ore &&
            lastTooltipGas == data.Gas &&
            lastTooltipPopulation == data.Population;

        if (unchanged)
            return; // 지난 프레임과 표시 내용이 완전히 같음 - 다시 그릴 필요 없음(결과는 동일)

        if (data.HasCost)
            TooltipUI.Instance.Show(rectTransform, data.Title, data.Description, data.Ore, data.Gas, data.Population);
        else
            TooltipUI.Instance.Show(rectTransform, data.Title, data.Description);

        hasShownTooltip = true;
        lastTooltipTitle = data.Title;
        lastTooltipDescription = data.Description;
        lastTooltipHasCost = data.HasCost;
        lastTooltipOre = data.Ore;
        lastTooltipGas = data.Gas;
        lastTooltipPopulation = data.Population;
    }
}