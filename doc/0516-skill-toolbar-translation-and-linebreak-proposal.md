# 0516. 스킬(고급유닛 특성) 툴바 텍스트 번역 누락 + 툴팁 설명 줄바꿈 안 되는 문제 조사/제안

**날짜:** 2026-08-11

## 요청 내용

> 스킬 툴바에 나오는 텍스트 번역 안됨
> 스킬 툴팁 설명 줄바꿈 안됨
> 스킬 설명에 대한 툴바도 번역 확인해줘

## 조사 내용

### 1) 스킬 이름/설명이 번역 안 되는 원인

`hasTraitChoice = true`인 유닛(고급유닛 특성 2택1 스킬 보유) 3종:
- Sharpshooter (unit ID 4) - traitA "Snipe", traitB "Cloak"
- SkyLancer (unit ID 7) - traitA "Air Superiority", traitB "Ground Bombardment"
- Guardian Drone (unit ID 9) - traitA "Focused Barrage", traitB "Shield Deployment"

`RTSUnitController.cs`에서 이 스킬들의 이름/설명을 커맨드 버튼에 채우는 두 지점(`UpdateUnitSkillUI()`)이
`UnitTraitOption.skillName` / `.description`(SO에 영어로 박혀있는 원문)을 **그대로** 사용:

- L1846-1849 (특성 선택 오버레이 "SkillSelect" 카드):
  ```csharp
  uIController.ShowSkillSelectPanel(
      new CommandButtonData(data.traitA.icon, ButtonAction.Simple(
          () => ChooseTrait(data.ID, TraitChoice.A), data.traitA.skillName, data.traitA.description)),
      new CommandButtonData(data.traitB.icon, ButtonAction.Simple(
          () => ChooseTrait(data.ID, TraitChoice.B), data.traitB.skillName, data.traitB.description)));
  ```
- L1876-1881 (order panel 슬롯 6/7 스킬 버튼):
  ```csharp
  uIController.ShowUnitSkillSlot(new CommandButtonData(
      trait.icon,
      ButtonAction.Simple(
          () => ActivateSkill(data.ID, trait),
          trait.skillName,
          description, // = trait.description (+ 단축키/쿨다운 접미사)
          trait.isActiveSkill ? trait.shortcutKey : KeyCode.None),
      trait.isActiveSkill && !onCooldown),
      useFallbackSlot);
  ```

반면 같은 파일의 유닛 생산 버튼(`UnitButtonAction`)·건물 건설 버튼(`BuildingButtonAction`)은 전부
`LocalizationManager.GetTextOrFallback("unit.nta.{id}.name", data.unitName)` 패턴으로 언어 파일을
먼저 찾고, 키가 없을 때만 SO 원문으로 폴백한다(`en.json`/`ko.json`에 `unit.nta.1.name` ~
`unit.nta.9.desc`, `building.nta.1.name` ~ `.desc` 키가 이미 다 있음). **스킬(트레이트) 쪽만 이
패턴이 적용된 적이 없어서, 언어를 한국어로 바꿔도 스킬 이름/설명만 항상 영어 원문 그대로 보임.**

### 2) 스킬 툴팁 설명이 줄바꿈 안 되는 원인

게임 내 모든 툴팁 설명(`cmd.move.desc`, `unit.nta.*.desc`, `building.nta.*.desc`,
`infopanel.*tooltip`, `squad.*tooltip` 등, `ko.json`/`en.json` 전수 확인)은 예외 없이 **작성자가
직접 `\n`을 박아넣어 줄을 미리 끊어놓은 짧은 문장들**이다. 실제로 `TooltipUI`가 쓰는
`GameManager.prefab`의 `titleText`/`descriptionText`(TextMeshProUGUI) 둘 다
`m_TextWrappingMode: 1`(`NoWrap`)로 설정돼 있고, `TooltipContentFitter`도 자동 줄바꿈이 아니라
"주어진 텍스트 그대로의 폭에 박스를 맞추는" 방식으로 설계돼 있다(doc/0471 - 비용 있는 버튼은 고정
폭, 없는 버튼은 내용에 맞춰 폭이 늘어남). 즉 **이 프로젝트의 툴팁은 애초에 자동 줄바꿈을 쓰지 않고,
전부 수동 `\n`으로 짧게 끊어서 보여주는 방식**이다.

그런데 스킬(트레이트) 설명만 이 관례를 안 따르고 한 문장짜리 긴 영어 원문을 그대로 쓴다
(예: `"Fires a high-powered shot at a single target, dealing 40 damage."`). `\n`이 하나도 없으니
`NoWrap` 설정에서 줄바꿈이 전혀 안 일어나고, 박스도 그 긴 한 줄 폭에 맞춰 늘어나 툴팁이 옆으로
길게 삐져나온다 - 이게 "줄바꿈 안됨"으로 보이는 원인이다.

