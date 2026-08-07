# 0450. 아군 OC 강제공격이 실제로 죽이지 못하던 버그 수정

**날짜:** 2026-08-07

## 요청 내용
> 아군 OC들을 플레이어 유닛이 강제공격해서 죽일수는 있게 해줘

## 조사 내용

`doc/0447`에서 "2.5. 아군 OC 클릭" 분기를 만들 때, 유닛 쪽은 `rtsUnitController.AttackSelectedUnits(allyUnit)`를
호출하도록 했었음(건물 쪽은 `AttackEnemyBuildingSelectedUnits`를 정확히 씀). 문제는 `AttackSelectedUnits`가
내부적으로 `UnitController.AttackUnitTarget(target)`을 호출한다는 점 — 이건 "지정 추격" 경로로,
`orderedTarget`을 세팅해두고 실제 사거리 판정은 `AttackRange.GetPreferredTarget()`이 매 프레임
`enemiesInRange`(Tag `"Enemy"`인 것만 트리거로 등록되는 목록)에 그 대상이 있는지로 판정함.

아군 OC는 (`doc/0447`에서 의도적으로) Tag가 `"Enemy"`가 아니라 `Untagged`라서 **절대 `enemiesInRange`에
들어가지 않음** → `GetPreferredTarget()`이 영원히 `null`을 반환 → `Update()`가 매 프레임 조용히
아무것도 안 함. 즉 유닛이 아군 OC를 향해 처음 한 번 이동은 하지만, 계속 쫓아가지도 실제로
공격하지도 못하고 그냥 멈춰있는 상태가 됨 - "죽일 수 없는" 버그의 원인.

`UnitController.cs`에는 정확히 이런 "Tag로 감지 안 되는 대상을 강제로 지정 공격"하기 위한 별도
경로가 이미 있음 — `AttackFriendlyTarget(MonoBehaviour target)` + 그걸 매 프레임 돌리는
`FriendlyAttackTick()`. 주석에 이미 명시돼 있음: "AttackRange는 'Enemy' 태그만 감지하므로 아군 대상
전투는 여기서 직접 처리한다." 이건 플레이어가 자기 자신의 유닛/건물을 강제공격할 때, 그리고
`doc/0447`의 아군 OC **건물** 강제공격에서 이미 쓰고 있던 바로 그 경로 — 건물 쪽은 처음부터 맞게
구현했었고 유닛 쪽만 잘못된 경로(`AttackUnitTarget`)를 썼던 것.

## 변경한 내용

### 1) `RTSUnitController.cs` — `AttackAllyUnitSelectedUnits` 신설

`AttackEnemyBuildingSelectedUnits`와 동일한 구조로, `AttackFriendlyTarget`을 쓰는 `EnemyUnitController`용
버전을 추가:

```csharp
public void AttackAllyUnitSelectedUnits(EnemyUnitController target)
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].AttackFriendlyTarget(target);
    }

    PlayRepresentativeUnitVoice(audio =>
    {
        audio.PlayAttackOrderVoice();
        audio.PlayOrderSFX();
    });
}
```

### 2) `UserControl.cs` — 아군 OC 유닛 클릭 시 새 메서드 호출

**Before:** `rtsUnitController.AttackSelectedUnits(allyUnit);` (Tag 기반 경로, 작동 안 함)
**After:** `rtsUnitController.AttackAllyUnitSelectedUnits(allyUnit);` (거리 기반 경로, 정상 작동)

### 3) `UnitController.cs` — `IsAirborne` 헬퍼에 `EnemyUnitController` 케이스 추가

`AttackFriendlyTarget`이 대상의 공중 여부를 판정하는 `IsAirborne(MonoBehaviour target)`은 기존에
`UnitController`/`BuildingController`만 알아봤음 - 아군 OC 공중 유닛(Raven/Ironhawk 등, doc/0448)을
공격할 때도 정확한 고도로 이동/조준하도록 케이스 추가:

```csharp
if (target is EnemyUnitController enemyUnit)
    return enemyUnit.IsAirUnit();
```

데미지 계산 자체(`UnitController.Attack()`의 `GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`)는
이미 `EnemyUnitController` 대상을 제대로 처리하고 있어서(둘 다 hostile-OC를 공격할 때부터 쓰던
공용 로직) 손댈 필요 없었음 — 문제는 순전히 "사거리 안에 들어왔는지 판정하는 경로" 하나였음.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 37`(기존 베이스라인과 동일).
- Unity 콘솔 Error 로그 0건.

## 변경된 파일

- `Assets/Scripts/System/RTSUnitController.cs` (`AttackAllyUnitSelectedUnits` 신설)
- `Assets/Scripts/UserControl/UserControl.cs` (아군 OC 유닛 클릭 분기가 새 메서드를 호출하도록 수정)
- `Assets/Scripts/Unit/UnitController.cs` (`IsAirborne`에 `EnemyUnitController` 케이스 추가)
