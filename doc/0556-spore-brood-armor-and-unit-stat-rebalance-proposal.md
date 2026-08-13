# 0556 - Spore Brood 방어력(1) 도입 + 립팽/스피터/스키터윙 스탯 재조정

## 날짜
2026-08-13

## 요청 내용
"spore 종족에서 모든 유닛이 방어력 1을 가지도록도 해줘
그리고 립팽 체력 40 공격력 6 공격속도 0.3초
스피터 체력 80 공격력 10 공격속도 1.6초
스키터윙 체력 120에 공격력 18 공격속도 0.8초
로 바꿔주고 문서도 갱신해줘"

## 조사 내용
프로젝트 전체를 확인한 결과 **"방어력(피해 경감)" 수치 자체가 현재 어디에도 존재하지 않음**:
- `UnitData`(`Assets/Scripts/ScriptableObject/UnitDataSO.cs`)엔 `armorType`(Light/Heavy 분류 -
  특정 공격의 추가 피해 판정용 태그일 뿐, 피해 경감 계산엔 안 쓰임)만 있고 숫자형 방어력 필드가 없음.
- 실제 피해 적용부인 `HealthManager.GetDamage()`(`Assets/Scripts/Unit/HealthManager.cs:84`)는
  `currentHp -= damage`만 하고 있어 방어력을 깎을 자리 자체가 없음.

즉 "방어력 1"을 적용하려면 **새 스탯(숫자형 armor)과 피해 경감 계산 로직을 새로 설계/추가**해야 함 -
프로젝트 규칙(confirm-before-implementing)상 실제 코드를 건드리기 전에 먼저 여기서 설계안을 제시하고
승인을 받아야 함.

### 설계안
- `UnitData`에 `armor`(int, 기본값 0 - 기존 모든 유닛은 변화 없음) 필드 추가.
- `HealthManager`에 `armor` 필드 + `SetArmor(int)` 세터 추가, `GetDamage()`에서
  `받는 피해 = max(1, 원래 피해 - armor)`로 경감 후 적용 - **사용자 확인(2026-08-13): 방어력이 아무리
  높아도 공격 1회당 최소 1피해는 항상 들어가도록 결정**(완전 무효화 방식 대신 최소 피해 보장 방식 채택).
  - `OnDamaged` 이벤트엔 경감 "이후" 실제로 들어간 피해량을 넘김(원래 피해가 아님) - 이 이벤트를
    구독해 실제 피해량을 누적하는 `GuardianDroneSkill.damageTaken`(가디언 드론 실드) 등이 실제로
    막힌 만큼만 정확히 반영하도록.
- 스폰 시점에 `UnitData → 스탯 반영`을 담당하는 `ApplyUnitData()`가 `UnitController`/
  `EnemyUnitController`/`AllyController` 세 곳에 동일한 패턴으로 중복 구현돼 있음(이미 `attackDamage`/
  `armorType`/`InitializeHealth(data.hp)`를 셋 다 똑같이 반영 중) - 새 `armor`도 세 곳 모두
  `SetArmor(data.armor)`로 동일하게 반영(한 곳만 빠뜨리면 그 진영만 조용히 방어력이 안 먹는 버그가
  생기므로 세 곳 다 필요).
- 건물(`BuildingData`)엔 `armor` 필드를 추가하지 않음 - 이번 요청은 "유닛"에 한정되고, 건물의
  `HealthManager`는 `SetArmor()`가 호출되지 않아 기본값 0(경감 없음)을 유지하므로 기존 동작 그대로.
- UI(유닛 정보 패널 등)에 방어력 수치를 표시하는 건 이번 범위에 포함하지 않음 - 필요하시면 별도
  요청으로 추가 가능.

## 적용한 코드 변경

### 1. `Assets/Scripts/ScriptableObject/UnitDataSO.cs` - armor 필드 추가
```csharp
// 기존 코드
    [field: SerializeField]
    public ArmorType armorType { get; private set; }
    [field: SerializeField]
    public SizeType sizeType { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }
```
```csharp
// 변경 코드
    [field: SerializeField]
    public ArmorType armorType { get; private set; }
    [field: SerializeField]
    public SizeType sizeType { get; private set; }

    // 방어력 - 받는 피해에서 고정으로 깎이는 값(HealthManager.GetDamage 참고). 기본 0(기존 유닛 전부 무변화, doc/0556).
    [field: SerializeField]
    public int armor { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }
```

### 2. `Assets/Scripts/Unit/HealthManager.cs` - 방어력 필드 + 경감 계산
```csharp
// 기존 코드
    [SerializeField]
    private int maxHealth = 100;

    [SerializeField] private Slider healthSlider; // 체력바 UI (프리팹에서 직접 연결) - 체력 변화에 맞춰 값만 자동 갱신됨

    private int currentHp;
    private bool isDead;
```
```csharp
// 변경 코드
    [SerializeField]
    private int maxHealth = 100;

    [SerializeField] private Slider healthSlider; // 체력바 UI (프리팹에서 직접 연결) - 체력 변화에 맞춰 값만 자동 갱신됨

    private int currentHp;
    private bool isDead;
    private int armor; // 받는 피해를 고정으로 깎는 값 - UnitData.armor를 ApplyUnitData()가 SetArmor()로 반영(doc/0556)
```

