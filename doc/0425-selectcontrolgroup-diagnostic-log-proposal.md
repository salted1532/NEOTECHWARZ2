# 0425. SelectControlGroup 진단 로그 추가 (적용됨)

- 날짜: 2026-08-04

## 요청 내용

> 버튼이 클릭은 되는데 클릭시 판정이 될때도 있고 안될때도 있고 이게 클릭하는 위치따라서 UI상 가려진 부분이 있나 싶어서 확인해봐도 위치 문제는 아니고 왜그런지 모르겠네

## 조사 내용

Play 모드에서 [[0424-controlgrouppanel-click-debug-log-proposal]]로 추가한 클릭 로그를 확인하며
버튼 두 개를 20회 자동 클릭하는 스트레스 테스트를 돌려봤는데, 매번 클릭 로그가 찍히고 매번 정확히
선택됐다 - 즉 평상시 조건에서는 재현되지 않았다. 버튼/EventSystem 자체는 문제가 없다는 뜻이고,
실제 플레이 중 특정 상태와 겹쳐야 터지는 문제로 보인다.

코드를 다시 훑어보다가, **클릭은 되는데(onClick 발동) 선택은 조용히 무시될 수 있는 유일한 경로**를
찾았다. `RTSUnitController.SelectUnit()`:
```csharp
private void SelectUnit(UnitController unit)
{
    if (IsBuildMode())
        return;   // 건설모드(건물 배치 미리보기 중)면 아무것도 안 하고 조용히 리턴
    ...
    selectedUnitList.Add(unit);
}
```
`SelectControlGroup()` → `DragSelectUnit()` → `SelectUnit()` 경로에서, 이때 `IsBuildMode()`가
`true`면 로그는 찍히지만 `selectedUnitList`에는 아무것도 추가되지 않는다. 다만 실제 재현 없이는
이게 진짜 원인인지 확신할 수 없어서, 다음에 증상이 재현될 때 콘솔에서 바로 원인을 볼 수 있도록
로그를 추가하기로 했다.

## 코드 변경 (제안)

`SelectControlGroup()` 시작/끝에 그룹 인원수, `IsBuildMode()` 상태, 최종 선택된 유닛/건물 수를 한 줄로 남긴다.

**기존 코드** (`Assets/Scripts/System/RTSUnitController.cs`):
```csharp
    public void SelectControlGroup(int groupIndex)
    {
        if (PurgeAndCountControlGroup(groupIndex) == 0)
            return;

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);
    }
```

**변경 코드**:
```csharp
    public void SelectControlGroup(int groupIndex)
    {
        int memberCount = PurgeAndCountControlGroup(groupIndex);

        if (memberCount == 0)
        {
            Debug.Log($"[SelectControlGroup] 그룹 {groupIndex}: 인원 0명이라 선택 취소");
            return;
        }

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);

        Debug.Log($"[SelectControlGroup] 그룹 {groupIndex}: 저장된 인원 {memberCount}명, IsBuildMode={IsBuildMode()}, " +
            $"실제 선택된 유닛 {selectedUnitList.Count}개 / 건물 {selectedBuildingList.Count}개");
    }
```

## 요약 / 영향받는 파일

- `SelectControlGroup()` 시작/끝에 진단 로그 2줄 추가 - 저장된 인원수 대비 실제 선택된 개수, 그리고
  `IsBuildMode()` 상태를 같이 남겨서, "저장된 인원은 있는데 실제 선택 개수가 0"이면 `IsBuildMode()`
  게이트가 원인임을 바로 확인할 수 있다.
- 디버그용 로그이므로 원인이 확정되면 나중에 지워도 되는 임시 코드다.
- 영향받는 파일: `Assets/Scripts/System/RTSUnitController.cs` (적용 완료, 컴파일 확인 완료 - 0 errors)
