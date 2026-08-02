# 0368 — 버그수정(제안): 일꾼 선택 후 다른 유닛 선택 시 슬롯 6에 건설/취소 버튼이 남는 문제

**날짜:** 2026-08-02

## 질문

> 현재 발생한 버그가 게임을 처음 시작 -> 일꾼을 선택 -> 다른 유닛 선택시 6번 슬롯에 건설 버튼이 배치되거나
> cancel(취소 버튼)이 생김 원래 비어있어야하는 슬롯일 경우에도 버튼이 있고 스킬을 가지고 있는 유닛에 경우
> 특성 스킬을 찍으면 그 자리에 스킬이 들어가긴함

## 원인 확인

order panel 슬롯 6번(`UIController.UnitSkillSlotIndex = 6`)은 컨텍스트에 따라 서로 다른 두 경로가 겸용한다.

1. **`UIController.ShowWorkerPanel()`** — 일꾼 선택 시 매 프레임 `SetCommands(...)`로 슬롯 6에 **Build 아이콘**을
   직접 써넣는다(`UIController.cs:1062-1073`, 커맨드 배열의 7번째 요소가 물리적으로 슬롯 6).
   빌드모드로 들어가면 `ShowBuildPanel()`이 같은 슬롯에 **Cancel 아이콘**을 써넣기도 한다.
2. **`RTSUnitController.UpdateUnitSkillUI()`** — 고급유닛(특성 선택 가능) 대표 유닛이 선택돼 있으면
   `ShowUnitSkillSlot()`으로 슬롯 6에 **스킬 버튼**을 써넣는다(`RTSUnitController.cs:1620-1630`).

두 경로가 충돌하지 않도록, 일꾼이 아닌 유닛을 선택했을 때 쓰이는 `ShowAttackUnitPanel()`은 슬롯 6을
`UnitSkillSlotProtected`로 보호해서 **절대 `Clear()`하지 않는다**(`UIController.cs:1087-1098`) — "슬롯 6은
스킬 시스템이 알아서 관리한다"는 전제다.

문제는 `UpdateUnitSkillUI()`가 슬롯 6을 비울 때 **자기가 직접 스킬을 그려 넣은 적이 있을 때만**
(`skillSlotShown` 플래그) `ClearUnitSkillSlot()`을 호출한다는 점이다(`RTSUnitController.cs:1559-1569`,
`1586-1591`, `1595-1597`). 일꾼의 Build 버튼(또는 빌드모드의 Cancel 버튼)은 스킬 시스템을 거치지 않고
`ShowWorkerPanel`/`ShowBuildPanel`이 슬롯 6에 직접 써넣은 것이므로 `skillSlotShown`이 `true`가 된 적이 없다.

그래서 순서는 이렇게 된다:

1. 일꾼 선택 → `ShowWorkerPanel()`이 슬롯 6에 Build 아이콘을 씀. `skillSlotShown`은 여전히 `false`
   (스킬 시스템은 이 슬롯에 아무것도 쓴 적 없음).
2. 다른 유닛 선택 → `UpdateUnitSkillUI()`가 먼저 실행되지만, 새 유닛이 특성이 없으면
   `ClearSkillSlotIfShown()`이 `skillSlotShown == false`라서 아무 것도 안 함 → 슬롯 6 그대로.
3. 이어서 `ShowAttackUnitPanel()`이 실행되지만 슬롯 6을 `protectedSlotIndices`로 보호해서 건드리지 않음
   (`UIController.SetCommands`, `UIController.cs:398-411`: 보호된 슬롯은 commands 배열 길이를 넘어도
   `Clear()`하지 않음).

결과적으로 일꾼의 Build 아이콘(또는 빌드모드 Cancel 아이콘)이 다음 유닛 선택 화면까지 그대로 남는다.
반대로 새로 선택한 유닛이 특성 스킬을 가지고 있으면 `ShowUnitSkillSlot()`이 슬롯 6을
덮어쓰므로("특성 스킬을 찍으면 그 자리에 스킬이 들어가긴 함") 정상으로 보인다 — 그래서 스킬이 있는
유닛에서는 증상이 안 보이고, 없는 유닛에서만 잔상이 남는다.

## 제안 수정