```csharp
// 기존 코드
    public void GetDamage(int damage, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
    {
        if (isDead || damage <= 0)
            return;

        currentHp = Mathf.Max(0, currentHp - damage);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
        OnDamaged?.Invoke(damage, attackerPosition, attackType, isEnemyAttacker);

        if (currentHp <= 0)
        {
            Die();
        }
    }
```
```csharp
// 변경 코드
    public void GetDamage(int damage, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
    {
        if (isDead || damage <= 0)
            return;

        // 방어력만큼 경감하되, 공격 1회당 최소 1피해는 항상 들어간다(방어력이 아무리 높아도 완전 무적은 없음, doc/0556).
        int mitigated = Mathf.Max(1, damage - armor);

        currentHp = Mathf.Max(0, currentHp - mitigated);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
        OnDamaged?.Invoke(mitigated, attackerPosition, attackType, isEnemyAttacker);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    // 유닛 스폰 시 UnitData.armor 값을 반영한다(UnitController/EnemyUnitController/AllyController.ApplyUnitData 공통, doc/0556).
    public void SetArmor(int value) => armor = Mathf.Max(0, value);
```

### 3. `Assets/Scripts/Unit/UnitController.cs` - ApplyUnitData에 SetArmor 추가
```csharp
// 기존 코드
        healthManager?.InitializeHealth(data.hp);
```
```csharp
// 변경 코드
        healthManager?.SetArmor(data.armor);
        healthManager?.InitializeHealth(data.hp);
```

### 4. `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` - 동일
```csharp
// 기존 코드
        healthManager?.InitializeHealth(data.hp);
```
```csharp
// 변경 코드
        healthManager?.SetArmor(data.armor);
        healthManager?.InitializeHealth(data.hp);
```

### 5. `Assets/Scripts/FogOfWar/Ally/AllyController.cs` - 동일
```csharp
// 기존 코드
        healthManager?.InitializeHealth(data.hp);
```
```csharp
// 변경 코드
        healthManager?.SetArmor(data.armor);
        healthManager?.InitializeHealth(data.hp);
```

### 6. `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset` - 스탯 값 변경
**립팽 (Ripfang, ID 10)**
```yaml
# 기존
    <armorType>k__BackingField: 0
    <sizeType>k__BackingField: 0
    <hp>k__BackingField: 60
    <attackDamge>k__BackingField: 9
    <attackRange>k__BackingField: 2
    <attackSpeed>k__BackingField: 0.5
```
```yaml
# 변경
    <armorType>k__BackingField: 0
    <sizeType>k__BackingField: 0
    <armor>k__BackingField: 1
    <hp>k__BackingField: 40
    <attackDamge>k__BackingField: 6
    <attackRange>k__BackingField: 2
    <attackSpeed>k__BackingField: 0.3
```

**스피터 (Spitter, ID 11)**
```yaml
# 기존
    <armorType>k__BackingField: 0
    <sizeType>k__BackingField: 1
    <hp>k__BackingField: 50
    <attackDamge>k__BackingField: 11
    <attackRange>k__BackingField: 13
    <attackSpeed>k__BackingField: 1.1
```
```yaml
# 변경
    <armorType>k__BackingField: 0
    <sizeType>k__BackingField: 1
    <armor>k__BackingField: 1
    <hp>k__BackingField: 80
    <attackDamge>k__BackingField: 10
    <attackRange>k__BackingField: 13
    <attackSpeed>k__BackingField: 1.6
```

**스키터윙 (Skitterwing, ID 12)**
```yaml
# 기존
    <armorType>k__BackingField: 0
    <sizeType>k__BackingField: 1
    <hp>k__BackingField: 65
    <attackDamge>k__BackingField: 8
    <attackRange>k__BackingField: 11
    <attackSpeed>k__BackingField: 0.9
```
```yaml
# 변경
    <armorType>k__BackingField: 0
    <sizeType>k__BackingField: 1
    <armor>k__BackingField: 1
    <hp>k__BackingField: 120
    <attackDamge>k__BackingField: 18
    <attackRange>k__BackingField: 11
    <attackSpeed>k__BackingField: 0.8
```

기존에 이미 저장된 다른 유닛 데이터(OC/NTA)는 `armor` 필드가 YAML에 아예 없어도 Unity가 int
기본값(0)으로 처리하므로 별도로 손댈 필요 없음(경감 없음 = 기존과 동일한 동작 유지).

## 문서 갱신
`doc/0444-spore-brood-unit-building-data-and-controller-reuse-proposal.md`의 유닛 스탯 표(71-74번
줄)에 `armor` 컬럼을 추가하고 3개 유닛 스탯을 아래와 같이 갱신함.

## 사용자 확인 결과 (2026-08-13)
1. 방어력 경감 방식: "최소 1피해 보장" 방식으로 결정(방어력이 아무리 높아도 공격 1회당 최소 1피해는
   항상 들어감) - 완전 무효화 방식은 채택하지 않음.
2. 구현 진행 승인 - 아래 변경 전부 적용 완료, `npx uloop-cli compile` 확인 결과 에러 0건(기존부터
   있던 `FindFirstObjectByType` 관련 경고 40건은 이번 변경과 무관).

## 영향받는 파일
- 변경: `Assets/Scripts/ScriptableObject/UnitDataSO.cs`
- 변경: `Assets/Scripts/Unit/HealthManager.cs`
- 변경: `Assets/Scripts/Unit/UnitController.cs`
- 변경: `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- 변경: `Assets/Scripts/FogOfWar/Ally/AllyController.cs`
- 변경: `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset`
- 변경: `doc/0444-spore-brood-unit-building-data-and-controller-reuse-proposal.md` (스탯 표 갱신)
