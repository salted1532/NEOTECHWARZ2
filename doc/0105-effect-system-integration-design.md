# 0105 - 이펙트 시스템(공격/이동/건설/이착륙/사망/피격) 연동 설계

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 + 제안 코드**만 담고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 아래 내용을 검토한 뒤 실제로
> 적용할지(전체/일부) 알려주면 그때 `Assets/Scripts`에 반영한다.

## 1. 요청

"공격 이펙트, 이동 이펙트, 건설 이펙트, 이륙 이펙트, 착륙 이펙트, 사망 이펙트(혹은 래그돌/사망모션), 피격 이펙트를
에셋으로 구해서 붙일 예정인데, 이걸 코드적으로 어떻게 연결할지 문서로 정리해줘."

## 2. 현재 프로젝트 분석 (설계 근거)

- **이펙트 관련 컴포넌트가 프로젝트에 전무하다.** `Assets/Scripts` 전체에 `Animator`, `ParticleSystem`,
  `AudioSource` 참조가 단 한 곳도 없다(grep 결과 0건). 즉 완전히 새로 설계해야 하는 영역이며, 기존 관례를
  참고할 대상은 "마커/아이콘을 `[SerializeField]`로 프리팹에 직접 연결하는 패턴" 정도다
  (`UnitController.unitMarker`, `BuildingController.buildingMarker` 등).
- **전투는 단방향이다.** 데미지를 주는 코드는 `UnitController.Attack()`
  (`Assets/Scripts/Unit/UnitController.cs:770-796`) 한 곳뿐이다. `EnemyController`는 공격 로직이 없고
  피격만 당하는 쪽이다 — 공격 이펙트는 사실상 "플레이어 유닛이 발사하는 쪽"만 고려하면 된다.
- **각 이벤트의 실제 발생 지점(후킹 포인트)**:

| 이펙트 | 발생 지점 | 비고 |
|---|---|---|
| 공격 | `UnitController.Attack()` (`UnitController.cs:770`) | `alreadyAttacked` 쿨다운 통과 시 데미지 적용 직후 |
| 이동 | `UnitController.MoveAgentTo()` 호출 시점들, 또는 `navMeshAgent.velocity`/`isMovingAirUnit` 상태 | 이동 "명령"은 여러 진입점(MoveTo/AttackMoveTo/ChaseTarget/PatrolUnit 등)에 흩어져 있어, 명령 시점을 전부 후킹하기보다 **속도/플래그를 매 프레임 폴링**하는 편이 안전 |
| 건설(진행/완료) | `BaseStructure.Update()`(진행 중), `BaseStructure.CompleteConstruction()`(완료) | 시작은 `Initialize()` |
| 이륙 | `BuildingController.LiftOff()` (`BuildingController.cs:202`) | `isAscending = true` 되는 시점 |
| 착륙 | `BuildingController.Land()` (`BuildingController.cs:287`) | 하강 시작(`isDescending = true`, line 152)도 이펙트 후보 |
| 사망 | `HealthManager.Die()`(private, line 105) → 각 `IDestructible.Die()` | **문제: 전부 즉시 `Destroy(gameObject)`.** 아래 3.5절 참고 |
| 피격 | `HealthManager.GetDamage()` (line 59) | `OnHealthChanged` 이벤트가 있지만 `Heal()`도 같은 이벤트를 쏴서 회복과 구분이 안 됨 |

- **사망 처리 구조가 전부 동일 패턴이다**: `UnitController.Die()`, `BuildingController.Die()`,
  `EnemyController.Die()`, `BaseStructure.Die()`(→ `CancelConstruction()`) 모두 "목록에서 제거 →
  `Destroy(gameObject)`"를 같은 프레임에 끝낸다. 파티클 하나 터뜨리는 정도(폭발 이펙트)는 문제없지만,
  **오브젝트 자체가 몇 초간 남아 애니메이션을 재생해야 하는 래그돌/사망모션은 이 구조로는 불가능**하다.

## 3. 설계 방향

