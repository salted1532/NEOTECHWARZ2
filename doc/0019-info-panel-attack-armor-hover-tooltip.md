# 0019 - Info Panel 공격력/방어력 호버 툴팁

**날짜:** 2026-07-07

## 요청 내용
UIController에 info_panel 부분에 AttackDamage/Armor 이미지를 추가해서, 마우스 호버 시 유닛의 공격력과 방어력이 "Attack Damge : (숫자)", "Armor : (숫자)" 형식으로 출력되도록 요청.

## 조사 내용
- 이미 `Assets/Scripts/UI/Tooltip/TooltipUI.cs`(싱글턴, `Show(RectTransform, title, description)` / `Hide()`)와 `ProductionSlot.cs`(`IPointerEnterHandler`/`IPointerExitHandler` 구현)로 호버 툴팁 인프라가 갖춰져 있음 — 이를 재사용.
- `AttackDamageImage`/`ArmorImage`는 `ProductionSlot`처럼 별도 컴포넌트를 이미지 GameObject에 직접 붙이는 방식 대신, `UIController`가 `Start()`에서 `UnityEngine.EventSystems.EventTrigger`를 이미지 GameObject에 런타임으로 붙여 호버를 감지하도록 구현 — 사용자가 인스펙터에서 이미지 참조만 연결하면 되도록.
- `UIController.ShowInfoPanel(icon, name, health)`는 유닛/건물/자원 모두 공유하는 진입점이라, 공격력/방어력을 항상 받게 시그니처를 바꾸면 건물 쪽 호출부도 다 고쳐야 함 → 3-파라미터 기존 오버로드는 유지(내부적으로 0, 0으로 위임), 유닛 전용 5-파라미터 오버로드를 새로 추가.

## 코드 변경

### `Assets/Scripts/UI/UIController.cs` — using 추가

**기존 코드**
```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
```

**변경 코드**
```csharp
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
```

### 필드 추가

**기존 코드**
```csharp
    [Header("Info Panel (SelectInfo)")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TextMeshProUGUI infoNameText;
    [SerializeField] private TextMeshProUGUI infoHpText;

    private HealthManager infoBoundHealth; // 현재 Info_panel이 구독 중인 대상 (선택이 바뀌면 구독 해제 후 갈아끼움)
```

**변경 코드**
```csharp
    [Header("Info Panel (SelectInfo)")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Image infoIcon;
    [SerializeField] private TextMeshProUGUI infoNameText;
    [SerializeField] private TextMeshProUGUI infoHpText;
    [SerializeField] private Image attackDamageImage; // 호버 시 "Attack Damge : N" 툴팁 표시
    [SerializeField] private Image armorImage;         // 호버 시 "Armor : N" 툴팁 표시

    private HealthManager infoBoundHealth; // 현재 Info_panel이 구독 중인 대상 (선택이 바뀌면 구독 해제 후 갈아끼움)
    private int infoAttackDamage;
    private int infoArmor;
```

### `ShowInfoPanel` 오버로드 + 호버 툴팁 연결

**기존 코드**
```csharp
    public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health)
    {
        HideSquadPanel();

        if (infoPanel != null)
            infoPanel.SetActive(true);

        if (infoIcon != null)
        {
            infoIcon.sprite = icon;
            infoIcon.enabled = icon != null;
        }

        if (infoNameText != null)
            infoNameText.text = unitName;

        BindInfoHealth(health);
    }
```

**변경 코드**
```csharp
    // 건물/자원 등 공격력·방어력이 없는 대상용 (0으로 표시됨)
    public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health)
    {
        ShowInfoPanel(icon, unitName, health, 0, 0);
    }

    // 유닛 선택 시 공격력/방어력도 함께 받아 저장해둔다.
    public void ShowInfoPanel(Sprite icon, string unitName, HealthManager health, int attackDamage, int armor)
    {
        HideSquadPanel();

        if (infoPanel != null)
            infoPanel.SetActive(true);

        if (infoIcon != null)
        {
            infoIcon.sprite = icon;
            infoIcon.enabled = icon != null;
        }

        if (infoNameText != null)
            infoNameText.text = unitName;

        infoAttackDamage = attackDamage;
        infoArmor = armor;

        BindInfoHealth(health);
    }

    // attackDamageImage/armorImage에 EventTrigger를 붙여, 호버 시 TooltipUI로 현재 선택 유닛의
    // 공격력/방어력을 "Attack Damge : N" / "Armor : N" 형식으로 보여준다.
    private void SetupInfoStatHoverTooltips()
    {
        AddStatHoverTooltip(attackDamageImage, () => $"Attack Damge : {infoAttackDamage}");
        AddStatHoverTooltip(armorImage, () => $"Armor : {infoArmor}");
    }

    private void AddStatHoverTooltip(Image image, Func<string> textProvider)
    {
        if (image == null)
            return;

        EventTrigger trigger = image.gameObject.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = image.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => TooltipUI.Instance?.Show(image.rectTransform, textProvider(), string.Empty));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => TooltipUI.Instance?.Hide());
        trigger.triggers.Add(exitEntry);
    }
```

### `Start()`에서 훅 등록

**기존 코드**
```csharp
        HideSquadPanel();
        SetupSquadPageButtons();
    }
```

**변경 코드**
```csharp
        HideSquadPanel();
        SetupSquadPageButtons();
        SetupInfoStatHoverTooltips();
    }
```

### `Assets/Scripts/System/RTSUnitController.cs` — 유닛 단일 선택 시 `ShowInfoPanel` 호출

**기존 코드**
```csharp
                    UnitController unit = selectedUnitList[0];
                    uIController.ShowInfoPanel(unit.GetIcon(), GetUnitName(unit.GetUnitID()), unit.GetComponent<HealthManager>());
```

**변경 코드**
```csharp
                    UnitController unit = selectedUnitList[0];
                    uIController.ShowInfoPanel(unit.GetIcon(), GetUnitName(unit.GetUnitID()), unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor());
```

## 남은 작업 (당시 기준)
- Unity 에디터에서 `UIController`의 `Attack Damage Image` / `Armor Image` 필드에 실제 이미지 오브젝트를 드래그해서 연결해야 동작함.

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
