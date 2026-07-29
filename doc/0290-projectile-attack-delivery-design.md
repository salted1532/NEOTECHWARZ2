## 날짜
2026-07-29

## 요청 내용
SkyLancer(공중 유닛)의 공격에 실제로 날아가는 투사체를 넣고 싶다는 논의 끝에, 범용 기능으로 확장:
"유닛 스크립터블오브젝트에서 공격방식(히트스킨, 투사체) 이 두 가지 버전을 고를 수 있게 해서, 투사체일 시
투사체를 발사하고 그게 적 유닛을 끝까지 따라가다가 맞으면 그때 데미지가 들어가도록 하자."

## 조사 내용
`UnitController.Attack()`(`Assets/Scripts/Unit/UnitController.cs:841`)과 `EnemyUnitController.Attack()`
(`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:226`)은 완전히 대칭 구조 - 둘 다 데미지를
`targetHealth.GetDamage(finalDamage, transform.position, attackType)`로 **즉시** 적용하고, 그 옆에
`UnitEffects.PlayAttack()`(총구 이펙트), `LaserBeamAttack.Fire(target)`(레이저 유닛만, doc/0218),
`turretController.FireRecoil()`(포탑 유닛만) 같은 옵셔널 컴포넌트를 훅으로 나란히 호출하는 패턴.

`EnemyUnitDataSO`는 `UnitDataSO`와 완전히 같은 `UnitData` 클래스를 재사용하므로(doc/0230), `UnitData`에
필드 하나만 추가하면 아군/적 진영 둘 다 자동으로 적용됨.

`LaserBeamAttack`(doc/0218)이 정확히 같은 자리에 이미 있는 "옵셔널 컴포넌트 + 유닛별 프리팹/firePoint를
인스펙터에서 직접 연결" 패턴이라, 투사체도 그대로 따라가는 게 기존 코드와 가장 잘 맞음. 다만 결정적 차이:
`LaserBeamAttack`은 **순수 시각효과**(데미지는 이미 위에서 즉시 적용됨)라 인스턴스 하나를 계속 재사용해도
되지만, 투사체는 **데미지를 실어 나르는 실체**라 명중 여부/시점이 전투 결과에 직접 영향을 준다는 점이 다름.

## 설계안

### 1) 새 열거형 - `Assets/Scripts/Unit/DamageTypes.cs`
```csharp
// 공격 전달 방식: Hitscan(즉시 명중, 기존 동작) vs Projectile(투사체가 날아가 명중해야 데미지 적용).
public enum AttackDeliveryType { Hitscan, Projectile }
```

### 2) `UnitData`에 필드 추가 - `Assets/Scripts/ScriptableObject/UnitDataSO.cs`
```csharp
[Header("공격 전달 방식 (Projectile 선택 시 해당 유닛 프리팹에 ProjectileAttack 컴포넌트를 붙이고 투사체 프리팹을 연결해야 함)")]
[field: SerializeField]
public AttackDeliveryType attackDelivery { get; private set; } = AttackDeliveryType.Hitscan; // 기본값 = 기존 동작 그대로
```
`EnemyUnitDataSO`는 같은 `UnitData`를 쓰므로 별도 수정 불필요.

### 3) 새 컴포넌트 - `Assets/Scripts/Unit/ProjectileAttack.cs` (신규, `LaserBeamAttack.cs`와 같은 자리)
```csharp
[SerializeField] private GameObject projectilePrefab;
[SerializeField] private Transform firePoint;
[SerializeField] private float projectileSpeed = 30f;
[SerializeField] private float hitDistance = 0.5f; // 이 거리 안으로 들어오면 명중 처리

// UnitController/EnemyUnitController.Attack()이 (즉시 데미지 대신) 호출. targetHealth/damage/attackType은
// 발사 시점 기준으로 미리 계산해서 넘겨받는다(장갑/배율은 명중 시점이 아니라 발사 시점 기준 - 비행 중
// 대상 장갑이 바뀌는 경우는 없다고 가정, 계산 로직 중복 방지).
public void Fire(Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
{
    GameObject instance = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(target.position - firePoint.position));
    StartCoroutine(FlyRoutine(instance, target, targetHealth, damage, attackType));
}

private IEnumerator FlyRoutine(GameObject instance, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
{
    while (target != null) // 대상이 비행 중 파괴되면(다른 공격에 먼저 죽음) 데미지 없이 소멸
    {
        Vector3 toTarget = target.position - instance.transform.position;
        if (toTarget.magnitude <= hitDistance)
        {
            targetHealth?.GetDamage(damage, instance.transform.position, attackType); // 명중 - 여기서 처음 데미지 적용
            break;
        }
        instance.transform.position += toTarget.normalized * projectileSpeed * Time.deltaTime;
        instance.transform.rotation = Quaternion.LookRotation(toTarget);
        yield return null;
    }
    Destroy(instance);
}
```

