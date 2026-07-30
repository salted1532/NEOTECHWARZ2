# 0305. 차량 포탑 방황/엔진 떨림 + 보병 경계 회전 애니메이션 (제안)

날짜: 2026-07-30

## 요청 내용

> 두가지 dotween을 사용한 애니메이션을 만들어줘야하는데 어떤거냐면 1개는 차량형 유닛들에게 적용시킬건데
> idle 상태의 차량이 일정시간 한 10초 정도 가만히 있다가 포탑 부분이 현재 각도에 +-45정도 랜덤하게 포탑이
> 회전했다가 1~2초 있다가 다시 원상복귀 하는거야 그리고 차량형 유닛들이 가만히 있을떄 엔진 떨림처럼
> 미세하게 덜덜거리게 해줘
> 나머지 하나는 보병 유닛에게 적용시킬건데 10초 정도 가만히 있다가 랜덤한 360도 방향중 랜덤하게 유닛
> 자체가 회전해서 다른곳을 쳐다보는거야 뭔가 주변을 경계하고 있다 이런 느낌이 들도록 2개 스크립트
> 작성해서 컴포넌트로 집어넣으면 바로 작동하도록 해줘

## 조사 내용

- DOTween은 이미 `Assets/Plugins/Demigiant/DOTween`으로 설치되어 있고, `using DG.Tweening;` +
  `private Tween` 필드 + `OnDestroy()`에서 `Kill()`하는 패턴이 프로젝트 전역에서 일관되게 쓰인다.
- 참고한 기존 컴포넌트:
  - `Assets/Scripts/Unit/TurretController.cs` — 포탑 자식 오브젝트에 부착. `Update()`에서 매 프레임
    `attackRange.GetTrackingTarget()`이 있으면 그쪽을, 없으면 `parent.rotation * restLocalRotation`
    (몸체 기준 정면)을 `RotateTowards`로 계속 따라간다.
  - `Assets/Scripts/Animation/VehicleShake.cs` — 차량 메쉬 자식에 부착, "이동 중"에만
    `DOShakePosition`을 체이닝(`OnComplete`에서 재발사)해서 떨림을 준다. **지금 요청은 반대로 "가만히
    있을 때"** 떨리는 것이므로 트리거 조건만 반대로 둔 같은 패턴을 새 스크립트에 재사용.
  - `Assets/Scripts/Animation/HoverBob.cs`, `AutoRotate.cs` — 폴링 후 Start/Stop 토글, 단순 루프
    트윈 패턴 참고.
- 유닛 타입 구분: `UnitController`/`EnemyUnitController`는 서브클래싱이나 enum 없이 하나의 클래스이고,
  "차량(포탑 있음)" 여부는 순전히 자식에 `TurretController`가 붙어 있는지로 판별한다. "보병"을 가리키는
  별도 마커는 없음 — 즉 이번 요청도 새 스크립트를 해당 유닛 프리팹에 수동으로 부착하는 방식으로 간다
  (기존 컴포넌트들과 동일).
- Idle 판정도 별도 상태머신 이벤트가 없고, 다른 코스메틱 스크립트들처럼 매 프레임
  `!IsCurrentlyMoving() && !IsAttack()`을 폴링하는 방식이 기존 컨벤션(`UnitAnimatorDriver`,
  `VehicleShake`, `HoverBob`이 전부 이 패턴).
- 확인한 공개 API (아군/적 동일한 이름으로 존재):
  `UnitController.IsCurrentlyMoving()/IsAttack()/GetAttackRange()`,
  `EnemyUnitController.IsCurrentlyMoving()/IsAttack()/GetAttackRange()`,
  `AttackRange.GetTrackingTarget()`, `EnemyAttackRange.GetTrackingTarget()`.

## 설계 시 고민한 문제 — 포탑 방황과 TurretController의 충돌

`TurretController.Update()`는 조준 대상이 없어도 매 프레임 `transform.rotation`을 계속 갱신한다
(정면 복귀 로직). 새 스크립트가 같은 포탑 트랜스폼을 DOTween으로 동시에 돌리면 두 스크립트가 매 프레임
회전값을 다투게 되어 떨리거나 씹히는 현상이 생긴다.

해결: 포탑을 방황시키는 동안만 `turretController.enabled = false`로 잠깐 꺼서 제어권을 넘겨받고,
1~2초 대기 후 그냥 `enabled = true`로 되돌린다. 다시 켜지는 순간 `TurretController.Update()`가
"대상 없음 → 정면으로 RotateTowards"를 그대로 수행하므로, 방황에서 돌아오는 복귀 트윈을 따로 만들
필요가 없다 (기존 로직 재사용). 단, 방황/대기 중에 실제 조준 대상이 잡히면(`GetTrackingTarget() != null`)
포탑이 눈멀지 않도록 즉시 중단하고 `TurretController`를 되돌려준다.

