## 2026-07-22

### 요청 내용

사용자가 작성한 데미지 시스템 기획 (원문 그대로):

> [데미지 시스템]
> 이게임은 2가지 상태로 유닛의 종류를 분류
>
> <경장갑, 중장갑> -> 각 유닛의 고유 데미지 보너스 수치
> 경장갑 = 보병, 경차량
> 중장갑 = 전차, 대형유닛들
>
> <소형, 중형, 대형>
> 공격 방식에 따른 데미지 시스템
>
> 소형 = 작은 보병, 경차량
> 중형 = 중형유닛 (아마 일반형의 느낌 추가 데미지 없음)
> 대형 = 대형유닛 (특정 유닛에 의한 추가 데미지 혹은
>
> <공격 방식>
> 소총, 폭발, 레이저, 화염
>
> 소총 = 소형 100% / 중형 80% / 대형 60%
> 폭발 = 소형 70%/ 중형 100%/ 대형 130%
> 레이저 = 소형 100%/ 중형 100%/ 대형 100%
> 화염 = 소형 130%/ 중형 100%/ 대형 60%
>
> <유닛 밸런싱> ... (메인기지/병영/공장/공항 별 유닛 목록, 각 유닛의 공격 방식 및 특수 보너스)

요약하면: 유닛을 **장갑 타입**(경장갑/중장갑)과 **크기 타입**(소형/중형/대형)으로 분류하고, **공격 방식**(소총/폭발/레이저/화염)에 따라 대상 크기별로 데미지 배율이 달라지는 시스템을 추가. 일부 유닛은 특정 장갑 타입 상대로 고유 추가 데미지(%)를 가짐(예: 저격수 = 소총 + 중장갑 대상 +80%). "스크립터블오브젝트에 크기/장갑 필드를 추가하고 HealthManager에서 그에 맞는 추가 피해 로직을 넣되, 공격방식별 크기 데미지 퍼센티지 표는 코드에 박아넣지 말고 인스펙터에서 조정 가능한 별도 에셋으로 빼달라"는 것이 핵심 요구.

### 조사 내용

- 현재 데미지 계산은 `UnitController.Attack()`(`Assets/Scripts/Unit/UnitController.cs:781`) 한 곳에서만 이루어짐: `finalDamage = Max(1, GetAttackDamage() - targetArmor)` — **뺄셈식 고정 방어력**만 존재하고 배율(퍼센티지) 개념 자체가 없음.
- `HealthManager.GetDamage(damage, attackerPosition, attackType)`(`Assets/Scripts/Unit/HealthManager.cs:65`)는 이미 계산되어 넘어온 최종 데미지 정수를 그대로 체력에서 깎기만 함 — 배율 계산 로직이 들어갈 자리가 원래 없음.
- `AttackEffectType`(Bullet/Explosive/Laser/Flame) enum이 `UnitController.cs:13`에 이미 존재하고, 사용자가 말한 "소총/폭발/레이저/화염"과 정확히 일치. 단, 현재는 **피격 이펙트 선택**에만 쓰이고 데미지 계산에는 전혀 관여하지 않음.
- "장갑"은 이미 있지만(`UnitController.armor`, `EnemyController.armor` — 단일 int, 연구로 증감), "경장갑/중장갑" 같은 **타입 분류**는 없음. "크기 타입"은 코드 전체에 전무.
- 공격력(`attackDamage`)/방어력(`armor`)/공격방식(`attackType`) 모두 `UnitDataSO`(생산 비용/툴팁 표시용)와 `UnitController`/`EnemyController`(실제 전투 계산용) **두 곳에 중복 저장**되어 있고, 런타임에는 서로 연결되어 있지 않음(각 유닛 프리팹의 인스펙터 값이 실제 전투에 쓰이는 값). 이번에 추가하는 장갑타입/크기타입도 이 기존 관례를 따르는 게 일관적이라고 판단함(아래 "설계 판단" 참고).
- `Lab`의 공격력/방어력 연구(`doc/0198`)는 `UpgradeManager`를 통해 `RTSUnitController.GlobalAttackBonus/GlobalArmorBonus`로 노출되고, `GetAttackDamage()/GetArmor()`가 이를 더해서 반환함. 이번 배율 시스템은 이 기존 연구 보너스 체계를 건드리지 않고 그 위에 곱연산으로 얹는 방식으로 설계함.
- 건물(`BaseStructure`/`BuildingController`)도 `HealthManager`를 갖지만, 실제로 공격받는 경로가 사실상 없음(아군 강제공격 테스트 정도). 사용자의 기획도 유닛에 대해서만 장갑/크기를 언급하므로 건물은 이번 범위에서 제외 — 건물이 공격 대상이 되면 크기=중형/장갑=경장갑(배율 100%, 보너스 없음)으로 안전하게 기본 동작(기존과 동일)하도록 처리.
- `EnemyController.cs`에는 공격 로직 자체가 없음(적이 공격 안 함). 하지만 **적 유닛이 지금 게임에서 사실상 유일한 주요 공격 대상**이므로, 장갑타입/크기타입은 `EnemyController`에도 반드시 필요함(공격은 못 해도 "맞는 쪽" 데이터는 필요).

### 설계 판단 (확인 필요 - 제 추천안)

기획 문서에 나온 "장갑/크기 필드는 SO에, 추가 피해 로직은 HealthManager에" 라는 표현을 최대한 살리되, 실제 계산 위치는 아래처럼 잡는 게 낫다고 판단했습니다. 이견 있으면 알려주세요.

1. **장갑타입/크기타입 필드는 `UnitDataSO`와 `UnitController`/`EnemyController` 양쪽에 추가.** 기존 `attackDamage`/`armor`/`attackType`이 이미 이 이중 구조(SO=표시용, 프리팹=실전투용)라서, 새 필드만 SO 단일 소스로 만들면 오히려 일관성이 깨짐. `UnitDataSO` 쪽은 생산 패널 툴팁 등 향후 표시용, `UnitController`/`EnemyController` 쪽 값이 실제 전투 계산에 쓰임(둘 다 인스펙터에서 직접 채워 넣는 방식 — SO에 유닛 추가할 때, 그리고 유닛 프리팹 만들 때 각각 한 번씩).
2. **퍼센티지 배율 계산은 `HealthManager.GetDamage()`가 아니라 `UnitController.Attack()`에 넣음.** 이유: 기존 고정 방어력 감산(`GetTargetArmor` → `finalDamage = damage - armor`)이 이미 `Attack()`에 있고, 데미지 관련 계산을 한 곳에 몰아둬야 유지보수가 쉬움. `HealthManager.GetDamage()`는 지금처럼 "이미 계산된 최종 데미지 정수"만 받는 구조를 그대로 유지. (원래 기획대로 HealthManager 안에 넣으려면 HealthManager가 장갑/크기 조회, 배율표 참조, 공격자의 고유보너스까지 다 알아야 해서 책임이 과하게 커짐 — `Attack()`에 두는 편이 기존 코드 구조와 더 잘 맞음)
3. **최종 공식**: `공격력(연구 보너스 포함) × 크기배율(공격방식×대상크기) × 고유보너스배율(공격자의 장갑타입 특효) → 반올림 → 대상의 고정 방어력만큼 감산 → 최소 1 보장.` 크기배율과 고유보너스배율은 곱연산으로 같이 적용(둘 다 있으면 중첩).
4. **공격방식×크기 배율표는 `DamageMultiplierTableSO`라는 새 ScriptableObject 에셋으로 분리.** 코드에는 수치가 전혀 없고, 인스펙터에서 4(공격방식)×3(크기) = 12개 퍼센티지 값을 자유롭게 조정 가능. 씬에는 이 에셋 하나만 있으면 되고 `RTSUnitController`가 참조를 들고 있다가 `UnitController.Attack()`이 조회.
5. **유닛 고유 보너스**(예: 저격수 = 소총+중장갑 대상 +80%)는 테이블이 아니라 각 유닛 프리팹(`UnitController`)에 `bonusVersusArmorType`(타입) + `bonusVersusArmorPercent`(퍼센트, 0이면 없음) 필드로 개별 지정. "고유"라는 표현대로 유닛마다 다르므로 공용 테이블이 아니라 유닛별 값이 맞다고 판단.

### 코드 변경 (예정)

#### 1) 신규 파일 `Assets/Scripts/Unit/DamageTypes.cs` — 장갑/크기 타입 enum

```csharp
// 장갑 타입: 경장갑(보병/경차량) vs 중장갑(전차/대형유닛). 특정 유닛의 고유 추가 데미지가 어느 쪽을 노리는지 판정하는 데 쓰인다.
public enum ArmorType { Light, Heavy }

// 크기 타입: 공격 방식(AttackEffectType)에 따른 데미지 배율(DamageMultiplierTableSO)을 조회하는 키로 쓰인다.
public enum SizeType { Small, Medium, Large }
```

#### 2) 신규 파일 `Assets/Scripts/ScriptableObject/DamageMultiplierTableSO.cs` — 공격방식×크기 배율표 (인스펙터에서 조정)

```csharp
using UnityEngine;

// 공격 방식(소총/폭발/레이저/화염) x 대상 크기(소형/중형/대형)별 데미지 배율표.
// 코드에 하드코딩하지 않고 별도 에셋으로 분리해서, 밸런스 수치를 인스펙터에서 언제든 조정할 수 있게 한다.
[CreateAssetMenu]
public class DamageMultiplierTableSO : ScriptableObject
{
    [System.Serializable]
    public class SizeMultiplier
    {
        [Tooltip("퍼센트 값. 100 = 기본 데미지 그대로, 130 = +30%, 60 = -40%")]
        public float smallPercent = 100f;
        public float mediumPercent = 100f;
        public float largePercent = 100f;

        public float GetPercent(SizeType size)
        {
            switch (size)
            {
                case SizeType.Small: return smallPercent;
                case SizeType.Medium: return mediumPercent;
                case SizeType.Large: return largePercent;
                default: return 100f;
            }
        }
    }

    public SizeMultiplier bullet = new SizeMultiplier { smallPercent = 100f, mediumPercent = 80f, largePercent = 60f };
    public SizeMultiplier explosive = new SizeMultiplier { smallPercent = 70f, mediumPercent = 100f, largePercent = 130f };
    public SizeMultiplier laser = new SizeMultiplier { smallPercent = 100f, mediumPercent = 100f, largePercent = 100f };
    public SizeMultiplier flame = new SizeMultiplier { smallPercent = 130f, mediumPercent = 100f, largePercent = 60f };

    // attackType/targetSize 조합에 해당하는 배율(1.0 = 100%)을 반환한다.
    public float GetMultiplier(AttackEffectType attackType, SizeType targetSize)
    {
        SizeMultiplier table = attackType switch
        {
            AttackEffectType.Bullet => bullet,
            AttackEffectType.Explosive => explosive,
            AttackEffectType.Laser => laser,
            AttackEffectType.Flame => flame,
            _ => null
        };

        return table != null ? table.GetPercent(targetSize) / 100f : 1f;
    }
}
```

#### 3) `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — armorType/sizeType 필드 추가 (표시용)

**기존 코드**
```csharp
    [field: SerializeField]
    public int tier { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }
```

**변경 코드**
```csharp
    [field: SerializeField]
    public int tier { get; private set; }

    // 장갑 타입(경장갑/중장갑)과 크기 타입(소형/중형/대형). 실제 전투 계산에는 유닛 프리팹의 UnitController 값이
    // 쓰이고(기존 attackDamage/armor/attackType과 동일한 방식), 여기 값은 생산 패널 등 표시용.
    [field: SerializeField]
    public ArmorType armorType { get; private set; }
    [field: SerializeField]
    public SizeType sizeType { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }
```

#### 4) `Assets/Scripts/Unit/UnitController.cs` — armorType/sizeType/고유보너스 필드 + 배율 적용

**기존 코드** (30~36행)
```csharp
    // ===== 전투 스탯 (공격력/방어력) =====
    // 공격력은 기존 AttackRange.AttackDamage였던 것을 이곳으로 옮겨 UnitController가 함께 관리한다.
    // Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값이기도 하다.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;
    // 이 유닛의 공격 수단 (총기 든 유닛은 Bullet, 탱크류는 Explosive 등) - 피격 이펙트 선택에 사용됨
    [SerializeField] private AttackEffectType attackType = AttackEffectType.Bullet;
```

**변경 코드**
```csharp
    // ===== 전투 스탯 (공격력/방어력) =====
    // 공격력은 기존 AttackRange.AttackDamage였던 것을 이곳으로 옮겨 UnitController가 함께 관리한다.
    // Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값이기도 하다.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;
    // 이 유닛의 공격 수단 (총기 든 유닛은 Bullet, 탱크류는 Explosive 등) - 피격 이펙트 선택에 사용됨
    [SerializeField] private AttackEffectType attackType = AttackEffectType.Bullet;

    // 이 유닛이 "공격받을 때" 적용되는 분류 (DamageMultiplierTableSO/고유 보너스 판정에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;

    [Header("고유 추가 데미지 (해당 없으면 Percent를 0으로 둘 것)")]
    [Tooltip("이 유닛이 특정 장갑 타입 상대로만 추가 데미지를 줄 때 설정 (예: 저격수 = Heavy, 80)")]
    [SerializeField] private ArmorType bonusVersusArmorType = ArmorType.Light;
    [SerializeField] private float bonusVersusArmorPercent = 0f;
```

**기존 코드** (`Attack()`, 781~822행)
```csharp
    public void Attack(Vector3 end, GameObject enemy)
    {
        ...
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            int targetArmor = GetTargetArmor(enemy);
            int finalDamage = Mathf.Max(1, GetAttackDamage() - targetArmor); // 방어력만큼 감산, 최소 1 데미지는 보장
            targetHealth.GetDamage(finalDamage, transform.position, attackType); // 위치+공격 타입을 같이 넘겨 피격 이펙트 선택/방향 계산에 사용
            GetComponent<UnitEffects>()?.PlayAttack();
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
    }

    // 공격 대상의 방어력을 조회한다 (아군 유닛이면 연구 보너스가 반영된 GetArmor(), 적 유닛이면 EnemyController의 armor, 그 외(건물/자원)는 0).
    private int GetTargetArmor(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetArmor();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetArmor();

        return 0;
    }
```

**변경 코드**
```csharp
    public void Attack(Vector3 end, GameObject enemy)
    {
        ...
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            int targetArmor = GetTargetArmor(enemy);
            int finalDamage = CalculateFinalDamage(enemy, targetArmor);
            targetHealth.GetDamage(finalDamage, transform.position, attackType); // 위치+공격 타입을 같이 넘겨 피격 이펙트 선택/방향 계산에 사용
            GetComponent<UnitEffects>()?.PlayAttack();
        }

        alreadyAttacked = true;
        Invoke(nameof(ResetAttack), timeBetweenAttacks);
    }

    // 공격방식×대상크기 배율(DamageMultiplierTableSO)과 이 유닛의 고유 장갑타입 보너스를 곱연산으로 적용한 뒤,
    // 대상의 고정 방어력을 감산해 최종 데미지를 계산한다. 최소 1은 항상 보장.
    private int CalculateFinalDamage(GameObject target, int targetArmor)
    {
        SizeType targetSize = GetTargetSizeType(target);
        ArmorType targetArmorType = GetTargetArmorType(target);

        DamageMultiplierTableSO table = rtsController != null ? rtsController.DamageMultiplierTable : null;
        float sizeMultiplier = table != null ? table.GetMultiplier(attackType, targetSize) : 1f;

        float bonusMultiplier = (bonusVersusArmorPercent != 0f && targetArmorType == bonusVersusArmorType)
            ? 1f + bonusVersusArmorPercent / 100f
            : 1f;

        int scaledAttack = Mathf.RoundToInt(GetAttackDamage() * sizeMultiplier * bonusMultiplier);
        return Mathf.Max(1, scaledAttack - targetArmor);
    }

    // 공격 대상의 방어력을 조회한다 (아군 유닛이면 연구 보너스가 반영된 GetArmor(), 적 유닛이면 EnemyController의 armor, 그 외(건물/자원)는 0).
    private int GetTargetArmor(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetArmor();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetArmor();

        return 0;
    }

    // 공격 대상의 크기 타입을 조회한다 (건물/자원 등 타입 정보가 없는 대상은 Medium → 배율 100%로 영향 없음).
    private SizeType GetTargetSizeType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetSizeType();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetSizeType();

        return SizeType.Medium;
    }

    // 공격 대상의 장갑 타입을 조회한다 (건물/자원 등은 고유 보너스가 적용될 일이 없으므로 Light를 기본값으로 반환).
    private ArmorType GetTargetArmorType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetArmorType();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetArmorType();

        return ArmorType.Light;
    }
```

**추가 getter** (`GetArmor()`/`GetAttackType()` 근처)
```csharp
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
```

#### 5) `Assets/Scripts/Enemy/EnemyController.cs` — armorType/sizeType 필드 (공격받는 쪽 데이터)

**기존 코드**
```csharp
    // ===== 전투 스탯 (공격력/방어력) =====
    // UnitController와 동일한 패턴: Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;
```

**변경 코드**
```csharp
    // ===== 전투 스탯 (공격력/방어력) =====
    // UnitController와 동일한 패턴: Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;
    // 이 적 유닛이 "공격받을 때" 적용되는 분류 (UnitController.Attack()의 배율 계산에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;
```

**변경 코드** (getter 추가, `GetArmor()` 옆)
```csharp
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
```

#### 6) `Assets/Scripts/System/RTSUnitController.cs` — 배율표 에셋 참조 추가

**기존 코드** (43~46행)
```csharp
    [SerializeField]
    private BuildingDataSO buildingDatabase;
    [SerializeField]
    private UpgradeManager upgradeManager;
```

**변경 코드**
```csharp
    [SerializeField]
    private BuildingDataSO buildingDatabase;
    [SerializeField]
    private UpgradeManager upgradeManager;
    [SerializeField]
    private DamageMultiplierTableSO damageMultiplierTable;
```

**변경 코드** (`GlobalAttackBonus`/`GlobalArmorBonus` 프로퍼티 옆에 추가, 1470행 부근)
```csharp
    public int GlobalAttackBonus => upgradeManager.GetBonus(ResearchType.Attack);
    public int GlobalArmorBonus => upgradeManager.GetBonus(ResearchType.Armor);
    public DamageMultiplierTableSO DamageMultiplierTable => damageMultiplierTable;
```

### 기존 유닛 7종 장갑/크기 기본값 (제안 — 인스펙터에서 언제든 조정 가능)

현재 `UnitDataSO` 에셋에 이미 존재하는 7종에 대한 제 추정값입니다. 유닛 능력치와 마찬가지로 데이터일 뿐이니 감으로 정한 값은 나중에 인스펙터에서 자유롭게 바꾸시면 됩니다.

| 유닛 (건물) | 공격방식 | 장갑 | 크기 | 고유 보너스 |
|---|---|---|---|---|
| Worker Drone (메인기지) | - | Light | Small | 없음 |
| Assault Trooper (병영) | 소총 | Light | Small | 없음 |
| Scout Drone (병영) | 레이저 | Light | Small | 없음 |
| Pulasr Tank (공장) | 폭발 | Heavy | Medium | 없음 |
| Ranger IFV (공장) | 폭발 | Light | Medium | 없음 |
| Firehawk (공항) | 레이저 | Light | Medium | 경장갑 대상 +30% |
| Guardian Drone (공항) | 폭발 | Heavy | Large | 중장갑 대상 +40% |

**범위 밖 — 이번에 안 만듦:** 기획에 나온 **Sharpshooter(저격수)**와 **SkyLancer(스카이랜서)**는 현재 `UnitDataSO`/씬 프리팹에 아예 존재하지 않는 신규 유닛입니다. 프리팹(모델/애니메이션 포함) 생성은 코드 수정만으로는 할 수 없어서 이번 변경에 포함하지 않았습니다. 지난 tier 필드 작업 덕분에, 프리팹만 준비되면 `UnitID` 상수 추가 + `UnitDataSO`에 항목 추가(tier/armorType/sizeType/bonusVersusArmorType 등 포함)만으로 바로 통합됩니다.

### 요약/영향받는 파일

- `Assets/Scripts/Unit/DamageTypes.cs` (신규) — `ArmorType`, `SizeType` enum
- `Assets/Scripts/ScriptableObject/DamageMultiplierTableSO.cs` (신규) — 공격방식×크기 배율표 에셋 (인스펙터에서 조정)
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `armorType`/`sizeType` 필드 추가
- `Assets/Scripts/Unit/UnitController.cs` — `armorType`/`sizeType`/`bonusVersusArmorType`/`bonusVersusArmorPercent` 필드, `Attack()`의 데미지 계산을 배율 적용 방식으로 교체, 관련 getter 추가
- `Assets/Scripts/Enemy/EnemyController.cs` — `armorType`/`sizeType` 필드 + getter 추가
- `Assets/Scripts/System/RTSUnitController.cs` — `DamageMultiplierTableSO` 참조 필드 + `DamageMultiplierTable` 프로퍼티 추가
- (마이그레이션) `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 기존 7종에 armorType/sizeType 값 채움

**구현 후 실제로 값을 채워야 하는 작업 (코드 밖):**
1. 새 `DamageMultiplierTableSO` 에셋을 하나 생성(Create → DamageMultiplierTableSO)하고 씬의 `RTSUnitController`에 연결 — 기본값이 이미 기획 수치(소총 100/80/60 등)로 세팅되어 있어 별도 입력 없이 바로 사용 가능.
2. 기존 7종 유닛 **프리팹**(`UnitController` 컴포넌트)에 위 표의 장갑/크기/고유보너스 값을 인스펙터에서 채워 넣기 — 이건 `.asset`(SO) 파일과 달리 프리팹이라 이 세션에서 YAML로 직접 수정하기엔 위험도가 높아, 코드/SO 변경만 적용하고 프리팹 값 채우기는 사용자가 에디터에서 직접 하시거나, 원하시면 프리팹 GUID를 알려주시면 마저 채워드리겠습니다.

---

### 적용 완료 (2026-07-22)

사용자 확인 후 위 설계대로 구현함. 계획에 없던 추가 작업 2가지가 필요해서 같이 처리함:

1. **기존 유닛 7종 프리팹의 `attackType` 불일치 수정.** 프리팹을 열어보니 Scout Drone(화염→**레이저**), Ranger IFV(화염→**폭발**), Firehawk(총기→**레이저**)가 이번 기획 문서에 적힌 공격 방식과 어긋나 있었음. 배율표가 attackType 기준으로 조회되므로 고쳐두지 않으면 새 시스템이 기획과 다르게 동작해서 함께 수정함.
2. **`DamageMultiplierTable.asset` 에셋을 직접 생성**하고 `TestScene.unity`/`SampleScene.unity` 두 씬의 `RTSUnitController.damageMultiplierTable` 슬롯에 연결함(기획 수치 그대로: 소총 100/80/60, 폭발 70/100/130, 레이저 100/100/100, 화염 130/100/60). 원래는 사용자가 에디터에서 직접 만들어 연결해야 한다고 안내할 계획이었으나, `.asset`/`.meta`/씬 YAML을 직접 써도 안전하다고 판단해 대신 처리함.

**실제 반영된 유닛별 최종 값:**

| 유닛 (ID) | 공격방식 | 장갑 | 크기 | 고유 보너스 |
|---|---|---|---|---|
| Worker Drone (1) | Bullet(미사용) | Light | Small | 없음 |
| Assault Trooper (2) | Bullet(소총) | Light | Small | 없음 |
| Scout Drone (3) | Laser(레이저) ※수정됨 | Light | Small | 없음 |
| Ranger IFV (4) | Explosive(폭발) ※수정됨 | Light | Medium | 없음 |
| Pulsar Tank (5) | Explosive(폭발) | Heavy | Medium | 없음 |
| Firehawk (6) | Laser(레이저) ※수정됨 | Light | Medium | 경장갑 대상 +30% |
| Guardian Drone (7) | Explosive(폭발) | Heavy | Large | 중장갑 대상 +40% |

Sharpshooter/SkyLancer는 여전히 프리팹이 없어 범위 밖(문서 본문 참고).

**변경/생성된 파일 최종 목록:**
- 신규: `Assets/Scripts/Unit/DamageTypes.cs`, `Assets/Scripts/ScriptableObject/DamageMultiplierTableSO.cs`, `Assets/Scripts/ScriptableObject/DamageMultiplierTable.asset`(+ 관련 `.meta` 3종)
- 수정: `UnitDataSO.cs`, `UnitController.cs`, `EnemyController.cs`, `RTSUnitController.cs`, `New Unit Data SO.asset`
- 수정(계획에 없었음): 유닛 프리팹 7종(`Worker Drone`, `Assault Trooper`, `Scout Drone`, `Ranger Infantry Fighting Vehicle`, `Pulsar Tank`, `Firehawk`, `Guardian Drone`.prefab) — armorType/sizeType/고유보너스 필드 채움 + attackType 3종 수정
- 수정(계획에 없었음): `Assets/Scenes/TestScene.unity`, `Assets/Scenes/SampleScene.unity` — `damageMultiplierTable` 참조 연결
