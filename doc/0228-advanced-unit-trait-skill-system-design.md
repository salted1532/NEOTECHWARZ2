# 0228 - 고급유닛 특성(2택1 스킬) 시스템 설계

**날짜:** 2026-07-26

## 요청 내용

> RTS게임에서 고급유닛에게 스킬을 추가하려고하는데 2개의 스킬중 하나를 선택하는 특성형 방식으로 하려고해 1개는 장점을 극대화하는 스킬, 1개는 단점을 보완하는 스킬중 유저가 선택하는 식으로 할건데 이 스킬 특성을 찍는 구간을 어디로 할지 고민이야.
> 1. 연구소에서 연구하는 방식(모든 같은유닛에 적용)
> 2. 처음 유닛 선택 시 위에 스킬 선택 특성창이 보이고 찍으면 안보이기(모든 같은유닛에 적용)
> 3. 유닛의 레벨 시스템이 있어서 경험치를 쌓아 레벨업 시 각 유닛별로 따로 특성 스킬을 찍을수 있는 시스템(각 유닛별로 따로 적용)
>
> 2번같은 경우는 처음 생산한 유닛의 특성을 찍으면 모든 유닛에게 적용되는 방식으로 하려고
> 3번을 하게 되면 40마리 유닛 있으면 각 유닛별로 다 찍어줘야해
> 난 여기서 2번방식을 적용시켜볼려고 하는데 이런식이면 어떤 유닛이 고급 유닛이고 각 유닛별로 2개의 스킬을 구현해야하고 2개의 스킬을 선택하는 특성창, 스킬을 찍으면 그 스킬이 order panel의 버튼으로 추가 + 단축키도 추가해야해 전체적인 구현 방식을 만들어줄래

**아직 아무 것도 구현하지 않음 - 설계만 정리.** 실제 코드 반영은 아래 "열린 질문"에 대한 답을 받고 승인 후 별도로 진행.

## 방식 1/2/3 비교 (사용자가 2번을 선택한 이유 정리)

| 방식 | 적용 단위 | 장점 | 단점 |
|---|---|---|---|
| 1. 연구소 연구 | 유닛 종류 전체 | 기존 `UpgradeManager`(공격/방어 연구) 패턴 그대로 재사용 가능 | 특성 선택이 "생산 준비"가 아니라 "연구 완료 대기"라서, 이미 뽑아둔 유닛에 늦게 적용되는 시차가 생김 |
| **2. 첫 유닛 선택 시 특성창 (채택)** | 유닛 종류 전체 | 그 유닛을 처음 쓰는 순간 바로 결정 → 즉시 전체 적용, 대기시간 없음 | 유닛 종류마다 "아직 선택 안 한 상태"를 UI가 감지해서 떠야 함(신규 로직 필요) |
| 3. 레벨업 특성 | 유닛 개체별 | 유닛마다 다른 빌드가 가능(다양성) | 40마리면 40번 찍어야 함 - 사용자가 이미 배제 |

방식 2는 `UpgradeManager`처럼 "전역(유닛 타입 단위) 상태 + 신규 생산되는 유닛에도 자동 적용"이라는 뼈대는 방식 1과 동일하고, 트리거 시점만 "연구소에서 연구 완료"가 아니라 "그 유닛 종류를 처음 선택(또는 첫 생산)했을 때"로 바뀌는 것이라, 기존 코드 재사용 폭이 크다.

## 전체 아키텍처 개요

```
UnitDataSO(유닛 데이터)                 RTSUnitController(중앙 상태)
 ├─ hasTraitChoice: bool                 ├─ chosenTraits: Dictionary<int unitID, TraitChoice>
 ├─ traitA (장점 극대화 스킬 정의)        │   (UpgradeManager와 동급의 "매치 동안만 유지되는 전역 상태")
 └─ traitB (단점 보완 스킬 정의)          │
                                          ├─ UpdateUI()에서 감지:
                                          │   선택된 유닛의 unitID가 hasTraitChoice=true 이고
                                          │   chosenTraits[unitID] == None 이면
                                          │   → 기존 ShowAttackUnitPanel 대신 특성 선택 패널 표시
                                          │
                                          └─ 특성 선택 시 OnTraitChosen(unitID, choice):
                                              1) chosenTraits[unitID] = choice 저장
                                              2) UnitList 중 unitID 일치하는 "현재 살아있는" 유닛 전체에 즉시 적용
                                              3) 이후 같은 unitID로 새로 생산되는 유닛도
                                                 UnitController.Start()에서 자동으로 같은 선택을 적용받음
```

