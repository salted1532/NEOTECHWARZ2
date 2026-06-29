using System;
using UnityEngine;
using UnityEngine.UI;

public class ProductionSlot : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;

    private Action callback;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// 슬롯에 데이터 표시
    /// </summary>
    public void SetData(UIController.CommandButtonData data)
    {
        gameObject.SetActive(true);

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
    /// 슬롯 초기화
    /// </summary>
    public void Clear()
    {
        callback = null;

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
}