`RTSUnitController.UpdateUnitSkillUI()`에서 슬롯 6을 비우는 조건을 "스킬 시스템이 그린 적 있을 때"뿐 아니라
"현재 프레임에 일꾼이 아닌 유닛이 선택돼 있고(=슬롯 6이 스킬 전용인 컨텍스트) 스킬을 표시하지 않을 때"도
포함하도록 바꾼다. 즉 `selectedUnitList.Count > 0`인 두 분기(특성 없음 / 아직 특성 미선택)에서
`ClearSkillSlotIfShown()` 대신, `useFallbackSlot`이 `false`인 경우(일꾼이 아님) 무조건
`uIController.ClearUnitSkillSlot(false)`를 호출하도록 수정.

- `selectedUnitList.Count == 0`(건물 선택/선택 없음) 분기는 그대로 둔다 — 이 경우 슬롯 6은 생산건물
  랠리 버튼(`BuildingRallySlotIndex`)이 쓸 수도 있으므로(doc/0363) 기존처럼 `skillSlotShown` 기준으로만
  조심스럽게 비운다.
- 일꾼이 선택된 프레임(`useFallbackSlot == true`)은 어차피 `ShowWorkerPanel()`이 슬롯 6을 매 프레임
  직접 채우므로 손댈 필요 없음(기존 그대로).

## 사용자 재확인 (2026-08-02)

> 특성 스킬의 경우 6번째 슬롯에 버튼을 추가해주는거고 일꾼의 경우만 그냥 예외로 무시처리 해주면 되는거야
> 그냥 건물에 경우는 그 자리가 랠리 버튼이 있는거고 6번째 슬롯에 집어넣는데 만약 6번째 슬롯에 이미 버튼이
> 있으면 그냥 아무일도 안일어나는 예외처리하거나 debug.log로 남겨두면 나중에 문제를 확인하는데 도움이
> 될거 같아. 전체적인 시스템을 좀 확인해보고 이게 내가 생각한 로직이니깐 참고해서 다시한번 생각해줘

전체 시스템을 다시 확인한 결과, 랠리 버튼 쪽은 이미 컨텍스트를 벗어날 때 `ClearBuildingPanelExceptLiftSlots()`가
슬롯 6을 제대로 비워주고 있어서(랠리 없는 건물 선택 시) 잔상 문제가 없었다. 유일하게 "컨텍스트를 벗어날 때
정리"가 빠져 있던 게 일꾼의 Build 버튼이었다. 이를 반영해 아래처럼 수정 범위를 확정:

1. 유닛이 선택돼 있고(건물/선택없음 아님) 일꾼이 아니면(`useFallbackSlot == false`) 스킬을 표시하지 않을 때
   `skillSlotShown` 플래그와 무관하게 슬롯 6을 무조건 비운다.
2. 일꾼이면(`useFallbackSlot == true`) 그냥 예외 처리 — 아무것도 안 함(`ShowWorkerPanel`이 매 프레임
   직접 채움).
3. 건물/선택없음 분기는 랠리 버튼 보호를 위해 기존 `skillSlotShown` 게이트 로직 그대로 유지.
4. 진단용 안전장치: 스킬이 슬롯 6/7을 처음 차지하려는 순간 이미 다른 버튼이 남아있으면(정상 흐름이면
   절대 발생 안 함) `Debug.LogWarning`을 남기고, 그래도 스킬 버튼 표시는 항상 보장하기 위해 덮어쓰기는
   그대로 진행한다.

## 적용 결과 (2026-08-02)

사용자 확인 후 적용.

- `Assets/Scripts/UI/ProductionSlot.cs`: `public bool HasData => hasData;` getter 추가.
- `Assets/Scripts/UI/UIController.cs`: `IsSkillSlotOccupied(bool useFallbackSlot)` 헬퍼 추가
  (`ClearUnitSkillSlot` 바로 아래) — 슬롯 6/7에 이미 데이터가 있는지 확인.
- `Assets/Scripts/System/RTSUnitController.cs` (`UpdateUnitSkillUI()`):
  - `ClearUnitContextSkillSlot(bool useFallbackSlot)` 헬퍼 추가 — 일꾼이면 예외로 무시, 아니면
    `skillSlotShown` 무관하게 무조건 슬롯 6을 비움.
  - "특성 없음"/"특성 미선택" 두 분기에서 기존 `ClearSkillSlotIfShown()` 대신 위 헬퍼 사용
    (건물/선택없음 분기는 `ClearSkillSlotIfShown()` 그대로 유지).
  - 실제 스킬 버튼을 쓰기 직전(`ShowUnitSkillSlot` 호출 전)에 `!skillSlotShown &&
    uIController.IsSkillSlotOccupied(useFallbackSlot)`이면 `Debug.LogWarning` 추가.