기존 코드에 이미 있는, 그대로 재사용할 수 있는 뼈대:
- **"매치 동안 유지되는 유닛 타입 전역 상태"**: `Assets/Scripts/Upgrade/UpgradeManager.cs`가 이미 이 패턴(연구 보너스를 `RTSUnitController`를 거쳐서만 조회/기록)을 쓰고 있음. `chosenTraits`도 동일하게 `RTSUnitController` 안에 두고 `UnitController`가 직접 뒤지지 않고 `RTSUnitController.GetChosenTrait(unitID)` 같은 접근자로만 조회하게 하면 기존 컨벤션과 일치.
- **"버튼 하나가 자기 단축키를 스스로 감지"**: `ProductionSlot`(doc/0042)이 이미 `ButtonAction.Shortcut`을 갖고 자체적으로 `Input.GetKeyDown`을 감지해 스스로 클릭되므로, 스킬 버튼도 단축키 처리 코드를 새로 만들 필요 없이 `ButtonAction.Simple(ActivateSkill, title, desc, traitOption.shortcutKey)`만 넘기면 끝.
- **"패널 상태에 따라 버튼 구성이 바뀜"**: `UIController.ShowPanel(UISelectionState state, params CommandButtonData[] commands)`(범용 버전)이 이미 있어서, `ShowAttackUnitPanel` 전용 메서드를 건드리지 않고도 RTSUnitController 쪽에서 "기존 5버튼 + 스킬 버튼 1개"를 조립해 `ShowPanel(...)`로 넘기면 됨(UIController.cs 수정 최소화).
- **"유닛 타입별 분기 로직은 RTSUnitController가 중앙집중"**: `SpawnUnit(unitID)`, `UnitButtonAction(...)` 등 이미 unitID로 switch하는 패턴이 자리잡혀 있어서, 스킬 발동 로직(`ActivateSkill(UnitController unit)`)도 같은 자리에 같은 스타일로 추가하면 기존 코드와 결이 맞음.

## 1) 데이터 모델 - `UnitDataSO.cs`

```csharp
// 스킬 하나(장점 극대화 or 단점 보완)의 정의 - order panel 버튼/단축키/효과에 필요한 정보를 모두 담는다.
[System.Serializable]
public class UnitTraitOption
{
    [field: SerializeField] public string skillName { get; private set; }
    [field: SerializeField, TextArea(2, 4)] public string description { get; private set; }
    [field: SerializeField] public Sprite icon { get; private set; }

    // true면 order panel에 버튼+단축키가 추가되는 "액티브 스킬"(쿨다운 있음).
    // false면 버튼 없이 선택 즉시 스탯/행동에 조용히 반영되는 "패시브 특성"(예: 장점 극대화가 그냥 공격력 +n%인 경우).
    // 두 트레이트(A/B) 모두 액티브일 수도, 하나만 액티브일 수도, 둘 다 패시브일 수도 있음 - 유닛/스킬마다 다르게 설정.
    [field: SerializeField] public bool isActiveSkill { get; private set; }
    [field: SerializeField] public KeyCode shortcutKey { get; private set; } // isActiveSkill=true일 때만 사용
    [field: SerializeField] public float cooldown { get; private set; }     // isActiveSkill=true일 때만 사용
}
```

`UnitData`에 추가:
```csharp
[Header("고급유닛 특성 (2택1, 비워두면 일반 유닛)")]
[field: SerializeField] public bool hasTraitChoice { get; private set; }
[field: SerializeField] public UnitTraitOption traitA { get; private set; } // 장점 극대화
[field: SerializeField] public UnitTraitOption traitB { get; private set; } // 단점 보완
```
→ "어떤 유닛이 고급유닛인가"는 코드가 아니라 인스펙터에서 `hasTraitChoice` 체크 여부로 결정되므로, 나중에 대상 유닛이 바뀌어도 코드 수정이 필요 없음.

## 2) 상태 저장 - `RTSUnitController.cs`

```csharp
public enum TraitChoice { None, A, B }

// UpgradeManager와 동급: 매치 동안만 유지되는, unitID 단위 전역 상태.
private readonly Dictionary<int, TraitChoice> chosenTraits = new Dictionary<int, TraitChoice>();

public TraitChoice GetChosenTrait(int unitID) =>
    chosenTraits.TryGetValue(unitID, out var c) ? c : TraitChoice.None;

public void ChooseTrait(int unitID, TraitChoice choice)
{
    chosenTraits[unitID] = choice;

    // 이미 살아있는 같은 종류 유닛 전체에 즉시 반영
    foreach (UnitController unit in UnitList)
    {
        if (unit != null && unit.GetUnitID() == unitID)
            unit.ApplyTrait(choice);
    }
}
```

