## 2026-07-23

### 요청 내용

> 건물 같은경우 이륙했을때 공중판정되는건 잘 구현이 됬는데 공중판정일때 공중 공격 -> 건물 착륙 -> 그래도 공격 이러한 메커니즘으로 작동하거든 매번 공격주기가 돌아서 Attack할때 그 유닛이 공중인지 지상인지 확인해줬으면 좋겠어

### 조사 내용

- `doc/0213`에서 만든 도메인 차단은 **명령을 내리는 시점에 딱 한 번만** 체크한다:
  - `AttackFriendlyTarget(target)` 맨 앞에서 `IsAirborne(target)`을 한 번 계산해 `CanAttackDomain`으로 막는다.
- 하지만 `BuildingController`는 이/착륙으로 실시간으로 `IsLifted()`(공중 여부)가 바뀔 수 있다(`doc/0054` 건물 리프트). 명령을 내린 뒤 대상 건물이 착륙/이륙해서 도메인이 바뀌어도, 이미 진행 중인 공격은 재검증 없이 계속된다.
- 실제 데미지가 들어가는 지점은 `UnitController.Attack(Vector3 end, GameObject enemy)`(`Assets/Scripts/Unit/UnitController.cs:809`)이다. 이 메서드는 사거리 안에 있는 동안 `FriendlyAttackTick()`/`AttackRange.Update()`에서 **매 프레임 호출**되지만, 실제 데미지는 `alreadyAttacked` 쿨다운이 풀렸을 때만(`timeBetweenAttacks` 주기, 즉 "공격 한 사이클") 들어간다. 그런데 이 시점에 도메인 재검증이 없어서, 건물이 착륙해 도메인이 바뀌어도 계속 데미지가 들어간다.
- `Attack()`은 `GameObject enemy`만 받고 대상의 "지금" 공중 여부를 조회하는 수단이 없다. 기존에 있는 `GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`과 동일한 패턴(`TryGetComponent<UnitController>`/`TryGetComponent<EnemyController>`)으로 `IsTargetAirborne(GameObject target)`을 추가하면 됨. (`EnemyController`는 `doc/0213`에서 정한 대로 아직 공중 개념이 없으므로 이번에도 건드리지 않고, 항상 지상(`false`)으로 취급 — 나중에 추가되면 자연스럽게 반영됨.)

### 설계

- `UnitController`에 `private bool IsTargetAirborne(GameObject target)` 추가: `UnitController.IsAirUnit()` → `BuildingController.IsLifted()` → 그 외(EnemyController/자원 등)는 `false`.
- `Attack(Vector3 end, GameObject enemy)`에서 `alreadyAttacked` 체크 통과 직후(=이번이 실제로 데미지가 들어갈 "공격 사이클") **매번** `CanAttackDomain(IsTargetAirborne(enemy))`를 재확인한다.
  - 실패하면 `Debug.Log`로 알리고 데미지 없이 `return` — **`alreadyAttacked`/쿨다운은 소비하지 않음**. 그래야 대상이 다시 공격 가능한 도메인으로 돌아오는 즉시(다음 프레임) 바로 공격을 재개할 수 있다(쿨다운 때문에 대기하지 않음).
  - 성공하면 기존 로직 그대로 데미지 적용.
- 이 위치에서 처리하면 `FriendlyAttackTick`뿐 아니라 `AttackRange.Update()`를 통한 모든 공격 경로(적/아군 공통)에 자동으로 적용된다 — 호출부를 따로 안 건드려도 됨.

### 코드 변경 (예정)

#### 1) `Assets/Scripts/Unit/UnitController.cs` — 대상의 "현재" 공중 여부 조회 헬퍼 추가

**기존 코드** (`GetTargetArmorType` 아래)
```csharp
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

**변경 코드**
```csharp
    // 공격 대상의 장갑 타입을 조회한다 (건물/자원 등은 고유 보너스가 적용될 일이 없으므로 Light를 기본값으로 반환).
    private ArmorType GetTargetArmorType(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.GetArmorType();

        if (target.TryGetComponent<EnemyController>(out var enemyUnit))
            return enemyUnit.GetArmorType();

        return ArmorType.Light;
    }

    // 공격 대상이 "지금" 공중 상태인지 조회한다. 건물은 이/착륙으로 실시간 바뀔 수 있어(BuildingController.IsLifted)
    // 매 공격 사이클마다 다시 확인해야 한다 - 명령을 내린 시점에 캐싱해둔 값을 계속 쓰면 안 된다.
    // EnemyController는 아직 공중 개념이 없어(doc/0213) 항상 지상(false)으로 취급한다.
    private bool IsTargetAirborne(GameObject target)
    {
        if (target.TryGetComponent<UnitController>(out var friendlyUnit))
            return friendlyUnit.IsAirUnit();

        if (target.TryGetComponent<BuildingController>(out var building))
            return building.IsLifted();

        return false;
    }
```

#### 2) `Assets/Scripts/Unit/UnitController.cs` — `Attack()`에서 매 공격 사이클마다 도메인 재검증

**기존 코드**
```csharp
        if (alreadyAttacked)
            return;

        Debug.Log("공격성공!");
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
```

**변경 코드**
```csharp
        if (alreadyAttacked)
            return;

        bool targetIsAir = IsTargetAirborne(enemy);
        if (!CanAttackDomain(targetIsAir))
        {
            // 쿨다운(alreadyAttacked)은 건드리지 않는다 - 대상이 다시 공격 가능한 도메인으로 돌아오면(예: 건물 착륙)
            // 대기 없이 바로 다음 프레임에 공격을 재개할 수 있어야 하기 때문.
            Debug.Log($"{name}: 이 유닛은 {(targetIsAir ? "공중" : "지상")} 대상을 공격할 수 없습니다.");
            return;
        }

        Debug.Log("공격성공!");
        if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
```

### 요약/영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs` — `IsTargetAirborne()` 헬퍼 추가, `Attack()`이 매 공격 사이클마다 대상의 실시간 도메인을 재검증

`AttackFriendlyTarget`의 명령 시점 체크(`doc/0213`)는 그대로 유지 — "애초에 불가능한 대상을 공격 명령으로 받지 않기" 역할이고, 이번 변경은 "명령을 받은 뒤 대상의 상태가 바뀌는 경우"를 추가로 커버하는 것이라 서로 겹치지 않고 보완적입니다.

---

**적용 완료** (2026-07-23) — 사용자 확인 후 계획대로 그대로 적용함(`IsTargetAirborne()` 헬퍼 추가 + `Attack()` 내 매 공격 사이클 도메인 재검증). `BuildingController.IsLifted()`/`UnitController.IsAirUnit()` 둘 다 이미 public getter로 존재해 추가 컴파일 이슈 없음.
