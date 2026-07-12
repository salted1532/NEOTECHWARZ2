# 0090. 건설 위치로 이동 중인 일꾼이 죽었을 때도 건물 가격 환불 (제안)

**날짜:** 2026-07-12

## 요청 내용
> 이제 일꾼이 건물을 지으러 가는 도중에 죽으면 그것도 환불 되도록 해줘

## 조사 내용

[[0089-refund-on-build-move-cancelled]]에서 "이동/공격 명령으로 건설 이동이 취소되는 경우"의 환불은 이미 처리했다. 이 환불은 `UnitController.CancelBuildOrder()`가 `hasBuildOrder`가 `true`일 때 `onBuildCancelled` 콜백(그리드 예약 해제 + `RefundBuilding` 호출)을 실행해주는 구조다 (`UnitController.cs:632-644`, `PlacementSystem.cs`의 `GoBuild` 호출부).

그런데 `UnitController.Die()`(`UnitController.cs:1233`)는 대기열 이탈/유닛리스트 제거/선택 해제/인구수 반환만 하고, **`CancelBuildOrder()`를 호출하지 않는다.** 즉 일꾼이 건설 위치로 이동 중(`hasBuildOrder == true`, 아직 `BaseStructure`가 생성되기 전)에 죽으면:
- 그리드 예약이 영구히 풀리지 않고,
- 건설 위치에 남겨둔 고스트도 영원히 안 지워지고,
- (0089로 이미 차감된) 건물 가격도 환불되지 않는다.

`CancelBuildOrder()`는 이미 `hasBuildOrder`가 `false`일 때 아무 것도 안 하고 바로 리턴하는 안전한 함수이므로, `Die()`에서 무조건 호출해도 평소(이동 중이 아닐 때 죽는 일반적인 경우)엔 영향이 없다.

**범위 참고**: 이번 요청은 "지으러 가는 도중"(이동 중, `hasBuildOrder`)에 한정된다. 일꾼이 이미 도착해서 `BaseStructure`에 붙어(`isConstructing == true`, `BeginConstruction` 이후) 건설을 진행하던 중 죽는 경우는 별개 상황이며, 지금은 `Die()`가 그 경우도 별도 처리하지 않는다(=`BaseStructure`가 담당 일꾼을 잃은 채로 건설이 그냥 멈춘 상태로 남음, 건물 자체가 취소되는 게 아니라서 환불 대상도 아님). 필요하시면 별도 요청으로 다뤄드릴게요.

## 설계안

**`UnitController.cs`** — `Die()`에 `CancelBuildOrder()` 호출 추가:

```csharp
// 기존 코드
    public void Die()
    {
        gatherTargetNode?.LeaveQueue(this); // 대기열/채취 중에 사망해도 자리를 비워줌

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.UnitList.Remove(this);
        controller?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록
        controller?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환

        Destroy(gameObject);
    }
```
```csharp
// 변경 코드
    public void Die()
    {
        gatherTargetNode?.LeaveQueue(this); // 대기열/채취 중에 사망해도 자리를 비워줌
        CancelBuildOrder(); // 건설 위치로 이동 중(hasBuildOrder)에 사망해도, 다른 명령으로 취소될 때와 동일하게
                             // 그리드 예약 해제 + 건물 가격 환불(onCancelled 콜백, 0089)이 실행되도록 함

        RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
        controller?.UnitList.Remove(this);
        controller?.selectedUnitList.Remove(this); // 선택된 채로 죽었을 때 UI(Info_panel/Squad_panel 등)가 유령 참조를 들고 있지 않도록
        controller?.ReleaseUnitPopulation(unitID); // 죽은 유닛이 차지하던 인구수를 현재 인구수에서 반환

        Destroy(gameObject);
    }
```

새 환불 로직을 따로 만들 필요 없이, 0089에서 만든 `GoBuild`의 `onCancelled` 콜백(그리드 해제 + `RefundBuilding`)을 죽음 시점에도 그대로 타게 만드는 것뿐이다.

## 결정한 세부 동작
- **적용 범위**: 이동 중(`hasBuildOrder == true`) 사망만 해당. 이미 `BaseStructure`에 붙어 건설 중(`isConstructing == true`)인 상태에서의 사망은 이번 범위 밖(위 "범위 참고" 참조).
- **환불액**: 0089/0044와 동일하게 건물 가격(광물/가스) 전액.

## 변경 예정 파일
- `Assets/Scripts/Unit/UnitController.cs` (1곳, `Die()`)

## 상태
**적용 완료** — 제안대로 `Assets/Scripts/Unit/UnitController.cs`의 `Die()`에 `CancelBuildOrder()` 호출을 추가함.
