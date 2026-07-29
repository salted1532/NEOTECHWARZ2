# 0304 - 영웅 유닛용 이름/공격력 직접 지정 지원

날짜: 2026-07-30

## 요청 내용

"현재 unitcontroller에서 유닛의 이름, 체력, 공격력등을 따로 직접 수치를 변경할수 있도록 할수 있어? 이게 코드 상 꼬일수도있으니깐 unit id를 0번으로 내가 바꿔서 집어넣을게 이거를 어디에 사용할거냐면 영웅 유닛(스토리에서 나오는 인물) 을 구현하는데 사용하려고 그래서 직접 수치를 따로 변경하게 만들어야해 healthmananger에서 체력은 직접 건드리고 이름이랑 공격력만 조정할수 있도록 해줘"

## 조사 내용

- `UnitController.Start()`(`Assets/Scripts/Unit/UnitController.cs:244`)는 매번 `ApplyUnitData(rtsController.GetUnitData(unitID))`를 호출한다.
- `RTSUnitController.GetUnitData(int unitID)`(`RTSUnitController.cs:1791`)는 `unitDatabase.unitData.Find(d => d.ID == unitID)` - NTA/OC 유닛 데이터 전부 ID가 1부터 시작하므로(`OC Unit Data SO.asset`, `NTA Unit Data SO.asset` 확인 결과 ID 0 사용 유닛 없음) **`unitID = 0`은 항상 매치되는 데이터가 없어 `null`을 반환**한다.
- `ApplyUnitData(UnitData data)`(`UnitController.cs:1483`)는 이미 `if (data == null) return;`으로 시작한다. 즉:
  - **공격력(`attackDamage`)**: `unitID = 0`이면 `ApplyUnitData`가 그냥 반환되어 아무것도 덮어쓰지 않는다 → 프리팹 인스펙터에 직접 넣어둔 `attackDamage` 값이 그대로 유지된다. **이미 지금 코드로 동작함, 수정 불필요.**
  - **체력**: 같은 이유로 `GetComponent<HealthManager>()?.InitializeHealth(data.hp);`도 호출되지 않는다 → `HealthManager` 자체의 인스펙터 값(요청하신 대로 "HealthManager에서 직접 건드리는" 방식)이 그대로 유지된다. **이미 지금 코드로 동작함, 수정 불필요.**
- **이름만 문제**: 유닛 이름은 `UnitController`에 필드 자체가 없고, Info Panel을 띄우는 시점에 `RTSUnitController.GetUnitName(unit.GetUnitID())`(`RTSUnitController.cs:1355`, 호출부 `RTSUnitController.cs:1524`)가 매번 ID로 중앙 데이터베이스를 조회해서 이름을 가져온다. `unitID = 0`이면 매치되는 데이터가 없어 `GetUnitName`이 빈 문자열(`string.Empty`)을 반환 → 영웅 유닛을 선택해도 Info Panel에 이름이 안 뜬다. 이 부분만 코드 변경이 필요하다.
- `GetUnitName` 호출부는 `RTSUnitController.cs:1524` 단 한 곳뿐이라(정보패널), 유닛 인스턴스(`unit`)에 접근 가능한 상태 - 유닛 자신에게 이름을 직접 물어보도록 바꾸면 된다.
- 참고(수정 대상 아님, 참고용 주의사항): `unitID = 0`을 여러 영웅에게 공용으로 쓰면 `GetChosenTrait(0)`(트레이트 선택 상태, 딕셔너리 키가 unitID)이나 `ReleaseUnitPopulation(0)`(사망 시 인구 반환, ID 0 데이터 없어 그냥 무시됨)처럼 ID 기반 시스템들이 영웅 유닛들 사이에서 상태를 공유하거나 무시될 수 있음 - 지금 요청 범위(이름/공격력/체력)엔 영향 없어서 손대지 않음.

## 확인 결과

사용자가 "장갑타입/크기타입도 못 불러오는 거 아니냐"고 재확인 → armorType/sizeType/canAttackGround/canAttackAir/attackDelivery/사거리도 attackDamage와 동일하게 `ApplyUnitData`의 null-guard 덕분에 이미 인스펙터 직접 입력으로 동작한다는 점을 설명 → "진행 (추천)"으로 heroName 필드만 추가하기로 확정, 아래대로 적용 완료. `npx uloop-cli compile` 결과 오류 0건(기존 무관 경고만 22건).

## 코드 변경 (적용 완료)

### `Assets/Scripts/Unit/UnitController.cs`

기존 코드 (`unitID` 필드 바로 아래):

```csharp
    // UnitDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetUnitName(unitID)로 조회)
    [SerializeField]
    private int unitID;
```

변경 코드:

```csharp
    // UnitDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetUnitName(unitID)로 조회)
    [SerializeField]
    private int unitID;

    // 영웅 유닛(스토리 등장인물) 전용 - unitID를 0(=UnitDataSO에 없는 값)으로 두면 ApplyUnitData가
    // null 데이터를 받아 아무것도 덮어쓰지 않으므로 attackDamage/HealthManager 값은 인스펙터에 넣은
    // 값이 그대로 유지된다. 이름만 원래 ID 조회 방식이라 별도 필드가 필요해서 추가함 (doc/0304).
    [SerializeField]
    private string heroName;
```

`GetUnitID()` getter 근처에 getter 추가:

기존 코드:

```csharp
    public int GetUnitID() => unitID;
```

변경 코드:

```csharp
    public int GetUnitID() => unitID;
    public string GetHeroName() => heroName;
```

### `Assets/Scripts/System/RTSUnitController.cs`

기존 코드 (`:1524`):

```csharp
                    uIController.ShowInfoPanel(unit.GetIcon(), GetUnitName(unit.GetUnitID()), unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor(),
                        unit.GetAttackType(), unit.GetArmorType(), unit.GetSizeType(), unit.GetShotCount());
```

변경 코드:

```csharp
                    // 영웅 유닛(heroName이 채워진 unitID=0 유닛)은 이름을 데이터베이스 대신 자기 자신에게서 가져온다 (doc/0304).
                    string displayName = string.IsNullOrEmpty(unit.GetHeroName()) ? GetUnitName(unit.GetUnitID()) : unit.GetHeroName();
                    uIController.ShowInfoPanel(unit.GetIcon(), displayName, unit.GetComponent<HealthManager>(), unit.GetAttackDamage(), unit.GetArmor(),
                        unit.GetAttackType(), unit.GetArmorType(), unit.GetSizeType(), unit.GetShotCount());
```

## 요약 / 사용 방법

- 영웅 유닛 프리팹을 만들 때: `UnitController`의 `unitID`를 `0`으로, `heroName`에 이름을, `attackDamage`에 원하는 공격력을 직접 입력. `HealthManager`도 인스펙터에서 직접 체력을 설정.
- 일반 유닛(unitID가 1 이상, DB에 있는 값)은 지금처럼 `ApplyUnitData`가 정상적으로 덮어써서 기존 동작 그대로 - 이번 변경으로 인한 영향 없음.
- 공격력/체력은 이미 되던 동작이라 코드 변경이 필요한 곳은 이름 하나뿐.

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` (수정 - heroName 필드 + getter 추가)
- `Assets/Scripts/System/RTSUnitController.cs` (수정 - Info Panel 이름 조회 시 heroName 우선)
- `doc/0304-hero-unit-name-override.md` (이 파일, 신규)
