# 0106 - 이펙트 시스템 연동 구현 (0105 설계 그대로 적용)

## 1. 요청
"이대로 코드 구현해줘" — [[0105-effect-system-integration-design]] 문서(다중 위치 리스트 반영본)를 실제
`Assets/Scripts`에 그대로 적용해달라는 요청.

## 2. 신규 파일 (0105 3.2/3.3/3.8절 코드를 그대로 생성)

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Effects/EffectPlayer.cs` | `Spawn`(단일), `SpawnAtPoints`(다중, 발사 후 잊기), `SpawnPersistentAtPoints`(다중, 지속형) 공용 정적 헬퍼 |
| `Assets/Scripts/Effects/UnitEffects.cs` | 유닛용: 공격(총구)/이동 트레일/피격(콜라이더 기준 동적 위치)/사망 이펙트 |
| `Assets/Scripts/Effects/BuildingEffects.cs` | 건물용: 이륙/착륙 이펙트 |
| `Assets/Scripts/Effects/ConstructionEffects.cs` | `BaseStructure`용: 건설 중 지속 이펙트 + 완공 이펙트 |

## 3. 기존 파일 수정 (before/after)

### 3.1 `Assets/Scripts/Unit/HealthManager.cs`

**전 (이벤트 선언)**
```csharp
public event System.Action<int, int> OnHealthChanged; // (currentHp, maxHealth)
public event System.Action OnDeath;
```
**후**
```csharp
public event System.Action<int, int> OnHealthChanged; // (currentHp, maxHealth)
public event System.Action OnDeath;
// 피격 이펙트 전용 이벤트. OnHealthChanged는 Heal()에서도 발생해 "회복"과 구분이 안 되므로 별도로 둔다.
// attackerPosition은 피격 이펙트를 콜라이더 표면의 "공격자 쪽" 지점에 스폰하는 데 쓰인다(UnitEffects 참고).
public event System.Action<int, Vector3> OnDamaged; // (damage amount, attacker world position)
```

**전 (`GetDamage`)**
```csharp
public void GetDamage(int damage)
{
    if (isDead || damage <= 0)
        return;

    currentHp = Mathf.Max(0, currentHp - damage);
    OnHealthChanged?.Invoke(currentHp, maxHealth);

    Debug.Log($"{gameObject.name} HP: {currentHp}/{maxHealth}");
```
**후**
```csharp
public void GetDamage(int damage, Vector3 attackerPosition)
{
    if (isDead || damage <= 0)
        return;

    currentHp = Mathf.Max(0, currentHp - damage);
    OnHealthChanged?.Invoke(currentHp, maxHealth);
    OnDamaged?.Invoke(damage, attackerPosition);

    Debug.Log($"{gameObject.name} HP: {currentHp}/{maxHealth}");
```

`GetDamage` 호출부는 프로젝트 전체에서 `UnitController.Attack()` 한 곳뿐임을 재확인 후 시그니처를 변경했다
(다른 호출부 없음 — 컴파일 깨질 위험 없음).

### 3.2 `Assets/Scripts/Unit/UnitController.cs`

**전 (`Attack()` 데미지 적용부)**
```csharp
Debug.Log("공격성공!");
if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
{
    targetHealth.GetDamage(attackDamage);
}
```
**후**
```csharp
Debug.Log("공격성공!");
if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
{
    targetHealth.GetDamage(attackDamage, transform.position); // 공격자 위치를 같이 넘겨 피격 이펙트 방향 계산에 사용
    GetComponent<UnitEffects>()?.PlayAttack();
}
```

**추가 (상태 확인 게터, `IsIdle`/`IsMove`/`IsAttack` 옆)**
```csharp
// 이동 이펙트(UnitEffects)가 상태머신을 직접 건드리지 않고 매 프레임 폴링으로 이동 여부를 판단할 수 있도록 노출.
public bool IsCurrentlyMoving()
{
    if (isAirUnit)
        return isMovingAirUnit;

    return navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f;
}
```

### 3.3 `Assets/Scripts/Building/BuildingController.cs`

**`LiftOff()` 끝부분 추가**
```csharp
isLifted = true;
isAscending = true;
verticalTarget = transform.position + Vector3.up * liftHeight;

GetComponent<BuildingEffects>()?.PlayTakeoff(); // 추가
```

**`Land()` 끝부분 추가**
```csharp
landed?.Invoke();

GetComponent<BuildingEffects>()?.PlayLanding(); // 추가
```

### 3.4 `Assets/Scripts/Building/BaseStructure.cs`

**`Initialize()` 끝부분 추가**
```csharp
if (healthManager != null)
{
    healthManager.SetMaxHealth(finalMaxHealth);
    healthManager.SetHealth(0);
}

GetComponent<ConstructionEffects>()?.StartLoop(); // 추가
```

**`CompleteConstruction()` 시작부분 추가**
```csharp
private void CompleteConstruction()
{
    GetComponent<ConstructionEffects>()?.StopLoopAndPlayComplete(); // 추가

    BuildingData data = buildingDatabase != null
    ...
```

`CancelConstruction()`(플레이어가 건설을 직접 취소하는 경로)은 별도로 `StopLoop`을 호출하지 않는다 — 건설
루프 이펙트는 `SpawnPersistentAtPoints`로 `BaseStructure` 계층 하위(자신 또는 자식 지점)에 부모로 붙어 있어서,
`Destroy(gameObject)`가 실행되면 Unity가 자식 이펙트 인스턴스도 함께 파괴한다. 별도 정리 코드가 필요 없다.

## 4. 반영하지 않은 것 (0105 문서에서 이미 "선택/추후"로 표시된 부분)

- **사망 이펙트 옵션 B(래그돌/사망모션)**: `Die()` 계열이 여전히 즉시 `Destroy(gameObject)`한다. 현재는
  옵션 A(파티클만 스폰)만 적용됨. 래그돌/사망모션 에셋을 실제로 쓰게 되면 별도로 `Die()` 구조 변경이 필요하다.
- **건물 피격 이펙트**: `BuildingEffects`에는 피격(`HandleDamaged`) 로직을 추가하지 않았다 (0105 3.7절에서
  범위 밖으로 명시).

## 5. 남은 작업 (에디터에서 수동으로 해야 하는 부분)

코드 연동은 끝났지만, 아직 다음은 실제 이펙트 **에셋**과 **프리팹 설정**이 있어야 동작한다(이번 요청 범위 밖):

1. 유닛/건물/`BaseStructure` 프리팹에 각각 `UnitEffects`/`BuildingEffects`/`ConstructionEffects` 컴포넌트를
   부착.
2. 구해온 파티클/사운드 프리팹을 각 컴포넌트의 인스펙터 필드(`muzzlePrefab`, `hitPrefab`, `deathPrefab`,
   `moveTrailPrefab`, `takeoffPrefab`, `landingPrefab`, `constructionLoopPrefab`, `completePrefab` 등)에 연결.
3. (선택) 다중 지점을 쓰고 싶으면 유닛/건물 자식으로 빈 오브젝트(총구, 발, 추진구 위치 등)를 만들어
   해당 `List<Transform>` 필드에 추가.
4. `UnitEffects.HandleDamaged()`가 쓰는 `bodyCollider`가 Box/Capsule/Sphere 프리미티브인지 유닛 프리팹에서
   확인(0105 3.7절 전제 조건) — Non-convex Mesh Collider인 유닛이 있다면 Convex 옵션을 켜야 함.

이 4가지는 코드가 아니라 씬/프리팹 편집 작업이라 여기서는 하지 않았다. 진행하려면 알려달라.
