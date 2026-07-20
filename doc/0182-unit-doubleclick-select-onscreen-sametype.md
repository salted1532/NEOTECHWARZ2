# 0182 - 유닛 더블클릭(좌클릭) 시 화면 안의 동일 유닛 전체 선택 설계 및 적용

## 요청

"이제 한 유닛을 더블클릭(좌클릭) 하면 카메라 화면 안에있는 그 유닛과 같은 유닛을 모두 선택하는걸
구현하고 싶어" — 유닛을 더블클릭하면 현재 카메라(화면)에 보이는 범위 안에서 그 유닛과 같은 종류인
유닛을 전부 선택하고 싶다는 요청. (스타크래프트/워크래프트류 RTS의 표준 더블클릭 선택 동작과 동일)

## 현재 구조 확인

- `Assets/Scripts/UserControl/UserControl.cs` `HandleLeftClick()`의 "1. 유닛 클릭" 분기(200~227번째 줄):
  - Attack 모드 중이면 강제 공격
  - Shift 누른 채면 `ShiftClickSelectUnit` (추가 선택)
  - 그 외에는 `pendingLeftClickSelect`에 `ClickSelectUnit(unit)`을 예약 → 마우스를 놓는 시점(`SelectObject()`)에
    드래그로 걸린 유닛이 없으면 실행됨. 더블클릭을 여기 추가하면 기존 흐름(드래그 우선, 마우스업 시 확정)을
    그대로 재사용할 수 있음.
- `Assets/Scripts/System/RTSUnitController.cs`
  - `UnitList`: 플레이어 소유 유닛 전체가 스폰 시 등록되고 죽으면 제거되는 리스트(`UnitController.cs:203`,
    `:1265`) — "화면 안의 모든 유닛"을 순회할 대상으로 이미 존재.
  - `DeselectAll()`, `DragSelectUnit(UnitController)`: 이미 공개 메서드로 존재, 그대로 재사용 가능.
- `UnitController.cs`
  - `GetUnitID()`: `UnitDataSO.ID`와 매칭되는 유닛 종류 식별자 — "같은 유닛(종류)"을 판별하는 기준으로 사용.
- "화면 안에 있다"의 판정 기준은 `SelectObject()`(`UserControl.cs:687` 부근)에서 이미 쓰는 패턴과 동일하게
  `mainCamera.WorldToScreenPoint(unit.transform.position)`으로 스크린 좌표를 구해서 `0~Screen.width`,
  `0~Screen.height` 범위 + 카메라 앞쪽(`z > 0`)인지 확인하면 됨(드래그 범위 대신 화면 전체 범위로 검사).

## 제안하는 변경 (모두 `UserControl.cs`)

### 1. 더블클릭 판정용 상태 추가

컨트롤 그룹 더블클릭과 동일한 방식이지만 별도 타이머로 관리(그룹 더블클릭과 서로 간섭하지 않도록):

```csharp
private const float UnitDoubleClickThreshold = 0.3f;
private float lastUnitClickTime = float.NegativeInfinity;
```

`Awake()`에서 별도 초기화는 필요 없음(기본값 0이 아니라 선언 시 `float.NegativeInfinity`로 이미 초기화됨).

### 2. `HandleLeftClick()`의 유닛 클릭 분기 수정

Before:
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectUnit(unit);
                else
                    pendingLeftClickSelect = () => { if (unit != null) rtsUnitController.ClickSelectUnit(unit); };

                return; // 👉 중요: 여기서 종료 (명령 안 함)
```

After:
```csharp
                if (Input.GetKey(KeyCode.LeftShift))
                    rtsUnitController.ShiftClickSelectUnit(unit);
                else
                {
                    bool isDoubleClick = Time.time - lastUnitClickTime <= UnitDoubleClickThreshold;
                    lastUnitClickTime = Time.time;

                    if (isDoubleClick)
                        pendingLeftClickSelect = () => { if (unit != null) SelectAllVisibleUnitsOfSameType(unit); };
                    else
                        pendingLeftClickSelect = () => { if (unit != null) rtsUnitController.ClickSelectUnit(unit); };
                }

                return; // 👉 중요: 여기서 종료 (명령 안 함)
