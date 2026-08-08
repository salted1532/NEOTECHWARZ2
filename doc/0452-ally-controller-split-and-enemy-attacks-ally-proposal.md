# 0452. 아군 OC 전용 `AllyController` 분리 + 적(외계종족/OC)이 아군 OC도 공격하도록 - 제안

**날짜:** 2026-08-08

## 요청 내용
> 아군OC의 경우 EnemyController를 사용하지 않고 별도에 AllyController를 사용했으면 좋겠네
> 그리고 EnemyController가 Ally들도 공격하도록

## 조사 내용

### 1) 현재 아군 OC는 `EnemyUnitController`/`EnemyBuildingController`를 그대로 재사용 중

`doc/0447`/`0448`에서 아군 OC 15종(유닛 9 + 건물 6)을 만들 때, 새 컨트롤러를 만들지 않고 기존
`EnemyUnitController`/`EnemyBuildingController`를 그대로 쓰기로 했었음(스탯 자가조회 로직이 진영과
무관하게 동일해서). 대신 자식의 사거리 감지 컴포넌트만 `AllyAttackRange : EnemyAttackRange`라는
"이름만 다른 얇은 상속 클래스"로 교체해서 혼동을 줄였음. `AllyController`가 없는 지금 상태가 이번
요청의 "EnemyController를 쓰지 말고 AllyController를 쓰라"는 부분의 대상.

### 2) 왜 지금은 "적이 아군 OC를 공격"하지 않는지

- 아군 OC 루트의 **Tag는 `Untagged`**(의도적, `doc/0447`) — 플레이어 쪽 `AttackRange`가 `Tag=="Enemy"`만
  자동교전하므로 플레이어가 아군을 안 쏘게 하려던 것.
- 적(OC 적대 진영 9종 + 외계종족 Spore Brood 3종, 총 12개 유닛 프리팹)의 `EnemyAttackRange.targetTags`
  기본값은 플레이어 진영 Tag 목록(`Worker/AttackUnit/MainBase/Tier1~3/SupplyDepot/Lab`)뿐이라, `Untagged`인
  아군 OC는애초에 이 목록에 없어서 감지 자체를 안 함.
- 즉 "적이 아군을 공격 안 함"은 필연이 아니라 **아군 OC를 식별할 전용 Tag가 아직 없어서** 생긴 부작용.

### 3) `targetTags`가 적 프리팹 12개에 전혀 직렬화되어 있지 않음 (중요한 발견)

