## 날짜
2026-07-29

## 요청 내용
`ProjectileAttack`(doc/0290)의 `firePoint`를 여러 개 넣을 수 있게 해달라는 요청.

## 조사 내용
`UnitEffects.cs`(총구 이펙트)에 이미 동일한 요구사항이 `List<Transform> firePoints`(다연장 무기면 여러 개 추가)로 구현되어 있고, `EffectPlayer.SpawnAtPoints`가 지점마다 동시에 이펙트를 스폰하는 방식 - 이 프로젝트의 기존 컨벤션.

투사체는 이펙트와 달리 데미지를 실어 나르므로, 지점마다 동시에 발사하면 지점 수만큼 데미지가 곱연산되는 실질적 효과가 있음(예: 2개 지점 = 사실상 2배 데미지, 각각 명중해야 각각 데미지 적용). 사용자에게 "동시 발사(기존 머즐플래시 방식, 데미지 배가)" vs "번갈아 1발씩(데미지 불변)" 선택지를 물어봤고, **동시 발사**로 확정 - 기존 `UnitEffects` 컨벤션과 동일하게 감.

## 코드 변경 (적용 완료)

### Assets/Scripts/Unit/ProjectileAttack.cs

**기존 코드**
```csharp
[SerializeField] private GameObject projectilePrefab;
[SerializeField] private Transform firePoint;
[SerializeField] private float projectileSpeed = 30f;
[SerializeField] private float hitDistance = 0.5f;

public void Fire(Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
{
    if (projectilePrefab == null || firePoint == null || target == null)
        return;

    Vector3 toTarget = target.position - firePoint.position;
    GameObject instance = Instantiate(projectilePrefab, firePoint.position, Quaternion.LookRotation(toTarget));
    StartCoroutine(FlyRoutine(instance, target, targetHealth, damage, attackType));
}
```

**변경 코드**
```csharp
[SerializeField] private GameObject projectilePrefab;
// 다연장 무기용 - UnitEffects.firePoints와 동일한 패턴. 비워두면 유닛 자신의 위치에서 1발 발사, 여러 개
// 채우면 공격 1회당 각 지점에서 동시에 1발씩(총 지점 수만큼) 발사되고 각각 명중 시 데미지가 따로 들어간다.
[SerializeField] private List<Transform> firePoints = new();
[SerializeField] private float projectileSpeed = 30f;
[SerializeField] private float hitDistance = 0.5f;

public void Fire(Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
{
    if (projectilePrefab == null || target == null)
        return;

    if (firePoints == null || firePoints.Count == 0)
    {
        FireFromPoint(transform, target, targetHealth, damage, attackType); // 지점 안 채웠으면 유닛 자신 위치에서 1발
        return;
    }

    foreach (Transform point in firePoints)
    {
        if (point != null)
            FireFromPoint(point, target, targetHealth, damage, attackType);
    }
}

private void FireFromPoint(Transform point, Transform target, HealthManager targetHealth, int damage, AttackEffectType attackType)
{
    Vector3 toTarget = target.position - point.position;
    GameObject instance = Instantiate(projectilePrefab, point.position, Quaternion.LookRotation(toTarget));
    StartCoroutine(FlyRoutine(instance, target, targetHealth, damage, attackType));
}
```
`FlyRoutine`은 변경 없음 - 발사 지점마다 독립적인 코루틴으로 각자 목표를 추적하고 명중 시 각자 데미지를 적용한다.

## 요약/남은 작업
적용 완료. 인스펙터에서 `Fire Points` 리스트 크기를 늘려 Transform을 여러 개 연결하면 됨(기존 `Fire Point` 단일 필드는 사라지고 리스트로 대체됐으므로, doc/0290 때 이미 연결해둔 유닛이 있다면 다시 연결 필요). 다연장이 아닌 유닛은 지점 1개만 넣으면 기존과 동일하게 동작.

## 변경된 파일
- `Assets/Scripts/Unit/ProjectileAttack.cs`
