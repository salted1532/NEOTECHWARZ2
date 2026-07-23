## 2026-07-23

### 요청 내용

> 이제 유닛들의 지상공격 공중공격을 구현하려고 해. 유닛스크립터블오브젝트에 유닛이 공격가능한 공격범위(지상,공중)을 고를수 있도록 만들어주고 UnitController에서도 만약 지상만 공격 가능한 유닛이 공중유닛을 공격하라고 명령했을 시 디버그 로그로 해당유닛은 공격할수 없다고 예외처리를 해주고 만약 공격 사거리 안에 들었는데 공중유닛이면 공격하러 이동하지 않고 유닛을 발견 못한것 처럼 있게 해줘

### 조사 내용

- `UnitDataSO`(`Assets/Scripts/ScriptableObject/UnitDataSO.cs`)에는 현재 지상/공중 공격 가능 여부를 나타내는 필드가 없음. `attackRange`/`attackSpeed` 등 전투 관련 필드만 있음.
- `UnitController`(`Assets/Scripts/Unit/UnitController.cs`)의 공격 명령 진입점은 3곳:
  - `AttackUnitTarget(EnemyController target)` — 적 우클릭/A모드 지정 공격
  - `AttackFriendlyTarget(MonoBehaviour target)` — 아군 유닛/건물 강제 공격 (친구 사격)
  - `AttackMoveTo(Vector3 destination)` — 땅 클릭 공격-이동 (특정 대상 지정 없음, 이동 중 `AttackRange`가 자동 교전)
- 사거리 내 자동 감지/교전은 `AttackRange.cs`가 담당하며, `OnTriggerEnter`에서 **"Enemy" 태그를 가진 오브젝트만** `enemiesInRange`에 추가한다. 즉 `AttackRange`의 자동 감지는 오직 `EnemyController` 대상에만 적용되고, 아군 강제공격(`AttackFriendlyTarget`)은 `FriendlyAttackTick()`이 별도로 직접 처리한다(주석에도 명시: "AttackRange는 'Enemy' 태그만 감지하므로 아군 대상 전투는 여기서 직접 처리한다").
- **공중 유닛 여부 판정 현황**:
  - `UnitController.isAirUnit` — 이미 존재 (아군 유닛만 해당)
  - `BuildingController.IsLifted()` — 이미 존재 (건물이 떠 있는지)
  - `EnemyController` — **공중 여부 필드가 아예 없음.** 현재 적 유닛은 전부 암묵적으로 지상 취급.
  - `UnitController`에 이미 `IsAirborne(MonoBehaviour target)`이라는 private static 헬퍼가 있어서 `UnitController`/`BuildingController` 두 타입의 공중 여부를 판정해 아군 강제공격(`AttackFriendlyTarget`) 시 사용 중.
- **중요한 함의**: 요청하신 3번째 항목("사거리 안에 들었는데 공중유닛이면 발견 못한 것처럼")은 `AttackRange`의 자동 감지 로직에 해당하는데, 이 로직은 오직 `EnemyController`만 감지 대상이다. 따라서 `EnemyController`에 공중 여부 필드가 없으면 이 요구사항은 실질적으로 아무 효과가 없다(항상 지상으로 취급되어 차단될 일이 없음). → 이번 작업에 `EnemyController`에도 `isAirUnit` 필드를 추가하는 것을 포함시켜야 이 요구사항이 실제로 동작함. (현재 적 프리팹은 전부 지상이라 기본값 `false`로 두면 기존 동작은 그대로 유지되고, 나중에 비행형 적을 추가할 때 체크박스만 켜면 됨.)
- 현재 유닛 9종 중 실제로 `isAirUnit = true`인 것은 **Firehawk(ID 8), Guardian Drone(ID 9)** 뿐이고 나머지 7종(Worker Drone/Assault Trooper/Scout Drone/Sharpshooter/Ranger IFV/Pulsar Tank/SkyLancer)은 전부 지상 유닛. (SkyLancer는 이름과 달리 현재 지상 유닛으로 확인됨.)
- 결론적으로 이번 기능은 현재 게임에서는 주로 **아군 강제공격(친구 사격)이 Firehawk/Guardian Drone을 대상으로 할 때** 바로 체감 가능하고, 적 자동교전 쪽은 향후 비행형 적이 추가되기 전까지는 인프라만 준비되는 상태.

### 설계