### 3.1 원칙
- 기존 관례를 따라 **이펙트 프리팹 참조는 각 게임오브젝트 프리팹에 직접 `[SerializeField]`로 연결**한다
  (아이콘/마커와 동일한 패턴). 유닛/건물 종류마다 중앙 카탈로그(SO)로 강제 통일하지 않는다 — 여러 유닛이
  같은 이펙트(예: 공용 피격 스파크)를 쓰고 싶으면 그냥 같은 프리팹을 각자 필드에 꽂으면 된다.
- 이펙트 재생 로직은 `UnitController`/`BuildingController`/`HealthManager` 등 **기존 상태머신 코드에
  섞지 않고**, 옆에 붙는 전담 컴포넌트(`UnitEffects`, `BuildingEffects`, `ConstructionEffects`)가 최소한의
  이벤트/게터를 통해 통지받아 처리한다. 상태머신 코드 자체는 최소 1~2줄(이벤트 호출 or 게터 추가)만 건드린다.
- 파티클/사운드는 기본적으로 "발사 후 잊기(fire-and-forget)" — 재생 후 자기 수명대로 스스로 파괴된다.
  지속형(건설 중 먼지, 이동 트레일)만 시작/종료를 명시적으로 호출한다.
- **오브젝트 풀링은 1단계에서 생략**한다. `Instantiate` + 자동 `Destroy(obj, duration)`으로 시작하고,
  실제로 유닛 수백 단위 동시 교전에서 GC/성능 문제가 보이면 그때 풀링을 도입한다(과설계 방지).

### 3.2 공용 헬퍼: `EffectPlayer` (정적 유틸리티) + 다중 위치 리스트 규칙

각 이펙트(이동/이륙/착륙/건설/사망 등)마다 스폰 위치를 **`Transform` 하나가 아니라 `List<Transform>`으로**
받는다. 지금까지 문서 초안은 "발밑 1곳", "건물 중심 1곳"처럼 위치가 하나였는데, 실제로는 다리/바퀴/추진구가
여럿인 유닛, 추진구가 여러 개인 건물, 비계가 여러 면에 있는 건설 현장처럼 **한 이펙트를 여러 지점에서 동시에
재생하고 싶은 경우가 많다.** 규칙은 하나로 통일한다: **리스트가 비어있으면 그 오브젝트 자신의 위치(`transform`)
하나에만 재생하고(기존과 동일하게 동작), 리스트에 `Transform`을 채워 넣으면 그 지점 각각에서 동시에 재생한다.**
그래야 에셋을 아직 안 채운 프리팹도 그대로 동작하고, 나중에 이펙트 지점을 늘리고 싶을 때 인스펙터에서 리스트에
자식 오브젝트를 끌어넣기만 하면 된다(코드 변경 불필요).

```csharp
// Assets/Scripts/Effects/EffectPlayer.cs (신규, static)
using System.Collections.Generic;
using UnityEngine;

public static class EffectPlayer
{
    // 이펙트 프리팹을 pos/rot에 스폰하고, ParticleSystem의 재생시간(main.duration + startLifetime.constantMax)
    // 을 기준으로 자동 파괴한다. AudioSource가 붙어있으면 같이 재생된다(프리팹에 미리 세팅).
    public static GameObject Spawn(GameObject effectPrefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (effectPrefab == null)
            return null;

        GameObject instance = Object.Instantiate(effectPrefab, pos, rot, parent);

        if (instance.TryGetComponent<ParticleSystem>(out var ps))
        {
            float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
            Object.Destroy(instance, lifetime);
        }

        return instance;
    }

    // 발사 후 잊기(fire-and-forget) 이펙트를 여러 지점에 동시에 스폰한다. points가 비어있으면
    // fallback(보통 자기 자신의 transform) 하나에만 스폰한다 - 지점을 안 채운 프리팹도 그대로 동작한다.
    public static void SpawnAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback, Transform parent = null)
    {
        if (effectPrefab == null)
            return;

        if (points == null || points.Count == 0)
        {
            Spawn(effectPrefab, fallback.position, fallback.rotation, parent);
            return;
        }

        foreach (Transform point in points)
        {
            if (point == null)
                continue;

            Spawn(effectPrefab, point.position, point.rotation, parent);
        }
    }

    // 지속형 이펙트(이동 트레일, 건설 중 파티클처럼 켜져 있는 동안 계속 유지되는 것)를 각 지점에 하나씩 스폰해
    // 그 지점에 붙여두고(parent = 그 지점 자신, 계속 따라다니게), 나중에 꺼야 할 때 정리할 수 있도록 인스턴스
    // 목록을 반환한다. 지속형이라 Spawn()과 달리 자동 파괴를 걸지 않는다 - 호출자가 명시적으로 Destroy한다.
    public static List<GameObject> SpawnPersistentAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback)
    {
        List<GameObject> instances = new List<GameObject>();
        if (effectPrefab == null)
            return instances;

        if (points == null || points.Count == 0)
        {
            instances.Add(Object.Instantiate(effectPrefab, fallback.position, fallback.rotation, fallback));
            return instances;
        }

        foreach (Transform point in points)
        {
            if (point == null)
                continue;

            instances.Add(Object.Instantiate(effectPrefab, point.position, point.rotation, point));
        }

        return instances;
    }
}
```

