# 0251 - Worker 패널 Build 버튼(슬롯 6)이 스킬 고정 슬롯과 겹쳐서 비활성화됨 (적용 완료)

## 요청

> 이제 버그 수정할건데 worker drone의 6번슬롯 = 건설버튼이 비활성화 되어있는데 일꾼들은 스킬들을
> 가지고있지 않을 생각이라 그 slot lock거는거 굳이 안해도 될거 같은데 만약 그 자리에 다른 오더 버튼이
> 있으면 그다음인 7~8번 순으로 위치하도록 하는식으로 수정해줘

**1차 제안 방향 수정**: 처음엔 "Build 버튼을 슬롯 7로 옮기자"는 안을 제시했는데, 사용자가 반대로
정정함 — **Build는 그대로 슬롯 6에 두고, 대신 스킬 쪽을 옮겨라.** 스킬이 슬롯 6에 들어가려 하는데 이미
다른 오더 버튼(Build)이 차지하고 있으면, 스킬을 그 다음 슬롯(7, 필요하면 8)으로 옮겨서 채우는 방식으로
수정.

## 조사 내용 - 원인: 스킬 시스템이 Worker 여부와 무관하게 매 프레임 슬롯 6을 강제로 비움

`UIController.cs`가 슬롯 6을 "고급유닛 특성(스킬) 버튼 전용 고정 슬롯"으로 정의해두었고(doc/0228):

```csharp
// UIController.cs:169
private const int UnitSkillSlotIndex = 6;
private static readonly HashSet<int> UnitSkillSlotProtected = new HashSet<int> { UnitSkillSlotIndex };
```

`RTSUnitController.UpdateUnitSkillUI()`가 **선택된 유닛 종류와 무관하게 매 프레임** 이 슬롯을 직접
갱신한다 - Worker를 선택 중이어도 예외 없이 호출된다:

```csharp
// RTSUnitController.cs:1356 (UpdateUI, 매 프레임 실행, switch보다 먼저)
UpdateUnitSkillUI();
```

```csharp
// RTSUnitController.cs:1285-1302 (UpdateUnitSkillUI)
if (selectedUnitList.Count == 0) { ...; uIController.ClearUnitSkillSlot(); return; }
...
if (data == null || !data.hasTraitChoice)   // Worker는 hasTraitChoice = false
{
    uIController.HideSkillSelectPanel();
    uIController.ClearUnitSkillSlot();      // ← 매 프레임 슬롯 6을 Clear() (gameObject.SetActive(false))
    return;
}
```

반면 `ShowWorkerPanel`은 Build 버튼을 파라미터 순서상 7번째(0-index 6)에 그대로 채운다 - 정확히
`UnitSkillSlotIndex`와 같은 자리:

```csharp
// UIController.cs:970-991 (현재 코드, 변경 없음 - Build는 계속 여기 그대로 둠)
public void ShowWorkerPanel(...)
{
    CurrentState = UISelectionState.Worker;

    SetCommands(                                    // protectedSlotIndices 없음(null)
        new CommandButtonData(moveIcon, onMove),     // slot 0
        new CommandButtonData(attackIcon, onAttack), // slot 1
        new CommandButtonData(stopIcon, onStop),     // slot 2
        new CommandButtonData(patrolIcon, onPatrol), // slot 3
        new CommandButtonData(holdIcon, onHold),     // slot 4
        new CommandButtonData(returnIcon, onReturn), // slot 5
        new CommandButtonData(buildIcon, onBuild)    // slot 6 ← UnitSkillSlotIndex와 충돌!
    );
}
```

즉 매 프레임 같은 슬롯(6)에서 다음이 반복된다:
1. `UpdateUnitSkillUI()` → `ClearUnitSkillSlot()` → `slots[6].Clear()` (`gameObject.SetActive(false)`, `interactable = false`)
2. 곧바로 `ShowWorkerPanel()` → `SetCommands(...)` → `slots[6].SetData(buildData)` (`gameObject.SetActive(true)`, `interactable = true`)

이 매 프레임 `SetActive(false)→true` 반복 자체가, `BuildingLiftSlotIndex`/`UnitSkillSlotIndex` 도입
당시 남겨둔 경고 그대로의 버그를 일으킨다(`UIController.cs:158-160`, `165-168` 주석):

