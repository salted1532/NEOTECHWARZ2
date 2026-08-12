# 0530 - 건설 중인 건물 선택 시 저장된 랠리 포인트 표시

## 결과
사용자 확인 후 제안대로 구현 완료. 아래 "코드 변경" 섹션과 동일하게 2개 파일에 반영됨:
`BaseStructure.cs`, `RTSUnitController.cs`.

Unity 컴파일 확인 완료(에러 0, 경고 0).

## 날짜
2026-08-12

## 요청 내용
"건설중 basestructure도 클릭 시 저장된 랠리 포인트가 보이도록 해줘" (클릭 시 = 선택 시)

## 조사 내용
- 완공된 생산 건물(`BuildingController`)을 선택하면 `RTSUnitController.SelectBuilding()`
  (`Assets\Scripts\System\RTSUnitController.cs:629-675`)이 `IsProductionBuildingState(BuildingSelectState)`일 때
  `userControl.ShowMovePointerAt(building.GetRallyPos())`를 호출해, 랠리 위치에 이동 포인터를 3초간 잠깐
  보여준다(기존 이동/랠리 확정 포인터 이펙트 재사용).
- 건설 중인 건물 기반(`BaseStructure`)은 doc/0529에서 우클릭으로 랠리 포인트를 지정하고 저장하는 기능
  (`SetRallyPosition()` / `hasCustomRally` 플래그, `BaseStructure.cs:34-35,176-183`)이 이미 추가됐지만,
  선택 시 그 값을 보여주는 처리는 없다. `RTSUnitController.SelectStructure()`(`RTSUnitController.cs:953-962`)는
  마커만 켜고 끝난다.

## 설계 (제안)
`SelectBuilding()`의 패턴을 그대로 따르되, "저장된" 랠리 포인트만 보여준다(사용자가 실제로 우클릭으로
지정한 적 없으면 표시할 게 없으므로 스킵) - 완공 건물처럼 생산 건물 타입 여부를 별도로 가릴 필요가 없다.

1. `BaseStructure`에 `HasCustomRally()` getter를 추가한다(`GetRallyPos()` 옆).
2. `RTSUnitController.SelectStructure()`에서 `selectedBaseStructure = structure;` 직후,
   `structure.HasCustomRally()`가 true면 `userControl.ShowMovePointerAt(structure.GetRallyPos())`를 호출한다.

## 코드 변경 (제안)

### Assets\Scripts\Building\BaseStructure.cs
기존 코드:
```csharp
    public Vector3 GetRallyPos() => RallyPosition;
```
변경 코드:
```csharp
    public Vector3 GetRallyPos() => RallyPosition;
    public bool HasCustomRally() => hasCustomRally;
```

### Assets\Scripts\System\RTSUnitController.cs
기존 코드 (`SelectStructure()`):
```csharp
    private void SelectStructure(BaseStructure structure)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.BaseStructureSelect;

        structure.SelectStructure();
        selectedBaseStructure = structure;
    }
```
변경 코드:
```csharp
    private void SelectStructure(BaseStructure structure)
    {
        if (IsBuildMode())
            return;

        RTScurrentSate = SelectState.BaseStructureSelect;

        structure.SelectStructure();
        selectedBaseStructure = structure;

        // 우클릭으로 지정해둔 랠리 포인트가 있으면 선택하는 순간 잠깐 보여준다
        // (완공 건물 선택 시와 동일한 패턴, doc/0530).
        if (structure.HasCustomRally())
            userControl.ShowMovePointerAt(structure.GetRallyPos());
    }
```

## 영향받는 파일
- `Assets\Scripts\Building\BaseStructure.cs`
- `Assets\Scripts\System\RTSUnitController.cs`

## 스코프 밖(안 하는 것)
- 랠리를 지정한 적 없는 `BaseStructure`에 기본 랠리 위치를 보여주는 것은 하지 않음 - 요청이 "저장된"
  랠리 포인트를 콕 집었고, 아직 지정 전이면 완공 건물이 자체 기본값을 새로 계산하므로(doc/0529) 지금
  보여줘봐야 의미가 없다.