`doc/0447`에서 `EnemyAttackRange.TargetTags`를 `static readonly` 배열 → `[SerializeField] protected`
인스턴스 필드로 바꿨는데, 그 시점엔 이미 적 유닛 프리팹(OC 9종 + Spore Brood 3종)이 먼저 존재했음.
실제로 12개 프리팹의 `.prefab` YAML을 확인해보니 **`targetTags` 값이 전혀 직렬화돼 있지 않음**
(`UnitRange`만 있고 배열은 없음 — 필드가 생기기 *전에* 컴포넌트가 저장된 프리팹이라 Unity가 그 이후
런타임엔 C# 기본값을 그대로 씀). 즉:

> **`EnemyAttackRange.cs`의 기본 배열 리터럴에 `"AllyOC"`만 추가하면, 프리팹 12개를 한 개도 직접
> 건드리지 않고도 적 전부가 즉시 아군 OC Tag를 감지 대상에 포함하게 됨.**

### 4) `isEnemyAttacker` 플래그가 "2진영" 전제로 하드코딩돼 있음

`HealthManager.GetDamage(..., bool isEnemyAttacker)`는 원래 딱 두 진영(플레이어=`false`,
OC/외계종족=`true`)만 있다는 전제로 전역에서 쓰임:

- `UnitController.Attack()`: 항상 `isEnemyAttacker: false`로 고정(공격자=플레이어).
- `EnemyUnitController.Attack()`: 항상 `isEnemyAttacker: true`로 고정(공격자=적, 주석: "공격자는 항상
  적 진영이므로").
- `EnemyUnitController.HandleAttacked()`(피격 반격 로직): `if (isEnemyAttacker) return;` — "공격자가
  같은 진영(OC)이면 반응 안 함, **플레이어에게 맞았을 때만** 반격하러 감".
- `UnitAudio`/`BuildingAudio`: `isEnemyAttacker`가 true일 때만 "적에게 공격받음" 경보음 재생.

아군 OC가 그대로 `EnemyUnitController`를 쓰는 지금도 이미 어긋나 있음 — 만약 지금 아군 OC가
`Attack()`을 호출하면(예: 외계종족과 자동교전) `isEnemyAttacker: true`(하드코딩)로 나가서, 맞은 대상이
플레이어 유닛이면 정상이지만, 맞은 대상이 외계종족(EnemyUnitController)이면 그 외계종족의
`HandleAttacked`가 "같은 진영이 때렸다"고 착각해 **반격하러 오지 않음**. 3진영(플레이어/아군/적)이
되는 이상, 이 이진 플래그의 기준을 "공격자가 나(피격자)와 다른 진영인가"로 일반화해야 함.

### 5) 대상 진영 판정 헬퍼들이 `UnitController`/`BuildingController`만 알아봄 (`EnemyUnitController`는 모름)

- `EnemyAttackRange.CanEngage()` (지상/공중 판정): `UnitController` → `BuildingController` → 그 외엔
  `targetIsAir = false`로 기본값. 아군 OC 유닛(`EnemyUnitController` 기반)은 어느 쪽도 아니라서 **공중
  아군 OC(Ironhawk/Raven Ally 등)를 무조건 지상으로 오판**함.
- `EnemyUnitController.IsAirborne()`/`GetTargetArmor()`/`GetTargetSizeType()`/`GetTargetArmorType()`도
  동일하게 `UnitController`만 알아보고 그 외엔 기본값(공중 아님/방어력 0/Medium/Light)으로 처리 -
  아군 OC를 공격할 땐 실제 스탯이 아니라 이 기본값으로 데미지가 계산됨.

이건 사실 **오늘 이미 존재하는 잠재 버그**이기도 함 - `AllyAttackRange`(아군 OC → 외계종족 자동교전)도
`EnemyAttackRange.CanEngage()`를 그대로 상속해서 쓰므로, 아군 OC가 **공중 외계종족(스키터윙 등)을
사거리 판정에서 이미 잘못 지상으로 오판하고 있었음**(대상 도메인 체크가 우연히 항상 지상으로 나가서
공중 유닛 상대 교전이 새는 케이스). 이번에 `EnemyUnitController` 케이스를 추가하면 이 기존 버그도
같이 고쳐짐 (`doc/0450`에서 플레이어 쪽 `UnitController.IsAirborne`에 `EnemyUnitController` 케이스를
추가했던 것과 정확히 대칭되는 조치).

## 제안하는 변경

### 1) 새 Tag `AllyOC` 추가 (`ProjectSettings/TagManager.asset`)

기존 Layer `AllyOC`(13번, `doc/0447`)와는 별개 개념(Tag) - 이름만 맞춰서 헷갈리지 않게 함.

### 2) 아군 OC Variant 프리팹 15개 - 루트 Tag 변경

`Untagged` → `AllyOC` (Layer는 그대로 `AllyOC` 유지 - 클릭/커서 판정은 Layer 기반이라 영향 없음).
플레이어 `AttackRange.OnTriggerEnter`는 `Tag=="Enemy"`만 보므로 이 변경으로 플레이어가 아군을
자동공격하게 되지는 않음(그대로 안전).

### 3) `EnemyAttackRange.cs` — 기본 `targetTags`에 `"AllyOC"` 추가

```csharp
[SerializeField]
protected string[] targetTags =
    { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab", "AllyOC" };
```

위 3)의 발견대로, 적 유닛 프리팹 12개(OC 9 + Spore Brood 3)는 이 필드를 직렬화하고 있지 않아서
**프리팹을 하나도 손대지 않아도** 전부 즉시 아군 OC를 감지 대상에 포함하게 됨.

### 4) `EnemyAttackRange.CanEngage()` — `EnemyUnitController` 케이스 추가

**Before:**
```csharp
    private bool CanEngage(GameObject target)
    {
        bool targetIsAir;

        if (target.TryGetComponent<UnitController>(out var playerUnit))
        {
            if (playerUnit.IsStealthed())
                return false;

            targetIsAir = playerUnit.IsAirUnit();
        }
        else if (target.TryGetComponent<BuildingController>(out var building))
            targetIsAir = building.IsLifted();
        else
            targetIsAir = false;

        return enemyUnit.CanAttackDomain(targetIsAir);
    }
```

**After:**
```csharp
    private bool CanEngage(GameObject target)
    {
        bool targetIsAir;

        if (target.TryGetComponent<UnitController>(out var playerUnit))
        {
            if (playerUnit.IsStealthed())
                return false;

            targetIsAir = playerUnit.IsAirUnit();
        }
        else if (target.TryGetComponent<BuildingController>(out var building))
            targetIsAir = building.IsLifted();
        else if (target.TryGetComponent<EnemyUnitController>(out var otherFactionUnit)) // 아군 OC(AllyUnitController) 포함 (doc/0452)
            targetIsAir = otherFactionUnit.IsAirUnit();
        else
            targetIsAir = false;

        return enemyUnit.CanAttackDomain(targetIsAir);
    }
```

`AllyUnitController`가 `EnemyUnitController`를 상속하므로(5번 항목) 이 케이스 하나로 "적이 아군 OC를
공격할 때"와 "아군 OC가 적을 공격할 때"(상속받은 `AllyAttackRange`도 동일한 `CanEngage`를 씀) 양쪽의
공중 판정이 함께 고쳐짐.

### 5) `EnemyUnitController.cs` — 진영 플래그 가상 프로퍼티 + 대상 판정 헬퍼 확장

**a) 진영 플래그 추가 + `Attack()`/`HandleAttacked()`에서 사용:**

```csharp
// 이 컨트롤러의 진영이 플레이어에게 적대적인지. 기본값 true(OC/외계종족) - AllyUnitController가
// false로 오버라이드해서 공격 시 isEnemyAttacker 플래그와 피격 반격 판정 기준을 뒤집는다(doc/0452).
protected virtual bool IsEnemyFaction => true;
```

`HandleAttacked()` **Before → After** (반격 판정을 "OC 진영인가"가 아니라 "나와 같은 진영인가"로 일반화):
```csharp
// Before
if (isEnemyAttacker)
    return; // 공격자가 같은 진영(OC)이면 반응하지 않음 - 플레이어에게 공격받았을 때만 반격하러 간다

// After
if (isEnemyAttacker == IsEnemyFaction)
    return; // 공격자가 나와 같은 진영이면 반응하지 않음 - 다른 진영에게 공격받았을 때만 반격하러 간다 (doc/0452)
```

`Attack()`의 두 `isEnemyAttacker: true` 하드코딩을 `isEnemyAttacker: IsEnemyFaction`으로 교체.

**b) 대상 진영 판정 헬퍼 4곳에 `EnemyUnitController` 케이스 추가** (`GetTargetArmor`/`GetTargetSizeType`/
`GetTargetArmorType`/`IsAirborne`) — `UnitController` 분기 다음에 `EnemyUnitController` 분기를 추가해서
그 대상의 실제 `GetArmor()`/`GetSizeType()`/`GetArmorType()`/`IsAirUnit()`을 조회(현재는 건물처럼
기본값으로 처리됨). `doc/0450`에서 `UnitController.IsAirborne`에 했던 것과 대칭.

