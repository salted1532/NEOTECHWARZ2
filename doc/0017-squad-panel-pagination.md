# 0017 - Squad Panel 페이지네이션 (최대 60마리)

**날짜:** 2026-07-07

## 요청 내용
여러 유닛 선택 시 보여지는 Squad Panel이 기존에는 0~11번(12마리) 슬롯만 표시했음. Squad Panel에 page1~page5 버튼을 UI 상 추가해뒀으니, 선택된 유닛을 12마리씩 페이지로 나눠 각 페이지 버튼을 누르면 해당 12마리를 슬롯에 보여주도록 요청. 예: 36마리 선택 시 1~3페이지 버튼만 활성화. 최대 60마리(12 x 5페이지)까지 지원. (이후 요청으로 페이지 버튼은 `interactable` 토글이 아니라 `SetActive`로 완전히 숨기도록 수정됨.)

## 조사 내용
- `Assets/Scripts/UI/UIController.cs`의 `ShowSquadPanel`/`HideSquadPanel`이 Squad Panel을 담당하며, `squadSlots`(12개 `ProductionSlot`) 배열만 있고 페이지 개념이 없었음.
- `Assets/Scripts/System/RTSUnitController.cs`의 `UpdateUI()`가 매 프레임(`Update()`에서 호출) `uIController.ShowSquadPanel(selectedUnitList, ClickSelectUnit)`을 호출 — 즉 페이지 상태를 매 프레임 리셋하면 안 되고, "선택 내용이 실제로 바뀐 경우"에만 페이지를 0으로 되돌려야 함.
- `selectedUnitList`는 재할당 없이 계속 재사용되는 동일 `List<UnitController>` 참조라 참조 비교로는 선택 변경을 감지할 수 없어, 내용(snapshot) 비교로 구현.
- 선택 인원수 자체에는 기존에 12마리 제한이 없음(드래그 선택 등에서 상한 없음) — 표시 로직만 손보면 됨.

## 코드 변경

### `Assets/Scripts/UI/UIController.cs` — 필드

**기존 코드**
```csharp
[Header("Squad Panel (SelectInfo)")]
[SerializeField] private GameObject squadPanel;
[SerializeField] private ProductionSlot[] squadSlots; // 인스펙터에서 12개(slot0~11) 연결

private void Start()
{
    rtsUnitController = FindFirstObjectByType<RTSUnitController>();

    ClearPanel();
    HideProductionUI();
    HideInfoPanel();
    HideSquadPanel();
}
```

**변경 코드**
```csharp
[Header("Squad Panel (SelectInfo)")]
[SerializeField] private GameObject squadPanel;
[SerializeField] private ProductionSlot[] squadSlots; // 인스펙터에서 12개(slot0~11) 연결
[SerializeField] private Button[] squadPageButtons; // 인스펙터에서 5개(page1~5) 순서대로 연결, 한 페이지당 12마리 (최대 60마리)

private const int SquadUnitsPerPage = 12;

private readonly List<UnitController> squadUnitsSnapshot = new List<UnitController>();
private Action<UnitController> squadOnSelectUnit;
private int squadCurrentPage;

private void Start()
{
    rtsUnitController = FindFirstObjectByType<RTSUnitController>();

    ClearPanel();
    HideProductionUI();
    HideInfoPanel();
    HideSquadPanel();
    SetupSquadPageButtons();
}
```

### `ShowSquadPanel` / `HideSquadPanel`

**기존 코드**
```csharp
public void ShowSquadPanel(IReadOnlyList<UnitController> units, Action<UnitController> onSelectUnit)
{
    HideInfoPanel();
    HideProductionUI();

    if (squadPanel != null)
        squadPanel.SetActive(true);

    int shownCount = Mathf.Min(units.Count, squadSlots.Length);

    for (int i = 0; i < squadSlots.Length; i++)
    {
        if (squadSlots[i] == null)
            continue;

        if (i < shownCount)
        {
            UnitController unit = units[i];
            squadSlots[i].SetData(new CommandButtonData(unit.GetIcon(), () => onSelectUnit(unit)));
        }
        else
        {
            squadSlots[i].Clear();
        }
    }
}

public void HideSquadPanel()
{
    if (squadPanel != null)
        squadPanel.SetActive(false);

    for (int i = 0; i < squadSlots.Length; i++)
        squadSlots[i]?.Clear();
}
```