> 매 프레임 SetActive(false)→true를 반복하면 실행 중이던 클릭 코루틴(단축키 시뮬레이션,
> `ProductionSlot.SimulateClickRoutine`)이 그 순간 강제 종료되어 버튼이 눌린 채로 멈추고 단축키도
> 동작하지 않는 버그가 생긴다.

단축키(B)로 Build를 누르면 `PointerDown` → 0.08초 대기 → `PointerUp`/`PointerClick` 순서로 여러
프레임에 걸쳐 코루틴이 실행되는데, 그 사이 프레임마다 `gameObject`가 비활성화(`Clear()`)됐다가
재활성화(`SetData()`)되면서 Unity가 코루틴을 강제 종료시켜 `PointerUp`(=실제 `onBuild` 콜백 호출)까지
도달하지 못한다 - "Build 버튼이 눌리지 않는다(비활성화된 것처럼 보인다)"는 증상의 원인. AttackUnit
패널은 이미 `UnitSkillSlotProtected`로 슬롯 6을 보호해서 이 문제가 없는데, Worker 패널은 슬롯 6을 Build
가 실제로 쓰고 있어서 애초에 "보호"가 아니라 "스킬 쪽이 양보"해야 하는 상황.

## 제안하는 수정 - 스킬 슬롯이 6이 이미 쓰이고 있으면(Worker) 7로 대신 들어가게 함

Build는 계속 슬롯 6에 그대로 둔다(위치 변경 없음). 대신 `RTSUnitController.UnitSelectState`가 이미
"지금 선택된 게 Worker인지"를 알고 있으므로, 그 정보를 스킬 슬롯 갱신 함수에 넘겨서 Worker일 때는
슬롯 6 대신 슬롯 7을 쓰도록 한다. Worker는 실제로 스킬을 가질 계획이 없어 지금 당장은 슬롯 7이 항상
비어있지만(스킬 자체가 없으니 `ClearUnitSkillSlot`만 호출됨), 나중에 Worker에도 스킬이 생기면 자동으로
슬롯 7에 자리를 잡는다.

**`Assets/Scripts/UI/UIController.cs`**

```csharp
// 기존 코드
    private const int UnitSkillSlotIndex = 6;
    private static readonly HashSet<int> UnitSkillSlotProtected = new HashSet<int> { UnitSkillSlotIndex };
```
```csharp
// 변경 코드
    private const int UnitSkillSlotIndex = 6;
    private static readonly HashSet<int> UnitSkillSlotProtected = new HashSet<int> { UnitSkillSlotIndex };

    // 슬롯 6이 이미 다른 오더 버튼(Worker의 Build)에 쓰이고 있을 때 스킬 버튼이 대신 들어갈 슬롯(doc/0251).
    private const int UnitSkillFallbackSlotIndex = 7;
    private static readonly HashSet<int> UnitSkillFallbackSlotProtected = new HashSet<int> { UnitSkillFallbackSlotIndex };
```

```csharp
// 기존 코드
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
```csharp
// 변경 코드
    // useFallbackSlot=true면(=슬롯 6을 이미 다른 오더 버튼이 쓰고 있으면, 현재는 Worker의 Build) 슬롯 7에 넣는다.
    public void ShowUnitSkillSlot(CommandButtonData data, bool useFallbackSlot = false)
    {
        int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
        if (index < slots.Length)
            slots[index]?.SetData(data);
    }

    public void ClearUnitSkillSlot(bool useFallbackSlot = false)
    {
        int index = useFallbackSlot ? UnitSkillFallbackSlotIndex : UnitSkillSlotIndex;
        if (index < slots.Length)
            slots[index]?.Clear();
    }
```

`ShowWorkerPanel`은 이제 슬롯 7(스킬 대체 슬롯)도 보호 목록에 추가한다(Build 배치 자체는 그대로):
```csharp
// 기존 코드
        SetCommands(

            new CommandButtonData(moveIcon, onMove),
            new CommandButtonData(attackIcon, onAttack),
            new CommandButtonData(stopIcon, onStop),
            new CommandButtonData(patrolIcon, onPatrol),
            new CommandButtonData(holdIcon, onHold),
            new CommandButtonData(returnIcon, onReturn),
            new CommandButtonData(buildIcon, onBuild)
        );