### 6) 신규 클래스 (`AllyAttackRange`와 동일한 "얇은 상속" 패턴, `doc/0448`)

`Assets/Scripts/FogOfWar/Ally/AllyUnitController.cs`:
```csharp
// 아군 OC 유닛 컨트롤러. EnemyUnitController를 그대로 상속해서 이동/전투 AI 로직은 100% 재사용하고,
// 진영 플래그만 뒤집는다 - 별도 클래스로 두는 이유는 AllyAttackRange(doc/0448)와 동일: 아군 OC
// 프리팹에 "EnemyUnitController"라는 이름이 붙어 있으면 헷갈리기 때문 (doc/0452).
public class AllyUnitController : EnemyUnitController
{
    protected override bool IsEnemyFaction => false;
}
```

`Assets/Scripts/FogOfWar/Ally/AllyBuildingController.cs`:
```csharp
// 아군 OC 건물 컨트롤러. EnemyBuildingController는 애초에 공격 능력이 없는 껍데기라(체력/선택/사망
// 처리만) 진영 플래그가 필요 없음 - 이름만 다른 순수 타입 식별용 상속 (doc/0452).
public class AllyBuildingController : EnemyBuildingController
{
}
```

### 7) 아군 OC Variant 프리팹 15개 — 루트 컨트롤러 컴포넌트 교체

유닛 9종의 `EnemyUnitController` → `AllyUnitController`, 건물 6종의 `EnemyBuildingController` →
`AllyBuildingController`로 컴포넌트 자체를 교체(직렬화된 필드 값은 그대로 유지 - `doc/0448`에서
`AttackRange` 자식 컴포넌트를 `EnemyAttackRange` → `AllyAttackRange`로 교체했던 것과 동일한 방식/도구).

### 영향받지 않는 부분 (그대로 둠)

- `UserControl.cs`의 "2.5 아군 OC 클릭" 분기, `RTSUnitController.AttackSelectedUnits`/
  `AttackAllyUnitSelectedUnits` 등은 전부 `EnemyUnitController`/`EnemyBuildingController` **타입**으로
  받는데, `AllyUnitController`/`AllyBuildingController`가 그 하위 타입이라 `GetComponent<EnemyUnitController>()`
  등이 그대로 찾아냄 - 코드 수정 불필요.
- `TurretController`, `UnitAnimatorDriver`, `InfantryIdleLookAround`, `VehicleIdleAnimation` 등 폴리모픽하게
  `EnemyUnitController` 타입으로 다루는 모든 곳 - 수정 불필요.
- OC 적대 진영/Spore Brood(적) 쪽은 `IsEnemyFaction` 기본값이 그대로 `true`라 동작 변화 없음.

## 확인하고 싶은 점 (1차안, 아래 2차 수정안으로 대체됨)

1. 이대로 진행해도 될까요? (Tag 1개 추가, 프리팹 15개의 Tag+컨트롤러 컴포넌트 교체, 스크립트 3개
   수정 + 2개 신규)
2. 4)번에서 다룬 `isEnemyAttacker`/반격 로직 일반화(`IsEnemyFaction`)까지 포함해서 진행할지, 아니면
   일단 "적이 아군을 감지/공격은 하되, 사거리 밖에서 맞았을 때 반격하러 쫓아가는 것"과 "경보음 진영
   판정" 세부는 나중으로 미룰지 - 포함하는 쪽을 권장함(그대로 두면 지금 막 생기는 3진영 전투에서
   피아식별이 뒤집혀 보이는 부작용이 있음).

---

## 2차 수정안 — "AllyController는 EnemyUnitController를 상속하지 않는 완전 독립 클래스로"

사용자가 위 1차안(`AllyUnitController : EnemyUnitController` 얇은 상속)에 대해 "그래도 AllyController를
만들어 둬서 아군 AI는 따로 조종하도록 하고 싶어"라고 요청 → 독립 수준을 물어봤고, **"완전 독립 클래스
(상속 없음)"**을 선택함. 이 선택이 실제로 어디까지 영향을 미치는지 다시 조사함.

### 왜 "완전 독립"이 유닛 쪽에만 크게 번지는지

`AllyUnitController`가 `EnemyUnitController`를 더는 상속하지 않으면, 지금까지 "아군 OC == EnemyUnitController
타입"이라는 전제로 짜여 있던 다형성 재사용이 전부 끊어짐:

- `RTSUnitController.selectedEnemyList`(`List<EnemyUnitController>`), `ClickSelectEnemy`/`SelectEnemy`/
  `ClearSelectedEnemyIfMatches`(전부 `EnemyUnitController` 타입 매개변수) — 아군 OC 클릭 선택이 지금
  이 목록/메서드를 그대로 빌려 쓰고 있었음(`doc/0447`). 완전 독립 타입이 되면 더 이상 이 리스트에
  넣을 수 없음 → **아군 전용 선택 목록/메서드를 병행해서 새로 만들어야 함**.
- `UnitController.IsAirborne(MonoBehaviour target)`(정적 헬퍼, `doc/0450`에서 `EnemyUnitController` 케이스를
  추가해 아군 강제공격 시 공중 여부 판정에 썼음), `UnitController.Attack()`의 `GetTargetArmor`/
  `GetTargetSizeType`/`GetTargetArmorType`(마찬가지로 `EnemyUnitController` 케이스로 아군의 실제 방어력/
  크기를 읽어옴) — 전부 새 타입 케이스를 **추가로** 얹어야 함(기존 케이스는 실제 적 대상용으로 계속 필요).
