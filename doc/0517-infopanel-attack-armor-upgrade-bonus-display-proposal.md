# 0517. Info Panel 공격력/방어력 툴팁에 연구 업그레이드 보너스를 "+N"으로 별도 표기

**날짜:** 2026-08-11

## 요청 내용

> 공격력,방어력 업글시 +1 +2로 업글에 따라 얼마나 늘었는지 옆에 텍스트로 표시 합산한 값이
> 나오도록이 아니라 원래 공격력 방어력에다가 +숫자 로 나오도록

## 조사 내용

- 유닛 선택 시 Info Panel의 공격력/방어력 아이콘(`attackDamageImage`/`armorImage`)에 마우스를
  올리면 `UIController.SetupInfoStatHoverTooltips()`가 `infopanel.attacktooltip`/`infopanel.armortooltip`
  키로 툴팁을 띄움(`UIController.cs:696-703`).
- 이 숫자는 `RTSUnitController.cs:1960`에서 `unit.GetAttackDamage()` / `unit.GetArmor()`를 그대로
  넘겨서 온 값인데, `UnitController.cs:2064-2065`:
  ```csharp
  public int GetAttackDamage() => attackDamage + (rtsController != null ? rtsController.GlobalAttackBonus : 0);
  public int GetArmor() => armor + (rtsController != null ? rtsController.GlobalArmorBonus : 0);
  ```
  즉 **기본값 + 연구 보너스가 이미 합산된 값**만 반환한다. `GlobalAttackBonus`/`GlobalArmorBonus`는
  연구소(`ResearchQueue`)가 공격/방어 연구를 완료할 때마다 `UpgradeManager.AddBonus()`로 쌓이는
  전역 보너스(`RTSUnitController.cs:2252-2253`).
- 요청하신 "원래 공격력에 +숫자"로 보이려면, 이 둘을 분리해서 각각 넘겨야 함 - 지금은 이미 더해진
  하나의 숫자만 UI까지 전달되고 있어서 UI 쪽만 고쳐서는 안 되고, 기본값/보너스를 분리해서 넘기는
  경로가 필요함.
- 참고로 `GetAttackDamage()`/`GetArmor()`(합산값)는 실제 전투 계산(`UnitController.cs:1329` 등)에서
  그대로 써야 하므로 **건드리지 않음** - Info Panel 표시용으로 기본값/보너스를 얻는 새 getter만
  추가하는 방식으로 접근.
- 적/아군(`EnemyUnitController`/`AllyController`)의 `GetAttackDamage()`/`GetArmor()`는 애초에
  보너스 없이 base 값만 반환하므로(연구 보너스는 플레이어 유닛 전용), 그쪽 호출부는 그대로 둬도
  자동으로 "보너스 없음" 상태로 잘 표시됨.

## 계획된 변경

### `UnitController.cs`
`GetAttackDamage()`/`GetArmor()` 옆에 기본값/보너스를 각각 반환하는 getter 4개 추가:
```csharp
public int GetBaseAttackDamage() => attackDamage;
public int GetBaseArmor() => armor;
public int GetAttackBonus() => rtsController != null ? rtsController.GlobalAttackBonus : 0;
public int GetArmorBonus() => rtsController != null ? rtsController.GlobalArmorBonus : 0;
```

### `UIController.cs`
- `ShowInfoPanel(...)` 시그니처 끝에 `int attackBonus = 0, int armorBonus = 0` 추가(기본값 0이라
  건물/적/아군 등 기존 호출부는 안 고쳐도 그대로 동작).
- `infoAttackDamage`/`infoArmor` 필드는 이제 "기본값"을 저장하고, `infoAttackBonus`/`infoArmorBonus`
  필드를 새로 추가해서 보너스를 저장.
- `SetupInfoStatHoverTooltips()`에서 툴팁에 넘기는 숫자를 문자열로 조합:
  ```csharp
  string attackDisplay = infoAttackBonus > 0 ? $"{infoAttackDamage} +{infoAttackBonus}" : infoAttackDamage.ToString();
  string armorDisplay = infoArmorBonus > 0 ? $"{infoArmor} +{infoArmorBonus}" : infoArmor.ToString();
  ```
  → 보너스가 0이면 지금과 동일하게 숫자만, 1 이상이면 `"6 +2"`처럼 표시. 로컬라이제이션 템플릿
  (`infopanel.attacktooltip`/`armortooltip`)의 `{0}`/`{1}` 자리에 이 문자열을 그대로 꽂아 넣으므로
  `en.json`/`ko.json` 수정은 필요 없음.

