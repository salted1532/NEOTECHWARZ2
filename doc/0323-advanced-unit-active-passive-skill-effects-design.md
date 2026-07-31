# 0323 - 샤프슈터/스카이 랜서/가디언 드론 스킬 효과 구현 설계

**아직 아무 것도 구현하지 않음 - 설계만 정리.** 승인 후 별도로 구현 진행.

## 요청 내용

> 패시브 -> 버프형(수치 +/기능), 액티브 -> 단일 유닛 선택 or 범위형.
> 액티브 스킬 버튼 클릭 → A공격모드처럼 지정 모드 진입 → 단일/범위 지정 → 사거리 안에 들어가면 스킬 사용 → 이펙트/투사체 → 데미지.
>
> - **샤프슈터**: (액티브)저격 - 단일 대상 40데미지. (액티브)은신 - 15초간 피탐지 불가(포탑 포함), 반투명 흰색 표시.
> - **스카이 랜서**: (패시브)공중 강화 - 공격 명중 시 7초간 초당 2데미지 화염 도트, 재공격 시 갱신. (액티브)지상 폭격 - 범위 지정, 사거리까지 접근 후 반경 내 아군 포함 전체 20데미지.
> - **가디언 드론**: (액티브)집중 포화 - 단일 대상에 강화 폭격 3회(투사체), 100×3=300데미지. (액티브)쉴드 전개 - 20초간 최대체력 +150, 그 전에 150만큼 깎이면 즉시 원복.
> - 패시브 버프 공통 메소드(추가 데미지/도트/힐) + 다른 버프형 패시브(공격력/방어력/공격속도 +) 설계.

## 기존 코드 확인 결과

- 고급유닛 특성(2택1) 시스템은 `doc/0228`에서 이미 1차 구현 완료 - `UnitDataSO.UnitTraitOption`(스킬명/설명/아이콘/액티브 여부/단축키/쿨다운), `RTSUnitController.TraitChoice`/`chosenTraits`/`ActivateSkill`, `UnitController.currentTrait`/`skillCooldownRemaining`/`UseTraitSkill()`, order panel 슬롯 6 고정까지 전부 연결되어 있음. **다만 실제 스킬 효과(`IUnitSkill` 구현체)는 아직 0개** - `UseTraitSkill()`이 `GetComponent<IUnitSkill>()`을 찾아 위임하는 연결점만 있고 호출할 대상이 없는 상태.
- 기존 `IUnitSkill.Activate(UnitController unit, TraitChoice trait)`는 "자기 자신에게 즉시 적용되는 논타겟 스킬"만 상정한 시그니처라, 단일 대상 지정형(저격/집중포화)·범위 지정형(지상폭격) 스킬을 표현할 수 없음 → 확장 필요(아래 1번).
- "A공격모드처럼 지정 모드 진입"에 해당하는 기존 뼈대: `RTSUnitController.EnterAttackMode()` → `UserControl.SetOrderState("Attack")` → `UserControl.HandleLeftClick()`이 `OrderState.Attack`일 때 클릭 대상에 따라 분기(적 유닛/적 건물/아군/땅). 스킬 지정 모드도 이 패턴을 그대로 복제하면 됨.
- "사거리 안에 들어가면 자동 사용"에 해당하는 기존 뼈대: `UnitController.AttackOrderTick()`(지정 추격) / `FriendlyAttackTick()`(아군 강제공격) - 목적지로 이동하다 사거리 안이면 정지 후 실행, 그 패턴을 스킬용으로 복제.
- 투사체(가디언 드론 집중포화)는 기존 `ProjectileAttack`/`Projectile`을 그대로 재사용 가능(발사 시점 데미지 계산을 스킬 쪽에서 넘겨주기만 하면 됨, doc/0290/0319 구조 그대로).
- 은신 반투명 표시는 기존 `PreviewSystem.ApplyGhostMaterial`(건물 배치 프리뷰가 쓰는 반투명 흰색 고스트 머티리얼)과 동일한 기법 - 살아있는 유닛의 실제 렌더러 머티리얼을 일시적으로 교체했다가 복원하면 됨.
- 쉴드 전개(임시 최대체력)는 `HealthManager.SetMaxHealth`/`Heal`/`OnDamaged` 이벤트가 이미 있어서 새 메서드 없이 조합만으로 구현 가능.
- 은신 판정 회피는 `EnemyAttackRange.CanEngage(GameObject target)` 한 곳(적 유닛의 감지/포탑 조준이 전부 여길 거쳐감, `GetTrackingTarget()`도 동일 필터를 씀)만 고치면 차량형 포탑 포함 전부 커버됨 - 별도로 여러 곳을 고칠 필요 없음.