- 반면 **건물은 다름**: `EnemyBuildingController`는 AI/이동/전투 로직이 전혀 없는 순수 껍데기(체력/선택/
  사망만)라서, "AI를 따로 조종"할 대상 자체가 없음. `AllyBuildingController : EnemyBuildingController`로
  **그대로 얇게 상속**하면 `selectedEnemyBuilding`/`ClickSelectEnemyBuilding`/`AttackEnemyBuildingSelectedUnits`가
  다형성으로 계속 그대로 작동함 - 건물 쪽은 중복도, 파급도 없음. 그래서 **"완전 독립"은 유닛
  컨트롤러(`AllyController`)에만 적용하고, 건물(`AllyBuildingController`)은 1차안 그대로 얇은 상속을
  유지**하는 것을 제안함(득이 없는 곳까지 중복시키지 않음 - YAGNI).

### 최종 설계

#### 1) `Assets/Scripts/FogOfWar/Ally/AllyController.cs` (신규, `EnemyUnitController` 비상속)

`EnemyUnitController.cs`의 이동/전투 AI 로직 전체(약 670줄: `Awake`/`Update`/`MoveTo`/`AttackMoveTo`/
`ChaseTarget`/`MoveAgentTo`/`Attack`/`CalculateFinalDamage`/공중 유닛 처리/선택-마커/`ApplyUnitData`/`Die`
등)를 그대로 복제해서 시작 - 이후 이 클래스와 `EnemyUnitController`는 서로 독립적으로 진화 가능(한쪽을
고쳐도 다른 쪽엔 전혀 영향 없음, 이게 이번 요청의 핵심).

두 곳만 원본과 동작이 달라짐(피아식별 방향이 반대이므로):

- `Attack()`: `isEnemyAttacker: true` 하드코딩 → **`isEnemyAttacker: false`**(공격자가 아군이라는 뜻 -
  `UnitController.Attack()`과 동일한 값). 이래야 아군에게 맞은 외계종족/OC가 "같은 진영이 때렸다"고
  착각해 반격을 무시하지 않고 정상적으로 반격하러 옴(`EnemyUnitController.HandleAttacked`의 기존
  `if (isEnemyAttacker) return;` 조건과 맞물림 - 이쪽은 수정 없이 그대로 둬도 됨).
- `HandleAttacked()`: `if (isEnemyAttacker) return;` → **`if (!isEnemyAttacker) return;`**(피아식별
  반대) - 플레이어가 자기 아군 OC를 강제공격(`doc/0450`)했을 땐 반격하러 쫓아가지 않고, 실제 외계종족/
  OC에게 공격받았을 때만(`isEnemyAttacker == true`) 반격하러 감.

나머지 필드/메서드 이름은 기계적 복제 상태를 유지(`enemyMarker`/`enemyName`/`enemyUnitID`/
`GetEnemyUnitData` 조회 등 그대로) - 이름을 아군식으로 다듬는 리네이밍은 이번 범위에 넣지 않음(직렬화된
프리팹 필드 값을 그대로 복사해서 옮기는 절차를 단순하게 유지하기 위함). 다만 외부에서 호출하는 공개
API 4개(`SelectEnemy`/`DeselectEnemy`/`GetEnemyName`/`GetEnemyUnitID`)는 `SelectAlly`/`DeselectAlly`/
`GetAllyName`/`GetAllyUnitID`로 바꿔 부름(직렬화 값이 아니라 순수 메서드 이름이라 위험 없이 가능).

사거리 감지 자식 컴포넌트는 그대로 `AllyAttackRange`(`doc/0448`, `EnemyAttackRange` 상속)를 계속 씀 -
`attackRange` 필드 타입을 `EnemyAttackRange`로 두고 `GetComponentInChildren<EnemyAttackRange>()`로 찾으면
다형성으로 `AllyAttackRange`를 그대로 찾아냄(이 부분은 "AI 조종 분리"와 무관한 감지용 보조 컴포넌트라
1차안 그대로 유지 - 사용자 질문도 "AllyController"에 한정돼 있었음).

#### 2) `Assets/Scripts/FogOfWar/Ally/AllyBuildingController.cs` — 1차안과 동일(변경 없음)

```csharp
public class AllyBuildingController : EnemyBuildingController
{
}
```

#### 3) `RTSUnitController.cs` — 아군 유닛 전용 선택 목록/메서드 병행 추가

```csharp
public enum SelectState
{
    None, UnitSelect, BuildingSelect, EnemySelect, EnemyBuildingSelect,
    OreSelect, BaseStructureSelect, BuildMode,
    AllySelect // 아군 OC 유닛 선택 (doc/0452)
}

public List<AllyController> selectedAllyList; // EnemyUnitController와 완전히 독립된 아군 전용 선택 목록

// Awake()
selectedAllyList = new List<AllyController>();

// DeselectAll() - 기존 selectedEnemyList foreach 옆에 나란히 추가
foreach (AllyController ally in selectedAllyList)
    ally.DeselectAlly();
...
selectedAllyList.Clear();

// ClickSelectEnemy/SelectEnemy/ClearSelectedEnemyIfMatches와 완전히 동일한 패턴으로 신설
public void ClickSelectAlly(AllyController ally) { DeselectAll(); SelectAlly(ally); }
private void SelectAlly(AllyController ally) { if (IsBuildMode()) return; RTScurrentSate = SelectState.AllySelect; ally.SelectAlly(); selectedAllyList.Add(ally); }
public void ClearSelectedAllyIfMatches(AllyController ally) { ... } // ClearSelectedEnemyIfMatches와 동일 패턴

// AttackAllyUnitSelectedUnits(EnemyUnitController target) → AttackAllyUnitSelectedUnits(AllyController target)로 매개변수 타입만 변경
// (내부는 selectedUnitList[i].AttackFriendlyTarget(target) 그대로 - AttackFriendlyTarget이 MonoBehaviour를
// 받으므로 타입 변경에 영향 없음)

// Info Panel 표시 switch문에 EnemySelect 옆에 나란히 케이스 추가
case SelectState.AllySelect:
    if (selectedAllyList.Count > 0) { var ally = selectedAllyList[0]; uIController.ShowInfoPanel(ally.GetIcon(), ally.GetAllyName(), ...); }
    else uIController.HideInfoPanel();
    break;
```

