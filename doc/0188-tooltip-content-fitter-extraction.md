# 0188 - 툴팁 크기 조절을 전담 스크립트(TooltipContentFitter)로 분리 - 수정 제안

## 요청

"유닛이 squad_panel에 있는 경우에서 호버는 왜 크기가 안커져 이거 각 상황별로 커지는게 아니라
tooltip안에 글자가 들어갔을시 크기를 조절하는 스크립트로 따로 빼서 만들어줘 그래야 어느 상황에서도
잘 작동하도록" — [[tooltip-dynamic-height-for-long-description]](0187)에서 만든 "설명 있음/비용
있음/컴팩트" 상황별 분기 방식이 Squad_panel 유닛 호버에서는 실제로 커지지 않는 문제 발생. 상황별로
따로 처리하는 대신, 툴팁 안의 텍스트 양에 맞춰 크기를 조절하는 로직을 별도 스크립트로 분리해서
어떤 상황(제목만/설명 포함/비용 포함, 설명이 몇 줄이든)이든 동일하게 잘 작동하도록 해달라는 요청.

## 원인/재설계 방향

0187의 `ApplyDescriptionExpansion()`은 `TMP_Text.GetPreferredValues(width, 0f)`로 직접 필요한 높이를
계산한 뒤, Awake에서 캐싱해둔 기본값에 "차이(extraHeight)"만큼을 더하는 수동 계산 방식이었다.
계산 자체는 씬의 실제 좌표(0020/0187에서 확인한 pivot=(0.5,0.5) 좌표계)를 기준으로 역산해서 만든
것이라 이론상으론 맞지만, 실제로 Squad_panel 호버에서 커지지 않는 것을 보면 상황별 분기(컴팩트냐
아니냐)에 로직이 얽혀 있어 특정 경로에서 깨지기 쉬운 구조였다는 뜻이다. 사용자 요청대로 "글자가
들어갔을 때 크기를 맞추는" 책임을 완전히 별도 컴포넌트로 분리하고, 직접 `GetPreferredValues` 수식을
손으로 계산하는 대신 유니티가 이미 검증해서 제공하는 **`ContentSizeFitter` + TMP의
`ILayoutElement.preferredHeight`** 조합을 코드로 붙여서 사용한다 - 이 조합은 "텍스트 폭 고정, 높이만
내용에 맞게 자동 계산"을 하는 표준적인 방식이라 특정 조건에서 계산이 안 맞는 경우를 원천적으로
줄인다.

새 컴포넌트 `TooltipContentFitter`는:
- `TooltipUI`가 이미 인스펙터에서 들고 있는 참조(root/titleText/descriptionText/costRows)를 그대로
  넘겨받아 초기화한다(씬/프리팹에 새로 필드를 연결할 필요 없음 - `TooltipUI.Awake()`에서
  `gameObject.AddComponent<TooltipContentFitter>()`로 코드로만 붙임).
- title/description 텍스트 오브젝트에 `ContentSizeFitter`(가로=Unconstrained, 세로=PreferredSize)를
  코드로 추가해서, 유니티 레이아웃 시스템이 실제 필요한 높이를 계산하게 한다.
- `Fit(hasDescription, hasCost)` 한 메서드가 제목 유무/설명 유무/설명 줄 수/비용 유무를 전부 같은
  계산식(위→아래로 제목 → 설명 → 여백만큼 쌓아 올리는 방식)으로 처리한다 - 상황별로 다른 코드 경로를
  타지 않는다. 설명도 비용도 없는 "컴팩트"만 유일하게 별도 분기(제목 높이 + 여백만큼만 축소)로 남긴다
  (0020에서 의도한 "제목만 있을 때 배경을 줄이는" 동작은 그대로 유지해야 하므로).

## 계획된 코드 변경

### 1. `Assets/Scripts/UI/Tooltip/TooltipContentFitter.cs` (신규 파일)

Before: (파일 없음)