## 제안 코드 (신규 파일 2개)

### `Assets/Scripts/Animation/VehicleIdleAnimation.cs` (신규)

차량 메쉬 자식 오브젝트(`VehicleShake`와 같은 오브젝트)에 부착.

```csharp
using System.Collections;
using UnityEngine;
using DG.Tweening;

// 지상 차량 유닛의 비주얼(메쉬) 자식 오브젝트에 부착한다(VehicleShake와 같은 오브젝트) - 루트는
// UnitController/NavMeshAgent가 이동 중 매 프레임 좌표를 갱신하므로 피한다.
// 가만히 있을 때(IsCurrentlyMoving()==false && IsAttack()==false)만 동작한다:
//  1) 엔진 떨림: 아주 미세한 DOShakePosition을 계속 이어붙여 "덜덜거리는" 느낌을 낸다
//     (VehicleShake와 동일한 체이닝 패턴, 진폭만 훨씬 작음).
//  2) 포탑 방황: idleWaitTime(기본 10초)마다 포탑을 현재 각도 기준 ±turretWanderAngle 만큼
//     랜덤 회전시켰다가 1~2초 대기 후 원위치로 되돌린다. 회전 중에는 TurretController를 잠시 꺼서
//     같은 트랜스폼을 두 스크립트가 동시에 건드리지 않게 하고, 다시 켜면 TurretController.Update()의
//     RotateTowards가 알아서 정면으로 복귀시키므로 별도의 "복귀 트윈"이 필요 없다. 대기/회전 중 실제
//     조준 대상이 잡히면(AttackRange) 즉시 TurretController에 제어권을 돌려줘서 포탑이 눈멀지 않게 한다.
public class VehicleIdleAnimation : MonoBehaviour
{
    [Header("포탑 방황 (선택 - 포탑 없는 차량이면 비워도 자동으로 못 찾으면 스킵됨)")]
    [SerializeField] private TurretController turretController;
    [SerializeField] private float idleWaitTime = 10f;       // 이 시간 동안 가만히 있으면 포탑 방황 1회 재생
    [SerializeField] private float turretWanderAngle = 45f;  // 현재 각도 기준 ±각도
    [SerializeField] private float turretWanderDuration = 1f;
    [SerializeField] private float turretHoldMin = 1f;
    [SerializeField] private float turretHoldMax = 2f;

    [Header("엔진 떨림 (DOTween)")]
    [SerializeField] private float idleShakeStrength = 0.01f;
    [SerializeField] private int idleShakeVibrato = 10;
    [SerializeField] private float idleShakeCycleDuration = 0.15f;

    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private AttackRange attackRange;
    private EnemyAttackRange enemyAttackRange;

    private Vector3 basePosition;
    private Tween shakeTween;
    private Tween wanderTween;
    private Coroutine wanderRoutine;
    private bool isIdling;

    private void Awake()
    {
        unitController = GetComponentInParent<UnitController>();
        enemyUnitController = GetComponentInParent<EnemyUnitController>();
        basePosition = transform.localPosition;

        if (turretController == null)
            turretController = transform.root.GetComponentInChildren<TurretController>();
    }

    private void Start()
    {
        if (unitController != null)
            attackRange = unitController.GetAttackRange();
        else if (enemyUnitController != null)
            enemyAttackRange = enemyUnitController.GetAttackRange();
    }

    // VehicleShake/HoverBob과 동일한 폴링 토글 패턴(doc/0105).
    private void Update()
    {
        bool idle = IsIdle();

        if (idle && !isIdling)
        {
            isIdling = true;
            PlayIdleShakeCycle();
            if (turretController != null)
                wanderRoutine = StartCoroutine(IdleTurretWanderRoutine());
        }
        else if (!idle && isIdling)
        {
            isIdling = false;
            StopIdleShake();

            if (wanderRoutine != null)
            {
                StopCoroutine(wanderRoutine);
                wanderRoutine = null;
            }
            wanderTween?.Kill();

            if (turretController != null)
                turretController.enabled = true;
        }
    }

    private bool IsIdle()
    {
        if (unitController != null)
            return !unitController.IsCurrentlyMoving() && !unitController.IsAttack();
        if (enemyUnitController != null)
            return !enemyUnitController.IsCurrentlyMoving() && !enemyUnitController.IsAttack();
        return false;
    }

    private bool HasTrackingTarget()
    {
        if (attackRange != null) return attackRange.GetTrackingTarget() != null;
        if (enemyAttackRange != null) return enemyAttackRange.GetTrackingTarget() != null;
        return false;
    }

    private IEnumerator IdleTurretWanderRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(idleWaitTime);

            if (HasTrackingTarget())
                continue; // 이미 조준 중인 대상이 있으면 방황시키지 않는다

            turretController.enabled = false;

            float randomYaw = Random.Range(-turretWanderAngle, turretWanderAngle);
            Vector3 targetEuler = turretController.transform.localEulerAngles + new Vector3(0f, randomYaw, 0f);
            bool rotateDone = false;
            wanderTween = turretController.transform.DOLocalRotate(targetEuler, turretWanderDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => rotateDone = true);

            while (!rotateDone)
            {
                if (HasTrackingTarget()) break;
                yield return null;
            }

            float hold = Random.Range(turretHoldMin, turretHoldMax);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                if (HasTrackingTarget()) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            wanderTween?.Kill();
            turretController.enabled = true; // 정면 복귀는 TurretController.Update()의 RotateTowards가 처리
        }
    }

    // basePosition 기준으로 매 사이클 새로 시작해서 여러 번 반복해도 누적 오차 없이 위치가 흐트러지지 않는다
    // (VehicleShake.PlayShakeCycle과 동일한 체이닝 패턴).
    private void PlayIdleShakeCycle()
    {
        shakeTween = transform.DOShakePosition(idleShakeCycleDuration, idleShakeStrength, idleShakeVibrato, 90f, false, true)
            .OnComplete(() =>
            {
                if (isIdling)
                    PlayIdleShakeCycle();
            });
    }

    private void StopIdleShake()
    {
        shakeTween?.Kill();
        transform.DOLocalMove(basePosition, 0.15f).SetEase(Ease.OutSine);
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
        wanderTween?.Kill();
    }
}
```