### 3.3 유닛: `UnitEffects` (신규, 유닛 프리팹에 `UnitController`와 같이 부착)

```csharp
// Assets/Scripts/Effects/UnitEffects.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class UnitEffects : MonoBehaviour
{
    [Header("공격 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject muzzlePrefab;
    [SerializeField] private List<Transform> firePoints = new(); // 총구/무기 끝 - 다연장 무기면 여러 개 추가

    [Header("이동 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject moveTrailPrefab;
    [SerializeField] private List<Transform> moveTrailPoints = new(); // 발/바퀴/추진구 등 - 여러 개면 각 지점에 트레일이 붙어 따라다님
    private List<GameObject> activeTrails = new();

    [Header("피격 (위치는 동적 계산 - 3.7절, 리스트 아님)")]
    [SerializeField] private GameObject hitPrefab;

    [Header("사망 (비워두면 유닛 자신의 위치에서 재생)")]
    [SerializeField] private GameObject deathPrefab;
    [SerializeField] private List<Transform> deathPoints = new(); // 큰 유닛의 다중 폭발 지점(선택)

    private UnitController unitController;
    private HealthManager healthManager;
    private Collider bodyCollider; // 피격 이펙트 위치 계산용 (AttackRange의 트리거 콜라이더가 아니라 유닛 본체 콜라이더)

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        healthManager = GetComponent<HealthManager>();
        bodyCollider = GetComponent<Collider>(); // 클릭 판정(UserControl의 layerUnit 레이캐스트)에 쓰는 것과 동일한 콜라이더
    }

    private void OnEnable()
    {
        if (healthManager != null)
        {
            healthManager.OnDamaged += HandleDamaged; // 3.7절: HealthManager에 신규 이벤트 추가 필요
            healthManager.OnDeath += HandleDeath;      // 기존 이벤트 재사용
        }
    }

    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnDamaged -= HandleDamaged;
            healthManager.OnDeath -= HandleDeath;
        }
    }

    // 이동 중 여부는 상태머신을 건드리지 않고 매 프레임 폴링으로 판단한다(3.6절).
    private void Update()
    {
        bool moving = unitController != null && unitController.IsCurrentlyMoving();
        SetMoveTrail(moving);
    }

    private void SetMoveTrail(bool moving)
    {
        if (moving && activeTrails.Count == 0 && moveTrailPrefab != null)
        {
            activeTrails = EffectPlayer.SpawnPersistentAtPoints(moveTrailPrefab, moveTrailPoints, transform);
        }
        else if (!moving && activeTrails.Count > 0)
        {
            foreach (GameObject trail in activeTrails)
                if (trail != null) Destroy(trail);
            activeTrails.Clear();
        }
    }

    // UnitController.Attack()에서 데미지 적용 직후 호출 (3.4절). 대상 쪽 피격 이펙트는 HandleDamaged가
    // 별도로 처리하므로(3.7절, 콜라이더 기준 정밀 위치), 여기서는 발사 쪽(총구) 이펙트만 다룬다.
    public void PlayAttack()
    {
        EffectPlayer.SpawnAtPoints(muzzlePrefab, firePoints, transform);
    }

    // attackerPosition: 데미지를 준 유닛의 위치 (HealthManager.OnDamaged가 넘겨줌, 3.7절 참고).
    // bodyCollider.ClosestPoint(attackerPosition)이 "공격자 쪽을 향한 콜라이더 표면의 가장 가까운 점"을
    // 그대로 계산해준다 - 방향 벡터를 직접 구해서 레이캐스트로 테두리를 찾을 필요가 없다. 피격 위치는 맞을
    // 때마다 방향이 달라지므로 다른 이펙트처럼 고정 리스트로 미리 지정할 수 없어 매번 동적으로 계산한다.
    private void HandleDamaged(int amount, Vector3 attackerPosition)
    {
        Vector3 hitPoint = bodyCollider != null
            ? bodyCollider.ClosestPoint(attackerPosition)
            : transform.position; // 콜라이더가 없는 예외 상황 fallback

        Vector3 outward = hitPoint - transform.position;
        Quaternion rot = outward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(outward.normalized) : Quaternion.identity;

        EffectPlayer.Spawn(hitPrefab, hitPoint, rot);
    }

    private void HandleDeath()
    {
        EffectPlayer.SpawnAtPoints(deathPrefab, deathPoints, transform);
        // 래그돌/사망모션을 쓰려면 3.5절 옵션 B 구조가 필요 — 이 메서드만으로는 "지점마다 파티클 터뜨리기"까지만 가능.
    }
}
```