## 1) 공통 인프라 - 스킬 지정 모드 (단일 유닛 / 범위)

### `IUnitSkill` 인터페이스 확장
```csharp
// 스킬 발동 시 함께 넘어가는 대상 정보. targetType(None/SingleUnit/AreaGround)에 따라 unitTarget 또는
// groundPoint 중 하나만 의미 있고, 자기 자신에게 쓰는 논타겟 스킬(None)은 둘 다 비워둔 채로 호출된다.
public readonly struct SkillActivationContext
{
    public readonly GameObject unitTarget;
    public readonly Vector3 groundPoint;
    public static readonly SkillActivationContext Self = new SkillActivationContext(null, default);
    public SkillActivationContext(GameObject unitTarget, Vector3 groundPoint)
    { this.unitTarget = unitTarget; this.groundPoint = groundPoint; }
}

public interface IUnitSkill
{
    void Activate(UnitController unit, RTSUnitController.TraitChoice trait, SkillActivationContext context);
}
```
아직 구현체가 0개라 시그니처를 바꿔도 영향받는 기존 코드가 없음(안전한 변경).

### `UnitTraitOption`(`UnitDataSO.cs`)에 지정 방식 필드 추가
```csharp
public enum SkillTargetType { None, SingleUnit, AreaGround } // None=자기자신 즉시 발동, SingleUnit=적 클릭, AreaGround=땅 클릭(범위)

[field: SerializeField] public SkillTargetType targetType { get; private set; }
[field: SerializeField] public float skillRange { get; private set; }   // SingleUnit/AreaGround - 이 사거리 안이어야 실제 발동
[field: SerializeField] public float areaRadius { get; private set; }   // AreaGround 전용 - 피해 범위 반지름
```

### 지정 모드 진입 - 기존 `RTSUnitController.ActivateSkill(int unitID, UnitTraitOption trait)`를 그대로 확장
**새 메소드/새 버튼을 추가하지 않고, doc/0228에서 이미 슬롯 6 버튼(`UpdateUnitSkillUI()`의 `ButtonAction.Simple(() => ActivateSkill(data.ID, trait), ...)`)에 연결되어 있는 기존 `ActivateSkill`의 내부 로직만 `targetType`에 따라 분기한다** - "현재 스킬 선택(2택1 특성)과 바로 연결"이라는 확정 사항에 맞춰 `UIController`/버튼 배선은 전혀 건드리지 않는다.
```csharp
// RTSUnitController - 기존 즉시발동 로직을 targetType 분기로 확장
private int pendingSkillUnitID;          // SkillUnit/SkillGround 지정 대기 중인 스킬의 대상 unitID
private UnitTraitOption pendingSkillTrait;

public void ActivateSkill(int unitID, UnitTraitOption trait)
{
    if (trait.targetType != SkillTargetType.None)
    {
        // 단일/범위 지정형: 쿨다운을 아직 시작하지 않는다 - "사거리까지 이동해서 실제로 스킬을 쓴 시점"부터
        // 시작해야 하므로(확정 사항), 여기서는 지정 모드 진입만 하고 클릭 확정은 Confirm 단계(아래)로 넘긴다.
        pendingSkillUnitID = unitID;
        pendingSkillTrait = trait;
        userControl.SetOrderState(trait.targetType == SkillTargetType.SingleUnit ? "SkillUnit" : "SkillGround");
        return;
    }

    // 기존 로직 그대로: 자기자신 논타겟 스킬(은신/쉴드 전개)은 이동할 필요가 없어 즉시 발동 + 즉시 쿨다운 시작
    foreach (UnitController unit in selectedUnitList)
    {
        if (unit == null || unit.GetUnitID() != unitID || !unit.CanUseSkill())
            continue;

        unit.UseTraitSkill(SkillActivationContext.Self);
        unit.StartSkillCooldown(trait.cooldown);
    }
}

// UserControl이 SkillUnit 모드에서 적 클릭을 받으면 호출 (아래 "클릭 처리" 참고)
public void ConfirmSkillUnitTarget(GameObject target)
{
    foreach (UnitController unit in selectedUnitList)
    {
        if (unit == null || unit.GetUnitID() != pendingSkillUnitID || !unit.CanUseSkill())
            continue;

        unit.MoveToUseSkillOnUnit(target, pendingSkillTrait); // 사거리 도착 판정+실제 발동은 UnitController.SkillOrderTick이 담당
    }
}

// UserControl이 SkillGround 모드에서 땅 클릭을 받으면 호출
public void ConfirmSkillAreaTarget(Vector3 point)
{
    foreach (UnitController unit in selectedUnitList)
    {
        if (unit == null || unit.GetUnitID() != pendingSkillUnitID || !unit.CanUseSkill())
            continue;

        unit.MoveToUseSkillOnArea(point, pendingSkillTrait);
    }
}

public float GetPendingSkillAreaRadius() => pendingSkillTrait?.areaRadius ?? 0f; // 범위 마커(사용자 제작 예정)용 반지름 조회
```
"여러 마리를 함께 선택했어도 한 마리씩 자기 쿨다운으로 독립 발동"이라는 doc/0228의 기존 규칙을 그대로 유지한다 - 지정 순간엔 `CanUseSkill()`만 체크하고, 그 뒤 각 유닛은 `UnitController.SkillOrderTick()`이 각자 알아서 이동/도착 판정/실제 발동+쿨다운 시작을 처리한다(유닛마다 도착 시점이 달라도 문제없음).