## 3) 신규 생산 유닛에도 자동 적용 - `UnitController.cs`

```csharp
void Start()
{
    ...
    ApplyUnitData(rtsController.GetUnitData(unitID));

    // 이 유닛 종류가 이미 특성을 선택한 상태라면(과거에 골랐음) 새로 생산된 이 유닛에도 그대로 적용
    TraitChoice chosen = rtsController.GetChosenTrait(unitID);
    if (chosen != TraitChoice.None)
        ApplyTrait(chosen);
}

public void ApplyTrait(TraitChoice choice)
{
    currentTrait = choice; // 이 개체가 지금 장착한 특성 (order panel 버튼 구성/스킬 발동에 사용)
    // 장점 극대화(A)/단점 보완(B) 각각의 스탯 보정치 적용은 유닛별로 다르므로
    // RTSUnitController.ApplyTraitStats(this, choice) 등으로 위임해 유닛 타입 분기를 한 곳에 모은다.
}
```

## 결정 사항 (사용자 확인 완료)

1. **고급유닛 판정 기준**: 코드에 하드코딩된 유닛 목록이 아니라 **`UnitDataSO`의 `hasTraitChoice` 체크 여부**로 결정 (위 데이터 모델 설계 그대로 확정). 특정 유닛(Goliath 등)을 문서에 못 박지 않고, 기획자가 인스펙터에서 유닛별로 켜고 끄는 방식.
2. **특성 선택 UI는 오버레이(비모달)**: 특성 선택 패널이 떠 있는 동안에도 기존 이동/공격/정지/순찰/홀드 명령은 그대로 사용 가능해야 함 → 커맨드 패널(`slots[]`, 9칸)을 교체하는 방식이 아니라, **별도의 오버레이 UI 오브젝트**로 커맨드 패널 위에 겹쳐서 띄운다 (아래 4번에서 구체화).
3. **스킬 성격은 트레이트마다 다름**: 액티브(쿨다운+버튼)/패시브(자동 적용, 버튼 없음) 여부가 유닛마다, 트레이트(A/B)마다 다를 수 있음 → 위 데이터 모델에 이미 반영한 `isActiveSkill` 플래그로 트레이트 단위로 개별 결정 (둘 다 액티브, 하나만 액티브, 둘 다 패시브 전부 가능).

## 4) 트리거 UI - "처음 그 유닛 종류를 선택했을 때" (오버레이, 비모달)

기존 커맨드 패널(`slots[]`)은 그대로 두고, **별도의 오버레이 패널**(`traitOverlayRoot` + 전용 2개 버튼)을 `UIController`에 새로 추가한다. 커맨드 패널을 갈아끼우는 다른 `ShowXXXPanel`들과 달리, 이 오버레이는 "떠 있음/숨김"만 매 프레임 독립적으로 토글되고 커맨드 패널 갱신 로직과 간섭하지 않는다.

```csharp
// RTSUnitController.UpdateUI() - case UnitState.AttackUnit 분기 안, 기존 ShowAttackUnitPanel(...) 호출은 그대로 둔 채로 추가
UnitController representative = selectedUnitList[0];
UnitData data = GetUnitData(representative.GetUnitID());

if (data.hasTraitChoice && GetChosenTrait(data.ID) == TraitChoice.None)
{
    uIController.ShowTraitSelectOverlay(
        data.traitA, () => ChooseTrait(data.ID, TraitChoice.A),
        data.traitB, () => ChooseTrait(data.ID, TraitChoice.B));
}
else
{
    uIController.HideTraitSelectOverlay();
}
```
```csharp
// UIController.cs - 커맨드 패널(slots[])과 완전히 별개의 UI 오브젝트/버튼 2개
[Header("Trait Select Overlay (독립 UI - 커맨드 패널과 별개)")]
[SerializeField] private GameObject traitOverlayRoot;
[SerializeField] private Button traitAButton;
[SerializeField] private Image traitAIcon;
[SerializeField] private TextMeshProUGUI traitADescText;
[SerializeField] private Button traitBButton;
[SerializeField] private Image traitBIcon;
[SerializeField] private TextMeshProUGUI traitBDescText;

public void ShowTraitSelectOverlay(UnitTraitOption traitA, Action onTraitA, UnitTraitOption traitB, Action onTraitB)
{
    traitOverlayRoot.SetActive(true);
    traitAIcon.sprite = traitA.icon;
    traitADescText.text = $"{traitA.skillName}\n{traitA.description}";
    traitAButton.onClick.RemoveAllListeners();
    traitAButton.onClick.AddListener(() => onTraitA());
    // traitB도 동일 패턴
}

public void HideTraitSelectOverlay() => traitOverlayRoot.SetActive(false);
```
- 커맨드 패널(`slots[]`)을 전혀 건드리지 않으므로, 오버레이가 떠 있는 동안에도 이동/공격 등 기존 버튼과 단축키가 그대로 정상 동작한다(요구사항 그대로 충족).
- 씬 계층상 오버레이 오브젝트를 커맨드 패널보다 위(Canvas의 나중 sibling 순서 또는 별도 상위 정렬)에 배치해서 "위에 겹쳐 보이게" 한다.
- 선택이 바뀌거나(다른 유닛 선택) 특성을 고르고 나면 다음 프레임에 `HideTraitSelectOverlay()`가 호출돼 자동으로 사라짐 - "찍으면 안 보이기" 요구사항 충족.