### `Assets/Scripts/Animation/InfantryIdleLookAround.cs` (신규)

보병 유닛 루트(`UnitController`/`EnemyUnitController`가 붙어 있는 오브젝트)에 직접 부착.

```csharp
using UnityEngine;
using DG.Tweening;

// 보병 유닛 루트(UnitController/EnemyUnitController가 붙은 오브젝트)에 직접 부착한다 - 이동 중에는
// NavMeshAgent가, 공격 중에는 UnitController의 회전 로직이 이미 이 트랜스폼의 회전을 담당하므로
// 그 두 경우를 제외한 "가만히 있는" 상태에서만 개입한다.
// idleWaitTime(기본 10초)째 계속 가만히 있으면 랜덤한 Y축 방향(0~360도)으로 몸을 돌려 주변을
// 경계하는 느낌을 준다. 다시 이동/공격이 시작되면 즉시 트윈을 끊고 기존 로직에 회전을 넘겨준다.
public class InfantryIdleLookAround : MonoBehaviour
{
    [SerializeField] private float idleWaitTime = 10f; // 이 시간 동안 가만히 있으면 방향 전환 1회
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private Ease turnEase = Ease.InOutSine;

    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private float idleTimer;
    private Tween turnTween;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
    }

    private void Update()
    {
        if (!IsIdle())
        {
            turnTween?.Kill();
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer < idleWaitTime)
            return;

        idleTimer = 0f;
        turnTween?.Kill();
        float randomYaw = Random.Range(0f, 360f);
        turnTween = transform.DORotate(new Vector3(0f, randomYaw, 0f), turnDuration).SetEase(turnEase);
    }

    private bool IsIdle()
    {
        if (unitController != null)
            return !unitController.IsCurrentlyMoving() && !unitController.IsAttack();
        if (enemyUnitController != null)
            return !enemyUnitController.IsCurrentlyMoving() && !enemyUnitController.IsAttack();
        return false;
    }

    private void OnDestroy() => turnTween?.Kill();
}
```

## 부착 방법 (구현 승인 시)

- `VehicleIdleAnimation`: 차량 프리팹의 메쉬 자식 오브젝트(`VehicleShake`가 붙어있는 바로 그 오브젝트)에
  추가. 포탑이 있는 차량이면 `Turret Controller` 필드를 비워둬도 `transform.root`에서 자동으로 찾는다.
  포탑 없는 차량이면 그대로 둬도 엔진 떨림만 동작.
- `InfantryIdleLookAround`: 보병 유닛 루트(같은 오브젝트에 `UnitController` 또는 `EnemyUnitController`가
  있는 곳)에 추가.
- 둘 다 인스펙터에 노출된 값(대기 시간, 회전 폭, 지속시간 등)은 유닛별로 다르게 조정 가능.

## 영향받는 파일

- `Assets/Scripts/Animation/VehicleIdleAnimation.cs` (신규)
- `Assets/Scripts/Animation/InfantryIdleLookAround.cs` (신규)
- 기존 파일은 수정하지 않음 (`TurretController`, `UnitController`, `EnemyUnitController` 등은 손대지 않고
  공개 API만 재사용).

## 다음 단계

위 2개 신규 스크립트를 실제로 프로젝트에 생성해도 될지 확인 부탁드립니다.