#### 4) `UserControl.cs` — "2.5 아군 OC 클릭" 블록의 유닛 분기만 타입 교체

```csharp
// Before
EnemyUnitController allyUnit = allyOcHit.transform.GetComponent<EnemyUnitController>();
...
rtsUnitController.AttackAllyUnitSelectedUnits(allyUnit);
...
pendingLeftClickSelect = () => { if (allyUnit != null) rtsUnitController.ClickSelectEnemy(allyUnit); };

// After
AllyController allyUnit = allyOcHit.transform.GetComponent<AllyController>();
...
rtsUnitController.AttackAllyUnitSelectedUnits(allyUnit);
...
pendingLeftClickSelect = () => { if (allyUnit != null) rtsUnitController.ClickSelectAlly(allyUnit); };
```
건물 분기(`EnemyBuildingController allyBuilding = ...`)는 `AllyBuildingController`가 그대로
`EnemyBuildingController`를 상속하므로 **변경 없음**.

#### 5) `UnitController.cs` — 아군 강제공격 경로에 `AllyController` 인식 추가

- 정적 헬퍼 `IsAirborne(MonoBehaviour target)`(`doc/0450`에서 아군 강제공격용으로 `EnemyUnitController`
  케이스를 추가했던 자리) - 아군이 더 이상 그 타입이 아니므로 케이스를 **`EnemyUnitController` →
  `AllyController`로 교체**(실제 적 대상 이 static 헬퍼를 타는 경로가 없어 교체가 맞음 - 순수 아군
  강제공격 전용 헬퍼였음).
- `Attack(Vector3 end, GameObject enemy)` 내부의 `enemy.TryGetComponent<EnemyUnitController>(out var
  targetEnemyUnit)` 및 `GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`/`IsTargetAirborne` -
  이 경로는 평소엔 진짜 적(외계종족/OC) 대상 전투가 지나가는 주 경로라 `EnemyUnitController` 케이스는
  **그대로 유지**하고, `AllyController` 케이스를 **추가**함(플레이어가 자기 아군 OC를 강제공격해서
  죽일 때도 실제 방어력/크기/공중여부를 정확히 반영하기 위해 - `doc/0450`에서 이미 만들어둔 사실상
  같은 자리에 진영 하나를 더 얹는 것).

#### 6) `EnemyAttackRange.cs` — 1차안과 동일

- 기본 `targetTags`에 `"AllyOC"` 추가(적 프리팹 12개는 이 필드가 직렬화돼 있지 않아 자동 반영).
- `CanEngage()`에 새 케이스 추가 - 단, 1차안의 `EnemyUnitController` 대신 **`AllyController`**:
  ```csharp
  else if (target.TryGetComponent<AllyController>(out var allyUnit))
      targetIsAir = allyUnit.IsAirUnit();
  ```

#### 7) `EnemyUnitController.cs` — 1차안의 `IsEnemyFaction` 가상 프로퍼티 안 씀

1차안(상속 기반)에서 필요했던 `protected virtual bool IsEnemyFaction`과 그에 따른 `Attack()`/
`HandleAttacked()` 일반화는 **더 이상 불필요** - `AllyController`가 완전히 별개 클래스라 자기만의
하드코딩된 값(2)번 항목)을 쓰면 되고, `EnemyUnitController` 자체는 건드릴 필요가 없어짐(순수 적 전용
클래스로 그대로 남음, 오히려 1차안보다 `EnemyUnitController`에 대한 변경이 더 적어짐).

#### 8) 아군 OC Variant 프리팹 9종(유닛만) — 루트 컨트롤러 컴포넌트 교체

`EnemyUnitController` → `AllyController`(직렬화된 필드 값은 그대로 복사해서 이전 - `doc/0448`에서
`AttackRange` 자식을 `EnemyAttackRange` → `AllyAttackRange`로 교체했던 것과 동일한 도구/방식). 건물
Variant 6종은 **변경 없음**(1차안대로 `AllyBuildingController`만 새로 만들어서 교체 - 이 자체는
1차안과 동일하게 여전히 필요).

### 정리 - 1차안 대비 늘어나는/줄어드는 범위

- **늘어남**: `RTSUnitController.cs`(선택 목록/메서드 병행 신설, Info Panel 케이스 추가),
  `UnitController.cs`(강제공격 경로에 `AllyController` 케이스 추가/교체) - "AllyController가
  더 이상 EnemyUnitController가 아니어서" 다형성으로 공짜로 되던 걸 병행 구현해야 하는 부분.
- **줄어듦**: `EnemyUnitController.cs` 자체는 이번엔 **아예 안 건드림**(1차안의 `IsEnemyFaction`
  가상 프로퍼티, `HandleAttacked`/`Attack` 수정이 전부 사라지고 `AllyController.cs` 신규 파일
  안에서만 자체적으로 처리됨) - 순수 적 전용 클래스로서의 안정성은 오히려 이쪽이 더 높음.
- 건물(`AllyBuildingController`)은 AI가 없어 1차안과 동일하게 얇은 상속 유지, 파급 없음.