→ **1)의 번역 키를 추가하면서 값 안에 `\n`을 다른 툴팁들과 같은 스타일로 넣어주면, 번역과 줄바꿈
문제가 한 번에 같이 해결된다.** `TooltipContentFitter`/TMP 줄바꿈 모드 자체는 게임 전체 툴팁이
공유하는 설계라 건드리지 않는 게 안전함(다른 모든 버튼의 "내용에 딱 맞는 좁은 툴팁" 동작이 깨질
위험이 있어서 - 스킬 설명 텍스트 데이터만 고치는 쪽이 root cause에 가장 가깝고 영향 범위도 가장
좁음).

## 계획된 변경

`unit.nta.{id}.name`/`.desc` 관례를 그대로 따라 6개 스킬(3유닛 × A/B)에 대해
`trait.nta.{unitID}.a.name` / `.a.desc` / `.b.name` / `.b.desc` 키를 `en.json`/`ko.json`에 추가.

### 추가할 키 (제안 값)

| unit ID | 슬롯 | key | en.json | ko.json |
|---|---|---|---|---|
| 4 (Sharpshooter) | A | `trait.nta.4.a.name` | `Snipe` | `저격` |
| 4 | A | `trait.nta.4.a.desc` | `Fires a high-powered shot at a single target,\ndealing 40 damage.` | `단일 대상에게 고위력 사격을 가해\n40의 피해를 입힙니다.` |
| 4 | B | `trait.nta.4.b.name` | `Cloak` | `은신` |
| 4 | B | `trait.nta.4.b.desc` | `Enters stealth for 15 seconds, becoming untargetable\nby enemy attacks. Attacking or using abilities breaks the cloak.` | `15초간 은신하여 적의 공격 대상이 되지 않습니다.\n공격하거나 스킬을 사용하면 은신이 해제됩니다.` |
| 7 (SkyLancer) | A | `trait.nta.7.a.name` | `Air Superiority` | `제공권 장악` |
| 7 | A | `trait.nta.7.a.desc` | `Air attacks ignite enemy air units, dealing 2 damage\nper second for 7 seconds.` | `공중 공격 시 적 공중 유닛에 화염을 붙여\n7초 동안 초당 2의 피해를 입힙니다.` |
| 7 | B | `trait.nta.7.b.name` | `Ground Bombardment` | `지상 폭격` |
| 7 | B | `trait.nta.7.b.desc` | `Calls in an airstrike on a target area, dealing\n20 damage to all units, including allies.` | `지정 지역에 공습을 요청하여 아군을 포함한\n모든 유닛에게 20의 피해를 입힙니다.` |
| 9 (Guardian Drone) | A | `trait.nta.9.a.name` | `Focused Barrage` | `집중 포격` |
| 9 | A | `trait.nta.9.a.desc` | `Unleashes three enhanced bombardments on a single\ntarget, dealing a total of 300 damage.` | `단일 대상에게 강화된 포격을 3회 퍼부어\n총 300의 피해를 입힙니다.` |
| 9 | B | `trait.nta.9.b.name` | `Shield Deployment` | `실드 전개` |
| 9 | B | `trait.nta.9.b.desc` | `Deploys an energy shield, granting 150 bonus health.` | `에너지 실드를 전개하여 150의 추가 체력을 부여합니다.` |

(`trait.shortcutsuffix`/`trait.cooldownsuffix`는 이미 별도 키로 존재해서 그대로 뒤에 붙으니 손댈
필요 없음. `trait.nta.4.b.desc`처럼 원문이 이미 2줄인 것도 있고 `trait.nta.9.b.desc`처럼 짧아서 원래
1줄로 끝나는 것도 있음 - 각 문장 길이에 맞춰 자연스러운 지점에서만 `\n`을 넣음.)

### `RTSUnitController.cs` 코드 변경

`UnitButtonAction`/`BuildingButtonAction`과 동일한 패턴으로 두 지점을 `LocalizationManager.GetTextOrFallback`
경유로 변경:

```diff
         if (chosen == TraitChoice.None)
         {
             ClearUnitContextSkillSlot(useFallbackSlot);
+            string traitAName = LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.a.name", data.traitA.skillName);
+            string traitADesc = LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.a.desc", data.traitA.description);
+            string traitBName = LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.b.name", data.traitB.skillName);
+            string traitBDesc = LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.b.desc", data.traitB.description);
             uIController.ShowSkillSelectPanel(
                 new CommandButtonData(data.traitA.icon, ButtonAction.Simple(
-                    () => ChooseTrait(data.ID, TraitChoice.A), data.traitA.skillName, data.traitA.description)),
+                    () => ChooseTrait(data.ID, TraitChoice.A), traitAName, traitADesc)),
                 new CommandButtonData(data.traitB.icon, ButtonAction.Simple(
-                    () => ChooseTrait(data.ID, TraitChoice.B), data.traitB.skillName, data.traitB.description)));
+                    () => ChooseTrait(data.ID, TraitChoice.B), traitBName, traitBDesc)));
             return;
         }
```

