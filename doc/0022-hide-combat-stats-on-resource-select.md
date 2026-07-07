# 0022 - 자원(Ore/Gas) 선택 시 AttackDamageImage/ArmorImage 숨김

**날짜:** 2026-07-08

## 요청 내용
Ore/Gas 같은 자원 노드를 선택했을 때는 Info_panel의 `AttackDamageImage`/`ArmorImage`가 아예 보이지 않도록 해달라는 요청.

## 조사 내용
- Info_panel 진입점은 세 가지: `ShowInfoPanel(icon, name, health)`(건물/자원 겸용 3인자, 내부적으로 5인자에 0/0 위임), `ShowInfoPanel(icon, name, health, attackDamage, armor)`(유닛/적 전용 5인자, [0019](0019-info-panel-attack-armor-hover-tooltip.md)/[0021](0021-enemycontroller-armor-attackdamage.md)), `ShowResourceInfoPanel(icon, name, remainingAmount)`(자원 전용, 체력 대신 채취량 표시).
- 지금까지는 `attackDamageImage`/`armorImage`가 항상 활성 상태로 남아있어서, 자원을 선택해도 이전에 표시되던 유닛의 공격력/방어력 아이콘이 그대로 보이는(또는 0으로 보이는) 문제가 있었음.

## 코드 변경

### `Assets/Scripts/UI/UIController.cs` — `ShowInfoPanel` 5-인자 오버로드에 표시 처리 추가

**기존 코드**
```csharp
        infoAttackDamage = attackDamage;
        infoArmor = armor;

        BindInfoHealth(health);
    }
```

**변경 코드**
```csharp
        infoAttackDamage = attackDamage;
        infoArmor = armor;

        SetCombatStatsVisible(true);
        BindInfoHealth(health);
    }

    // 자원(Ore/Gas) 선택 시처럼 공격력/방어력 개념이 없는 대상에서는 두 아이콘 자체를 숨긴다.
    private void SetCombatStatsVisible(bool visible)
    {
        if (attackDamageImage != null)
            attackDamageImage.gameObject.SetActive(visible);

        if (armorImage != null)
            armorImage.gameObject.SetActive(visible);
    }
```

### `ShowResourceInfoPanel`에서 숨김 처리

**기존 코드**
```csharp
        if (infoNameText != null)
            infoNameText.text = resourceName;

        BindInfoHealth(null); // 자원은 HealthManager가 없으므로 체력 구독은 해제
```

**변경 코드**
```csharp
        if (infoNameText != null)
            infoNameText.text = resourceName;

        SetCombatStatsVisible(false);
        BindInfoHealth(null); // 자원은 HealthManager가 없으므로 체력 구독은 해제
```

## 요약
- 자원 선택 경로(`ShowResourceInfoPanel`)에서는 `AttackDamageImage`/`ArmorImage`를 `SetActive(false)`로 완전히 숨김.
- 유닛/적 선택 경로(`ShowInfoPanel` 5-인자)에서는 다시 켜짐.
- 이미지가 비활성 상태여도 이미 붙어있는 `EventTrigger`(0019에서 추가한 호버 툴팁)는 비활성 오브젝트에서 이벤트를 받지 않으므로 별도 처리 불필요.

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
