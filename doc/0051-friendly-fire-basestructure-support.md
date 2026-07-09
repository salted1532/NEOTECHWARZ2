# 0051. 아군 강제 공격에 건설 중인 구조체(BaseStructure)도 포함

**날짜:** 2026-07-10

## 요청 내용
> 구조체도 BaseStructrue 도 아군 강제 공격을 받을수 있도록 해줘

[[0014-friendly-fire-building-support|0014]]에서 완공된 건물(`BuildingController`)에 대한 아군 강제 공격(A 모드 + 아군 좌클릭)을 지원했는데, 건설 중인 구조체(`BaseStructure`)는 그때 범위에서 빠져 있었다. `UserControl.cs`의 현재 `BaseStructure` 클릭 처리에도 이렇게 명시돼 있음:
```csharp
// 건설 중인 BaseStructure 좌클릭 = 선택 (항상 단일 선택, A 모드 강제공격 등은 없음)
```
이번 요청은 이 주석 그대로의 제약을 없애고, `BaseStructure`도 `BuildingController`와 동일하게 A 모드 강제 공격 대상이 되도록 만드는 것.

## 기반 확인
- `UnitController.AttackFriendlyTarget(MonoBehaviour target)`은 이미 `UnitController`/`BuildingController` 어느 쪽이든 받을 수 있게 일반화돼 있음(0014) — `BaseStructure`도 `MonoBehaviour`이자 `.transform`/`.gameObject`만 쓰므로 **그대로 재사용 가능**, 이 메서드 자체는 변경 불필요.
- `BaseStructure`는 이미 `IDestructible`을 구현하고 있고(`Die()` → `CancelConstruction()`), `HealthManager`도 붙어있어 데미지를 받으면 정상적으로 죽는다(0014 도입 당시 "이론상의 대비"로 미리 구현해둔 것이 이번에 실제로 쓰이게 됨).
- 즉, 필요한 변경은 **① 강제 공격을 발행하는 진입점 메서드**(`RTSUnitController`)와 **② 그걸 호출하는 입력 처리**(`UserControl`) 두 곳뿐.

## 설계안

**`RTSUnitController.cs`** — `AttackFriendlyBuildingSelectedUnits` 바로 아래에 구조체용 버전 추가:
```csharp
// 기존 코드
    /// <summary>
    /// 아군 건물 강제 공격 (A 모드에서 아군 건물 좌클릭): 대상이 파괴될 때까지 끝까지 공격한다.
    /// </summary>
    public void AttackFriendlyBuildingSelectedUnits(BuildingController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
        }
    }
```
```csharp
// 변경 코드
    /// <summary>
    /// 아군 건물 강제 공격 (A 모드에서 아군 건물 좌클릭): 대상이 파괴될 때까지 끝까지 공격한다.
    /// </summary>
    public void AttackFriendlyBuildingSelectedUnits(BuildingController target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
        }
    }

    /// <summary>
    /// 아군 구조체(건설 중인 BaseStructure) 강제 공격 (A 모드에서 아군 구조체 좌클릭): 대상이 파괴(건설 취소)될 때까지 끝까지 공격한다.
    /// </summary>
    public void AttackFriendlyStructureSelectedUnits(BaseStructure target)
    {
        for (int i = 0; i < selectedUnitList.Count; ++i)
        {
            selectedUnitList[i].AttackFriendlyTarget(target);
        }
    }
```

**`UserControl.cs`** — `HandleLeftClick()`의 "3. 건물 클릭" 블록 안, `BaseStructure` 처리 부분에 A 모드 분기 추가 (건물/적/아군 유닛과 동일한 패턴):
```csharp
// 기존 코드
            // 건설 중인 BaseStructure 좌클릭 = 선택 (항상 단일 선택, A 모드 강제공격 등은 없음)
            BaseStructure baseStructure = BuildingHit.transform.GetComponent<BaseStructure>();
            if (baseStructure != null)
            {
                rtsUnitController.ClickSelectStructure(baseStructure);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
```
```csharp
// 변경 코드
            // 건설 중인 BaseStructure 좌클릭 = 선택 또는 아군 강제 공격 (A 모드 중이면 강제로 공격, 아니면 선택)
            BaseStructure baseStructure = BuildingHit.transform.GetComponent<BaseStructure>();
            if (baseStructure != null)
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackFriendlyStructureSelectedUnits(baseStructure);
                    baseStructure.FlashMarker(); // 어느 구조체가 공격 대상인지 마커 깜빡임으로 표시

                    attackPointer.transform.position = baseStructure.transform.position;
                    attackPointer.SetActive(true);

                    UsercurrentState = OrderState.None;

                    return;
                }

                rtsUnitController.ClickSelectStructure(baseStructure);

                return; // 👉 중요: 여기서 종료 (명령 안 함)
            }
```

## 참고
- `BuildingHit`(`layerBuilding`)에 `BaseStructure` 프리팹의 콜라이더도 이미 포함돼 있음 — 기존 선택 처리(`ClickSelectStructure`)와 우클릭 건설 재개 처리(`AssignBuilderToStructure`)가 같은 레이어/`GetComponent<BaseStructure>()` 패턴으로 이미 동작 중이므로 별도 레이어 설정 변경 불필요.
- 0015에서 고쳤던 "건물 클릭 블록이 땅 클릭보다 앞에 있어야 한다"는 순서 문제는 `BaseStructure` 처리가 이미 같은 "3. 건물 클릭" 블록(땅 클릭보다 앞) 안에 있으므로 이번 변경에서 재발하지 않음.
- 건설 진행 중 공격받아 파괴되면 `BaseStructure.Die() → CancelConstruction()`이 호출되어, 건물 가격 전액 환불 + 그리드 예약 해제 + 담당 일꾼 해제까지 기존 취소 로직을 그대로 탄다(전투로 파괴된 것과 플레이어가 직접 취소한 것을 구분하지 않음 — 0014에서 이미 그렇게 설계된 부분).

## 변경 예정 파일
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료** — 설계안 그대로 `RTSUnitController.cs`, `UserControl.cs`에 반영함.