```
```csharp
// 변경 코드
        // 슬롯 7은 Worker용 스킬 대체 슬롯(doc/0251) - 지금은 항상 비어있지만, 나중에 Worker도 스킬이
        // 생기면 RTSUnitController.UpdateUnitSkillUI()가 이 슬롯을 독립적으로 채운다. 여기서 같이
        // Clear()해버리면 그때도 같은 버그(매 프레임 Clear/SetData 반복)가 재현되므로 미리 보호해둔다.
        SetCommands(
            new CommandButtonData[]
            {
                new CommandButtonData(moveIcon, onMove),
                new CommandButtonData(attackIcon, onAttack),
                new CommandButtonData(stopIcon, onStop),
                new CommandButtonData(patrolIcon, onPatrol),
                new CommandButtonData(holdIcon, onHold),
                new CommandButtonData(returnIcon, onReturn),
                new CommandButtonData(buildIcon, onBuild)
            },
            UnitSkillFallbackSlotProtected);
```

**`Assets/Scripts/System/RTSUnitController.cs`**

```csharp
// 기존 코드
    private void UpdateUnitSkillUI()
    {
        if (selectedUnitList.Count == 0)
        {
            uIController.HideSkillSelectPanel();
            uIController.ClearUnitSkillSlot();
            return;
        }

        UnitController representative = selectedUnitList[0];
        UnitData data = GetUnitData(representative.GetUnitID());

        if (data == null || !data.hasTraitChoice)
        {
            uIController.HideSkillSelectPanel();
            uIController.ClearUnitSkillSlot();
            return;
        }

        TraitChoice chosen = GetChosenTrait(data.ID);

        if (chosen == TraitChoice.None)
        {
            uIController.ClearUnitSkillSlot();
            uIController.ShowSkillSelectPanel(...);
            return;
        }

        ...
        uIController.ShowUnitSkillSlot(new CommandButtonData(
            trait.icon,
            ButtonAction.Simple(...),
            trait.isActiveSkill));
    }
```
```csharp
// 변경 코드
    private void UpdateUnitSkillUI()
    {
        // Worker는 슬롯 6을 Build 버튼이 이미 쓰고 있으므로(doc/0251), 스킬은 그 다음 슬롯(7)으로 넘긴다.
        bool useFallbackSlot = UnitSelectState == UnitState.Worker;

        if (selectedUnitList.Count == 0)
        {
            uIController.HideSkillSelectPanel();
            uIController.ClearUnitSkillSlot(useFallbackSlot);
            return;
        }

        UnitController representative = selectedUnitList[0];
        UnitData data = GetUnitData(representative.GetUnitID());

        if (data == null || !data.hasTraitChoice)
        {
            uIController.HideSkillSelectPanel();
            uIController.ClearUnitSkillSlot(useFallbackSlot);
            return;
        }

        TraitChoice chosen = GetChosenTrait(data.ID);

        if (chosen == TraitChoice.None)
        {
            uIController.ClearUnitSkillSlot(useFallbackSlot);
            uIController.ShowSkillSelectPanel(...);
            return;
        }

        ...
        uIController.ShowUnitSkillSlot(new CommandButtonData(
            trait.icon,
            ButtonAction.Simple(...),
            trait.isActiveSkill),
            useFallbackSlot);
    }
```
(`...`로 생략한 부분은 기존 로직 그대로 - `ShowSkillSelectPanel` 호출 인자, `trait`/`description` 계산
등은 변경 없음.)

## 참고 / 영향 범위

- AttackUnit 패널은 전혀 영향 없음 - `UnitSelectState != UnitState.Worker`이므로 `useFallbackSlot`이
  항상 `false`, 기존과 동일하게 슬롯 6을 그대로 씀.
- Build 버튼의 위치(슬롯 6)·단축키(B)·콜백 전부 변경 없음.
- "7~8번 순으로"의 8번(=`BuildingLiftSlotIndex`)까지는 지금 당장 필요하지 않음 - 슬롯 7과 충돌하는
  다른 오더 버튼이 아직 없기 때문. 나중에 슬롯 7도 다른 용도로 쓰이게 되면 그때 같은 패턴(8로 한 번 더
  대체)을 추가하면 됨.

## 변경된 파일

- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함(설계와 구현 간 차이 없음).
