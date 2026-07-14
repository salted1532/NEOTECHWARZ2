# 0115 - 건물/Construction 피격 이펙트 추가 (+ 공통 로직 정리)

## 1. 요청
"건물이랑 Construction도 피격 이펙트를 추가할수 있도록 해줘"

## 2. 배경
`HealthManager.OnDamaged(damage, attackerPosition, attackType)`는 유닛 전용이 아니라 `HealthManager`가
붙은 모든 대상(유닛/건물/`BaseStructure`)에서 공통으로 나가는 이벤트다. 지금까지는 `UnitEffects`만 이걸
구독해서 피격 이펙트를 재생했고, `BuildingEffects`(이착륙)/`ConstructionEffects`(건설중/완공)에는 피격
처리가 없었다 — [[0108-hit-effect-attack-type-variants]]에서 "건물은 범위 밖"으로 명시했던 부분.

세 컴포넌트에 똑같은 로직(공격 타입별 4개 프리팹 슬롯 + `ClosestPoint` 기반 위치 계산)을 그대로 복붙하면
중복이 커서, 공용 부분을 `HitEffectSet`(직렬화 가능한 데이터 묶음)과 `EffectPlayer.PlayHit()`(위치 계산 +
스폰 로직)으로 뽑아냈다. `UnitEffects`도 이 공용 헬퍼를 쓰도록 같이 정리했다.

## 3. 신규/변경 파일

### `Assets/Scripts/Effects/HitEffectSet.cs` (신규)
```csharp
[System.Serializable]
public class HitEffectSet
{
    [SerializeField] private GameObject bulletHitPrefab;
    [SerializeField] private GameObject explosiveHitPrefab;
    [SerializeField] private GameObject laserHitPrefab;
    [SerializeField] private GameObject flameHitPrefab;

    public GameObject GetPrefab(AttackEffectType attackType) { ... } // 기존 UnitEffects.GetHitPrefab과 동일 로직
}
```

### `Assets/Scripts/Effects/EffectPlayer.cs` — `PlayHit()` 추가
```csharp
public static void PlayHit(Transform target, Collider bodyCollider, Vector3 attackerPosition, GameObject hitPrefab)
{
    Vector3 hitPoint = bodyCollider != null ? bodyCollider.ClosestPoint(attackerPosition) : target.position;
    Vector3 outward = hitPoint - target.position;
    Quaternion rot = outward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(outward.normalized) : Quaternion.identity;
    Spawn(hitPrefab, hitPoint, rot);
}
```
기존 `UnitEffects.HandleDamaged()`에 있던 위치 계산 로직을 그대로 옮긴 것.

### `Assets/Scripts/Effects/UnitEffects.cs` (리팩터링, 동작 변화 없음)
`bulletHitPrefab`/`explosiveHitPrefab`/`laserHitPrefab`/`flameHitPrefab` 4개 필드 + `GetHitPrefab()` switch를
`[SerializeField] private HitEffectSet hitEffects = new();` 하나로 교체. `HandleDamaged()`는
`EffectPlayer.PlayHit(transform, bodyCollider, attackerPosition, hitEffects.GetPrefab(attackType));` 한 줄로 축약.

**주의**: 인스펙터 필드 구조가 바뀌어서(개별 필드 4개 → `HitEffectSet` 중첩 객체), 혹시 이미 유닛
프리팹에서 피격 이펙트 프리팹을 채워둔 게 있다면 재할당이 필요할 수 있다.

### `Assets/Scripts/Effects/BuildingEffects.cs` — 피격 추가
`HealthManager`/`Collider` 참조 + `OnEnable`/`OnDisable` 구독 + `HandleDamaged()`를 `UnitEffects`와 동일한
패턴으로 추가. 이착륙 이펙트 코드는 그대로.

### `Assets/Scripts/Effects/ConstructionEffects.cs` — 피격 추가
동일한 패턴. `BaseStructure`도 `HealthManager`를 갖고 있고(우호 공격 등으로 데미지를 받을 수 있음,
[[0051-friendly-fire-basestructure-support]]) 실제로 `GetDamage()` 호출 경로가 있으므로 유효한 추가.

## 4. 남은 작업 (에디터)
- 건물/`BaseStructure` 프리팹에 `BuildingEffects`/`ConstructionEffects`가 이미 붙어있다면(이착륙/건설
  이펙트 때문에 이미 붙였을 것) 인스펙터에 새로 생긴 "피격" 섹션(`Hit Effects` - Bullet/Explosive/Laser/
  Flame Hit Prefab)만 채우면 된다. 컴포넌트를 아직 안 붙인 프리팹은 [[0105-effect-system-integration-design]]
  대로 부착부터 필요.
