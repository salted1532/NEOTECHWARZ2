# 0187 - 툴팁 배경이 설명 텍스트 양에 맞게 세로로 늘어나도록 - 수정 제안

## 요청

"squad패널에 각 버튼 호버시 나오는거 좋은데 설명칸 글씨가 많이서 튀어나오네 toolTip 자체 크기를
해당하는 상황에서 크기를 글자양에 맞게 크기가 커지도록 해줬으면 좋겠어" — [[squad-panel-shift-ctrl-click-tooltip-proposal]](0186)에서 추가한 3줄짜리 안내문("Click: .../Shift+Click: .../Ctrl+Click: ...")이
기존 고정 크기 툴팁 배경보다 커서 글씨가 배경 밖으로 삐져나온다. 설명(description) 텍스트 양에
맞춰 툴팁 배경 크기가 늘어나도록 해달라는 요청.

## 원인

`Assets/Scripts/UI/Tooltip/TooltipUI.cs`는 Layout Group/ContentSizeFitter 없이 전부 좌표를 코드로
계산하는 방식이다(0020에서 이미 이 구조로 확정). `Assets/Scenes/SampleScene.unity`에서 실제 값 확인:

- `root`(배경): `anchorMin=anchorMax=pivot=(0.5,0.5)`, `sizeDelta=(200,100)`.
- `DescriptionText`: `anchorMin=anchorMax=pivot=(0.5,0.5)`, `anchoredPosition=(15,3)`, `sizeDelta=(200,40)`,
  `m_overflowMode: 0`(Overflow, 텍스트 클리핑 없음), `m_VerticalAlignment: 256`(Top-정렬).
- `TitleText`: `anchoredPosition=(15,35)`.
- 비용 3줄(Ore/Gas/population 이미지): `y=-30` 부근.

`description`이 Top-정렬 + Overflow라서, 텍스트가 `sizeDelta.y=40`(대략 1~2줄)보다 많아지면 그
칸 밑으로(그리고 `root`의 고정 배경 바깥으로) 그대로 삐져나온다 - 지금까지의 설명들은 전부 1~2줄
안에 들어가서 문제가 없었을 뿐이다. `ApplyCompactLayout()`은 "설명이 아예 없을 때"(제목 한 줄만)만
크기를 줄이는 로직만 있고, "설명이 원래보다 많을 때" 늘리는 로직은 없다.

## 계획된 코드 변경

전부 `Assets/Scripts/UI/Tooltip/TooltipUI.cs` 한 파일.

### 1. 기본값 캐싱 확장 (`description`/비용 3줄의 원래 위치·크기도 캐싱)

Before:
```csharp
    private bool isVisible;
    private RectTransform currentTarget;

    // Show()가 배경 크기/제목 위치를 건드리므로, 설명/비용이 있는 원래 레이아웃으로 되돌릴 때 쓸 기본값을 캐싱해둔다.
    private Vector2 defaultRootSize;
    private Vector2 defaultTitlePosition;

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

        Hide();
    }
```

After:
```csharp
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

### 2. `ApplyCompactLayout()`의 "설명 있음" 분기를 고정 복원 대신 동적 확장으로 교체

Before:
```csharp
    private void ApplyCompactLayout(bool isCompact)
    {
        if (root == null || titleText == null)
            return;

        if (!isCompact)
        {
            root.sizeDelta = defaultRootSize;
            titleText.rectTransform.anchoredPosition = defaultTitlePosition;
            return;
        }

        RectTransform titleRect = titleText.rectTransform;
        float compactHeight = titleRect.sizeDelta.y + compactVerticalPadding;

        root.sizeDelta = new Vector2(defaultRootSize.x, compactHeight);
        titleRect.anchoredPosition = new Vector2(defaultTitlePosition.x, 0f);
    }
```

After:
```csharp
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

`ShowInternal()`은 수정 없음 - 이미 `descriptionText.text = description;`을 설정한 다음
`ApplyCompactLayout(isCompact)` → `LayoutRebuilder.ForceRebuildLayoutImmediate(root)` →
`PositionAboveTarget(target)` 순서로 호출하고 있어서, 새 로직이 그 흐름에 그대로 끼워진다.
`PositionAboveTarget()`도 이미 `root.rect.height`를 매번 다시 읽으므로 늘어난 높이를 자동으로
반영한다 - 수정 불필요.

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/UI/Tooltip/TooltipUI.cs` (필드 캐싱 확장 + `ApplyDescriptionExpansion()`
  신규 + `ApplyCompactLayout()`의 "설명 있음" 분기 교체).
- 동작 변화:
  - 설명이 짧을 때(`requiredHeight <= 40`, 기존 대부분의 이동/공격/생산 툴팁)는 `extraHeight=0`이라
    지금과 완전히 동일하게 보인다.
  - 설명이 길 때(Squad_panel의 3줄 안내문 등)는 배경/제목/비용 3줄이 함께 아래로 늘어나 글씨가 배경
    밖으로 삐져나오지 않는다.
  - 비용이 있는 생산 버튼(광물/가스/인구수 텍스트)의 설명이 나중에 길어지는 경우에도 같은 로직으로
    자동 대응된다(현재는 전부 짧아서 체감 차이 없음).
  - "설명 없음"(Compact, 예: Attack Damage/Armor 호버) 케이스는 완전히 별개 분기라 영향 없음.

## 확인 필요

이대로 진행해도 될까요?