### 3.4 `UnitController.cs` 최소 수정 (2곳)

```csharp
// 1) 이동 중 여부를 외부에서 폴링할 수 있도록 게터 추가 (IsIdle/IsMove/IsAttack 옆에)
public bool IsCurrentlyMoving()
{
    if (isAirUnit)
        return isMovingAirUnit;

    return navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f;
}

// 2) Attack() 안, 데미지 적용 직후 (UnitController.cs:791 근처)
if (enemy.TryGetComponent<HealthManager>(out var targetHealth))
{
    targetHealth.GetDamage(attackDamage, transform.position); // 공격자 위치를 같이 넘김 (3.7절)
    GetComponent<UnitEffects>()?.PlayAttack(); // firePoints 리스트 기준으로 재생 (3.3절)
}
```

### 3.5 사망 이펙트 — 구조적 문제와 두 가지 해결안

현재 `HealthManager.Die()`(line 105) → `IDestructible.Die()` 구현체가 전부 **같은 프레임에**
`Destroy(gameObject)`를 호출한다. 파티클 하나 터뜨리는 정도는 `EffectPlayer.Spawn()`이 원본과 별개의
새 오브젝트를 스폰하므로 문제없지만(원본이 사라져도 이펙트는 독립적으로 재생됨), **래그돌/사망모션처럼
"죽는 오브젝트 자신이 몇 초간 남아서 애니메이션을 재생"해야 하는 형태는 이 구조로 불가능**하다.

- **옵션 A (권장 / 최소 침습, 파티클 위주 에셋일 때)**: 지금 구조를 그대로 두고,
  `UnitEffects.HandleDeath()`처럼 `OnDeath` 이벤트 시점에 별도 파티클 프리팹만 스폰한다. `Destroy(gameObject)`
  타이밍은 안 건드림. 위 3.3절 코드가 이 옵션 기준.
- **옵션 B (래그돌/사망모션까지 필요할 때)**: `Die()` 구현체들에서 `Destroy(gameObject)`를 즉시 호출하는 대신,
  1) 게임플레이 관련 컴포넌트(콜라이더, `NavMeshAgent`/`NavMeshObstacle`, `AttackRange`, `UnitController`
     자체의 Update 로직)만 비활성화하고
  2) 렌더러/애니메이터는 유지한 채 사망 애니메이션(또는 래그돌 물리)을 재생하고
  3) `RTSUnitController`의 리스트 정리(`UnitList.Remove` 등)는 **지금과 동일한 타이밍(죽는 즉시)**에 실행하되
  4) 실제 `Destroy(gameObject)`만 코루틴으로 수 초 지연한다.

  이 옵션은 `UnitController.Die()`/`BuildingController.Die()`/`EnemyController.Die()`를 각각 수정해야 해서
  손이 더 간다. **어느 쪽을 쓸지는 실제로 구할 에셋이 파티클(폭발)인지 래그돌/애니메이션 클립인지에 달려있어
  6절에서 확인이 필요하다.**