## 확인하고 싶은 점 (최종, 승인됨)

1. 위 2차 수정안대로 진행해도 될까요? (`AllyController.cs` 신규 - `EnemyUnitController` 로직 복제,
   `AllyBuildingController.cs` 신규 - 상속, `RTSUnitController.cs`/`UnitController.cs`/
   `UserControl.cs`/`EnemyAttackRange.cs` 수정, TagManager에 `AllyOC` Tag 추가, 아군 OC 유닛 Variant
   9종의 루트 컨트롤러 컴포넌트 교체)
2. `isEnemyAttacker`(반격 판정) 처리는 `AllyController.cs` 자체 내부 로직으로 포함해서 진행함 - 이 부분
   자체에 이견 없는지만 확인.

사용자가 "이대로 진행시켜줘"로 승인함.

## 구현 결과

2차 수정안 그대로 전부 적용함.

### 코드 변경

- `ProjectSettings/TagManager.asset` - Tag 목록에 `AllyOC` 추가.
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs` (신규) - `EnemyUnitController` 비상속, 이동/전투
  AI 로직 전체 복제. `Attack()`의 `isEnemyAttacker: true` → `false`, `HandleAttacked()`의
  `if (isEnemyAttacker) return;` → `if (!isEnemyAttacker) return;`만 원본과 다름.
- `Assets/Scripts/FogOfWar/Ally/AllyBuildingController.cs` (신규) - `EnemyBuildingController` 상속,
  빈 클래스.
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` - 기본 `targetTags`에 `"AllyOC"` 추가,
  `CanEngage()`에 `AllyController` 케이스 추가(공중 판정).
- `Assets/Scripts/Unit/UnitController.cs` - 정적 `IsAirborne(MonoBehaviour)`의 `EnemyUnitController`
  케이스를 `AllyController`로 교체(아군 강제공격 전용 경로라 실제 적 대상이 이 경로를 안 탐).
  `Attack()`/`CalculateFinalDamage`/`GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`/
  `IsTargetAirborne`에 `AllyController` 케이스를 **추가**(기존 `EnemyUnitController` 케이스는 실제
  적 전투 주경로라 그대로 유지) - 플레이어가 자기 아군 OC를 강제공격할 때도 정확한 스탯 반영.
- `Assets/Scripts/System/RTSUnitController.cs` - `SelectState.AllySelect` 추가, `selectedAllyList`
  필드/초기화/`DeselectAll()` 처리 추가, `ClickSelectAlly`/`SelectAlly`/`ClearSelectedAllyIfMatches`
  신설(`ClickSelectEnemy` 계열과 동일한 패턴), `AttackAllyUnitSelectedUnits` 매개변수 타입을
  `EnemyUnitController` → `AllyController`로 변경, Info Panel switch문에 `AllySelect` 케이스 추가.
- `Assets/Scripts/UserControl/UserControl.cs` - "2.5 아군 OC 클릭" 블록의 유닛 분기를
  `GetComponent<AllyController>()`/`ClickSelectAlly`로 교체(건물 분기는 무변경).
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` - **무변경** (설계대로).

### 프리팹 변경 - 아군 OC Variant 15개의 루트 컨트롤러 컴포넌트 교체

Unity Editor 다이나믹 코드로 각 프리팹을 `PrefabUtility.LoadPrefabContents`로 열어, 기존 컴포넌트의
`[SerializeField]`/public 필드 값을 리플렉션으로 전부 읽은 뒤 새 컴포넌트를 추가하고 그 값을 그대로
옮겨 적용, 기존 컴포넌트를 제거하고 `SaveAsPrefabAsset`으로 저장하는 방식으로 처리함.

**시행착오**: 처음엔 Unity 표준 "컴포넌트 복사/붙여넣기" API(`ComponentUtility.CopyComponent` +
`PasteComponentValues`)로 시도했는데, 이 다이나믹 코드 실행 환경(에디터 GUI 포커스가 없는 헤드리스
호출)에서는 **아무 값도 옮겨지지 않고 전부 기본값(0/빈 문자열)으로 저장되는 조용한 실패**가 발생함 -
`git diff`로 실제 저장된 YAML을 확인하고서야 발견함(`enemyUnitID=0`, `attackDamage=0` 등). 15개 프리팹
전부에 이 방식으로 저장까지 마친 뒤 발견해서, `git checkout`으로 전부 원상복구하고 리플렉션 기반
방식으로 다시 시도함. 리플렉션 1차 시도에서도 유닛 9종은 `currentState`(private, `[SerializeField]`
아님 - `EnemyState`/`AllyState`라는 서로 다른 private enum 타입) 필드까지 무차별로 복사하려다 타입
불일치 예외로 실패(건물 6종은 이 필드가 없어서 먼저 성공) - `[SerializeField]`가 붙었거나 `public`인
필드만 걸러서 복사하도록 고친 뒤 재실행해서 최종 성공함. 이 두 시행착오 모두 **저장 직전에
예외/조용한 기본값 오염이 나서 원본 파일을 덮어쓰기 전에** 검증하거나(건물은 실제로 저장됐지만 값이
정상 복사됐음을 `git diff`로 재확인함) `git checkout`으로 되돌릴 수 있는 지점에서 잡음 - 최종적으로
15개 프리팹 모두 유실 없이 정확한 값으로 마무리됨.

**최종 검증 결과** (원본 OC 프리팹의 값과 대조 완료):

| 프리팹 | enemyUnitID | isAirUnit | attackDamage | armor |
|---|---|---|---|---|
| Nanobot Repair (Ally) | 1 | false | 5 | 0 |
| Cyborg Soldier (Ally) | 2 | false | 5 | 0 |
| Striker (Ally) | 3 | false | 6 | 0 |
| Railgunner (Ally) | 4 | false | 10 | 0 |
| Brute Mech (Ally) | 5 | false | 14 | 1 |
| Heavy Assault Tank (Ally) | 6 | false | 20 | 1 |
| Ironhawk (Ally) | 7 | false | 16 | 1 |
| Raven (Ally) | 8 | **true** | 8 | 1 |
| Strike Drone (Ally) | 9 | **true** | 25 | 1 |

건물 6종(`Ally_MainBase`/`Ally_SupplyDepot`/`Ally_Tier1`/`Ally_Tier2`/`Ally_Tier3`/`Ally_Lab`)도
`enemyBuildingID`/아이콘/마커/`groundLayer` 참조까지 전부 정상 이전됨(`buildingName`은 원본 프리팹도
비어 있던 필드 - `ApplyBuildingData()`가 Start()에서 SO 값으로 덮어쓰므로 게임 동작에는 영향 없음).

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- Unity 콘솔 `get-logs --log-type Error`: 0건.
- 15개 프리팹 모두 `oldRemoved=true`(이전 컴포넌트 완전 제거) + `newCount=1`(새 컴포넌트 정확히 1개)
  확인.

## 변경된 파일

- `ProjectSettings/TagManager.asset`
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs` (신규)
- `Assets/Scripts/FogOfWar/Ally/AllyBuildingController.cs` (신규)
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
- `Assets/prefabs/OC/Ally/Unit/*.prefab` 9개 (루트 컴포넌트 `EnemyUnitController` → `AllyController`)
- `Assets/prefabs/OC/Ally/Building/*.prefab` 6개 (루트 컴포넌트 `EnemyBuildingController` →
  `AllyBuildingController`)