## 5) 선택 후 - order panel에 스킬 버튼 + 단축키 추가 (액티브 트레이트만)

```csharp
UnitTraitOption trait = representative.GetCurrentTrait() == TraitChoice.A ? data.traitA : data.traitB;
bool hasActiveSkillButton = data.hasTraitChoice && GetChosenTrait(data.ID) != TraitChoice.None && trait.isActiveSkill;

if (hasActiveSkillButton)
{
    uIController.ShowPanel(UIController.UISelectionState.CombatUnit,
        new CommandButtonData(moveIcon, ButtonAction.Simple(EnterMoveMode, "Move", "...", KeyCode.M)),
        new CommandButtonData(attackIcon, ButtonAction.Simple(EnterAttackMode, "Attack", "...", KeyCode.A)),
        new CommandButtonData(stopIcon, ButtonAction.Simple(StopSelectedUnits, "Stop", "...", KeyCode.S)),
        new CommandButtonData(patrolIcon, ButtonAction.Simple(EnterPatrolMode, "Patrol", "...", KeyCode.P)),
        new CommandButtonData(holdIcon, ButtonAction.Simple(HoldSelectedUnits, "Hold", "...", KeyCode.H)),
        new CommandButtonData(trait.icon, ButtonAction.Simple(
            () => ActivateSkill(representative, trait),
            trait.skillName,
            $"{trait.description} \nshortcut key [<color=yellow>{trait.shortcutKey}</color>]",
            trait.shortcutKey)));
}
else
{
    uIController.ShowAttackUnitPanel(...); // 패시브 트레이트거나 아직 특성 없음 - 기존 5버튼 그대로
}
```
- 패시브 트레이트(`isActiveSkill == false`)는 `ApplyTrait()` 시점에 스탯/행동에 조용히 반영되고 order panel 버튼은 추가되지 않는다.
- 단축키는 `ProductionSlot`이 스스로 감지하므로(doc/0042) 새 키 입력 처리 코드가 필요 없음.
- 6번째 슬롯을 쓰므로 건물 리프트 전용 슬롯(인덱스 8)과 겹치지 않음 - 유닛 선택 패널은 애초에 리프트 슬롯을 안 씀.

## 6) 스킬 발동 로직 (액티브 트레이트에만 해당)

```csharp
// RTSUnitController - 기존 SpawnUnit(unitID) 스위치와 같은 자리, 같은 스타일
public void ActivateSkill(UnitController unit, UnitTraitOption trait)
{
    if (!unit.CanUseSkill()) return; // 쿨다운 체크(유닛 개체별로 독립적인 쿨다운)

    switch (unit.GetUnitID())
    {
        case UnitID.Goliath:
            if (unit.GetCurrentTrait() == TraitChoice.A) GoliathSkillA(unit);
            else GoliathSkillB(unit);
            break;
        case UnitID.Tank: ...
        case UnitID.Wraith: ...
        case UnitID.Guardian: ...
    }

    unit.StartSkillCooldown(trait.cooldown);
}
```
쿨다운은 `alreadyAttacked`/`timeBetweenAttacks`(공격 쿨다운)와 동일한 패턴(`float` 타이머 + `Invoke`)으로 `UnitController`에 추가.

패시브 트레이트는 `ActivateSkill` 대상이 아니고, `ApplyTrait(choice)` 안에서 유닛 타입별 스탯 보정(예: `attackDamage`, `armor` 등 기존 필드에 곱연산/가산)을 직접 적용한다 - 이 부분도 `RTSUnitController.ApplyTraitStats(unit, choice)`처럼 unitID 중앙 스위치로 모아두면 `SpawnUnit`/`ActivateSkill`과 결이 맞음.

