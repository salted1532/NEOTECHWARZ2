using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 툴팁 표시를 전담하는 싱글턴. 버튼(ProductionSlot)이 호버 시 Show()/Hide()를 직접 호출하고,
// 툴팁은 보여지는 동안 매 프레임 호버 중인 버튼의 상단 중앙 위치를 따라간다.
// (기존 TooltipTrigger/TooltipData 두 스크립트를 이 한 곳으로 통합)
public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance { get; private set; }

    [Header("Root / Position")]
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform canvasRect; // 툴팁이 속한 캔버스의 RectTransform
    [SerializeField] private Camera uiCamera;           // Canvas RenderMode가 Overlay면 비워둔다
    [SerializeField] private float verticalMargin = 8f; // 버튼 상단에서 얼마나 띄울지

    [Header("Title / Description")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Cost (유닛 생산 / 건물 건설 버튼에서만 표시)")]
    [SerializeField] private GameObject[] costRows; // Ore_image / Gas_image / population_image
    [SerializeField] private TMP_Text oreText;
    [SerializeField] private TMP_Text gasText;
    [SerializeField] private TMP_Text populationText;

    private bool isVisible;
    private RectTransform currentTarget;
    private TooltipContentFitter contentFitter;

    private void Awake()
    {
        Instance = this;

        // 툴팁 자신이 레이캐스트를 막으면 버튼 위에 떠 있는 동안 버튼의 OnPointerExit가 발생해
        // Show/Hide가 반복되며 깜빡이는 현상이 생긴다. 배경 이미지/텍스트를 전부 레이캐스트 대상에서 제외.
        if (root != null)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        // 크기 계산은 전담 컴포넌트(TooltipContentFitter)에 위임한다 - 씬/프리팹에 새로 필드를
        // 연결할 필요 없이, 이미 갖고 있는 참조를 그대로 넘겨서 코드로만 구성한다.
        contentFitter = gameObject.AddComponent<TooltipContentFitter>();
        contentFitter.Configure(root, titleText, descriptionText, costRows);

        Hide();
    }

    private void Update()
    {
        if (!isVisible || currentTarget == null)
            return;

        PositionAboveTarget(currentTarget);
    }

    // 자원 비용이 없는 일반 명령 버튼용 (이동/공격/정지 등 - 제목/설명만 표시)
    public void Show(RectTransform target, string title, string description)
    {
        ShowInternal(target, title, description, false, 0, 0, 0);
    }

    // 유닛 생산/건물 건설 버튼용 (제목/설명 + 광물/가스/인구수 비용 표시)
    public void Show(RectTransform target, string title, string description, int ore, int gas, int population)
    {
        ShowInternal(target, title, description, true, ore, gas, population);
    }

    public void Hide()
    {
        isVisible = false;
        currentTarget = null;

        if (root != null)
            root.gameObject.SetActive(false);
    }

    private void ShowInternal(RectTransform target, string title, string description, bool hasCost, int ore, int gas, int population)
    {
        if (root == null)
            return;

        isVisible = true;
        currentTarget = target;
        root.gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
            descriptionText.text = description;

        if (costRows != null)
        {
            foreach (GameObject row in costRows)
            {
                if (row != null)
                    row.SetActive(hasCost);
            }
        }

        if (hasCost)
        {
            if (oreText != null) oreText.text = ore.ToString();
            if (gasText != null) gasText.text = gas.ToString();
            if (populationText != null) populationText.text = population.ToString();
        }

        // 크기 계산은 TooltipContentFitter에 위임 - 제목만/설명 포함/비용 포함/설명이 몇 줄이든
        // 이 한 호출로 전부 처리된다.
        contentFitter.Fit(!string.IsNullOrEmpty(description), hasCost);

        // 위치 계산 전에 레이아웃을 즉시 갱신한다 (Fit() 내부에서 이미 필요한 만큼 갱신하지만,
        // 안전하게 한 번 더 - 기존 코드와 동일한 안전장치).
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        PositionAboveTarget(target);
    }

    // target(호버 중인 버튼)의 상단 중앙을 기준으로, 툴팁이 그 위에 뜨도록 위치를 계산한다.
    private void PositionAboveTarget(RectTransform target)
    {
        if (canvasRect == null || target == null)
            return;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners); // 0:bottom-left 1:top-left 2:top-right 3:bottom-right

        Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, topCenterWorld);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint))
            return;

        // 툴팁의 피벗이 중앙이라고 가정하고, 툴팁 절반 높이 + 여백만큼 위로 밀어 버튼과 겹치지 않게 한다.
        localPoint.y += root.rect.height * 0.5f + verticalMargin;

        root.anchoredPosition = localPoint;
    }
}