### 클릭 처리 - `UserControl.cs`
`OrderState`에 `SkillUnit`, `SkillGround` 추가. `HandleLeftClick()`에 `OrderState.Attack` 분기와 나란히 추가:
```csharp
if (UsercurrentState == OrderState.SkillUnit && clickedEnemy) {
    rtsUnitController.ConfirmSkillUnitTarget(enemyHit.transform.gameObject);
    UsercurrentState = OrderState.None;
    return;
}
if (UsercurrentState == OrderState.SkillGround && clickedGround) {
    rtsUnitController.ConfirmSkillAreaTarget(groundHit.point);
    UsercurrentState = OrderState.None;
    return;
}
```
`UpdatePointer()`/`UpdateCursor()`의 `commandPending` 목록에도 두 상태 추가(기존 attackPointer 재사용 가능). 범위 지정용 원형 마커는 **사용자가 직접 만들 예정**이므로, 코드 쪽은 `attackPointer`와 동일하게 위치만 따라다니게 하고 크기(반지름) 정보만 `RTSUnitController.GetPendingSkillAreaRadius()`로 노출해서 마커 스크립트가 읽어가게만 해둔다.

### 사거리 접근 후 자동 발동 - `UnitController.cs`
`AttackOrderTick()`과 같은 자리에 `SkillOrderTick()` 추가(둘 다 매 프레임 `Update()`에서 호출). **쿨다운은 여기, 즉 "실제로 사거리 안에 도착해서 스킬을 쓴 시점"에만 시작한다** (확정 사항) - `RTSUnitController.ActivateSkill`/`Confirm...Target` 단계에서는 쿨다운을 전혀 건드리지 않는다.
```csharp
private bool hasPendingSkillUnitOrder;
private GameObject pendingSkillUnitTarget;
private bool hasPendingSkillAreaOrder;
private Vector3 pendingSkillGroundTarget;
private UnitTraitOption pendingSkillTraitData;

public void MoveToUseSkillOnUnit(GameObject target, UnitTraitOption trait)
{
    CancelAttackOrder(); // 이전 이동/공격/스킬 지시를 먼저 정리(아래 "중간 취소" 참고)
    hasPendingSkillUnitOrder = true;
    pendingSkillUnitTarget = target;
    pendingSkillTraitData = trait;
    MoveAgentTo(target.transform.position);
}

public void MoveToUseSkillOnArea(Vector3 point, UnitTraitOption trait)
{
    CancelAttackOrder();
    hasPendingSkillAreaOrder = true;
    pendingSkillGroundTarget = point;
    pendingSkillTraitData = trait;
    MoveAgentTo(point);
}

private void SkillOrderTick()
{
    if (hasPendingSkillUnitOrder)
    {
        if (pendingSkillUnitTarget == null) { hasPendingSkillUnitOrder = false; return; } // 대상이 이동 중 파괴됨

        float dist = Vector3.Distance(transform.position, pendingSkillUnitTarget.transform.position);
        if (dist > pendingSkillTraitData.skillRange)
        {
            MoveAgentTo(pendingSkillUnitTarget.transform.position); // 계속 추격 이동(대상이 움직이는 유닛일 수 있음)
            return;
        }

        StopUnit();
        UseTraitSkill(new SkillActivationContext(pendingSkillUnitTarget, pendingSkillUnitTarget.transform.position));
        StartSkillCooldown(pendingSkillTraitData.cooldown); // "실제 발동 시점"에 쿨다운 시작
        hasPendingSkillUnitOrder = false;
        return;
    }

    if (hasPendingSkillAreaOrder)
    {
        float dist = Vector3.Distance(transform.position, pendingSkillGroundTarget);
        if (dist > pendingSkillTraitData.skillRange)
            return; // MoveAgentTo로 이미 그 지점으로 이동 중이므로 도착할 때까지 대기만 하면 됨

        StopUnit();
        UseTraitSkill(new SkillActivationContext(null, pendingSkillGroundTarget));
        StartSkillCooldown(pendingSkillTraitData.cooldown);
        hasPendingSkillAreaOrder = false;
    }
}
```

