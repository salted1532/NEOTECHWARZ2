## 2026-07-22

### 요청 내용

> 그럼 일단 스크립터블오브젝트의 있는 필드내용과 필드에있는 프리팹을 그 유닛프리팹과 연결하면 그 유닛이 해당하는 공격력과 사거리, 체력, 아이콘등을 가지도록 수정해 줄수 있어?

`doc/0202` 감사에서 확인했듯, 지금은 `UnitDataSO`의 `hp`/`attackDamge`/`attackRange`/`Icon`이 죽은 데이터이고 실제 전투 스탯은 스폰되는 프리팹 자체의 인스펙터 값(`HealthManager.maxHealth`/`UnitController.attackDamage`/`AttackRange.UnitRange`/`UnitController.icon`)에서 따로 옵니다. 이 요청은 그 반대로, **`UnitDataSO`의 값이 실제로 스폰된 유닛에 적용되도록** 만들어 달라는 것 — SO를 진짜 단일 소스로 만드는 작업.

### 조사 내용

- 유닛이 실제로 생성되는 지점은 딱 한 곳: `UnitSpawner.Spawn(int unitID)`(`Assets/Scripts/UnitSpawner/UnitSpawner.cs:88`). 여기서 `database.unitData`에서 `UnitData data`를 이미 찾아온 뒤 `Instantiate(data.Prefab, ...)`로 스폰하고 있어서, **`Instantiate` 직후에 `data`의 값을 새로 스폰된 인스턴스에 덮어씌우는 것이 가장 깔끔한 지점**입니다. (프로젝트 전체에서 유닛 프리팹을 `Instantiate`하는 곳은 여기 한 곳뿐 — `PlacementSystem`/`BaseStructure`의 `Instantiate`는 건물용이라 무관.)
- `HealthManager`에는 이미 `SetMaxHealth(int)`가 있지만, 이건 건물이 지어지는 동안 체력이 서서히 차오르는 용도라 **현재 체력을 새 최대치까지 채우지 않습니다**(`currentHp = Mathf.Min(currentHp, maxHealth)`). 방금 생산된 유닛은 항상 풀피여야 하므로, 최대치를 정하는 동시에 그만큼 꽉 채우는 별도 메서드가 필요합니다.
- `UnitController`의 `icon`/`attackDamage`/`armorType`/`sizeType`은 전부 `private` 필드지만, 적용 로직을 `UnitController` 자신의 새 public 메서드로 두면 별도 setter 없이 바로 대입 가능합니다.
- `AttackRange.UnitRange`는 이미 `public int`라 `unitController`가 들고 있는 자식 `AttackRange` 참조(`attackRange` 필드, `Awake()`에서 `GetComponentInChildren`으로 이미 채워짐)로 바로 대입 가능합니다.
- **범위 밖**: `armor`(고정 방어력), `attackType`(공격방식: 소총/폭발/레이저/화염), `bonusVersusArmorType`/`bonusVersusArmorPercent`(고유 장갑타입 보너스)는 애초에 `UnitDataSO`에 해당 필드가 없어서 이번 요청("공격력과 사거리, 체력, 아이콘 등")에 맞춰 이 3~4개는 그대로 프리팹 값만 씁니다. `armorType`/`sizeType`는 SO에 필드가 이미 있으니 같이 동기화 대상에 포함했습니다(정확히 `doc/0202`에서 발견한 "SO를 고쳐도 프리팹에 반영 안 됨" 버그를 해결하는 부분이기도 합니다).
- 이 변경으로 자동으로 고쳐지는 것: Ranger IFV/Pulasr Tank의 SO상 armorType/sizeType이 이제 실제로 반영됨, Sharpshooter/SkyLancer의 체력/공격력/사거리/장갑/크기/아이콘도 SO에 적어둔 값대로 스폰됨.
- 이 변경으로 **안 고쳐지는 것**: Sharpshooter/SkyLancer 프리팹의 `unitID`가 여전히 2/4로 남아있는 문제(SO 항목을 찾는 키 자체가 프리팹이 아니라 생산 시 넘겨받는 `unitID` 파라미터라서, 이건 이 매커니즘과 무관하게 프리팹의 `unitID` 필드를 직접 8/9로 고쳐야 함), `bonusVersusArmorPercent`가 0으로 남아있는 문제(SO에 해당 필드가 없음).

### 코드 변경 (예정)

#### 1) `Assets/Scripts/Unit/HealthManager.cs` — 스폰 시 풀피로 초기화하는 메서드 추가

