# 0020 - TooltipUI 배경 크기를 내용에 맞게 축소 (Compact 모드)

**날짜:** 2026-07-07

## 요청 내용
[0019](0019-info-panel-attack-armor-hover-tooltip.md)에서 만든 공격력/방어력 호버 툴팁은 title 텍스트에 "Attack Damge : N" 한 줄만 채워 넣고 description은 비워두는 방식인데, 기존 Tooltip 배경 이미지(`ToolTip` GameObject의 `Image`, `TooltipUI.root`와 동일 오브젝트)는 항상 고정 크기(200x100)라 아래쪽에 불필요한 여백이 남음. 실제로 채워진 내용(제목 한 줄)만큼만 배경 크기를 줄여달라는 요청. "적용해보고 이상하면 되돌려달라고 하겠다"는 전제로 진행.

## 조사 내용
- `Assets/Scenes/SampleScene.unity`에서 `TooltipUI` 컴포넌트(및 `root`로 연결된 `ToolTip` GameObject)의 실제 배치를 확인:
  - `root`(배경 Image와 동일 GameObject) `sizeDelta = (200, 100)`, pivot/anchor 모두 (0.5, 0.5).
  - 자식들은 전부 `root` pivot 기준 절대 `anchoredPosition`으로 배치되어 있고 **Layout Group/ContentSizeFitter는 전혀 없음** (title `(15, 35)` size `(200,20)`, description `(15, 3)` size `(200,40)`, 비용 3줄이 `y=-30` 부근에 배치).
  - 즉 `root.sizeDelta`만 줄이면 자식들의 고정 좌표는 그대로라 제목이 새 배경 바깥으로 삐져나가는 문제가 생김 — 단순 크기 조절만으로는 안 됨.
- 씬에 Layout Group을 새로 붙이는 방식은 기존에 이미 잘 동작 중인 일반 명령/생산 버튼 툴팁(제목+설명[+비용])의 배치를 깨뜨릴 위험이 커서 배제. 대신 `TooltipUI.cs` 코드에서만 처리하는 방식을 선택.

## 코드 변경

### `Assets/Scripts/UI/Tooltip/TooltipUI.cs` — 필드 + `Awake()`

**기존 코드**
```csharp
    [Header("Cost (유닛 생산 / 건물 건설 버튼에서만 표시)")]
    [SerializeField] private GameObject[] costRows; // Ore_image / Gas_image / population_image
    [SerializeField] private TMP_Text oreText;
    [SerializeField] private TMP_Text gasText;
    [SerializeField] private TMP_Text populationText;

    private bool isVisible;
    private RectTransform currentTarget;

    private void Awake()
    {
        Instance = this;

        if (root != null)
        {
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
        }

        Hide();
    }
```

**변경 코드**
```csharp
    [Header("Cost (유닛 생산 / 건물 건설 버튼에서만 표시)")]
    [SerializeField] private GameObject[] costRows; // Ore_image / Gas_image / population_image
    [SerializeField] private TMP_Text oreText;
    [SerializeField] private TMP_Text gasText;
    [SerializeField] private TMP_Text populationText;

    [Header("Compact (설명/비용 없이 제목 한 줄만 표시할 때)")]
    [SerializeField] private float compactVerticalPadding = 10f; // 제목 높이 위아래로 남길 여백 합

    private bool isVisible;
    private RectTransform currentTarget;

    // Show()가 배경 크기/제목 위치를 건드리므로, 설명/비용이 있는 원래 레이아웃으로 되돌릴 때 쓸 기본값을 캐싱해둔다.
    private Vector2 defaultRootSize;
    private Vector2 defaultTitlePosition;

    private void Awake()
    {
        Instance = this;

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

### `ShowInternal()` + 신규 `ApplyCompactLayout()`

**기존 코드**
```csharp
        if (hasCost)
        {
            if (oreText != null) oreText.text = ore.ToString();
            if (gasText != null) gasText.text = gas.ToString();
            if (populationText != null) populationText.text = population.ToString();
        }

        // 텍스트가 바뀌어 크기가 달라질 수 있으니, 위치 계산 전에 레이아웃을 즉시 갱신한다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        PositionAboveTarget(target);
    }
```

**변경 코드**
```csharp
        if (hasCost)
        {
            if (oreText != null) oreText.text = ore.ToString();
            if (gasText != null) gasText.text = gas.ToString();
            if (populationText != null) populationText.text = population.ToString();
        }

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

## 요약
- `description`이 비고 비용도 없는 경우("Attack Damge : N" 같은 제목 한 줄짜리)만 컴팩트 모드로 판단해 제목을 세로 중앙으로 옮기고 배경 높이를 줄임.
- 설명/비용이 있는 기존 명령·생산 버튼 툴팁은 캐싱해둔 기본값으로 항상 복원되므로 기존 모양 그대로 유지.
- 되돌리는 방법: `Assets/Scripts/UI/Tooltip/TooltipUI.cs` 한 파일만 되돌리면 이번 변경 전 상태(고정 200x100 배경)로 복원됨.

## 변경된 파일
- `Assets/Scripts/UI/Tooltip/TooltipUI.cs`