실제 스킬 효과(예: 골리앗 "장점 극대화" = 일시적 공격력 증가 액티브 버프, "단점 보완" = 방어력 영구 상승 패시브 등)와 각 트레이트의 액티브/패시브 여부는 유닛별로 balance 설계가 필요하므로 이 문서 범위 밖 - 유닛/스킬이 정해지면 유닛별로 별도 `doc/NNNN-*.md`에서 구체 수치와 함께 구현.

## 예상 변경 파일 (구현 승인 시)
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` - `UnitTraitOption`(`isActiveSkill` 포함), `hasTraitChoice/traitA/traitB` 추가
- `Assets/Scripts/System/RTSUnitController.cs` - `TraitChoice` enum, `chosenTraits` 딕셔너리, `ChooseTrait`/`GetChosenTrait`/`ActivateSkill`/`ApplyTraitStats`, `UpdateUI()`에 오버레이 토글 + 스킬 버튼 조건부 추가 로직
- `Assets/Scripts/Unit/UnitController.cs` - `currentTrait`, `ApplyTrait`, 스킬 쿨다운 필드/메서드(`CanUseSkill`/`StartSkillCooldown`)
- `Assets/Scripts/UI/UIController.cs` - `ShowTraitSelectOverlay`/`HideTraitSelectOverlay` 추가(커맨드 패널과 독립된 오버레이 UI, 씬에 오버레이용 GameObject/버튼 2개를 새로 배치해야 함 - 에디터 작업 필요)
- (고급유닛으로 지정된 각 유닛의 스킬 효과 구현 - 유닛/스킬 확정 후 별도 파일)

## 후속 요청 (같은 세션) - 슬롯 인덱스 고정 + 사용자가 이미 만들어둔 SkillSelect UI 연결

> 스킬 선택으로 추가된 스킬은 인덱스값 [6]번 slot에 추가되도록 해주고 현재는 스킬 구현은 아직없지만 구현된 스킬은 아마도 UnitController나 새로운 스크립트는 추가해서 구현해야할거 같아서 이를 인지하고 연결하기 좋도록 만들어주고
> SkillSelect이라는 새로운 툴팁같은 느낌의 특성창을 만들었어 여기엔 Slot0, 1이 있고 여기에 해당하는 스킬이 들어가서 이를 클릭시 스킬 선택이 될거야 그럼 유닛스크립터블오브젝트 -> 고급유닛 유무 확인 -> 해당유닛의 고유 스킬2개 불러오기 -> 해당 유닛 선택 시(스킬 선택을 안했을시) 특성창이 order_panel 위에(order_panel의 위쪽 모서리 위에)위치 하고 -> 스킬 선택시 6번쨰 슬롯 slot6에 추가 -> 해당 스킬의 단축키, 아이콘등이 등록 되어야하고 스킬 사용도 되어야함.(스킬을 찍은 그 유닛과 같은유닛들은 모두 그 스킬이 보여야함) 이런식인거 같아 이에 맞게 설계 변경할건 변경하고 구현해줘

**실제 코드에 반영 완료.**

### 슬롯 6 고정 - `UIController.cs`
`ShowAttackUnitPanel`이 커맨드 패널의 index 6을 `BuildingLiftSlotIndex(8)`와 동일한 방식으로 보호하도록 변경(`UnitSkillSlotProtected`). 그래야 `RTSUnitController`가 매 프레임 별도로 슬롯 6만 갱신해도 Move/Attack 등 나머지 5버튼의 `SetCommands` 호출이 그 슬롯을 Clear()하지 않는다 - 그렇지 않으면 같은 프레임에 Clear→SetData가 반복되면서 단축키 클릭 시뮬레이션 코루틴(doc/0042)이 끊길 위험이 있었음.
```csharp
// 기존 코드
    public void ShowAttackUnitPanel(
    ButtonAction onMove, ButtonAction onAttack, ButtonAction onStop, ButtonAction onPatrol, ButtonAction onHold)
    {
        CurrentState = UISelectionState.CombatUnit;
        SetCommands(
            new CommandButtonData(moveIcon, onMove),
            new CommandButtonData(attackIcon, onAttack),
            new CommandButtonData(stopIcon, onStop),
            new CommandButtonData(patrolIcon, onPatrol),
            new CommandButtonData(holdIcon, onHold)
        );
    }