**기존 코드** (`SetMaxHealth` 아래)
```csharp
    // 최대 체력을 동적으로 재설정한다 (예: BaseStructure가 어떤 건물을 지을지에 따라 최대체력을 다시 지정할 때).
    // 현재 체력이 새 최대치를 넘지 않도록 클램프한다.
    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = Mathf.Min(currentHp, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }
```

**변경 코드**
```csharp
    // 최대 체력을 동적으로 재설정한다 (예: BaseStructure가 어떤 건물을 지을지에 따라 최대체력을 다시 지정할 때).
    // 현재 체력이 새 최대치를 넘지 않도록 클램프한다.
    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = Mathf.Min(currentHp, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 유닛이 생산되어 스폰되는 시점 전용: 최대 체력을 새로 지정하는 동시에 그만큼 꽉 채운다.
    // 건설 중 서서히 차오르게 하는 SetMaxHealth와 달리, 방금 생산된 유닛은 항상 풀피로 시작해야 하므로 currentHp도 함께 채운다.
    public void InitializeHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = maxHealth;
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }
```

#### 2) `Assets/Scripts/Unit/UnitController.cs` — UnitData 적용 메서드 추가

**변경 코드** (새 public 메서드, `GetArmorType()`/`GetSizeType()` getter 근처에 추가)
```csharp
    // 생산 시점에 UnitDataSO의 값으로 전투 스탯(체력/공격력/사거리/아이콘/장갑타입/크기타입)을 덮어쓴다.
    // 프리팹 자체에 미리 박아둔 값은 인스펙터 프리뷰/테스트용 기본값 역할만 하고, 실제로 생산되어 스폰된
    // 유닛은 이 메서드를 통해 UnitDataSO 값을 반영받는다 (UnitSpawner.Spawn()에서 호출).
    public void ApplyUnitData(UnitData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;

        if (attackRange != null)
            attackRange.UnitRange = data.attackRange;

        GetComponent<HealthManager>()?.InitializeHealth(data.hp);
    }
```

#### 3) `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — 스폰 직후 적용

**기존 코드** (`Spawn()`, 88~106행)
```csharp
    private void Spawn(int unitID)
    {
        int index = database.unitData.FindIndex(d => d.ID == unitID);

        if (index == -1)
            return;

        UnitData data = database.unitData[index];
        

        Vector3 spawnPos = transform.position + new Vector3(0, 0, -2f);

        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        spawnunit.GetComponent<UnitController>().MoveTo(buildingController.GetRallyPos());
        
        PrintQueue();

    }
```

**변경 코드**
```csharp
    private void Spawn(int unitID)
    {
        int index = database.unitData.FindIndex(d => d.ID == unitID);

        if (index == -1)
            return;

        UnitData data = database.unitData[index];
        

        Vector3 spawnPos = transform.position + new Vector3(0, 0, -2f);

        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        if (spawnunit.TryGetComponent<UnitController>(out var unitController))
        {
            unitController.ApplyUnitData(data); // 체력/공격력/사거리/아이콘/장갑타입/크기타입을 SO 값으로 덮어씀
            unitController.MoveTo(buildingController.GetRallyPos());
        }

        PrintQueue();

    }
```

### 요약/영향받는 파일

- `Assets/Scripts/Unit/HealthManager.cs` — `InitializeHealth(int)` 메서드 추가
- `Assets/Scripts/Unit/UnitController.cs` — `ApplyUnitData(UnitData)` 메서드 추가
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — `Spawn()`에서 `Instantiate` 직후 `ApplyUnitData` 호출

**이후로 프리팹의 attackDamage/icon/armorType/sizeType/HealthManager.maxHealth/AttackRange.UnitRange 값은 실질적으로 "인스펙터 프리뷰용 기본값"이 되고, 실제 게임에 나타나는 값은 전부 SO 기준이 됩니다.** 프리팹 값을 그대로 안 써도 되니, 이후 밸런스 조정은 `UnitDataSO` 하나만 고치면 됩니다.

**이 변경으로 안 고쳐지는 것 (별도 확인 필요):**
- Sharpshooter/SkyLancer 프리팹의 `unitID`가 여전히 2/4 — 프리팹에서 직접 8/9로 고쳐야 진짜 구분되는 유닛이 됨.
- `armor`/`attackType`/`bonusVersusArmorType`/`bonusVersusArmorPercent`는 SO에 필드가 없어서 계속 프리팹 값을 씀. 이것도 SO로 옮기고 싶으면 별도로 알려주세요.

---

**적용 완료** (2026-07-22) — 사용자 확인 후 위 3개 파일 변경을 그대로 적용함.