**Before** (`RTSUnitController.cs`, `UpdateUnitSkillUI` 발췌):
```csharp
private void ClearSkillSlotIfShown()
{
    if (!skillSlotShown)
        return;

    uIController.ClearUnitSkillSlot(skillSlotUsedFallback);
    skillSlotShown = false;
}

private void UpdateUnitSkillUI()
{
    bool useFallbackSlot = UnitSelectState == UnitState.Worker;

    if (selectedUnitList.Count == 0)
    {
        uIController.HideSkillSelectPanel();
        ClearSkillSlotIfShown();
        return;
    }

    UnitController representative = selectedUnitList[0];
    UnitData data = GetUnitData(representative.GetUnitID());

    if (data == null || !data.hasTraitChoice)
    {
        uIController.HideSkillSelectPanel();
        ClearSkillSlotIfShown();
        return;
    }

    TraitChoice chosen = GetChosenTrait(data.ID);

    if (chosen == TraitChoice.None)
    {
        ClearSkillSlotIfShown();
        uIController.ShowSkillSelectPanel(...);
        return;
    }

    uIController.HideSkillSelectPanel();
    ...
    uIController.ShowUnitSkillSlot(new CommandButtonData(...), useFallbackSlot);
    skillSlotShown = true;
    skillSlotUsedFallback = useFallbackSlot;
    ...
}
```

**After:**
```csharp
private void ClearSkillSlotIfShown()
{
    if (!skillSlotShown)
        return;

    uIController.ClearUnitSkillSlot(skillSlotUsedFallback);
    skillSlotShown = false;
}

private void ClearUnitContextSkillSlot(bool useFallbackSlot)
{
    if (useFallbackSlot)
        return;

    uIController.ClearUnitSkillSlot(false);
    skillSlotShown = false;
}

private void UpdateUnitSkillUI()
{
    bool useFallbackSlot = UnitSelectState == UnitState.Worker;

    if (selectedUnitList.Count == 0)
    {
        uIController.HideSkillSelectPanel();
        ClearSkillSlotIfShown();
        return;
    }

    UnitController representative = selectedUnitList[0];
    UnitData data = GetUnitData(representative.GetUnitID());

    if (data == null || !data.hasTraitChoice)
    {
        uIController.HideSkillSelectPanel();
        ClearUnitContextSkillSlot(useFallbackSlot);
        return;
    }

    TraitChoice chosen = GetChosenTrait(data.ID);

    if (chosen == TraitChoice.None)
    {
        ClearUnitContextSkillSlot(useFallbackSlot);
        uIController.ShowSkillSelectPanel(...);
        return;
    }

    uIController.HideSkillSelectPanel();
    ...
    if (!skillSlotShown && uIController.IsSkillSlotOccupied(useFallbackSlot))
    {
        Debug.LogWarning($"[RTSUnitController] Unit skill slot(fallback={useFallbackSlot}) already had a button " +
            "before the skill claimed it - check other slot 6/7 writers (Worker Build / Rally / Cancel).");
    }

    uIController.ShowUnitSkillSlot(new CommandButtonData(...), useFallbackSlot);
    skillSlotShown = true;
    skillSlotUsedFallback = useFallbackSlot;
    ...
}
```

`ProductionSlot.cs`:
```csharp
// Before
private bool hasData;

// After
private bool hasData;

public bool HasData => hasData;
```

`UIController.cs` (`ClearUnitSkillSlot` 바로 아래에 추가):
```csharp
public bool IsSkillSlotOccupied(bool useFallbackSlot)
{
    int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
    return index < slots.Length && slots[index] != null && slots[index].HasData;
}
```

## 검증

- `npx uloop-cli compile`: 에러 0개(기존과 동일한 무관한 경고 33개만 남음, 새로 추가된 코드발 경고 없음).
- 사용자가 에디터에서 직접 재현 테스트 후 정상 동작 확인함("이제 제대로 작동하네", 2026-08-02).

## 영향받는 파일

- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/UI/ProductionSlot.cs`
