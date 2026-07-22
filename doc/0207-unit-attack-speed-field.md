## 2026-07-22

### 요청 내용

> 스크립터블오브젝트의 유닛의 공격주기(공격속도)도 필드를 추가해서 연결해줄래

`doc/0205`에서 확립한 패턴(SO 필드 추가 → `UnitController.ApplyUnitData`에서 반영 → `Start()`에서 자가 적용) 그대로 공격주기에도 적용.

### 조사 내용

- 실제 공격 쿨다운은 `UnitController.timeBetweenAttacks`(`Assets/Scripts/Unit/UnitController.cs:86`, `public float`)이며, `Attack()`에서 `Invoke(nameof(ResetAttack), timeBetweenAttacks);`로 사용됨(값이 작을수록 더 자주 공격 = 공격속도가 빠름). `UnitDataSO`에는 대응 필드가 없음.
- 기존 9개 유닛 프리팹의 현재 값(마이그레이션 시 그대로 유지할 기준값):

| 유닛 | timeBetweenAttacks |
|---|---|
| Worker Drone | 0.6 |
| Assault Trooper | 0.6 |
| Scout Drone | 1.2 |
| Sharpshooter | 0.6 |
| Ranger IFV | 1 |
| Pulsar Tank | 1.5 |
| SkyLancer | 1 |
| Firehawk | 1.2 |
| Guardian Drone | 1.2 |

### 코드 변경 (예정)

#### 1) `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `attackSpeed` 필드 추가

**변경 코드** (`attackRange` 필드 옆)
```csharp
    [field: SerializeField]
    public int attackRange { get; private set; }

    // 공격 1회 후 다음 공격까지 걸리는 시간(초). 값이 작을수록 더 빨리(자주) 공격한다.
    // (UnitController.timeBetweenAttacks와 동일한 의미 - "공격속도"가 아니라 "공격 간격"이지만
    // 기획 쪽 명칭인 attackSpeed를 그대로 필드명으로 씀)
    [field: SerializeField]
    public float attackSpeed { get; private set; }
```

#### 2) `Assets/Scripts/Unit/UnitController.cs` — `ApplyUnitData`에 반영

**기존 코드**
```csharp
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

**변경 코드**
```csharp
    public void ApplyUnitData(UnitData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;
        timeBetweenAttacks = data.attackSpeed;

        if (attackRange != null)
            attackRange.UnitRange = data.attackRange;

        GetComponent<HealthManager>()?.InitializeHealth(data.hp);
    }
```

#### 3) `New Unit Data SO.asset` — 기존 9개 유닛에 현재 프리팹 값 그대로 마이그레이션

위 표의 값을 각 유닛 항목에 `<attackSpeed>k__BackingField: N`으로 채워 넣음(값 변화 없이 그대로 SO로 옮기는 것뿐 — 밸런스는 안 바뀜).

### 요약/영향받는 파일

- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` — `attackSpeed` 필드 추가
- `Assets/Scripts/Unit/UnitController.cs` — `ApplyUnitData`에서 `timeBetweenAttacks` 덮어씀
- `Assets/Scripts/ScriptableObject/New Unit Data SO.asset` — 9개 유닛 마이그레이션

적용 후엔 프리팹의 `timeBetweenAttacks` 인스펙터 값은 미리보기용 기본값이 되고, 실제 공격주기는 SO 값이 결정합니다(기존 hp/attackDamge/attackRange와 동일한 패턴).

---

**적용 완료** (2026-07-22) — 사용자 확인 후 적용. 마이그레이션 시점에 사용자가 에디터에서 유닛 ID를 1~9로 재정렬하고 attackRange/attackDamge/hp 등 여러 값을 직접 튜닝해둔 상태였어서, 프리팹 guid로 각 유닛을 다시 매칭해 정확한 attackSpeed 값을 넣음(문서 상단 표의 유닛-값 매핑은 그대로 유효 — ID 숫자만 사용자가 바꿈).