```

### 3. 새 private 메서드 추가 (`SelectObject()` 근처)

```csharp
// 더블클릭한 유닛과 같은 종류(GetUnitID() 일치)이면서 현재 카메라 화면 안에 보이는 유닛을 전부 선택한다.
private void SelectAllVisibleUnitsOfSameType(UnitController referenceUnit)
{
    int unitID = referenceUnit.GetUnitID();
    List<UnitController> matches = new List<UnitController>();

    foreach (UnitController unit in rtsUnitController.UnitList)
    {
        if (unit == null || unit.GetUnitID() != unitID)
            continue;

        Vector3 screenPos = mainCamera.WorldToScreenPoint(unit.transform.position);

        if (screenPos.z <= 0f)
            continue; // 카메라 뒤쪽에 있으면 화면에 보이지 않는 것으로 취급

        if (screenPos.x < 0f || screenPos.x > Screen.width || screenPos.y < 0f || screenPos.y > Screen.height)
            continue;

        matches.Add(unit);
    }

    if (matches.Count == 0)
        return;

    rtsUnitController.DeselectAll();

    foreach (UnitController unit in matches)
        rtsUnitController.DragSelectUnit(unit);
}
```

## 확인이 필요한 부분

1. **더블클릭 판정 시간(0.3초)** — 컨트롤 그룹 더블클릭과 동일한 값을 재사용할 예정. 다르게 원하시면
   알려주세요.
2. **"같은 유닛"의 기준** — `GetUnitID()`(= `UnitDataSO.ID`, 유닛 종류)가 같으면 동일 유닛으로 판정.
   혹시 "정확히 같은 프리팹"이 아니라 다른 기준(예: 같은 컨트롤 그룹, 같은 진영 내 특정 태그 등)을
   원하시면 말씀해 주세요.
3. **Shift+더블클릭 처리** — 이번 설계는 Shift를 누르지 않은 더블클릭만 다루고(기존 선택을 지우고 새로
   선택), Shift를 누른 상태에서의 더블클릭은 별도 처리하지 않음(현재처럼 `ShiftClickSelectUnit` 그대로
   동작 — 즉 Shift+더블클릭은 그냥 유닛 하나만 추가 선택됨). 이 부분도 "화면 안 동일 유닛 추가 선택"으로
   확장할지 확인 부탁드립니다.

## 적용 결과

사용자가 설계 그대로 진행하기로 확인(0.3초 임계값 유지, `GetUnitID()` 기준, Shift+더블클릭은
확장하지 않고 기존 `ShiftClickSelectUnit` 그대로 유지)하여 `Assets/Scripts/UserControl/UserControl.cs`에
설계안 그대로 적용함:

- `UnitDoubleClickThreshold`(0.3f) 상수와 `lastUnitClickTime` 필드 추가(컨트롤 그룹 더블클릭 타이머와
  별개로 관리).
- `HandleLeftClick()`의 유닛 클릭(Shift 아닌) 분기에서 더블클릭 여부를 판정해, 더블클릭이면
  `pendingLeftClickSelect`에 `SelectAllVisibleUnitsOfSameType(unit)`을 예약(단일 클릭이면 기존대로
  `ClickSelectUnit(unit)`).
- `SelectAllVisibleUnitsOfSameType(UnitController referenceUnit)` 추가: `rtsUnitController.UnitList`를
  순회하며 `GetUnitID()`가 같고 `mainCamera.WorldToScreenPoint()` 결과가 화면 범위(`0~Screen.width`,
  `0~Screen.height`) 안이며 카메라 앞쪽(`z > 0`)인 유닛만 모아 `DeselectAll()` 후 `DragSelectUnit()`으로
  전부 선택.