- `UnitDataSO`에 `canAttackGround`(bool, 기본 true), `canAttackAir`(bool, 기본 true) 두 필드 추가 — 스타크래프트류의 "지상 공격/공중 공격" 개별 체크박스 방식. 기존 값 없는 유닛도 기본 true/true라 지금 당장은 아무 유닛의 동작도 바뀌지 않음(순수 추가 인프라). 특정 유닛을 대공 불가/대지 불가로 만들고 싶으면 나중에 SO 에디터에서 직접 체크 해제.
- `UnitController`에 동일한 `canAttackGround`/`canAttackAir` 필드를 두고 `ApplyUnitData`에서 그대로 반영(기존 `attackDamage`/`armorType` 등과 동일 패턴).
- `UnitController.CanAttackDomain(bool targetIsAirUnit)` 공용 판정 헬퍼 추가 — 아래 두 곳에서 공용으로 사용.
- **명령 시점 차단** (`AttackUnitTarget`, `AttackFriendlyTarget` 맨 앞): 대상 도메인을 공격할 수 없으면 `Debug.Log`로 "이 유닛은 공격할 수 없습니다" 출력 후 그냥 `return`(상태 변경 없음 — 기존에 하던 행동 계속).
- **자동 감지 무시** (`AttackRange.GetClosestEnemy`): 후보를 고를 때 이 유닛이 공격 불가능한 도메인의 적은 애초에 후보에서 제외 → 사거리 안에 들어와도 쫓아가지도, 공격하지도 않고 마치 없는 것처럼 그냥 지나침.
- `EnemyController`에 `isAirUnit`(기본 false) 필드 + `IsAirUnit()` getter 추가 — 위 "조사 내용"에서 설명한 이유로 필요.

**범위 밖(이번엔 처리 안 함)**: `AttackFriendlyTarget`으로 이미 교전 중인 대상이 나중에 도메인이 바뀌는 경우(예: 건물이 공격받는 도중 이륙)는 매 프레임 재검증하지 않음 — 명령을 내린 시점에만 체크. 필요하시면 별도로 `FriendlyAttackTick`에도 매 프레임 체크를 추가할 수 있음.

### 코드 변경 (예정)

#### 1) `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `canAttackGround`/`canAttackAir` 필드 추가

**기존 코드**
```csharp
    [field: SerializeField]
    public float attackSpeed { get; private set; }

    [field: SerializeField]
    public int mineral { get; private set; }
```

**변경 코드**
```csharp
    [field: SerializeField]
    public float attackSpeed { get; private set; }

    // 이 유닛이 지상/공중 유닛을 공격할 수 있는지. 기본값은 둘 다 true(기존 동작과 동일 - 제한 없음).
    // 대공 사격이 불가능한 유닛은 canAttackAir를, 대지 공격이 불가능한 유닛은 canAttackGround를 false로.
    [field: SerializeField]
    public bool canAttackGround { get; private set; } = true;
    [field: SerializeField]
    public bool canAttackAir { get; private set; } = true;

    [field: SerializeField]
    public int mineral { get; private set; }
```

#### 2) `Assets/Scripts/Unit/UnitController.cs` — 필드 추가

**기존 코드**
```csharp
    // 이 유닛이 "공격받을 때" 적용되는 분류 (DamageMultiplierTableSO/고유 보너스 판정에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;
```

**변경 코드**
```csharp
    // 이 유닛이 "공격받을 때" 적용되는 분류 (DamageMultiplierTableSO/고유 보너스 판정에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;

    // 이 유닛이 "공격할 때" 적용되는 제한 - 지상/공중 유닛을 각각 공격할 수 있는지
    // (UnitDataSO.canAttackGround/canAttackAir가 ApplyUnitData에서 그대로 반영됨). 둘 다 기본 true(제한 없음).
    [SerializeField] private bool canAttackGround = true;
    [SerializeField] private bool canAttackAir = true;
```

#### 3) `Assets/Scripts/Unit/UnitController.cs` — `ApplyUnitData`에 반영

**기존 코드**
```csharp
        icon = data.Icon;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;
        timeBetweenAttacks = data.attackSpeed;

        if (attackRange != null)
```

**변경 코드**
```csharp
        icon = data.Icon;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;
        timeBetweenAttacks = data.attackSpeed;
        canAttackGround = data.canAttackGround;
        canAttackAir = data.canAttackAir;

        if (attackRange != null)
```

#### 4) `Assets/Scripts/Unit/UnitController.cs` — 판정 헬퍼 추가

**기존 코드**
```csharp
    public AttackEffectType GetAttackType() => attackType;
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
```