## 후속 수정 - 루트 Tag를 빠뜨렸던 것을 마저 적용

컨트롤러 컴포넌트 교체 작업에 집중하다가, 1차안 항목 2("루트 Tag `Untagged` → `AllyOC` 변경")를
실제로 프리팹에 적용하는 걸 빠뜨림 - `EnemyAttackRange.targetTags`에 `"AllyOC"`를 추가하고
`CanEngage()`에 케이스까지 다 만들어놨는데 정작 감지될 오브젝트의 Tag가 여전히 `Untagged`라서,
`IsValidTarget()`(Tag 매칭)이 전부 걸러버려 적이 실제로는 아군을 전혀 감지하지 못하던 상태였음.
사용자가 "EnemyController가 Ally들을 자동공격 하도록 해줘"라고 재요청해서 발견/수정함.

Unity Editor 다이나믹 코드로 15개 프리팹 전부 루트 `tag`를 `"AllyOC"`로 설정하고 저장 - 전부
`Untagged -> AllyOC` 확인됨. 플레이어 쪽 `AttackRange.OnTriggerEnter`는 `CompareTag("Enemy")`만
보므로 영향 없음(계속 안전), `Untagged`를 별도로 체크하는 코드도 없음을 grep으로 확인.

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- Unity 콘솔 Error 0건.

## 후속 조사 - "왜 EnemyController가진 유닛들이 AllyController를 공격 안할까"

Tag까지 다 고쳤는데도 실제로 안 됨 - 재조사함. `TestScene`에 배치된 실제 인스턴스(`Cyborg Soldier `,
호스타일)의 `EnemyAttackRange.targetTags`를 리플렉션으로 직접 읽어보니 `AllyOC`가 빠진 옛날 8개짜리
배열이 나옴 - 심지어 **그 프리팹을 새로 `PrefabUtility.InstantiatePrefab`으로 인스턴스화해도** 옛날
배열이 나옴. 반면 프리팹을 전혀 거치지 않고 **완전히 새 GameObject에 `AddComponent<EnemyAttackRange>()`
로 직접 붙이면 정상적으로 `AllyOC`가 포함된 새 배열**이 나옴 - 즉 컴파일된 어셈블리 자체는 최신인데,
**프리팹 에셋의 캐시된 인메모리 스냅샷만 옛날 값으로 굳어 있었음**.

원인: 이번 세션에서 Unity 에디터를 한 번 띄운 채로 여러 차례 재컴파일을 거듭했는데, `targetTags`
필드는 이 12개 호스타일 프리팹의 `.prefab` 파일에 애초에 직렬화돼 있지 않아서(코드 기본값을 그대로
씀, 위 "3) 발견" 참고) - 프리팹 에셋 자체가 다시 임포트되지 않는 한, 이미 메모리에 캐시된 (내가 코드를
고치기 이전 시점의) 기본값 스냅샷을 계속 재사용함. 이건 실제 코드/데이터 버그가 아니라 **에디터를
새로 껐다 켜면 자연히 해소되는 세션 캐시 문제** - 다음에 Unity를 새로 열면(또는 지금 세션에서 프리팹을
강제 재임포트하면) 디스크에 없는 필드는 최신 코드 기본값으로 정상적으로 채워짐.

지금 세션에서 바로 확인 가능하도록 `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate)`로
영향받는 호스타일 유닛 프리팹 13개(OC 9종 + Spore Brood 3종 + `TestEnemy.prefab`)를 강제 재임포트하고
`TestScene`을 다시 로드해서 재확인함 - `targetTags`에 `AllyOC`가 정상적으로 포함됨을 확인함
(재임포트는 파일 내용을 바꾸지 않는 순수 캐시 갱신이라 `git status`에 아무 변경도 안 잡힘).

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- Unity 콘솔 Error 0건.
- 재임포트 후 재확인: `Cyborg Soldier ` 인스턴스의 `EnemyAttackRange.targetTags` =
  `[Worker,AttackUnit,MainBase,Tier1,Tier2,Tier3,SupplyDepot,Lab,AllyOC]` (정상).

