# 0019 - Info Panel 공격력/방어력 호버 툴팁

**날짜:** 2026-07-07

## 요청 내용
`UIController`의 Info_panel 부분에 `AttackDamageImage`, `ArmorImage` 필드를 추가해서 사용자가 직접 인스펙터에서 연결할 예정. 마우스를 그 이미지 위에 올리면 현재 선택된 유닛의 정보를 "Attack Damge : (숫자)", "Armor : (숫자)" 형식으로 출력하도록 요청. ([[0018]]에서 `UnitController`에 `attackDamage`/`armor` 필드와 `GetAttackDamage()`/`GetArmor()`를 이미 추가해둔 상태를 이어서 사용)

## 조사 내용
- 기존에 이미 `Assets/Scripts/UI/Tooltip/TooltipUI.cs`(싱글턴, `Show(RectTransform, title, description)` / `Hide()`)와 `ProductionSlot.cs`(`IPointerEnterHandler`/`IPointerExitHandler` 구현)로 호버 툴팁 인프라가 갖춰져 있음 — 이를 그대로 재사용.
- `AttackDamageImage`/`ArmorImage`는 `ProductionSlot`처럼 별도 컴포넌트를 만들어 유닛이 각 이미지 GameObject에 직접 붙여야 하는 방식 대신, `UIController`가 `Start()`에서 `UnityEngine.EventSystems.EventTrigger`를 이미지 GameObject에 런타임으로 붙여 호버를 감지하도록 구현 — 사용자가 요청한 대로 "UIController에 필드만 추가하고 이미지는 인스펙터에서 연결"만 하면 되도록.
- `UIController.ShowInfoPanel(icon, name, health)`는 유닛/건물/자원 모두 공유하는 진입점이라, 공격력/방어력을 항상 받게 시그니처를 바꾸면 건물 쪽 호출부도 다 고쳐야 함 → 3-파라미터 기존 오버로드는 그대로 두고(내부적으로 0, 0으로 위임), 유닛 전용 5-파라미터 오버로드를 새로 추가하는 방식으로 최소 변경.

## 변경 내용
### `Assets/Scripts/UI/UIController.cs`
- `using UnityEngine.EventSystems;` 추가.
- Info Panel 헤더에 `[SerializeField] private Image attackDamageImage;`, `[SerializeField] private Image armorImage;` 필드 추가 (인스펙터에서 사용자가 직접 드래그 연결).
- `infoAttackDamage`/`infoArmor` private 필드 추가 — 현재 Info_panel에 표시 중인 대상의 공격력/방어력 값을 저장.
- `ShowInfoPanel(icon, name, health)`: 공격력/방어력이 없는 대상(건물/자원)용으로 남기고, 내부적으로 `ShowInfoPanel(icon, name, health, 0, 0)`을 호출하도록 위임.
- `ShowInfoPanel(icon, name, health, int attackDamage, int armor)` 새 오버로드 추가: 기존 로직 그대로 수행하면서 `infoAttackDamage`/`infoArmor`도 저장.
- `SetupInfoStatHoverTooltips()`: `Start()`에서 호출. `attackDamageImage`/`armorImage`에 각각 `AddStatHoverTooltip`을 연결.
- `AddStatHoverTooltip(Image image, Func<string> textProvider)`: 이미지 GameObject에 `EventTrigger`를 붙이고(없으면 추가), PointerEnter 시 `TooltipUI.Instance.Show(image.rectTransform, textProvider(), "")`, PointerExit 시 `TooltipUI.Instance.Hide()`를 연결. `textProvider`가 매 호버 시점에 `infoAttackDamage`/`infoArmor`를 읽으므로 항상 최신 선택 유닛 값을 보여줌.
- 표시 문구는 요청대로 `"Attack Damge : {n}"`, `"Armor : {n}"` (오타 "Damge"는 기존 `UnitDataSO.attackDamge` 필드명 표기와 동일하게 요청 문구 그대로 반영).

### `Assets/Scripts/System/RTSUnitController.cs`
- 유닛 단일 선택 시 `ShowInfoPanel` 호출을 5-파라미터 오버로드로 변경: `uIController.ShowInfoPanel(unit.GetIcon(), GetUnitName(unit.GetUnitID()), unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor());`
- 건물 선택 쪽 `ShowInfoPanel` 호출은 기존 3-파라미터 그대로 유지 (공격력/방어력 0으로 표시됨).

## 남은 작업 (에디터에서 사용자가 해야 할 부분)
- Unity 에디터에서 `UIController`의 `Attack Damage Image` / `Armor Image` 필드에 Info_panel 안의 실제 이미지 오브젝트를 드래그해서 연결해야 동작함.
- 해당 이미지들의 `Raycast Target`이 켜져 있어야 `EventTrigger` 호버가 감지됨 (Unity `Image` 기본값이 On이라 보통 별도 조치 불필요).

## 변경된 파일
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