### 3.6 이동 이펙트 판정 근거
- 지상 유닛: `navMeshAgent.velocity.sqrMagnitude` (정지 명령/도착 시 자동으로 0이 됨 — 별도 이벤트 불필요)
- 공중 유닛: `isMovingAirUnit` 플래그 (현재 `private` → 3.4절처럼 게터 하나만 추가하면 상태머신 코드는
  전혀 안 건드리고 외부에서 읽을 수 있다)
- `UnitEffects.Update()`에서 매 프레임 이 값을 읽어 트레일 파티클을 켜고 끈다. 발자국 사운드처럼 "일정 간격
  반복"이 필요하면 `UnitEffects`에 자체 타이머를 두면 됨 (상태머신과 무관하게 독립적으로 관리 가능).

### 3.7 피격 이펙트 — 이벤트 분리 + 공격자 위치 기반 히트 포인트
`HealthManager.OnHealthChanged(int current, int max)`는 `GetDamage()`와 `Heal()` 양쪽에서 모두 발생시키므로
그대로 구독하면 "회복"과 "피격"을 구분할 수 없다. 데미지 전용 이벤트를 하나 추가하는 것을 권장한다.

여기에 더해, **피격 이펙트가 대상의 중심이 아니라 "공격자 쪽을 향한 콜라이더 표면"에서 나오게** 하려면
`OnDamaged`가 공격자의 위치도 같이 넘겨줘야 한다. 대상 쪽(`UnitEffects`)이 자기 콜라이더의
`ClosestPoint(공격자 위치)`를 부르면 그 표면 점이 바로 나온다 — 이 프로젝트가 이미 같은 API를
`UnitController.DistanceToTarget()`(`UnitController.cs:1203-1210`, 건물처럼 콜라이더가 큰 대상과의 거리를
표면 기준으로 잴 때)에서 쓰고 있어 관례상으로도 자연스럽다.

```csharp
// HealthManager.cs 수정
public event System.Action<int, Vector3> OnDamaged; // (damage amount, attacker world position) - GetDamage() 전용

public void GetDamage(int damage, Vector3 attackerPosition)
{
    if (isDead || damage <= 0)
        return;

    currentHp = Mathf.Max(0, currentHp - damage);
    OnHealthChanged?.Invoke(currentHp, maxHealth);
    OnDamaged?.Invoke(damage, attackerPosition); // 추가

    ...
}
```

`UnitController.Attack()`의 유일한 호출부만 `transform.position`(공격자 자신의 위치)을 같이 넘기도록
바꾸면 된다(3.4절 코드 반영 완료). `UnitEffects.HandleDamaged()`는 넘겨받은 위치로
`bodyCollider.ClosestPoint(attackerPosition)`을 호출해 히트 포인트를 계산하고, 그 지점이 몸통 중심에서
바깥쪽을 향하는 방향으로 이펙트를 회전시켜 재생한다(3.3절 코드 참고).

**전제 조건**: `Collider.ClosestPoint()`는 콜라이더가 **Box/Capsule/Sphere 같은 프리미티브이거나 Convex로
설정된 Mesh Collider**일 때만 정확히 동작한다(비-Convex 일반 Mesh Collider에 호출하면 예외가 난다).
`UserControl`이 클릭 판정에 쓰는 유닛 콜라이더(`layerUnit`/`layerEnemy`)가 실제로 프리미티브인지 각
유닛 프리팹에서 확인이 필요하다 — 대부분의 RTS 유닛은 캡슐/박스 콜라이더를 쓰므로 문제없을 가능성이 높지만,
정밀한 커스텀 Mesh Collider를 쓰는 유닛이 있다면 그 프리팹만 Convex 옵션을 켜야 한다.

건물(`BuildingEffects`)에도 같은 패턴을 적용할 수 있지만, 현재 설계는 건물 피격 이펙트를 범위에 넣지 않았다
— 필요하면 `BuildingEffects`에도 동일한 `bodyCollider`/`HandleDamaged` 쌍을 추가하면 된다(우호 사격
관련 기존 로직상 건물도 `HealthManager`를 통해 데미지를 받으므로 구조적으로 그대로 재사용 가능).

