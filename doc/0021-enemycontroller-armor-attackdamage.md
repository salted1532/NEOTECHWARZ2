# 0021 - EnemyController에 공격력/방어력 필드 추가 및 Info_panel 연동

**날짜:** 2026-07-08

## 요청 내용
적 유닛(`EnemyController`)도 [0018](0018-unitcontroller-armor-and-attackdamage-move.md)에서 `UnitController`에 추가한 것과 같은 방식으로 공격력/방어력 필드를 갖도록 하고, 적을 선택했을 때 Info_panel의 공격력/방어력 호버 툴팁([0019](0019-info-panel-attack-armor-hover-tooltip.md))에 해당 값이 출력되도록 요청.

## 조사 내용
- `Assets/Scripts/Enemy/EnemyController.cs`는 선택 마커, 아이콘/이름 조회, 사망 처리만 담당하고 있었고 공격력/방어력 필드는 없었음.
- 적 유닛의 실제 공격 로직(예: `AttackRange`에 대응하는 적 전용 컴포넌트)은 코드베이스에 아직 존재하지 않음 — `EnemyController`가 직접 데미지를 주는 코드는 없고, 현재는 플레이어 유닛(`UnitController.Attack`)이 적을 공격하는 방향만 구현되어 있음. 이번 요청은 전투 로직이 아니라 Info_panel 표시용 데이터 필드 추가로 범위를 한정.
- `Assets/Scripts/System/RTSUnitController.cs`의 적 선택 처리(`SelectState.EnemySelect`)에서 `ShowInfoPanel`이 기존 3-인자(공격력/방어력 없는) 오버로드를 쓰고 있어 항상 0으로만 표시되던 상태였음.
- `EnemyController`를 사용하는 프리팹은 `Assets/prefabs/Test/TestEnemy.prefab` 하나뿐 — 마이그레이션할 기존 값은 없음.

## 코드 변경

### `Assets/Scripts/Enemy/EnemyController.cs` — 필드 추가

**기존 코드**
```csharp
    [SerializeField]
    private string enemyName; // Info_panel에 표시할 이름

    [SerializeField] private float flashInterval = 0.3f; // 공격 명령(우클릭/A) 피드백 깜빡임 간격
```

**변경 코드**
```csharp
    [SerializeField]
    private string enemyName; // Info_panel에 표시할 이름

    // ===== 전투 스탯 (공격력/방어력) =====
    // UnitController와 동일한 패턴: Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;

    [SerializeField] private float flashInterval = 0.3f; // 공격 명령(우클릭/A) 피드백 깜빡임 간격
```

### Getter 추가

**기존 코드**
```csharp
    public Sprite GetIcon() => icon;
    public string GetEnemyName() => enemyName;
```

**변경 코드**
```csharp
    public Sprite GetIcon() => icon;
    public string GetEnemyName() => enemyName;
    public int GetAttackDamage() => attackDamage;
    public int GetArmor() => armor;
```

### `Assets/Scripts/System/RTSUnitController.cs` — 적 단일 선택 시 `ShowInfoPanel` 호출

**기존 코드**
```csharp
                    EnemyController enemy = selectedEnemyList[0];
                    uIController.ShowInfoPanel(enemy.GetIcon(), enemy.GetEnemyName(), enemy.GetComponent<HealthManager>());
```

**변경 코드**
```csharp
                    EnemyController enemy = selectedEnemyList[0];
                    uIController.ShowInfoPanel(enemy.GetIcon(), enemy.GetEnemyName(), enemy.GetComponent<HealthManager>(), enemy.GetAttackDamage(), enemy.GetArmor());
```

### `Assets/prefabs/Test/TestEnemy.prefab`

**기존 코드**
```yaml
  m_EditorClassIdentifier: Assembly-CSharp::EnemyController
  enemyMarker: {fileID: 5286784239653861491}
  icon: {fileID: 21300000, guid: 49a178a278581a04cacb00ceb865dadd, type: 3}
  enemyName: Cyborg Soldier
```

**변경 코드**
```yaml
  m_EditorClassIdentifier: Assembly-CSharp::EnemyController
  enemyMarker: {fileID: 5286784239653861491}
  icon: {fileID: 21300000, guid: 49a178a278581a04cacb00ceb865dadd, type: 3}
  enemyName: Cyborg Soldier
  attackDamage: 5
  armor: 1
```

> `attackDamage`/`armor`는 처음에 `0`/`0`으로 채워 넣었으나, 이후 사용자가 에디터에서 `5`/`1`로 직접 값을 입력함(다음 시스템 알림으로 확인됨).

## 남은 작업
- `TestEnemy.prefab`을 비롯해 향후 추가될 적 유닛 프리팹들의 `attackDamage`/`armor` 값을 실제 밸런스에 맞게 인스펙터에서 채워야 함.
- 적 유닛이 실제로 공격을 가하는 로직(플레이어의 `AttackRange`에 대응하는 것)은 아직 없음.

## 변경된 파일
- `Assets/Scripts/Enemy/EnemyController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/prefabs/Test/TestEnemy.prefab`
