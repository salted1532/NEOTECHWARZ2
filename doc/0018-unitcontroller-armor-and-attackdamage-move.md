# 0018 - UnitController로 공격력 이동 + 방어력 필드 추가

**날짜:** 2026-07-07

## 요청 내용
Info_panel에서 유닛의 공격력/방어력을 보여주는 선행 작업으로: (1) `UnitController`에 방어력(armor) 필드 추가, (2) 기존 `AttackRange`에 있던 공격력(`AttackDamage`) 필드를 `UnitController`로 이동, (3) 공격력/방어력 둘 다 `UnitController`가 관리하도록 요청.

## 조사 내용
- `Assets/Scripts/Unit/AttackRange.cs`에 `public int UnitRange`, `public int AttackDamage` 두 필드가 있었고, `Update()`에서 사거리 판정 후 `unitController.Attack(pos, AttackDamage, target)` 형태로 호출.
- `Assets/Scripts/Unit/UnitController.cs`의 `FriendlyAttackTick()`(아군 강제공격)도 `attackRange.AttackDamage`를 직접 읽어 `Attack()`에 넘기고 있었음 — 이동 대상 사용처가 두 곳.
- `AttackDamage` 필드는 9개 유닛 프리팹(`Test/TestUnit`, `Test/TestAirUnit`, `NTA/Unit/MainBase/Worker Drone`, `NTA/Unit/Tier1/Assault Trooper`, `NTA/Unit/Tier1/Scout Drone`, `NTA/Unit/Tier2/Pulsar Tank`, `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle`, `NTA/Unit/Tier3/Guardian Drone`, `NTA/Unit/Tier3/Firehawk`)에 값이 직접 세팅되어 있었음 — 필드를 단순히 스크립트에서만 옮기면 프리팹에 저장된 값이 유실되므로, 각 프리팹의 YAML도 함께 수정해 값을 그대로 이전함.
- `UnitDataSO`(`Assets/Scripts/ScriptableObject/UnitDataSO.cs`)에도 `attackDamge`(오타) 필드가 있지만 생산 비용/스펙 표시용 데이터일 뿐 런타임 `AttackRange`/`UnitController`와는 연결되어 있지 않아 이번 작업 범위에서 제외.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs` — 필드 추가

**기존 코드**
```csharp
    // UnitDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetUnitName(unitID)로 조회)
    [SerializeField]
    private int unitID;

    private NavMeshAgent navMeshAgent;
```

**변경 코드**
```csharp
    // UnitDataSO.ID와 매칭되는 값 (Info_panel에 이름을 표시할 때 RTSUnitController.GetUnitName(unitID)로 조회)
    [SerializeField]
    private int unitID;

    // ===== 전투 스탯 (공격력/방어력) =====
    // 공격력은 기존 AttackRange.AttackDamage였던 것을 이곳으로 옮겨 UnitController가 함께 관리한다.
    // Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값이기도 하다.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;

    private NavMeshAgent navMeshAgent;
```

### `Attack()` 시그니처 단순화

**기존 코드**
```csharp
    public void Attack(Vector3 end, int damage, GameObject enemy)
    {
        ...
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            targetHealth.GetDamage(damage);
        }
        ...
    }
```

**변경 코드**
```csharp
    public void Attack(Vector3 end, GameObject enemy)
    {
        ...
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
        {
            targetHealth.GetDamage(attackDamage);
        }
        ...
    }
```

### `FriendlyAttackTick()` 호출부

**기존 코드**
```csharp
        if (attackRange != null && distance <= attackRange.UnitRange)
        {
            Attack(friendlyTarget.transform.position, attackRange.AttackDamage, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
```

**변경 코드**
```csharp
        if (attackRange != null && distance <= attackRange.UnitRange)
        {
            Attack(friendlyTarget.transform.position, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
```

### Getter 추가

**기존 코드**
```csharp
    public Sprite GetIcon() => icon;
    public int GetUnitID() => unitID;
}
```

**변경 코드**
```csharp
    public Sprite GetIcon() => icon;
    public int GetUnitID() => unitID;
    public int GetAttackDamage() => attackDamage;
    public int GetArmor() => armor;
}
```

### `Assets/Scripts/Unit/AttackRange.cs` — 필드 제거

**기존 코드**
```csharp
    public int UnitRange;
    public int AttackDamage;
```

**변경 코드**
```csharp
    public int UnitRange;
```

### `AttackRange.Update()` 호출부

**기존 코드**
```csharp
            if (distance <= UnitRange)
            {
                unitController.Attack(target.transform.position, AttackDamage, target);
            }
```

**변경 코드**
```csharp
            if (distance <= UnitRange)
            {
                unitController.Attack(target.transform.position, target);
            }
```

## 프리팹 마이그레이션 (`AttackRange.AttackDamage` → `UnitController.attackDamage`)

필드를 스크립트에서만 옮기면 이미 프리팹에 저장돼 있던 값이 사라지므로, 아래 9개 프리팹의 YAML도 함께 수정해 값을 그대로 이전했다 (`armor`는 기존 데이터가 없어 전부 `0`으로 채움).

예시 — `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab`:

**기존 코드**
```yaml
  m_EditorClassIdentifier: Assembly-CSharp::AttackRange
  UnitRange: 8
  AttackDamage: 6
```
```yaml
  m_EditorClassIdentifier: Assembly-CSharp::UnitController
  ...
  unitID: 2
  moveSpeed: 10
```

**변경 코드**
```yaml
  m_EditorClassIdentifier: Assembly-CSharp::AttackRange
  UnitRange: 8
```
```yaml
  m_EditorClassIdentifier: Assembly-CSharp::UnitController
  ...
  unitID: 2
  attackDamage: 6
  armor: 0
  moveSpeed: 10
```

나머지 8개 프리팹도 동일한 패턴(`AttackDamage: N` 줄 제거 → `UnitController`에 `attackDamage: N` / `armor: 0` 추가)으로 처리:

| 프리팹 | 이전 AttackDamage(AttackRange) | 이후 attackDamage(UnitController) |
| --- | --- | --- |
| `Test/TestUnit.prefab` | 1 | 1 |
| `Test/TestAirUnit.prefab` | 1 | 1 |
| `NTA/Unit/MainBase/Worker Drone.prefab` | 5 | 5 |
| `NTA/Unit/Tier1/Scout Drone.prefab` | 20 | 20 |
| `NTA/Unit/Tier2/Pulsar Tank.prefab` | 30 | 30 |
| `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab` | 12 | 12 |
| `NTA/Unit/Tier3/Guardian Drone.prefab` | 25 | 25 |
| `NTA/Unit/Tier3/Firehawk.prefab` | 8 | 8 |

## 남은 작업
- Info_panel에 UnitDamage/UnitArmor 이미지 호버 시 `GetAttackDamage()`/`GetArmor()` 값을 보여주는 UI 로직은 이 시점엔 아직 미구현 — [0019](0019-info-panel-attack-armor-hover-tooltip.md)에서 진행.
- 각 유닛 프리팹의 `armor` 값(전부 0)을 실제 밸런스에 맞게 인스펙터에서 채워야 함.

## 변경된 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/AttackRange.cs`
- `Assets/prefabs/Test/TestUnit.prefab`
- `Assets/prefabs/Test/TestAirUnit.prefab`
- `Assets/prefabs/NTA/Unit/MainBase/Worker Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Scout Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Pulsar Tank.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`