```
```csharp
// 변경 코드
    public void ShowAttackUnitPanel(
    ButtonAction onMove, ButtonAction onAttack, ButtonAction onStop, ButtonAction onPatrol, ButtonAction onHold)
    {
        CurrentState = UISelectionState.CombatUnit;

        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(moveIcon, onMove),
                new CommandButtonData(attackIcon, onAttack),
                new CommandButtonData(stopIcon, onStop),
                new CommandButtonData(patrolIcon, onPatrol),
                new CommandButtonData(holdIcon, onHold)
            },
            UnitSkillSlotProtected);
    }

    public void ShowUnitSkillSlot(CommandButtonData data)
    {
        if (UnitSkillSlotIndex < slots.Length)
            slots[UnitSkillSlotIndex]?.SetData(data);
    }

    public void ClearUnitSkillSlot()
    {
        if (UnitSkillSlotIndex < slots.Length)
            slots[UnitSkillSlotIndex]?.Clear();
    }
```
`private const int UnitSkillSlotIndex = 6;` + `UnitSkillSlotProtected` 해시셋을 `BuildingLiftSlotIndex` 옆에 추가.

### SkillSelect 오버레이 연결 - `UIController.cs`
사용자가 씬에 이미 만들어둔 "SkillSelect"(Slot0/Slot1을 가진 툴팁형 오브젝트, order panel 위쪽에 배치됨)를 인스펙터로 연결할 수 있도록 필드와 Show/Hide 메서드 추가. 커맨드 패널(`slots[]`)과 완전히 분리된 오브젝트라 이 오버레이가 떠 있는 동안에도 이동/공격 등 기존 명령을 그대로 쓸 수 있다(비모달 - 사용자가 선택한 방식).
```csharp
[Header("Skill Trait Select Overlay (SkillSelect)")]
[SerializeField] private GameObject skillSelectPanel; // "SkillSelect" 루트 오브젝트
[SerializeField] private ProductionSlot[] skillSelectSlots; // Slot0=traitA(장점 극대화), Slot1=traitB(단점 보완)

public void ShowSkillSelectPanel(CommandButtonData traitA, CommandButtonData traitB)
{
    if (skillSelectPanel != null)
        skillSelectPanel.SetActive(true);

    if (skillSelectSlots != null)
    {
        if (skillSelectSlots.Length > 0) skillSelectSlots[0]?.SetData(traitA);
        if (skillSelectSlots.Length > 1) skillSelectSlots[1]?.SetData(traitB);
    }
}