After:
```csharp
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

    private Vector2[] defaultCostRowPositions;
    private float defaultRootHeight;
    private bool isConfigured;

    // TooltipUI가 이미 인스펙터에서 갖고 있는 참조를 그대로 넘겨서 초기화한다 - 씬/프리팹에 새로
    // 필드를 연결할 필요가 없다.
    public void Configure(RectTransform root, TMP_Text titleText, TMP_Text descriptionText, GameObject[] costRows)
    {
        this.root = root;
        this.titleText = titleText;
        this.descriptionText = descriptionText;

        if (root != null)
            defaultRootHeight = root.sizeDelta.y;

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

    // 텍스트 폭은 그대로 두고(Unconstrained) 높이만 내용에 맞추도록(PreferredSize) ContentSizeFitter를
    // 코드로 붙인다 - 유니티/TMP가 이미 검증한 계산식을 그대로 활용해서, 수동으로 GetPreferredValues를
    // 계산하다 특정 상황에서 어긋나는 문제를 피한다.
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

    // hasDescription/hasCost: 지금 이 툴팁에 그 요소가 실제로 표시되는지 (title/description의 text는
    // 이 호출 전에 이미 세팅돼 있어야 한다).
    public void Fit(bool hasDescription, bool hasCost)
    {
        if (!isConfigured || root == null || titleText == null)
            return;

        // ContentSizeFitter가 이번 프레임에 적용한 새 높이를 즉시 읽으려면 레이아웃을 강제로 갱신해야 한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.rectTransform);
        float titleHeight = titleText.rectTransform.rect.height;

        bool isCompact = !hasDescription && !hasCost;

        if (isCompact)
        {
            root.sizeDelta = new Vector2(root.sizeDelta.x, titleHeight + compactVerticalPadding);
            SetY(titleText.rectTransform, 0f);
            return;
        }

        float descriptionHeight = 0f;

        if (hasDescription && descriptionText != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(descriptionText.rectTransform);
            descriptionHeight = descriptionText.rectTransform.rect.height;
        }

        float contentHeight = titleHeight + (hasDescription ? titleDescriptionGap + descriptionHeight : 0f);
        float totalHeight = Mathf.Max(defaultRootHeight, topPadding + contentHeight + bottomPadding);

        root.sizeDelta = new Vector2(root.sizeDelta.x, totalHeight);

        float halfHeight = totalHeight * 0.5f;
        float titleY = halfHeight - topPadding - titleHeight * 0.5f;
        SetY(titleText.rectTransform, titleY);

        if (hasDescription && descriptionText != null)
        {
            float descriptionY = titleY - titleHeight * 0.5f - titleDescriptionGap - descriptionHeight * 0.5f;
            SetY(descriptionText.rectTransform, descriptionY);
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
}
```

### 2. `Assets/Scripts/UI/Tooltip/TooltipUI.cs` - 크기 계산 로직을 전부 걷어내고 `TooltipContentFitter`에 위임

Before:
```csharp
    [Header("Compact (설명/비용 없이 제목 한 줄만 표시할 때)")]
    [SerializeField] private float compactVerticalPadding = 10f; // 제목 높이 위아래로 남길 여백 합

    private bool isVisible;
    private RectTransform currentTarget;

    // Show()가 배경 크기/제목 위치를 건드리므로, 설명/비용이 있는 원래 레이아웃으로 되돌릴 때 쓸 기본값을 캐싱해둔다.
    private Vector2 defaultRootSize;
    private Vector2 defaultTitlePosition;
    private Vector2 defaultDescriptionSize;
    private Vector2[] defaultCostRowPositions;

    private void Awake()
    {
        Instance = this;

        // 툴팁 자신이 레이캐스트를 막으면 버튼 위에 떠 있는 동안 버튼의 OnPointerExit가 발생해
        // Show/Hide가 반복되며 깜빡이는 현상이 생긴다. 배경 이미지/텍스트를 전부 레이캐스트 대상에서 제외.
        if (root != null)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            defaultRootSize = root.sizeDelta;
        }

        if (titleText != null)
            defaultTitlePosition = titleText.rectTransform.anchoredPosition;

        if (descriptionText != null)
            defaultDescriptionSize = descriptionText.rectTransform.sizeDelta;

        if (costRows != null)
        {
            defaultCostRowPositions = new Vector2[costRows.Length];
            for (int i = 0; i < costRows.Length; i++)
            {
                if (costRows[i] != null)
                    defaultCostRowPositions[i] = costRows[i].GetComponent<RectTransform>().anchoredPosition;
            }
        }

        Hide();
    }
```

After:
```csharp
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
```