**변경 코드**
```csharp
    public AttackEffectType GetAttackType() => attackType;
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;

    // 대상이 공중 유닛인지에 따라 이 유닛이 그 대상을 공격할 수 있는 도메인(지상/공중)인지 판정한다.
    // (AttackUnitTarget/AttackFriendlyTarget의 명령 시점 차단, AttackRange의 자동 감지 필터링 양쪽에서 공용으로 사용)
    public bool CanAttackDomain(bool targetIsAirUnit) => targetIsAirUnit ? canAttackAir : canAttackGround;
```

#### 5) `Assets/Scripts/Unit/UnitController.cs` — `AttackUnitTarget`에 차단 로직 추가

**기존 코드**
```csharp
    public void AttackUnitTarget(EnemyController target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
```

**변경 코드**
```csharp
    public void AttackUnitTarget(EnemyController target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        if (!CanAttackDomain(target.IsAirUnit()))
        {
            Debug.Log($"{name}: 이 유닛은 {(target.IsAirUnit() ? "공중" : "지상")} 유닛을 공격할 수 없습니다.");
            return;
        }

        CancelGatheringForNewCommand();
```

#### 6) `Assets/Scripts/Unit/UnitController.cs` — `AttackFriendlyTarget`에 차단 로직 추가

**기존 코드**
```csharp
    public void AttackFriendlyTarget(MonoBehaviour target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();

        orderedTarget = null;
        attackMoveDestination = null;
        friendlyTarget = target;
        hasFriendlyOrder = true;
        followTarget = null;
        hasFollowOrder = false;
        CancelBuildOrder();

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position, IsAirborne(target));
    }
```

**변경 코드**
```csharp
    public void AttackFriendlyTarget(MonoBehaviour target)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        bool targetIsAir = IsAirborne(target);
        if (!CanAttackDomain(targetIsAir))
        {
            Debug.Log($"{name}: 이 유닛은 {(targetIsAir ? "공중" : "지상")} 대상을 공격할 수 없습니다.");
            return;
        }

        CancelGatheringForNewCommand();

        orderedTarget = null;
        attackMoveDestination = null;
        friendlyTarget = target;
        hasFriendlyOrder = true;
        followTarget = null;
        hasFollowOrder = false;
        CancelBuildOrder();

        arrived = false;
        UnitcurrentState = UnitState.Attack;

        MoveAgentTo(target.transform.position, targetIsAir);
    }
```

#### 7) `Assets/Scripts/Enemy/EnemyController.cs` — `isAirUnit` 필드 + getter 추가

**기존 코드**
```csharp
    // 이 적 유닛이 "공격받을 때" 적용되는 분류 (UnitController.Attack()의 배율 계산에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;
```

**변경 코드**
```csharp
    // 이 적 유닛이 "공격받을 때" 적용되는 분류 (UnitController.Attack()의 배율 계산에 쓰임)
    [SerializeField] private ArmorType armorType = ArmorType.Light;
    [SerializeField] private SizeType sizeType = SizeType.Medium;
    // 이 적이 공중 유닛인지 (UnitController.canAttackGround/canAttackAir 판정 대상). 현재 존재하는 적 유닛은
    // 전부 지상(false)이지만, 나중에 비행형 적을 추가할 때를 대비해 필드를 미리 마련해둔다.
    [SerializeField] private bool isAirUnit = false;
```

```csharp
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
```
→
```csharp
    public ArmorType GetArmorType() => armorType;
    public SizeType GetSizeType() => sizeType;
    public bool IsAirUnit() => isAirUnit;
```

#### 8) `Assets/Scripts/Unit/AttackRange.cs` — `GetClosestEnemy()`에서 공격 불가 도메인 제외

**기존 코드**
```csharp
    // 감지된 적들 중 자신과의 거리(제곱 거리)가 가장 짧은 적을 찾아 반환한다.
    private GameObject GetClosestEnemy()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject enemy in enemiesInRange)
        {
            if (enemy == null)
                continue;

            float sqrDist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = enemy;
            }
        }

        return closest;
    }
```

**변경 코드**
```csharp
    // 감지된 적들 중 자신과의 거리(제곱 거리)가 가장 짧은 적을 찾아 반환한다.
    // 이 유닛이 공격할 수 없는 도메인(지상 전용 유닛에게 공중 적, 또는 그 반대)의 적은 아예 후보에서 제외한다 -
    // 그래야 사거리 안에 들어와도 "발견하지 못한 것"처럼 무시하고 쫓아가지 않는다.
    private GameObject GetClosestEnemy()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject enemy in enemiesInRange)
        {
            if (enemy == null)
                continue;

            bool enemyIsAir = enemy.TryGetComponent<EnemyController>(out var enemyController) && enemyController.IsAirUnit();
            if (!unitController.CanAttackDomain(enemyIsAir))
                continue;

            float sqrDist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = enemy;
            }
        }

        return closest;
    }
```