### 3.8 건물: `BuildingEffects` / `ConstructionEffects`

```csharp
// Assets/Scripts/Effects/BuildingEffects.cs (신규, BuildingController와 같이 부착)
using System.Collections.Generic;
using UnityEngine;

public class BuildingEffects : MonoBehaviour
{
    [Header("이륙 (비워두면 건물 자신의 위치에서 재생)")]
    [SerializeField] private GameObject takeoffPrefab;
    [SerializeField] private List<Transform> takeoffPoints = new(); // 추진구/랜딩기어 등 여러 지점

    [Header("착륙 (비워두면 건물 자신의 위치에서 재생)")]
    [SerializeField] private GameObject landingPrefab;
    [SerializeField] private List<Transform> landingPoints = new();

    public void PlayTakeoff() => EffectPlayer.SpawnAtPoints(takeoffPrefab, takeoffPoints, transform);
    public void PlayLanding() => EffectPlayer.SpawnAtPoints(landingPrefab, landingPoints, transform);
}
```

```csharp
// BuildingController.cs 수정 (2곳, 각 1줄)
public void LiftOff()
{
    ...
    isLifted = true;
    isAscending = true;
    verticalTarget = transform.position + Vector3.up * liftHeight;
    GetComponent<BuildingEffects>()?.PlayTakeoff(); // 추가
}

private void Land()
{
    ...
    landed?.Invoke();
    GetComponent<BuildingEffects>()?.PlayLanding(); // 추가 (위치 계산 끝난 뒤)
}
```

```csharp
// Assets/Scripts/Effects/ConstructionEffects.cs (신규, BaseStructure와 같이 부착)
using System.Collections.Generic;
using UnityEngine;

public class ConstructionEffects : MonoBehaviour
{
    [Header("건설 중 지속 (비워두면 건물 중심 1곳)")]
    [SerializeField] private GameObject constructionLoopPrefab; // 건설 중 지속(먼지/스파크)
    [SerializeField] private List<Transform> constructionPoints = new(); // 비계/모서리 등 여러 지점
    private List<GameObject> activeLoops = new();

    [Header("완공 순간")]
    [SerializeField] private GameObject completePrefab; // 완공 순간 섬광
    [SerializeField] private List<Transform> completePoints = new();

    public void StartLoop()
    {
        if (activeLoops.Count > 0)
            return; // 이미 재생 중

        activeLoops = EffectPlayer.SpawnPersistentAtPoints(constructionLoopPrefab, constructionPoints, transform);
    }

    public void StopLoopAndPlayComplete()
    {
        foreach (GameObject loop in activeLoops)
            if (loop != null) Destroy(loop);
        activeLoops.Clear();

        EffectPlayer.SpawnAtPoints(completePrefab, completePoints, transform);
    }
}
```

```csharp
// BaseStructure.cs 수정 (2곳)
public void Initialize(...)
{
    ...
    GetComponent<ConstructionEffects>()?.StartLoop(); // Initialize 끝부분에 추가
}

private void CompleteConstruction()
{
    GetComponent<ConstructionEffects>()?.StopLoopAndPlayComplete(); // Destroy(gameObject) 직전에 추가
    ...
    Destroy(gameObject);
}
```

