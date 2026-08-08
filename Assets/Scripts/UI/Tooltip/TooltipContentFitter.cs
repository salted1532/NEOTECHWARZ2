using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 툴팁 배경(root)의 세로 크기를 "지금 실제로 표시 중인 제목/설명 텍스트 분량"에 맞춰 매번 다시
// 계산해서 맞추는 전담 컴포넌트. TooltipUI는 텍스트/비용 표시 여부만 세팅하고 Fit()만 호출하면
// 되므로, 제목만/설명 포함/비용 포함/설명이 몇 줄이든 전부 이 하나의 로직으로 처리된다.
public class TooltipContentFitter : MonoBehaviour
{
    private RectTransform root;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private RectTransform[] costRowRects;

    [Header("여백 (기존 씬에 배치된 좌표를 역산한 기본값)")]
    [SerializeField] private float topPadding = 5f;            // root 위쪽 끝 ~ title 사이
    [SerializeField] private float titleDescriptionGap = 2f;   // title 아래쪽 ~ description 위쪽 사이
    [SerializeField] private float bottomPadding = 33f;        // 마지막 내용 아래 ~ root 아래쪽 끝 (비용 3줄이 이 안에 위치)
    [SerializeField] private float compactVerticalPadding = 10f; // 설명/비용이 전혀 없을 때 제목 위아래로 남길 여백 합
    [SerializeField] private float horizontalPadding = 20f;    // 좌우 여백 합 (텍스트 폭 자동 조절 시 사용, doc/0471)

    private Vector2[] defaultCostRowPositions;
    private float defaultRootHeight;
    private float defaultRootWidth;
    private bool isConfigured;

    // TooltipUI가 이미 인스펙터에서 갖고 있는 참조를 그대로 넘겨서 초기화한다 - 씬/프리팹에 새로
    // 필드를 연결할 필요가 없다.
    public void Configure(RectTransform root, TMP_Text titleText, TMP_Text descriptionText, GameObject[] costRows)
    {
        this.root = root;
        this.titleText = titleText;
        this.descriptionText = descriptionText;

        if (root != null)
        {
            defaultRootHeight = root.sizeDelta.y;
            defaultRootWidth = root.sizeDelta.x;
        }

        SetupAutoHeight(titleText);
        SetupAutoHeight(descriptionText);

        if (costRows != null)
        {
            costRowRects = new RectTransform[costRows.Length];
            defaultCostRowPositions = new Vector2[costRows.Length];

            for (int i = 0; i < costRows.Length; i++)
            {
                if (costRows[i] == null)
                    continue;

                costRowRects[i] = costRows[i].GetComponent<RectTransform>();
                defaultCostRowPositions[i] = costRowRects[i].anchoredPosition;
            }
        }

        isConfigured = true;
    }

    // 높이는 항상 내용에 맞춘다(PreferredSize) - 유니티/TMP가 이미 검증한 계산식을 그대로 활용해서,
    // 수동으로 GetPreferredValues를 계산하다 특정 상황에서 어긋나는 문제를 피한다. 폭은 기본값(고정,
    // Unconstrained)으로 붙여두고, 실제 폭 자동조절 여부는 Fit()에서 hasCost에 따라 매번 다시 정한다
    // (doc/0471 - 비용 3줄 아이콘은 고정 폭 레이아웃이라 폭까지 늘어나면 배치가 어긋나기 때문).
    private static void SetupAutoHeight(TMP_Text text)
    {
        if (text == null)
            return;

        ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = text.gameObject.AddComponent<ContentSizeFitter>();

        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static void SetHorizontalFit(TMP_Text text, bool autoWidth)
    {
        if (text == null)
            return;

        ContentSizeFitter fitter = text.GetComponent<ContentSizeFitter>();
        if (fitter != null)
            fitter.horizontalFit = autoWidth ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
    }

    // hasDescription/hasCost: 지금 이 툴팁에 그 요소가 실제로 표시되는지 (title/description의 text는
    // 이 호출 전에 이미 세팅돼 있어야 한다).
    public void Fit(bool hasDescription, bool hasCost)
    {
        if (!isConfigured || root == null || titleText == null)
            return;

        // 비용(Ore/Gas/Population 아이콘) 3줄은 root 기준 고정 좌표에 배치돼 있어서, 폭까지 늘리면
        // 그 배치가 어긋난다 - 비용이 없는 경우에만 폭도 내용에 맞춰 자동으로 늘어나게 한다(doc/0471).
        bool autoWidth = !hasCost;
        SetHorizontalFit(titleText, autoWidth);
        SetHorizontalFit(descriptionText, autoWidth);

        // ContentSizeFitter가 이번 프레임에 적용한 새 크기를 즉시 읽으려면 레이아웃을 강제로 갱신해야 한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.rectTransform);
        float titleHeight = titleText.rectTransform.rect.height;
        float titleWidth = titleText.rectTransform.rect.width;

        bool isCompact = !hasDescription && !hasCost;

        if (isCompact)
        {
            float compactWidth = Mathf.Max(defaultRootWidth, horizontalPadding + titleWidth);
            root.sizeDelta = new Vector2(compactWidth, titleHeight + compactVerticalPadding);
            SetY(titleText.rectTransform, 0f);
            SetX(titleText.rectTransform, 0f);
            return;
        }

        float descriptionHeight = 0f;
        float descriptionWidth = 0f;

        if (hasDescription && descriptionText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionText.rectTransform);
            descriptionHeight = descriptionText.rectTransform.rect.height;
            descriptionWidth = descriptionText.rectTransform.rect.width;
        }

        float contentHeight = titleHeight + (hasDescription ? titleDescriptionGap + descriptionHeight : 0f);
        float totalHeight = Mathf.Max(defaultRootHeight, topPadding + contentHeight + bottomPadding);

        float totalWidth = root.sizeDelta.x;
        if (autoWidth)
        {
            float contentWidth = Mathf.Max(titleWidth, descriptionWidth);
            totalWidth = Mathf.Max(defaultRootWidth, horizontalPadding + contentWidth);
        }

        root.sizeDelta = new Vector2(totalWidth, totalHeight);

        float halfHeight = totalHeight * 0.5f;
        float titleY = halfHeight - topPadding - titleHeight * 0.5f;
        SetY(titleText.rectTransform, titleY);
        if (autoWidth)
            SetX(titleText.rectTransform, 0f);

        if (hasDescription && descriptionText != null)
        {
            float descriptionY = titleY - titleHeight * 0.5f - titleDescriptionGap - descriptionHeight * 0.5f;
            SetY(descriptionText.rectTransform, descriptionY);
            if (autoWidth)
                SetX(descriptionText.rectTransform, 0f);
        }

        // 비용 3줄은 늘어난 높이의 절반만큼 아래로 밀어서, description과의 원래 간격을 그대로 유지한다.
        if (hasCost && costRowRects != null && defaultCostRowPositions != null)
        {
            float rootHeightDelta = totalHeight - defaultRootHeight;

            for (int i = 0; i < costRowRects.Length; i++)
            {
                if (costRowRects[i] == null)
                    continue;

                costRowRects[i].anchoredPosition =
                    defaultCostRowPositions[i] - new Vector2(0f, rootHeightDelta * 0.5f);
            }
        }
    }

    private static void SetY(RectTransform rect, float y)
    {
        Vector2 pos = rect.anchoredPosition;
        pos.y = y;
        rect.anchoredPosition = pos;
    }

    private static void SetX(RectTransform rect, float x)
    {
        Vector2 pos = rect.anchoredPosition;
        pos.x = x;
        rect.anchoredPosition = pos;
    }
}