Before:
```csharp
        // 설명/비용 없이 제목 한 줄만 있는 경우(예: Attack Damge/Armor 호버) 배경을 제목 높이에 맞게 줄이고,
        // 그 외(기존 명령/생산 버튼 툴팁)에는 원래 크기·위치를 그대로 유지한다.
        bool isCompact = string.IsNullOrEmpty(description) && !hasCost;
        ApplyCompactLayout(isCompact);

        // 텍스트가 바뀌어 크기가 달라질 수 있으니, 위치 계산 전에 레이아웃을 즉시 갱신한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        PositionAboveTarget(target);
    }

    private void ApplyCompactLayout(bool isCompact)
    {
        if (root == null || titleText == null)
            return;

        if (!isCompact)
        {
            ApplyDescriptionExpansion();
            return;
        }

        RectTransform titleRect = titleText.rectTransform;
        float compactHeight = titleRect.sizeDelta.y + compactVerticalPadding;

        root.sizeDelta = new Vector2(defaultRootSize.x, compactHeight);
        titleRect.anchoredPosition = new Vector2(defaultTitlePosition.x, 0f);
    }

    // description이 기본 칸 높이(defaultDescriptionSize.y)보다 더 필요하면(줄 수가 많으면) 그 차이만큼
    // root/title/costRows를 "아래쪽으로만" 늘려서 배경 밖으로 글씨가 삐져나오지 않게 한다.
    //
    // 전부 pivot=(0.5,0.5) 기준 좌표라, root 높이를 늘리면 위아래로 절반씩 같이 커진다. 그래서:
    // - title은 늘어난 만큼의 절반(extraHeight/2)만큼 위로 밀어서 "root 위쪽 여백 + title" 간격을 그대로 유지.
    // - description은 늘어난 만큼(extraHeight)을 그대로 높이에 더한다 - 여기서도 위아래로 절반씩 커지는데,
    //   위로 커지는 절반이 title이 위로 밀리는 것과 정확히 상쇄돼서 anchoredPosition은 안 바꿔도 된다
    //   (title 하단 ~ description 상단 간격이 그대로 유지됨).
    // - costRows는 description보다 아래에 있으므로 늘어난 만큼(extraHeight) 그대로 아래로 밀어야
    //   description과 겹치지 않는다.
    private void ApplyDescriptionExpansion()
    {
        float extraHeight = 0f;

        if (descriptionText != null)
        {
            float requiredHeight = descriptionText.GetPreferredValues(defaultDescriptionSize.x, 0f).y;
            extraHeight = Mathf.Max(0f, requiredHeight - defaultDescriptionSize.y);

            descriptionText.rectTransform.sizeDelta =
                new Vector2(defaultDescriptionSize.x, defaultDescriptionSize.y + extraHeight);
        }

        root.sizeDelta = new Vector2(defaultRootSize.x, defaultRootSize.y + extraHeight);
        titleText.rectTransform.anchoredPosition = defaultTitlePosition + new Vector2(0f, extraHeight * 0.5f);

        if (costRows != null && defaultCostRowPositions != null)
        {
            for (int i = 0; i < costRows.Length; i++)
            {
                if (costRows[i] == null)
                    continue;

                costRows[i].GetComponent<RectTransform>().anchoredPosition =
                    defaultCostRowPositions[i] + new Vector2(0f, -extraHeight);
            }
        }
    }
```

After:
```csharp
        // 크기 계산은 TooltipContentFitter에 위임 - 제목만/설명 포함/비용 포함/설명이 몇 줄이든
        // 이 한 호출로 전부 처리된다.
        contentFitter.Fit(!string.IsNullOrEmpty(description), hasCost);

        // 위치 계산 전에 레이아웃을 즉시 갱신한다 (Fit() 내부에서 이미 필요한 만큼 갱신하지만,
        // 안전하게 한 번 더 - 기존 코드와 동일한 안전장치).
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        PositionAboveTarget(target);
    }
```

`ApplyCompactLayout`/`ApplyDescriptionExpansion` 메서드와 `compactVerticalPadding`/`defaultRootSize`/
`defaultTitlePosition`/`defaultDescriptionSize`/`defaultCostRowPositions` 필드는 전부 삭제(새
컴포넌트로 이동).

## 요약/영향받는 파일

- 신규 파일: `Assets/Scripts/UI/Tooltip/TooltipContentFitter.cs`.
- 수정 파일: `Assets/Scripts/UI/Tooltip/TooltipUI.cs` (크기 계산 책임을 전부 걷어내고 새 컴포넌트에
  위임, 관련 필드/메서드 삭제).
- 씬/프리팹 수정 불필요 - `TooltipContentFitter`는 `TooltipUI.Awake()`에서 코드로
  `AddComponent`되고, 이미 인스펙터에 연결된 `TooltipUI`의 참조(root/titleText/descriptionText/
  costRows)를 그대로 넘겨받는다.
- 동작 변화:
  - 제목만 있는 컴팩트 툴팁(Attack Damage/Armor 호버 등): 기존과 동일하게 제목 높이 + 여백만큼만
    축소.
  - 설명이 짧은 기존 명령/생산 버튼 툴팁: `Mathf.Max(defaultRootHeight, ...)`로 원래 크기(100) 밑으로
    줄어들지 않아 기존과 동일하게 보임.
  - 설명이 긴 툴팁(Squad_panel의 3줄 안내문 등): `ContentSizeFitter`가 실제 필요한 높이를 계산하고,
    그 값을 기준으로 배경/제목/비용줄이 함께 재배치되어 이제 정상적으로 커진다.
  - 비용이 있는 생산 버튼의 설명이 나중에 길어지는 경우도 동일한 로직으로 자동 대응(비용 3줄은
    늘어난 만큼 아래로 이동).

## 확인 필요

이대로 진행해도 될까요?
