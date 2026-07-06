using System;
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
        callback?.Invoke();
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