**변경 코드**
```csharp
public void ShowSquadPanel(IReadOnlyList<UnitController> units, Action<UnitController> onSelectUnit)
{
    HideInfoPanel();
    HideProductionUI();

    if (squadPanel != null)
        squadPanel.SetActive(true);

    squadOnSelectUnit = onSelectUnit;

    if (!SquadUnitsEqual(squadUnitsSnapshot, units))
    {
        squadUnitsSnapshot.Clear();
        squadUnitsSnapshot.AddRange(units);
        squadCurrentPage = 0;
    }

    int pageCount = Mathf.Max(1, Mathf.CeilToInt((float)squadUnitsSnapshot.Count / SquadUnitsPerPage));
    squadCurrentPage = Mathf.Clamp(squadCurrentPage, 0, pageCount - 1);

    UpdateSquadPageButtons(pageCount);
    RefreshSquadSlots();
}

public void SelectSquadPage(int page)
{
    if (squadPageButtons == null || page < 0 || page >= squadPageButtons.Length)
        return;

    if (!squadPageButtons[page].gameObject.activeSelf)
        return;

    squadCurrentPage = page;
    RefreshSquadSlots();
}

private void RefreshSquadSlots()
{
    int startIndex = squadCurrentPage * SquadUnitsPerPage;

    for (int i = 0; i < squadSlots.Length; i++)
    {
        if (squadSlots[i] == null)
            continue;

        int unitIndex = startIndex + i;

        if (unitIndex < squadUnitsSnapshot.Count)
        {
            UnitController unit = squadUnitsSnapshot[unitIndex];
            squadSlots[i].SetData(new CommandButtonData(unit.GetIcon(), () => squadOnSelectUnit(unit)));
        }
        else
        {
            squadSlots[i].Clear();
        }
    }
}

// 필요한 페이지 버튼만 SetActive(true)로 노출 (예: 36마리면 page1~3만 보임)
private void UpdateSquadPageButtons(int pageCount)
{
    if (squadPageButtons == null)
        return;

    for (int i = 0; i < squadPageButtons.Length; i++)
    {
        if (squadPageButtons[i] == null)
            continue;

        squadPageButtons[i].gameObject.SetActive(i < pageCount);
    }
}

private void SetupSquadPageButtons()
{
    if (squadPageButtons == null)
        return;

    for (int i = 0; i < squadPageButtons.Length; i++)
    {
        if (squadPageButtons[i] == null)
            continue;

        int page = i;
        squadPageButtons[i].onClick.AddListener(() => SelectSquadPage(page));
    }
}

private static bool SquadUnitsEqual(List<UnitController> current, IReadOnlyList<UnitController> incoming)
{
    if (current.Count != incoming.Count)
        return false;

    for (int i = 0; i < current.Count; i++)
    {
        if (current[i] != incoming[i])
            return false;
    }

    return true;
}

public void HideSquadPanel()
{
    if (squadPanel != null)
        squadPanel.SetActive(false);

    for (int i = 0; i < squadSlots.Length; i++)
        squadSlots[i]?.Clear();

    squadUnitsSnapshot.Clear();
    squadCurrentPage = 0;
}
```

## 요약
- 12마리 고정 표시 → 12마리 × 5페이지(최대 60마리) 페이지네이션으로 확장.
- 매 프레임 호출되는 구조라, 선택 내용이 실제로 바뀔 때만 페이지를 0으로 리셋하도록 스냅샷 비교(`SquadUnitsEqual`) 추가.
- 페이지 버튼은 처음엔 `interactable` 토글로 구현했다가, 같은 세션 내 후속 요청으로 `SetActive` 완전 숨김으로 수정됨(위 코드는 최종본).

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
