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

    private RectTransform rectTransform;
    private Action callback;
    private UIController.CommandButtonData data;
    private bool hasData;
    private KeyCode shortcut = KeyCode.None; // 이 슬롯을 대신 "누르는" 키보드 단축키 (없으면 KeyCode.None)

    private void Awake()
    {
        rectTransform = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
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

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (button != null)
            button.interactable = false;

        gameObject.SetActive(false);
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
        if (!hasData || string.IsNullOrEmpty(data.Title) || TooltipUI.Instance == null)
            return;

        if (data.HasCost)
            TooltipUI.Instance.Show(rectTransform, data.Title, data.Description, data.Ore, data.Gas, data.Population);
        else
            TooltipUI.Instance.Show(rectTransform, data.Title, data.Description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipUI.Instance != null)
            TooltipUI.Instance.Hide();
    }
}