## 4. 신규/수정 파일 목록 (제안)

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/Effects/EffectPlayer.cs` | 신규 | 이펙트 스폰(단발/다중지점/지속형) + 자동 파괴 공용 헬퍼 |
| `Assets/Scripts/Effects/UnitEffects.cs` | 신규 | 유닛 프리팹용: 공격/이동/피격/사망 이펙트 (공격·이동·사망은 `List<Transform>`으로 다중 지점 지정 가능) |
| `Assets/Scripts/Effects/BuildingEffects.cs` | 신규 | 건물 프리팹용: 이륙/착륙 이펙트 (각각 `List<Transform>`으로 다중 지점 지정 가능) |
| `Assets/Scripts/Effects/ConstructionEffects.cs` | 신규 | `BaseStructure`(건설중 파운데이션) 전용: 건설 진행/완료 이펙트 (다중 지점 지정 가능) |
| `Assets/Scripts/Unit/HealthManager.cs` | 수정 | `OnDamaged` 이벤트 추가 |
| `Assets/Scripts/Unit/UnitController.cs` | 수정 | `IsCurrentlyMoving()` 게터 + `Attack()`에서 `PlayAttack()` 호출 1줄 |
| `Assets/Scripts/Building/BuildingController.cs` | 수정 | `LiftOff()`/`Land()`에 호출 1줄씩 |
| `Assets/Scripts/Building/BaseStructure.cs` | 수정 | `Initialize()`/`CompleteConstruction()`에 호출 1줄씩 |
| 각 유닛/건물 프리팹 | 에디터 작업 | `UnitEffects`/`BuildingEffects`/`ConstructionEffects` 컴포넌트 부착 + 이펙트 프리팹 인스펙터 연결 |
| (선택, 옵션 B 채택 시) `UnitController.cs`/`BuildingController.cs`/`EnemyController.cs` | 수정 | `Die()`를 "컴포넌트 비활성화 + 지연 Destroy" 구조로 변경 |

## 5. 단계별 적용 순서 (제안)

1. `EffectPlayer` + `UnitEffects`(공격/피격/사망 파티클만, 옵션 A) — 가장 눈에 띄는 전투 피드백부터.
2. `UnitEffects` 이동 트레일 (`IsCurrentlyMoving()` 게터).
3. `BuildingEffects`(이착륙) + `ConstructionEffects`(건설).
4. 필요 시 옵션 B(래그돌/사망모션)로 전환 — 이건 `Die()` 계열 코드를 직접 손봐야 해서 별도 세션으로 분리 권장.

## 6. 열린 질문 (구해올 에셋 형태에 따라 결정)

- **사망 이펙트**: 파티클(폭발 등)만 쓸지, 래그돌/사망모션(스켈레톤 애니메이션) 에셋까지 쓸지 → 3.5절 옵션 A/B 선택에 직결.
- 애니메이션 클립 에셋(이동/공격 모션 등)을 쓸 계획인지 → 현재 `Animator` 자체가 프로젝트에 전혀 없어서, 쓴다면 `Animator` 파라미터 연동을 이 설계에 추가해야 함.
- 공격 이펙트가 즉발(히트스캔, 총구 섬광+피격 이펙트만)인지, 투사체(발사체가 날아가는 비주얼)인지 → 투사체면 별도 `Projectile` 컴포넌트(발사~명중까지 이동)가 필요하다. 현재 `Attack()`은 즉시 데미지를 적용하는 히트스캔 구조라, 투사체 비주얼을 넣어도 데미지 판정 타이밍 자체를 바꿀지(발사 시점 vs 명중 시점) 결정이 필요.
- 유닛/건물 종류마다 이펙트를 다르게 쓸지, 공용 이펙트 하나로 통일할지(공용이면 프리팹마다 필드를 다시 채울 필요 없이 하나만 만들어 재사용).
- (3.7절 관련) 유닛 콜라이더가 실제로 Box/Capsule/Sphere 같은 프리미티브인지, 아니면 Non-convex Mesh Collider를 쓰는 유닛이 섞여 있는지 — 후자라면 그 프리팹만 Convex 옵션을 켜야 `ClosestPoint()`가 정상 동작한다.
- (3.2/3.3절 관련) 이동 트레일처럼 다중 지점 지속형 이펙트를 "모든 지점에서 동시에 계속 재생"할지, 아니면 두발 보행 유닛의 발자국처럼 "지점을 번갈아가며 한 번씩" 재생할지 — 후자는 애니메이션 이벤트(발이 땅에 닿는 타이밍)에 맞춰야 자연스러워서, 위 설계(전 지점 동시 지속)보다 손이 더 간다. 실제 에셋/모션에 번갈아 재생이 필요하면 별도로 설계를 추가해야 한다.

## 7. 다음 단계

이 문서는 설계 + 제안 코드까지만이다. 실제로 `Assets/Scripts`에 반영할지, 반영한다면 어느 범위(전체/1~3단계만/
옵션 A vs B)까지 적용할지, 그리고 6절의 열린 질문에 대한 답을 알려주면 그 다음에 실제 코드 수정과
프리팹/씬 설정 안내를 진행하겠다.