## 후속 수정 - "EnemyController는 Ally를 공격하는데 Ally들은 Enemy를 공격 안 함" (진짜 코드 버그)

이번엔 실제 로직 버그였음. `EnemyAttackRange.Awake()`가 부모에서 대상 컨트롤러를 찾는 코드:

```csharp
enemyUnit = transform.parent.GetComponent<EnemyUnitController>();
```

`AllyController`가 (요청대로) `EnemyUnitController`를 상속하지 않는 완전 독립 클래스가 되면서, 아군 OC
유닛의 `AllyAttackRange`(자식)가 부모에서 `EnemyUnitController`를 찾으면 **항상 `null`**이 됨(부모는
`AllyController`이지 `EnemyUnitController`가 아니므로). `Update()`가 매 프레임 `enemyUnit.IsAttack()`을
호출하는데 `enemyUnit`이 null이라 매 프레임 조용히(또는 예외로) 아무 일도 안 하고 넘어감 - 그래서
아군 OC가 사거리 안에 적을 감지만 하고(`targetTags`/Tag는 정상) 실제로 공격/추격은 전혀 안 하던 것.

### 수정: `IAttackRangeUnit` 인터페이스 도입

`EnemyAttackRange`가 부모 컨트롤러에게 실제로 필요한 건 몇 개 메서드(`IsAttack`/`IsIdle`/`Attack`/
`ChaseTarget`/`CanAttackDomain`)뿐이므로, 그 계약만 인터페이스로 뽑아서 `EnemyUnitController`와
`AllyController` 양쪽이 각자 구현하게 함(`HealthManager.cs`에 `IDestructible`이 인라인으로 정의된
기존 컨벤션과 동일하게 `EnemyAttackRange.cs`에 인라인으로 정의):

```csharp
public interface IAttackRangeUnit
{
    bool IsAttack();
    bool IsIdle();
    void Attack(Vector3 end, GameObject target);
    bool ChaseTarget(Vector3 pos);
    bool CanAttackDomain(bool targetIsAirUnit);
}
```

- `EnemyAttackRange.enemyUnit` 필드 타입을 `EnemyUnitController` → `IAttackRangeUnit`로 변경, `Awake()`의
  조회도 `transform.parent.GetComponent<IAttackRangeUnit>()`로 변경(다형성으로 `EnemyUnitController`/
  `AllyController` 둘 다 찾아냄).
- `EnemyUnitController : MonoBehaviour, IDestructible` → `..., IDestructible, IAttackRangeUnit` (기존
  메서드 시그니처가 이미 전부 일치해서 본문 수정 없이 인터페이스 선언만 추가).
- `AllyController`도 동일하게 `IAttackRangeUnit` 추가.

### 검증 중 발견한 별개 문제 - Edit Mode에서 `Awake()`가 동기적으로 안 도는 것처럼 보였던 함정

수정 직후 Edit Mode(Play 아님)에서 리플렉션으로 `enemyUnit` 필드를 확인했더니 계속 `null`로 나와서
한참 헤맴 - `GetComponent<IAttackRangeUnit>()`를 스크립트에서 직접 호출하면 정상적으로 찾아지는데, 정작
`Awake()`가 채워놓은 필드값만 null이었음. Play Mode에 실제로 진입해서 같은 걸 확인하니 `enemyUnit`이
정상적으로 채워져 있었음 - Editor Edit Mode에서는 `AddComponent`/씬 로드 시점에 `Awake()`가 반드시
동기적으로 즉시 실행되는 게 보장되지 않는(지연될 수 있는) 것으로 보임. 즉 이 프로젝트의 자동교전
로직은 애초에 Play Mode에서만 의미 있게 검증 가능함 - Edit Mode 리플렉션 점검은 판단 근거로 부적절.

### 테스트 중 사고 - `TestScene.unity`가 실수로 훼손됐다가 복구됨

디버깅 과정에서 `EditorSceneManager.OpenScene(...)`으로 씬을 여러 차례 강제 리로드하고
`AssetDatabase.SaveAssets()`를 반복 호출하다가, 한 번 Play Mode 진입/종료를 거친 뒤 `TestScene.unity`
파일 자체가 디스크에 (본 요청과 무관하게) 대량으로 재작성된 것을 `git status`로 발견함(212줄 추가/278줄
삭제 - 일부 프리팹 인스턴스의 위치값이 완전히 다른 값으로 바뀌어 있었음). 의도한 변경이 아니라서 즉시
`git checkout`으로 커밋된 상태로 되돌리고, 에디터에서 씬을 다시 로드해 깨끗한 상태로 재확인함 - 최종
검증은 이 복구된 상태 기준으로 다시 수행함(위 "검증" 항목).

### 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.
- Play Mode 진입 후 `TestScene`의 아군 OC 인스턴스 3종(`Cyborg Soldier (Ally)`,
  `Brute Mech (Ally)`, `Cyborg Soldier (Ally) (1)`) 전부 `enemyUnit null=False` 확인(수정 전엔 전부
  `True`였음).
- Unity 콘솔 Error 0건 (Play Mode 포함).
- `git status`: 의도한 스크립트 변경 외 부수 변경 없음(`TestScene.unity`는 커밋 상태로 복구 완료,
  워터 메시 에셋의 반복 발생하는 재직렬화 노이즈는 매번 되돌림).

## 변경된 파일 (추가)

- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` (`IAttackRangeUnit` 인터페이스 신설,
  `enemyUnit` 필드 타입 변경)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`IAttackRangeUnit` 구현 선언 추가)
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs` (`IAttackRangeUnit` 구현 선언 추가)