### 중간 취소 - 이동/다른 명령이 들어오면 자동으로 취소
확정 사항: "이동이나 다른 명령으로 스킬을 취소할 수 있게". 기존 `CancelAttackOrder()`가 `MoveTo`/`AttackUnitTarget`/`AttackMoveTo`/`AttackFriendlyTarget`/`FollowUnit`/`GoBuild`/`StopUnit`/`PatrolUnit`/`HoldUnit` **전부에서 이미 호출되고 있는 공용 취소 지점**이므로, 새 명령마다 따로 취소 코드를 추가할 필요 없이 `CancelAttackOrder()` 안에 아래 두 줄만 추가하면 위 모든 명령이 자동으로 스킬 이동/발동 대기를 함께 취소한다(root-cause 방식 - 호출부를 늘리지 않고 공용 지점 하나만 수정).
```csharp
// CancelAttackOrder() 안, 기존 orderedTarget/friendlyTarget 초기화와 같은 자리에 추가
hasPendingSkillUnitOrder = false;
hasPendingSkillAreaOrder = false;
```
`MoveToUseSkillOnUnit`/`MoveToUseSkillOnArea`도 시작할 때 `CancelAttackOrder()`를 먼저 호출하므로, 스킬 지정 도중에 다른 스킬을 다시 지정해도(또는 같은 스킬을 다른 대상으로 재지정해도) 자연스럽게 이전 지정이 취소되고 새 지정으로 교체된다.

## 2) 공통 인프라 - 패시브 버프 메소드

요청하신 "패시브 버프 공통 메소드"는 성격이 서로 달라 하나의 메소드가 아니라 **역할별로 3개**가 맞다(각각 재사용 대상이 다름):

| 메소드 | 위치 | 용도 |
|---|---|---|
| `DamageOverTimeEffect`(신규 컴포넌트) | 피격 대상에 동적으로 부착 | 도트 데미지(화염/독 등) - 스카이 랜서 "공중 강화" |
| `HealthManager.Heal/SetMaxHealth` (기존 재사용) | 버프를 받는 유닛 | 힐, 임시 최대체력(쉴드) - 가디언 드론 "쉴드 전개" |
| `UnitController.AddAttackDamageBonus/AddArmorBonus/MultiplyAttackInterval`(신규) | 버프를 받는 유닛 | 영구 스탯 가산형 패시브 - 공격력/방어력/공격속도 |