```diff
         UnitTraitOption trait = chosen == TraitChoice.A ? data.traitA : data.traitB;
+        string traitSlotKey = chosen == TraitChoice.A ? "a" : "b";
+        string traitName = LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.{traitSlotKey}.name", trait.skillName);
+        string traitBaseDesc = LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.{traitSlotKey}.desc", trait.description);

         float skillCooldownRemaining = representative.GetSkillCooldownRemaining();
         bool onCooldown = trait.isActiveSkill && skillCooldownRemaining > 0f;

         string description = trait.isActiveSkill
-            ? $"{trait.description} " + LocalizationManager.GetText("trait.shortcutsuffix", ShortcutTag(trait.shortcutKey))
+            ? $"{traitBaseDesc} " + LocalizationManager.GetText("trait.shortcutsuffix", ShortcutTag(trait.shortcutKey))
                 + (onCooldown ? LocalizationManager.GetText("trait.cooldownsuffix", skillCooldownRemaining) : "")
-            : trait.description;
+            : traitBaseDesc;
         ...
         uIController.ShowUnitSkillSlot(new CommandButtonData(
             trait.icon,
             ButtonAction.Simple(
                 () => ActivateSkill(data.ID, trait),
-                trait.skillName,
+                traitName,
                 description,
                 trait.isActiveSkill ? trait.shortcutKey : KeyCode.None),
             trait.isActiveSkill && !onCooldown),
             useFallbackSlot);
```

## 변경 예정 파일

- `Assets/Resources/Localization/en.json`, `ko.json` (`trait.nta.{4,7,9}.{a,b}.{name,desc}` 12쌍 = 24줄 추가)
- `Assets/Scripts/System/RTSUnitController.cs` (`UpdateUnitSkillUI()` 두 지점을 `LocalizationManager.GetTextOrFallback` 경유로 변경)

## 확인 필요

- 위 한글 번역 문구가 어색하면 알려주세요 (`저격`/`은신`/`제공권 장악`/`지상 폭격`/`집중 포격`/`실드 전개` 이름과 각 설명).
- `\n` 삽입 위치가 표시된 툴팁 폭 기준으로 어색하게 잘리면(직접 플레이해봐야 정확히 보임) 위치 조정 필요할 수 있음 - 1차로 다른 기존 설명들의 평균 줄 길이에 맞춰 배치함.
- 이대로 진행해도 될까요?

---

## 적용 (사용자 승인 후)

> 이대로 진행시켜줘

### `en.json` / `ko.json`

제안 표의 12쌍(`trait.nta.{4,7,9}.{a,b}.{name,desc}`) 24줄을 각각 `trait.cooldownsuffix` 키 바로
뒤에 그대로 추가.

### `RTSUnitController.cs`

`UpdateUnitSkillUI()`의 두 지점을 제안했던 diff 그대로 적용:
- SkillSelect 오버레이 카드 생성 직전에 `traitAName`/`traitADesc`/`traitBName`/`traitBDesc`를
  `LocalizationManager.GetTextOrFallback($"trait.nta.{data.ID}.a/b.name/desc", ...)`로 조회해서
  `ShowSkillSelectPanel`에 전달.
- order panel 슬롯 6/7 스킬 버튼 생성부에 `traitSlotKey`("a"/"b")를 도입, `traitName`/`traitBaseDesc`를
  같은 방식으로 조회해서 `trait.skillName`/`trait.description` 직접 참조를 대체(쿨다운/단축키 접미사
  로직은 그대로 유지, `trait.description` → `traitBaseDesc`만 치환).

### 검증

- `npx uloop-cli compile` 실행 - `Success: true`, `ErrorCount: 0`. `WarningCount: 37`은 전부 이번
  변경과 무관한 기존 경고(`FindObjectOfType`/`FindObjectsSortMode` obsolete 계열).

## 변경된 파일

- `Assets/Resources/Localization/en.json`, `ko.json` (`trait.nta.{4,7,9}.{a,b}.{name,desc}` 24줄 추가)
- `Assets/Scripts/System/RTSUnitController.cs` (`UpdateUnitSkillUI()` 두 지점을
  `LocalizationManager.GetTextOrFallback` 경유로 변경)