### `RTSUnitController.cs`
`ShowInfoPanel` 호출부(유닛 1개 선택 시, L1960) 하나만 base/bonus로 분리해서 전달:
```diff
- uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetHealthManager(), unit.GetAttackDamage(), unit.GetArmor(),
+ uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetHealthManager(), unit.GetBaseAttackDamage(), unit.GetBaseArmor(),
      unit.GetAttackType(), unit.GetArmorType(), unit.GetSizeType(), unit.GetShotCount(), unit.GetDescription(),
-     unit.GetCanAttackGround(), unit.GetCanAttackAir());
+     unit.GetCanAttackGround(), unit.GetCanAttackAir(), unit.GetAttackBonus(), unit.GetArmorBonus());
```
적(EnemyUnitController)/아군(AllyController)/건물 쪽 `ShowInfoPanel` 호출부는 그대로 둠(보너스
파라미터가 기본값 0이라 자동으로 지금과 동일하게 동작).

## 변경 예정 파일

- `Assets/Scripts/Unit/UnitController.cs` (getter 4개 추가)
- `Assets/Scripts/UI/UIController.cs` (`ShowInfoPanel` 파라미터 추가, 툴팁 표시 문자열 조합 로직)
- `Assets/Scripts/System/RTSUnitController.cs` (유닛 선택 시 `ShowInfoPanel` 호출부 1곳)

## 확인 필요

- 표기 형식이 `"6 +2"`(숫자, 공백, +보너스)로 괜찮을까요? 예: 요청하신 문구처럼 `"6+2"`(공백 없이)나
  `"6 (+2)"`(괄호)로도 가능합니다.
- 이대로 진행해도 될까요?

---

## 적용 (사용자 승인 후)

> "6 +2 (공백)" 선택 → 이대로 진행시켜줘

### `UnitController.cs`
`GetAttackDamage()`/`GetArmor()`(합산값, 전투 계산용) 바로 아래에 표시 전용 getter 4개 추가:
```csharp
public int GetBaseAttackDamage() => attackDamage;
public int GetBaseArmor() => armor;
public int GetAttackBonus() => rtsController != null ? rtsController.GlobalAttackBonus : 0;
public int GetArmorBonus() => rtsController != null ? rtsController.GlobalArmorBonus : 0;
```

### `UIController.cs`
- `infoAttackBonus`/`infoArmorBonus` 필드 추가, `infoAttackDamage`/`infoArmor`는 이제 기본값 의미로 재해석.
- `ShowInfoPanel(...)` 끝에 `int attackBonus = 0, int armorBonus = 0` 파라미터 추가(기본값 0이라
  건물/적/아군 호출부는 수정 없이 그대로 동작).
- `SetupInfoStatHoverTooltips()`에 `FormatStatWithBonus(baseValue, bonus)` 헬퍼 추가:
  `bonus > 0`이면 `"{base} +{bonus}"`, 아니면 지금까지와 동일하게 숫자만. 이 문자열을
  `infopanel.attacktooltip`/`armortooltip` 템플릿의 `{1}`/`{0}` 자리에 그대로 전달하므로
  `en.json`/`ko.json`은 수정 불필요.

### `RTSUnitController.cs`
유닛 1개 선택 시 `ShowInfoPanel` 호출부(L1960) 하나만 base/bonus로 분리해서 전달:
```diff
- uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetHealthManager(), unit.GetAttackDamage(), unit.GetArmor(),
+ uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetHealthManager(), unit.GetBaseAttackDamage(), unit.GetBaseArmor(),
      unit.GetAttackType(), unit.GetArmorType(), unit.GetSizeType(), unit.GetShotCount(), unit.GetDescription(),
-     unit.GetCanAttackGround(), unit.GetCanAttackAir());
+     unit.GetCanAttackGround(), unit.GetCanAttackAir(), unit.GetAttackBonus(), unit.GetArmorBonus());
```
적(EnemyUnitController)/아군(AllyController)/건물 쪽 호출부는 그대로 둠(보너스 파라미터 기본값 0).

### 검증

- `npx uloop-cli compile` 실행 - `Success: true`, `ErrorCount: 0`. `WarningCount: 37`은 전부 이번
  변경과 무관한 기존 경고(`FindObjectOfType`/`FindObjectsSortMode` obsolete 계열).
- 연구 미완료 상태(보너스 0)에서는 `FormatStatWithBonus`가 `baseValue.ToString()`만 반환해 기존과
  동일한 출력이 나옴을 코드로 확인 - 인게임 연구 완료 후 실제 툴팁 표기(예: `"6 +2"`)는 직접
  연구소에서 공격/방어 연구를 완료한 뒤 눈으로 확인 권장.

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` (getter 4개 추가)
- `Assets/Scripts/UI/UIController.cs` (`ShowInfoPanel` 파라미터 추가, `FormatStatWithBonus` 헬퍼 추가)
- `Assets/Scripts/System/RTSUnitController.cs` (유닛 선택 시 `ShowInfoPanel` 호출부 1곳)