### 도트 데미지 - `Assets/Scripts/Unit/DamageOverTimeEffect.cs` (신규, 재사용 가능한 범용 컴포넌트)
```csharp
// 대상에게 붙여서 일정 시간 동안 주기적으로 데미지를 주는 범용 도트 컴포넌트.
// 이미 붙어있는 상태에서 다시 요청하면(재공격) 지속시간만 갱신한다("재활성화" 요구사항).
public class DamageOverTimeEffect : MonoBehaviour
{
    private HealthManager targetHealth;
    private int damagePerTick;
    private float tickInterval;
    private float remainingDuration;
    private AttackEffectType attackType;
    private Coroutine routine;

    public static void ApplyOrRefresh(GameObject target, int damagePerTick, float tickInterval, float duration, AttackEffectType attackType)
    {
        var effect = target.GetComponent<DamageOverTimeEffect>() ?? target.AddComponent<DamageOverTimeEffect>();
        effect.Setup(damagePerTick, tickInterval, duration, attackType);
    }

    private void Setup(int dmg, float interval, float duration, AttackEffectType type)
    {
        targetHealth = GetComponent<HealthManager>();
        damagePerTick = dmg; tickInterval = interval; attackType = type;
        remainingDuration = duration; // 이미 돌고 있었다면 지속시간만 새로 덮어씀(갱신)
        if (routine == null) routine = StartCoroutine(TickRoutine());
    }

    private IEnumerator TickRoutine()
    {
        while (remainingDuration > 0f)
        {
            yield return new WaitForSeconds(tickInterval);
            remainingDuration -= tickInterval;
            targetHealth?.GetDamage(damagePerTick, transform.position, attackType, isEnemyAttacker: false);
        }
        Destroy(this);
    }
}
```
- 스카이 랜서 "공중 강화"는 패시브라 order panel 버튼이 없음 - `ApplyTrait()`가 아니라, 공격이 실제로 명중하는 순간(`UnitController.Attack()`이 `targetHealth.GetDamage(...)`를 호출하는 지점 또는 `LaserBeamAttack`/`ProjectileAttack` 명중 지점)에서 훅이 필요함. 가장 깔끔한 지점: `UnitController.Attack()`에서 데미지를 적용한 직후, `currentTrait`이 해당 패시브(공중 강화)이고 대상이 공중 유닛일 때만 `DamageOverTimeEffect.ApplyOrRefresh(enemy, 2, 1f, 7f, AttackEffectType.Flame)` 호출. 이 판정은 유닛 종류마다 다르므로 `IUnitSkill`에 4번째 메소드 없이, **`UseTraitSkill`과 별개로 `IUnitSkill`에 옵션 훅을 추가**하지 않고 대신 `SkyLancerSkill`(아래 3번) 컴포넌트가 `HealthManager`가 아니라 **자기 자신의 `UnitController`가 발행하는 이벤트**를 구독하는 방식을 제안한다 → `UnitController`에 `public event Action<GameObject> OnAttackHit;`를 추가하고 `Attack()`이 데미지를 넣는 지점에서 발행. 이러면 패시브 스킬 구현체가 `UnitController`/`RTSUnitController`를 건드리지 않고 이벤트만 구독해서 자기 효과를 붙일 수 있음(기존 `IUnitSkill` 연결점 철학과 동일 - "유닛별 스킬은 컴포넌트 하나 붙이는 걸로 끝").

### 임시 최대체력(쉴드) - 기존 `HealthManager` API만으로 조합
```csharp
// GuardianDroneSkill 내부 코루틴 (신규 컴포넌트 없이 스킬 구현체 안에 코루틴으로 처리)
private IEnumerator ShieldRoutine(HealthManager hp, int bonus, float duration)
{
    int damageTaken = 0;
    void OnDamaged(int dmg, Vector3 pos, AttackEffectType t, bool isEnemy) => damageTaken += dmg;

    hp.SetMaxHealth(hp.GetMaxHealth() + bonus);
    hp.Heal(bonus); // 최대치만 올리면 회복이 안 되므로 버프량만큼 즉시 채워줌
    hp.OnDamaged += OnDamaged;

    float elapsed = 0f;
    while (elapsed < duration && damageTaken < bonus)
    {
        yield return null;
        elapsed += Time.deltaTime;
    }

    hp.OnDamaged -= OnDamaged;
    hp.SetMaxHealth(hp.GetMaxHealth() - bonus); // SetMaxHealth가 현재체력도 자동으로 clamp해줌
}
```
`HealthManager`를 전혀 수정하지 않고 기존 공개 API(`SetMaxHealth`/`Heal`/`OnDamaged`)만으로 구현 가능 - 요청하신 "체력 버프 메소드"에 해당하는 범용 처리가 이미 `HealthManager`에 다 있다는 뜻이라 새 메소드가 필요 없음.