public void HideSkillSelectPanel()
{
    if (skillSelectPanel != null)
        skillSelectPanel.SetActive(false);

    if (skillSelectSlots != null)
        for (int i = 0; i < skillSelectSlots.Length; i++)
            skillSelectSlots[i]?.Clear();
}
```
`Start()`에 `HideSkillSelectPanel();` 추가(다른 패널들과 동일하게 시작 시 숨김 상태로 초기화).

**"order_panel의 위쪽 모서리 위에 위치"는 코드로 배치하지 않았다** - 사용자가 이미 씬에서 SkillSelect 오브젝트의 RectTransform을 그 위치에 배치해뒀다고 판단(에디터 레이아웃 작업). 코드는 `SetActive(true/false)`로 보이기/숨기기만 담당.

### RTSUnitController.cs - 상태 저장 + 흐름 연결
설계 문서의 2)/4)/5)/6) 섹션을 그대로 구현: `TraitChoice` enum, `chosenTraits` 딕셔너리, `GetChosenTrait`/`ChooseTrait`/`ActivateSkill`, 그리고 `UpdateUI()`에서 매 프레임(스위치 이전, 무조건) `UpdateUnitSkillUI()`를 호출해 아래 흐름을 그대로 처리:
`UnitDataSO.hasTraitChoice 확인` → `GetChosenTrait(unitID)`로 이미 골랐는지 확인 → 안 골랐으면 `ShowSkillSelectPanel(traitA, traitB)`(오버레이) → 고르면 `ChooseTrait()`가 같은 종류 전체에 반영 + 다음 프레임부터 오버레이 대신 `ShowUnitSkillSlot()`으로 슬롯 6에 버튼(액티브인 경우만, 아이콘/설명/단축키 포함) 표시.

`UpdateUI()` 스위치 이전에 무조건 호출하도록 배치한 이유: 유닛 선택 상태(`case SelectState.UnitSelect`) 안에서만 호출하면, 오버레이가 떠 있는 상태에서 사용자가 건물을 선택해버렸을 때(스위치가 `BuildingSelect` 쪽으로 빠짐) 오버레이를 못 끄고 화면에 남는 버그가 생기기 때문 - `UpdateUnitSkillUI()` 안에서 `selectedUnitList.Count == 0`이면 알아서 숨기므로, 매 프레임 무조건 호출해도 안전하고 오히려 필요함.

### UnitController.cs - 개체별 상태 + 스킬 실행 연결점
```csharp
public interface IUnitSkill
{
    void Activate(UnitController unit, RTSUnitController.TraitChoice trait);
}
```
`currentTrait`(개체가 지금 장착한 트레이트)와 `skillCooldownRemaining`(개체별 독립 쿨다운) 필드 추가, `Start()`에서 신규 생산 유닛도 기존 선택을 자동으로 물려받도록 `GetChosenTrait()` 체크 추가, `Update()`에 쿨다운 감소 한 줄 추가.

**스킬 구현 연결점 (아직 미구현 상태에서 "연결하기 좋게" 만든 부분)**: `UseTraitSkill()`이 `GetComponent<IUnitSkill>()`로 이 유닛 프리팹에 붙은 스킬 구현체를 찾아 위임한다. 아직 아무 것도 안 붙어있으면 로그만 남기고 끝난다.
```csharp
public void UseTraitSkill()
{
    IUnitSkill skill = GetComponent<IUnitSkill>();
    if (skill == null)
    {
        Debug.Log($"{name}: '{currentTrait}' 트레이트 스킬이 아직 구현되지 않았습니다 (IUnitSkill 컴포넌트 없음).");
        return;
    }

    skill.Activate(this, currentTrait);
}
```
→ 나중에 실제 스킬을 구현할 때는 `UnitController`나 `RTSUnitController`를 다시 건드릴 필요 없이, 예를 들어 `GoliathSkill : MonoBehaviour, IUnitSkill`처럼 유닛별 스크립트를 새로 만들어 `Activate(UnitController unit, TraitChoice trait)` 안에서 `trait == TraitChoice.A`/`B`로 분기해 효과를 구현하고, 그 스크립트를 해당 유닛 프리팹(예: Goliath 프리팹)에 컴포넌트로 추가하기만 하면 자동으로 연결된다.

### 실제로 아직 안 된 것 (다음 단계)
- **에디터 작업**: `UIController` 인스펙터에서 `skillSelectPanel`(SkillSelect 루트)과 `skillSelectSlots`(Slot0/Slot1) 필드를 실제 씬 오브젝트에 연결해야 함 - 코드만으로는 자동 연결되지 않음.
- **유닛별 `UnitDataSO` 데이터 채우기**: 어떤 유닛을 고급유닛으로 할지 `hasTraitChoice` 체크 + `traitA`/`traitB`(이름/설명/아이콘/액티브 여부/단축키/쿨다운) 값 입력.
- **실제 스킬 효과 구현**: 고급유닛으로 정해진 각 유닛 프리팹에 `IUnitSkill` 구현 스크립트를 새로 만들어 붙이는 작업 - 유닛/스킬 밸런스가 정해지는 대로 유닛별로 별도 `doc/NNNN-*.md`에서 진행.

## 후속 버그 3건 (같은 세션) - 에디터 연결 후 실제 테스트에서 발견

### 버그 1: SkillSelect 버튼이 안 눌림 + 위치가 order_panel 위에 안 붙음
**원인**: 사용자가 SkillSelect 오브젝트를 만들 때 기존 프로젝트의 유일한 `TooltipUI`(호버 툴팁 싱글턴) 오브젝트를 재활용해서 만들었는데, 그 컴포넌트를 제거하지 않고 그대로 남겨둠. `TooltipUI.Awake()`가 (1) `root`(=SkillSelect 자기 자신) 하위 모든 `Graphic`의 `raycastTarget`을 강제로 꺼버려서 Slot0/Slot1 버튼이 클릭을 못 받았고, (2) 전역 `TooltipUI.Instance`로 등록되어 다른 아무 버튼에 마우스를 올릴 때마다 `PositionAboveTarget()`이 SkillSelect의 위치를 그 버튼 위로 옮겨버림.
**해결**: 사용자가 에디터에서 SkillSelect 오브젝트의 `Tooltip UI (Script)` 컴포넌트를 Remove Component로 제거함(코드 변경 없음, 씬 편집만).
**남은 영향**: 이 오브젝트가 프로젝트에서 유일한 `TooltipUI`였기 때문에, 제거 후 `TooltipUI.Instance`가 계속 `null`이라 **프로젝트 전체의 호버 툴팁(Move/Attack 등 버튼 설명, 그리고 아래 패시브 스킬 설명 포함)이 지금 아무것도 안 뜬다.** 별도의 전용 Tooltip UI 오브젝트(title/description Text 등 정상 연결)를 새로 만들어야 호버 설명이 다시 보임 - 이번 세션 범위 밖, 별도 작업 필요.

### 버그 2: SkillSelect 위치가 고정 anchoredPosition이라 order_panel과 안 맞음
**해결**: `UIController.ShowSkillSelectPanel()`에서 매번 표시할 때마다 `PositionSkillSelectAbovePanel()`을 호출해 `panelRoot`의 실제 화면 좌표(월드 코너)를 기준으로 SkillSelect의 `anchoredPosition`을 다시 계산하도록 변경. Canvas가 Screen Space - Overlay라 카메라 인자 없이 계산 가능.
```csharp
private void PositionSkillSelectAbovePanel()
{
    if (skillSelectPanel == null || panelRoot == null) return;

    RectTransform overlayRect = skillSelectPanel.transform as RectTransform;
    RectTransform panelRect = panelRoot.transform as RectTransform;
    RectTransform parentRect = overlayRect != null ? overlayRect.parent as RectTransform : null;
    if (overlayRect == null || panelRect == null || parentRect == null) return;

    Vector3[] corners = new Vector3[4];
    panelRect.GetWorldCorners(corners);
    Vector3 topCenterWorld = (corners[1] + corners[2]) * 0.5f;

    Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, topCenterWorld);
    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, null, out Vector2 localPoint))
        return;

    localPoint.y += overlayRect.rect.height * 0.5f + skillSelectVerticalMargin;
    overlayRect.anchoredPosition = localPoint;
}
```
(예전에 SkillSelect에 잘못 남아있던 `TooltipUI.PositionAboveTarget()`과 계산 방식은 동일하되, 대상이 "호버 중인 버튼"이 아니라 "panelRoot 고정"으로 바뀐 버전.)

### 버그 3: 선택해도 슬롯 6에 버튼이 안 생김 - 설계 수정 (액티브/패시브 처리 방식 변경)
**원래 설계(1차 구현)**: `isActiveSkill == false`(패시브)면 슬롯 6에 아예 아무것도 넣지 않았음.
**사용자 피드백으로 설계 변경**: 액티브/패시브 상관없이 **슬롯 6엔 항상 선택된 트레이트를 넣는다.** 액티브면 클릭 가능(단축키도 동작), 패시브면 버튼을 넣긴 하되 **비활성화(Interactable=false)**로 표시 - 클릭은 안 되지만 마우스오버 시 스킬 설명은 볼 수 있어야 함.
```csharp
// 변경 코드 (RTSUnitController.UpdateUnitSkillUI 마지막 부분)
uIController.HideSkillSelectPanel();