**LaserBeamAttack과 다르게 매 발사마다 `Instantiate`/`Destroy`(풀링 안 함)** - 공격속도가 빠른 유닛은 이전
투사체가 아직 날아가는 중에 다음 발사가 나갈 수 있어서, 인스턴스 하나를 재사용하면 먼저 나간 투사체의
비행이 끊기고 새 발사 위치로 순간이동해버림. 여러 발이 동시에 공중에 떠 있을 수 있어야 하므로 발사마다
새로 만든다(추후 필요하면 오브젝트 풀로 최적화 가능하지만, 지금은 명중률/전투 스킬 유닛이 소수라 문제 없을 것으로 예상).

**명중 판정은 거리 임계값(`hitDistance`)만 사용** - 콜라이더/트리거 기반 물리 충돌은 안 씀(프로젝트 다른
곳에서도 이런 용도의 물리 충돌 패턴이 없음, `LaserBeamAttack`도 시각적 끝점 계산에만 `ClosestPoint`를
쓰지 물리 충돌 감지는 안 함). 대상이 빠르게 움직이면 `hitDistance`를 너무 작게 잡을 경우 스쳐 지나가
영원히 안 맞을 수 있으니, 유닛별 이동속도에 맞춰 여유 있게 잡아야 함(에디터에서 튜닝).

### 4) `UnitController.cs`/`EnemyUnitController.cs`의 `Attack()` 수정
```csharp
if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
{
    int targetArmor = GetTargetArmor(enemy);
    int finalDamage = CalculateFinalDamage(enemy, targetArmor);

    if (attackDelivery == AttackDeliveryType.Projectile && TryGetComponent(out ProjectileAttack projectileAttack))
        projectileAttack.Fire(enemy.transform, targetHealth, finalDamage, attackType);
    else
        targetHealth.GetDamage(finalDamage, transform.position, attackType); // 기존 Hitscan 동작 그대로

    GetComponent<UnitEffects>()?.PlayAttack();
    GetComponent<UnitAudio>()?.PlayAttackSFX();
    GetComponent<LaserBeamAttack>()?.Fire(enemy.transform);
    turretController?.FireRecoil();
}
```
`attackDelivery`는 `ApplyUnitData()`에서 `data.attackDelivery`로 채워지는 새 private 필드. `Projectile`로
설정했는데 `ProjectileAttack` 컴포넌트를 프리팹에 안 붙였으면(설정 실수) 자동으로 기존 즉시 데미지로
폴백 - 데미지가 아예 안 들어가는 사고를 방지.

## 확인 결과
사용자가 "이대로 진행"으로 확정 - 위 설계 그대로 적용.

## 코드 변경 (적용 완료)

### Assets/Scripts/Unit/DamageTypes.cs
`AttackDeliveryType { Hitscan, Projectile }` 열거형 추가.

### Assets/Scripts/ScriptableObject/UnitDataSO.cs
`UnitData`에 `attackDelivery`(기본값 `Hitscan`) 필드 추가. `EnemyUnitDataSO`가 같은 `UnitData`를 재사용하므로 자동 적용.

### Assets/Scripts/Unit/ProjectileAttack.cs (신규)
설계안 그대로 구현 - `Fire(target, targetHealth, damage, attackType)`가 발사마다 프리팹을 `Instantiate`, 코루틴이 매 프레임 `hitDistance` 안으로 들어올 때까지 대상 쪽으로 이동시키다가 명중 시 `HealthManager.GetDamage()` 호출 후 `Destroy`. 대상이 비행 중 파괴되면 데미지 없이 소멸.

### Assets/Scripts/Unit/UnitController.cs
- `attackDelivery` private 필드 추가, `ApplyUnitData()`에서 `data.attackDelivery`로 채움.
- `Attack()`: `attackDelivery == Projectile`이고 `ProjectileAttack` 컴포넌트가 붙어있으면 `Fire()` 호출(데미지는 명중 시 적용), 아니면 기존처럼 `GetDamage()` 즉시 호출(Hitscan 폴백 포함).

### Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs
`UnitController`와 동일하게 `attackDelivery` 필드/`ApplyUnitData()`/`Attack()` 수정 - `EnemyUnitDataSO`가 같은 `UnitData`를 쓰므로 적 진영 유닛도 동일하게 지원됨.

## 요약/남은 작업
코드/컴포넌트 준비 완료. 아직 안 된 것(에디터 수동 작업, doc/0218 때와 동일한 성격):
1. SkyLancer(또는 원하는 유닛)의 `UnitData.attackDelivery`를 인스펙터에서 `Projectile`로 변경.
2. 해당 유닛 프리팹에 `ProjectileAttack` 컴포넌트 추가, `Projectile Prefab`(투사체 3D 모델 - 이 세션에서 새로 만들 수 없음)과 `Fire Point` Transform 연결.
3. `Projectile Speed`/`Hit Distance` 값을 유닛 이동속도/크기에 맞게 튜닝(기본 30, 0.5).
4. 플레이 모드에서 투사체가 firePoint에서 대상까지 실제로 날아가고, 명중 시에만 데미지가 들어가는지, 대상이 죽으면 투사체가 조용히 사라지는지 확인.

## 변경된 파일
- `Assets/Scripts/Unit/DamageTypes.cs`
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs`
- `Assets/Scripts/Unit/ProjectileAttack.cs` (신규)
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