### 영구 스탯 가산 패시브 - `UnitController.cs`에 3개 메소드 추가
```csharp
public void AddAttackDamageBonus(int amount) => attackDamage += amount;
public void AddArmorBonus(int amount) => armor += amount;
public void MultiplyAttackInterval(float multiplier) => timeBetweenAttacks *= multiplier; // 1보다 작으면 공격속도 증가
```
`ApplyTrait(TraitChoice choice)` 안에서 유닛 타입별로 호출(doc/0228에 이미 있던 자리, "장점 극대화/단점 보완" 스탯 보정을 여기서 처리하기로 되어 있었음). 이번 6개 스킬 중에는 해당하는 게 없어서 실제 호출 코드는 안 넣고, 메소드 3개만 미리 만들어 둔다(요청하신 "다른 버프형 패시브" 대비 확장 포인트).
**한시적 버프(예: N초간 공격력 +X%)가 필요해지면** 위 3개 메소드를 그대로 쓰되 호출부에서 `StartCoroutine`으로 지속시간 후 반대 연산(음수/역수)을 한 번 더 호출하는 방식으로 충분 - 별도의 타이머·스택 관리 프레임워크는 지금 필요한 스킬에 없으므로 만들지 않음(YAGNI, 실제로 필요한 스킬이 나오면 그때 추가).

## 3) 은신 - 감지 회피 + 반투명 시각효과

### 감지 회피 - `UnitController.cs` + `EnemyAttackRange.cs`
```csharp
// UnitController
private bool isStealthed;
public bool IsStealthed() => isStealthed;
public void SetStealthed(bool value) => isStealthed = value;
```
```csharp
// EnemyAttackRange.CanEngage() 안, 기존 도메인(지상/공중) 체크와 같은 자리에 한 줄 추가
if (target.TryGetComponent<UnitController>(out var playerUnit) && playerUnit.IsStealthed())
    return false;
```
`GetClosestTarget()`/`GetTrackingTarget()`(포탑 조준용)이 전부 `CanEngage()`를 거치므로 이 한 곳만 고치면 일반 유닛/차량형 포탑 전부 동일하게 은신 유닛을 무시하게 됨 - "포탑도 인식 불가" 요구사항 자동 충족.

### 반투명 흰색 시각효과 - `Assets/Scripts/Unit/StealthVisual.cs` (신규, 범용 컴포넌트)
```csharp
// 살아있는 유닛의 렌더러 머티리얼을 일시적으로 반투명 흰색(PreviewSystem의 고스트 머티리얼과 동일 톤)으로
// 바꿨다가 복원한다. 프리뷰용 임시 오브젝트가 아니라 "그 유닛 자신"의 원본 머티리얼을 보존/복원해야 하므로
// PreviewSystem과는 별도 컴포넌트로 둔다(PreviewSystem은 배치 중인 고스트 인스턴스 전용).
public class StealthVisual : MonoBehaviour
{
    [SerializeField] private Material stealthMaterial; // 반투명 흰색 (PreviewSystem의 previewMaterialPrefab과 같은 에셋 재사용 가능)
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();

    public void EnterStealth()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            originalMaterials[r] = r.materials;
            Material[] ghosts = new Material[r.materials.Length];
            for (int i = 0; i < ghosts.Length; i++) ghosts[i] = stealthMaterial;
            r.materials = ghosts;
        }
    }

    public void ExitStealth()
    {
        foreach (var kv in originalMaterials) if (kv.Key != null) kv.Key.materials = kv.Value;
        originalMaterials.Clear();
    }
}
```

## 4) 유닛별 스킬 구현 (`IUnitSkill` 구현체, 유닛 프리팹에 붙임)

### 샤프슈터 - `Assets/Scripts/Unit/Skills/SharpshooterSkill.cs`
- **traitA(저격)**: `hasTraitChoice`/`isActiveSkill=true`, `targetType=SingleUnit`, `skillRange=`저격 전용 사거리(확정: 유닛 기본 공격 사거리 `AttackRange.UnitRange`와 별개로 `UnitTraitOption.skillRange`에 독립적인 값을 넣음 - 저격총답게 기본 사거리보다 길게 설정 가능), `cooldown=`기획값.
  `Activate(unit, trait, context)` → `context.unitTarget`의 `HealthManager.GetDamage(40, unit.transform.position, AttackEffectType.Bullet, isEnemyAttacker:false)` 즉시 적용 + 저격 SFX(`UnitAudio`에 전용 SFX 슬롯 필요 - 기존 `UnitSoundBankSO` 패턴 재사용, 필드 하나 추가) 재생.