UnitTraitOption trait = chosen == TraitChoice.A ? data.traitA : data.traitB;

string description = trait.isActiveSkill
    ? $"{trait.description} \nshortcut key [<color=yellow>{trait.shortcutKey}</color>]"
    : trait.description;

uIController.ShowUnitSkillSlot(new CommandButtonData(
    trait.icon,
    ButtonAction.Simple(
        () => ActivateSkill(data.ID, trait),
        trait.skillName,
        description,
        trait.isActiveSkill ? trait.shortcutKey : KeyCode.None),
    trait.isActiveSkill)); // Interactable = 액티브일 때만 true
```
`CommandButtonData(Sprite icon, ButtonAction action, bool interactable = true)` 생성자의 `interactable` 인자로 `trait.isActiveSkill`을 넘겨서, `ProductionSlot.SetData()`가 `button.interactable = data.Interactable && data.Callback != null`을 그대로 적용하게 함 - 패시브는 아이콘/설명은 보이지만 버튼이 안 눌림.
**주의**: 마우스오버 설명이 실제로 보이려면 버그 1에서 언급한 "별도 Tooltip UI 오브젝트"가 씬에 다시 있어야 함 - 지금은 `TooltipUI.Instance == null`이라 패시브든 액티브든 호버 설명 자체가 아직 안 뜸.

## 상태
**1차 구현 + 후속 버그 3건 수정 완료.** `UnitDataSO.cs`/`RTSUnitController.cs`/`UnitController.cs`/`UIController.cs` 4개 파일 + 씬(TestScene.unity, 사용자가 에디터에서 직접 수정)에 반영함. 남은 것: (1) 별도 Tooltip UI 오브젝트 재구성(호버 설명 복구), (2) 유닛 데이터 채우기, (3) 실제 스킬 효과(`IUnitSkill`) 구현.
