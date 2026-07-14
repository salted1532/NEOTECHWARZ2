# 0108 - 피격 이펙트를 공격 타입(총기/폭발형/레이저/화염)별로 다르게 재생

## 1. 요청
"피격 이펙트도 공격한 유닛에 따라 여러 이펙트를 관리할수 있어? 그냥 총을 사용하는 유닛과 탱크같이 폭발형 공격일때
이펙트가 다르도록" → "그럼 총기, 폭발형, 레이저, 화염 이렇게까지만 타입을 늘려서 추가해줘" (4종류로 고정).

## 2. 설계
- 공격자([[0105-effect-system-integration-design]]에서 이미 `attackDamage`/`armor`를 직접 갖던) `UnitController`에
  `AttackEffectType`(Bullet/Explosive/Laser/Flame) 필드를 하나 더 추가 — "이 유닛이 어떤 수단으로 공격하는가"를
  선언한다.
- `HealthManager.GetDamage()`/`OnDamaged` 이벤트에 이 값을 실어 피격 대상에게 전달한다(공격자 위치를
  실어보낸 것과 같은 방식, [[0107-effectexamples-broken-materials-investigation]]과는 무관한 별개 변경).
- `UnitEffects`(피격 대상 쪽)는 피격 이펙트 프리팹을 4개로 고정 분리(`bulletHitPrefab`/`explosiveHitPrefab`/
  `laserHitPrefab`/`flameHitPrefab`)하고, 받은 타입에 맞는 것을 골라 재생한다. 타입 종류가 정확히 4개로
  고정되었으므로(사용자가 "이렇게까지만"이라 명시) 0105의 `List<Transform>` 다중 위치 패턴과 달리 별도의
  확장 가능한 리스트 구조 대신 직접 필드 4개 + switch로 단순하게 구현했다.

## 3. 변경 내용 (before/after)

### `Assets/Scripts/Unit/UnitController.cs`

**클래스 선언부 위에 enum 추가** (`ResourceType`이 `ResourceNode.cs`에 top-level로 선언된 것과 동일한 관례)
```csharp
public enum AttackEffectType { Bullet, Explosive, Laser, Flame }
```

**전투 스탯 필드 옆에 추가**
```csharp
[SerializeField] private int attackDamage;
[SerializeField] private int armor;
[SerializeField] private AttackEffectType attackType = AttackEffectType.Bullet;
```

**전 (`Attack()` 데미지 적용부)**
```csharp
targetHealth.GetDamage(attackDamage, transform.position);
GetComponent<UnitEffects>()?.PlayAttack();
```
**후**
```csharp
targetHealth.GetDamage(attackDamage, transform.position, attackType);
GetComponent<UnitEffects>()?.PlayAttack();
```

**게터 추가**
```csharp
public AttackEffectType GetAttackType() => attackType;
```

### `Assets/Scripts/Unit/HealthManager.cs`

**전**
```csharp
public event System.Action<int, Vector3> OnDamaged; // (damage amount, attacker world position)

public void GetDamage(int damage, Vector3 attackerPosition)
{
    ...
    OnDamaged?.Invoke(damage, attackerPosition);
```
**후**
```csharp
public event System.Action<int, Vector3, AttackEffectType> OnDamaged;

public void GetDamage(int damage, Vector3 attackerPosition, AttackEffectType attackType)
{
    ...
    OnDamaged?.Invoke(damage, attackerPosition, attackType);
```

### `Assets/Scripts/Effects/UnitEffects.cs`

**전**
```csharp
[SerializeField] private GameObject hitPrefab;
...
private void HandleDamaged(int amount, Vector3 attackerPosition)
{
    ...
    EffectPlayer.Spawn(hitPrefab, hitPoint, rot);
}
```
**후**
```csharp
[SerializeField] private GameObject bulletHitPrefab;
[SerializeField] private GameObject explosiveHitPrefab;
[SerializeField] private GameObject laserHitPrefab;
[SerializeField] private GameObject flameHitPrefab;
...
private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType)
{
    ...
    EffectPlayer.Spawn(GetHitPrefab(attackType), hitPoint, rot);
}

private GameObject GetHitPrefab(AttackEffectType attackType)
{
    switch (attackType)
    {
        case AttackEffectType.Explosive: return explosiveHitPrefab;
        case AttackEffectType.Laser: return laserHitPrefab;
        case AttackEffectType.Flame: return flameHitPrefab;
        default: return bulletHitPrefab;
    }
}
```

`GetDamage`/`OnDamaged` 호출부는 프로젝트 전체에서 `UnitController.Attack()` 한 곳뿐임을 재확인 후 시그니처를
변경했다 (다른 호출부 없음 — 컴파일 깨질 위험 없음).

## 4. 남은 작업 (에디터에서 수동)
- 각 유닛 프리팹의 `UnitController` 인스펙터에서 `Attack Type`을 실제 성격에 맞게 지정 (총 든 보병=Bullet,
  탱크=Explosive 등 — 기본값은 Bullet).
- 각 유닛 프리팹의 `UnitEffects`에서 4개 슬롯(`Bullet/Explosive/Laser/Flame Hit Prefab`) 중 필요한 것만 채우면
  된다. 비워둔 슬롯은 `EffectPlayer.Spawn`이 null 체크로 조용히 건너뛰므로 에러 없이 "그 타입으로 맞아도 이펙트
  없음" 상태로 동작한다.