- **traitB(은신)**: `targetType=None`(자기 자신), `cooldown=`기획값(예: 재사용 대기 20~30초).
  `Activate` → `unit.SetStealthed(true)`, `unit.GetComponent<StealthVisual>()?.EnterStealth()`, 15초 뒤 코루틴으로 `SetStealthed(false)` + `ExitStealth()`.
  **확정**: 은신 중에도 평소처럼 공격 가능하고, 공격해도 스텔스는 풀리지 않는다(15초 지속시간이 끝날 때만 해제) - `AttackRange`(플레이언 사거리 감지)는 그대로 두고, `EnemyAttackRange.CanEngage()`(적이 이 유닛을 인식하는 쪽)만 막는 위 설계 그대로 적용하면 이 동작이 자동으로 나온다(은신 유닛 스스로의 공격 로직은 전혀 건드리지 않으므로).

### 스카이 랜서 - `Assets/Scripts/Unit/Skills/SkyLancerSkill.cs`
- **traitA(공중 강화, 패시브)**: `isActiveSkill=false`, `targetType=None`. `ApplyTrait`으로는 아무 즉시효과가 없고, 대신 `Awake()`/`ApplyTrait()`에서 `unit.OnAttackHit += HandleAttackHit` 구독. 대상이 공중 유닛일 때만 `DamageOverTimeEffect.ApplyOrRefresh(target, 2, 1f, 7f, AttackEffectType.Flame)`.
- **traitB(지상 폭격, 액티브)**: `targetType=AreaGround`, `areaRadius=`기획값(예: 3~4), `skillRange=`이 유닛의 접근 사거리.
  `Activate(unit, trait, context)`:
  1. 폭격 이펙트/사운드 재생(`context.groundPoint`에 스폰).
  2. `Physics.OverlapSphere(context.groundPoint, trait.areaRadius)`로 범위 내 콜라이더 전부 조회.
  3. 각 콜라이더에서 `HealthManager`를 찾아 `GetDamage(20, unit.transform.position, AttackEffectType.Explosive, isEnemyAttacker:false)` 적용 - **아군/적 구분 없이 전부** 적용(요구사항 그대로, 태그 필터 없음). `unit` 자기 자신도 예외 처리하지 않는다 - `OverlapSphere`가 시전자의 콜라이더도 반경 안에 있으면 그대로 잡아오므로, "시전자 본인이 범위 안에 있으면 자신도 맞는다"(확정 사항)가 별도 분기 없이 자연스럽게 성립한다.
  중복 방지를 위해 `HashSet<HealthManager>`로 한 프레임 내 동일 대상 중복 데미지 방지(겹치는 콜라이더가 여러 개인 유닛 대비).

### 가디언 드론 - `Assets/Scripts/Unit/Skills/GuardianDroneSkill.cs`
- **traitA(집중 포화, 액티브)**: `targetType=SingleUnit`, `skillRange=`이 유닛 사거리, `cooldown=`기획값.
  `Activate` → 코루틴으로 `ProjectileAttack.Fire(target.transform, targetHealth, 100, AttackEffectType.Explosive, isEnemyAttacker:false)`를 0.2~0.3초 간격으로 3회 호출("3번의 강화 폭격"). `ProjectileAttack`/`Projectile`은 기존 코드 그대로 재사용(수정 없음) - 유닛 프리팹에 `ProjectileAttack` 컴포넌트와 강화폭격 전용 투사체 프리팹만 연결하면 됨.
- **traitB(쉴드 전개, 액티브)**: `targetType=None`, `cooldown=`기획값(예: 쉴드 지속시간보다 길게).
  `Activate` → 위 2번 섹션의 `ShieldRoutine(unit.GetComponent<HealthManager>(), 150, 20f)` 코루틴 시작.

## 5) 예상 신규/변경 파일