#### 9) `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 기존 9개 유닛 마이그레이션

모든 유닛에 `canAttackGround: 1`, `canAttackAir: 1` 추가(전부 제한 없음 — 지금 당장 밸런스/동작 변화 없음). 예시(Worker Drone):

**기존**
```yaml
    <attackSpeed>k__BackingField: 0.6
    <mineral>k__BackingField: 50
```

**변경**
```yaml
    <attackSpeed>k__BackingField: 0.6
    <canAttackGround>k__BackingField: 1
    <canAttackAir>k__BackingField: 1
    <mineral>k__BackingField: 50
```
나머지 8개 유닛도 동일하게 `canAttackGround: 1` / `canAttackAir: 1` 삽입.

### 요약/영향받는 파일

- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `canAttackGround`/`canAttackAir` 필드 추가
- `Assets/Scripts/Unit/UnitController.cs` — 필드 추가, `ApplyUnitData` 반영, `CanAttackDomain()` 헬퍼, `AttackUnitTarget`/`AttackFriendlyTarget` 차단 로직 + `Debug.Log`
- `Assets/Scripts/Enemy/EnemyController.cs` — `isAirUnit` 필드(기본 false) + `IsAirUnit()` getter 추가
- `Assets/Scripts/Unit/AttackRange.cs` — `GetClosestEnemy()`가 공격 불가 도메인 적을 후보에서 제외
- `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 9개 유닛에 `canAttackGround: 1` / `canAttackAir: 1` 추가(값 변화 없음)

적용 후에도 모든 유닛이 기본 true/true라 지금 당장 게임 동작은 바뀌지 않습니다. 특정 유닛을 "대공 불가"나 "대지 불가"로 만들고 싶으면 이후 SO 에디터에서 해당 유닛의 체크박스를 직접 꺼주시면 됩니다.

---

**적용 완료** (2026-07-23) — 사용자 확인 후 적용. 단, 7)/8)번(`EnemyController.isAirUnit` 필드 + `AttackRange.GetClosestEnemy()` 필터링)은 사용자가 "EnemyController는 추후에 제작할 예정"이라고 밝혀 **이번엔 제외**하고, 아래 4가지만 실제로 적용함:

- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `canAttackGround`/`canAttackAir` 필드 추가 (1번)
- `Assets/Scripts/Unit/UnitController.cs` — 필드 추가(2번), `ApplyUnitData` 반영(3번), `CanAttackDomain()` 헬퍼(4번), `AttackFriendlyTarget`(6번) 차단 로직
- `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 9개 유닛 전부 `canAttackGround: 1` / `canAttackAir: 1` 마이그레이션 (9번)

**5번(`AttackUnitTarget` 차단)은 계획과 다르게 적용됨**: 초안에서는 `target.IsAirUnit()`(`EnemyController`)을 호출하는 코드를 썼는데, `EnemyController`를 건드리지 않기로 했으므로 그 메서드가 존재하지 않아 컴파일 에러가 났음. `AttackUnitTarget`에는 차단 로직을 넣지 않고 주석으로 "EnemyController에 isAirUnit이 추가되면 AttackFriendlyTarget과 동일한 패턴으로 추가할 것"만 남겨둠.

**남은 작업 (EnemyController를 나중에 새로 만들 때 함께 처리)**:
- `EnemyController`에 `isAirUnit` 필드 + `IsAirUnit()` getter 추가
- `UnitController.AttackUnitTarget()` 맨 앞에 `CanAttackDomain(target.IsAirUnit())` 체크 + `Debug.Log` 추가 (계획의 5번)
- `AttackRange.GetClosestEnemy()`에서 `unitController.CanAttackDomain(enemyController.IsAirUnit())`로 후보 필터링 (계획의 8번 — 사거리 안에 들어온 공격 불가 도메인 적을 "발견 못한 것처럼" 무시하는 요구사항 3번은 이 세 가지가 갖춰져야 실제로 동작함)
- 지금 당장 관찰 가능한 효과: 지상 전용(`canAttackAir=false`)으로 설정한 유닛이 공중 유닛(현재 Firehawk/Guardian Drone)을 `AttackFriendlyTarget`(아군 강제공격)으로 지정하면 `Debug.Log`로 차단됨. `AttackUnitTarget`(적 공격)과 `AttackRange` 자동 감지 쪽은 `EnemyController`에 공중 개념이 없어 지금은 차단 효과가 없음(적이 전부 지상 취급).
