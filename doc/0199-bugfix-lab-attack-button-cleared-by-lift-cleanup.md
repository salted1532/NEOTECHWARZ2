# 0199. 버그수정: 연구소 공격력 업그레이드 버튼이 안 보임 (리프트 슬롯 정리 로직이 슬롯 0을 같이 지움)

날짜: 2026-07-21

## 요청 내용

"방어력 업그레이드 버튼은 뜨는데 공격력 업그레이드 버튼은 안뜨네 방어력은 [1]번 슬롯에 인덱스로 잘 넣어진거 같은데 [0]번 슬롯 인덱스는 비어있어"
(이후 UIController에서 attackResearchIcon/armorResearchIcon 스프라이트를 둘 다 정상적으로 연결했는데도 공격력 버튼이 계속 안 보인다고 확인해주심 — 아이콘 미연결 문제가 아님이 확정됨)

## 조사 내용

`UIController.ProductionSlot.SetData()`는 아이콘이 `null`이어도 슬롯 자체는 활성화된 채로 아이콘만 안 보이게 되는 구조라, 처음엔 "아이콘 미연결"을 의심했으나 사용자가 둘 다 연결했다고 확인해주면서 기각.

실제 원인은 `RTSUnitController.UpdateUI()`의 건물 선택(`SelectState.BuildingSelect`) 처리 흐름에 있었다:

1. `switch (TagToBuildingState(...))`에서 `BuildingState.Lab` 케이스가 `uIController.ShowLabPanel(...)`을 호출 → 커맨드 패널 슬롯 0에 공격 업그레이드 버튼, 슬롯 1에 방어 업그레이드 버튼을 정상적으로 채움 (`RTSUnitController.cs:1300-1306`).
2. 그 직후, 스위치문 바깥의 공통 후처리 코드가 실행됨 (`RTSUnitController.cs:1318-1338`):
   ```csharp
   if (representativeBuilding != null && representativeBuilding.CanLift())
   {
       // 리프트/착륙 버튼(슬롯 8)을 표시...
   }
   else if (selectedBuildingList.Count > 0)
   {
       uIController.ClearBuildingLiftSlots(); // ← 문제의 호출
   }
   ```
3. `Lab.prefab`은 `canLift: 0`(false)으로 설정되어 있음 (`Assets/prefabs/NTA/Building/Lab.prefab:316`). 그래서 `CanLift()`가 `false`를 반환해 `else if` 분기로 들어가 `ClearBuildingLiftSlots()`가 호출됨.
4. 그런데 기존 `ClearBuildingLiftSlots()`는 슬롯 8(리프트 버튼)뿐 아니라 **슬롯 0(`BuildingMoveSlotIndex`)까지 같이 `Clear()`** 하고 있었음 (`UIController.cs:1160-1167`, 수정 전):
   ```csharp
   public void ClearBuildingLiftSlots()
   {
       if (BuildingMoveSlotIndex < slots.Length)
           slots[BuildingMoveSlotIndex]?.Clear();

       if (BuildingLiftSlotIndex < slots.Length)
           slots[BuildingLiftSlotIndex]?.Clear();
   }
   ```
   → 바로 1번에서 `ShowLabPanel`이 슬롯 0에 넣어둔 공격 업그레이드 버튼이 곧바로 지워짐. 슬롯 1(방어 업그레이드)은 이 메서드가 건드리지 않는 슬롯이라 살아남음 — 사용자가 관찰한 증상과 정확히 일치.

이 메서드는 원래 "리프트 불가능한 건물을 선택했을 때, 이전에 선택했던 **리프트 가능하고 공중에 뜬** 건물이 슬롯 0에 남겨둔 '이동(Move)' 버튼의 잔상을 지운다"는 의도였다. 그런데 슬롯 0은 이미 그 앞의 스위치문(`ShowMainBasePanel`/`ShowBarracksPanel`/`ShowFactoryPanel`/`ShowAirportPanel`/`ShowLabPanel`은 각자 슬롯 0에 실제 커맨드를 채우고, 전용 패널이 없는 `SupplyDepot`/`None`은 `ClearBuildingPanelExceptLiftSlots(protectMoveSlot: false)`가 슬롯 0을 명시적으로 비움)에서 **항상 이미 올바르게 처리**되고 있어서, `ClearBuildingLiftSlots()`가 슬롯 0을 다시 지우는 부분은 애초에 불필요했다.

이 버그가 지금까지 드러나지 않았던 이유: `MainBase`/`Tier1`/`Tier2`/`Tier3` 프리팹은 전부 `canLift: 1`이라 `else if` 분기(`ClearBuildingLiftSlots` 호출)를 탈 일이 없었고, `SupplyDepot`은 `canLift: 0`이지만 전용 패널이 없어 슬롯 0이 처음부터 비어 있어서(지워도 티가 안 남) 문제가 안 보였다. `Lab`이 "`canLift: false`이면서 슬롯 0에 실제 버튼을 쓰는 첫 번째 건물"이라 이번에 처음 드러남.

## 코드 변경

**`Assets/Scripts/UI/UIController.cs`**

기존 코드:
```csharp
// 리프트 불가능한 건물을 선택했을 때(CanLift() == false) 이전에 선택했던 다른 건물의 리프트/이동 버튼이
// 잔상으로 남지 않도록 두 슬롯을 정리한다.
public void ClearBuildingLiftSlots()
{
    if (BuildingMoveSlotIndex < slots.Length)
        slots[BuildingMoveSlotIndex]?.Clear();

    if (BuildingLiftSlotIndex < slots.Length)
        slots[BuildingLiftSlotIndex]?.Clear();
}
```

변경 코드:
```csharp
// 리프트 불가능한 건물을 선택했을 때(CanLift() == false) 이전에 선택했던 다른 건물의 리프트 버튼이
// 잔상으로 남지 않도록 정리한다. 이동(0번) 슬롯은 건드리지 않는다 - 그 슬롯은 항상 방금 실행된 패널
// 표시 로직(ShowXPanel/ClearBuildingPanelExceptLiftSlots)이 이미 올바르게 채우거나 비워둔 상태라,
// 여기서 다시 지우면 Lab처럼 canLift=false이면서 슬롯 0에 실제 커맨드(공격 업그레이드 등)를 쓰는
// 패널의 버튼을 지워버리는 버그가 생긴다.
public void ClearBuildingLiftSlots()
{
    if (BuildingLiftSlotIndex < slots.Length)
        slots[BuildingLiftSlotIndex]?.Clear();
}
```

## 요약 / 영향받는 파일

- `Assets/Scripts/UI/UIController.cs` — `ClearBuildingLiftSlots()`가 더 이상 슬롯 0(이동 버튼 슬롯)을 지우지 않고, 슬롯 8(리프트 버튼 슬롯)만 정리하도록 수정.
- 이 변경으로 연구소(Lab) 선택 시 공격력/방어력 업그레이드 버튼이 둘 다 정상적으로 표시됨.
- 다른 건물(SupplyDepot 등 `canLift: false`)에는 영향 없음 — 슬롯 0은 원래도 그쪽 경로(`ClearBuildingPanelExceptLiftSlots`)에서 이미 올바르게 비워지고 있었음.

## 참고

작성 시점 문서 번호 참고: `doc/0198-lab-research-attack-armor-upgrade.md` (연구소 업그레이드 시스템 본 설계/구현 문서).