**신규**
- `Assets/Scripts/Unit/DamageOverTimeEffect.cs`
- `Assets/Scripts/Unit/StealthVisual.cs`
- `Assets/Scripts/Unit/Skills/SharpshooterSkill.cs`
- `Assets/Scripts/Unit/Skills/SkyLancerSkill.cs`
- `Assets/Scripts/Unit/Skills/GuardianDroneSkill.cs`

**변경**
- `Assets/Scripts/Unit/UnitController.cs` - `IUnitSkill.Activate` 시그니처 확장(`SkillActivationContext`), `SkillOrderTick`/`MoveToUseSkillOnUnit`/`MoveToUseSkillOnArea`, `IsStealthed`/`SetStealthed`, `OnAttackHit` 이벤트, `AddAttackDamageBonus`/`AddArmorBonus`/`MultiplyAttackInterval`
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` - `CanEngage()`에 은신 체크 한 줄
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` - `UnitTraitOption`에 `targetType`/`skillRange`/`areaRadius`
- `Assets/Scripts/System/RTSUnitController.cs` - `ActivateSkill` 분기(즉시/단일지정/범위지정), `ConfirmSkillUnitTarget`/`ConfirmSkillAreaTarget`, `GetPendingSkillAreaRadius`
- `Assets/Scripts/UserControl/UserControl.cs` - `OrderState.SkillUnit`/`SkillGround` 추가 + 클릭/포인터/커서 분기
- (에디터 작업) 각 유닛 프리팹에 위 3개 스킬 스크립트 부착 + `UnitDataSO`에 저격/은신/공중강화/지상폭격/집중포화/쉴드전개 6개 `UnitTraitOption` 데이터 입력(이름/설명/아이콘/사거리/범위/쿨다운/단축키) - 수치는 이 문서의 예시값이며 실제 밸런스는 인스펙터에서 자유롭게 조정 가능(코드에 하드코딩 안 함).

## 6) 확정 사항 (사용자 확인 완료, 2026-07-31)

1. **은신 중 공격 가능 여부**: 은신 중에도 평소처럼 공격 가능. 다만 **적은 은신 유닛을 인식/공격 불가**(포탑 포함) - `EnemyAttackRange.CanEngage()` 한 곳만 수정하는 위 설계 그대로.
2. **지상 폭격 자기 자신 피격**: 범위 안에 시전자 본인이 들어있으면 본인도 20데미지를 받는다 - `OverlapSphere` 결과에서 시전자를 예외 처리하지 않음(위 4번 섹션에 반영).
3. **저격/집중포화 등 지정형 스킬의 사거리**: 유닛 기본 공격 사거리(`AttackRange.UnitRange`)와 분리된 **스킬 전용 사거리**(`UnitTraitOption.skillRange`)를 쓴다 - 위 1)/4) 섹션 설계 그대로 확정.
4. **쿨다운 시작 시점**: 지정(클릭) 시점이 아니라 **"유닛이 사거리까지 이동해서 실제로 스킬을 사용한 시점"**부터 쿨다운을 시작한다 - `RTSUnitController.ActivateSkill`/`Confirm...Target`은 쿨다운을 건드리지 않고, `UnitController.SkillOrderTick()`이 실제 발동 순간에 `StartSkillCooldown()`을 호출하도록 확정(위 1) 섹션 코드 반영 완료).
5. **중간 취소**: 스킬 지정 후 이동 중에 다른 이동/공격/스킬 명령이 들어오면 즉시 취소된다 - 기존 `CancelAttackOrder()`(모든 이동/공격/정지/순찰/홀드/건설이동 명령이 이미 거쳐가는 공용 취소 지점)에 스킬 대기 플래그 초기화 두 줄만 추가해서 별도 호출부 없이 자동 커버(위 "중간 취소" 섹션 반영 완료).
6. **UI 연결**: 새 버튼/패널을 만들지 않고 doc/0228의 기존 2택1 특성 선택(슬롯 6, `ActivateSkill(int unitID, UnitTraitOption trait)`)에 그대로 연결한다 - `UpdateUnitSkillUI()`의 기존 호출부(`ButtonAction.Simple(() => ActivateSkill(data.ID, trait), ...)`)는 수정하지 않고 `ActivateSkill` 내부 로직만 확장(위 1) 섹션 반영 완료).
