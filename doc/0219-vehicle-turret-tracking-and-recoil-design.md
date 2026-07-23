# 0219 — 차량형 유닛 포탑 자동 조준 + DOTween 반동 설계 제안

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 아직 스크립트/기존 파일을 수정하지 않았다. 검토 후 확인해주면 그때 구현한다.

## 1. 요청
"차량형 유닛의 포탑이 적 유닛 발견 시 그쪽을 쳐다보게(몸체보다 빠르게 회전) 해줘. 포탑 전용 부가 스크립트로 만들고,
DOTween으로 공격 시 포신 반동도 추가해줘(수치는 인스펙터에서 조절 가능하게). 이동 명령 중엔 포탑이 적을 계속 쳐다만
보고 공격은 안 함. 이런 유닛은 공격할 때 몸체가 바로 안 돌고, 이동할 때만 몸체가 돌며, 포탑이 적을 조준해서
공격 이펙트 등을 발생시키는 구조였으면 함."

## 2. 조사 내용

- **유닛 모델 계층 확인** (`Assets/prefabs/Asset/unit_Tank_Heavy_A_yup.prefab`): `unit_Tank_Heavy_A_yup`(몸체, 루트) → `unit_Tank_Heavy_A_Turret_yup`(포탑, 자식) → `unit_Tank_Heavy_A_Gun_yup`(포신, 손자). 포탑만 따로 돌 수 있는 구조가 이미 있음. (이 모델 프리팹 자체엔 스크립트가 없고, `UnitController` 등은 이걸 감싸는 별도 "Unit" 프리팹에 있을 것 — 실제 부착은 유닛 프리팹 쪽에서 진행 필요)
- **몸체 회전 로직**: `UnitController.Attack(Vector3 end, GameObject enemy)`가 매 프레임(교전 중 `AttackRange.Update()` → `Attack()` 반복 호출) `RotateYOnly(end)`를 **무조건** 호출해서 몸체를 적 쪽으로 돌림(`Assets/Scripts/Unit/UnitController.cs:821`). 요구사항대로면 포탑 유닛은 이 호출을 건너뛰어야 함.
- **이동 중 몸체 회전은 이미 자동 처리됨**: 지상 유닛은 `NavMeshAgent`가 기본적으로 이동 방향으로 몸체를 자동 회전시킴(`updateRotation` 기본 true) — 그래서 "공격 때 안 돌고 이동 때만 돈다"는 요구사항은 **`Attack()`의 `RotateYOnly` 호출만 스킵하면 자동으로 충족됨** (별도 이동 회전 코드를 새로 짤 필요 없음).
- **적 탐지**: `AttackRange`가 트리거 콜라이더로 "Enemy" 태그를 감지해 `enemiesInRange`에 담아두고, `GetPreferredTarget()`(private)으로 우선순위 대상(명시 지정 대상 > 최근접 적)을 고름. `HasEnemyInRange`만 public이고 실제 타겟 자체는 외부에 노출 안 됨 — 포탑이 조준하려면 이 타겟을 물어볼 방법이 필요함.
- **"이동 중엔 조준만, 공격 안 함"이 이미 상태머신에 내재됨**: `MoveTo()`는 `UnitState.Move`로 전환하고, `AttackRange.Update()`는 `unitController.IsAttack() || unitController.IsIdle()`일 때만 `Attack()`을 호출함 — 즉 `Move` 상태에선 이미 자동 공격이 안 나감. 포탑이 "그래도 쳐다보기만은 하게" 하려면, 포탑의 조준 로직은 이 상태와 무관하게 **항상** `AttackRange`가 감지한 대상을 바라보게 하면 됨(공격 여부와 조준 여부를 분리).
- **DOTween 컨벤션**: `Assets/Scripts/Animation/AutoRotate.cs`(doc/0147)가 `DG.Tweening` 네임스페이스로 이미 쓰이고 있음 — 동일 스타일 재사용.
- **기존 공격 훅 지점**: `UnitController.Attack()` 안, 데미지 적용 직후 `UnitEffects.PlayAttack()` / `LaserBeamAttack.Fire()`가 옵셔널 컴포넌트로 호출됨(`?.` 패턴) — 포탑 반동도 동일한 자리에 같은 패턴으로 추가하면 됨.

## 3. 제안하는 구현

### 3.1 `AttackRange.cs` — 조준용 타겟 조회 메서드 추가
```csharp
// 포탑 등 "조준만 하고 공격 여부는 별개로 판단하는" 스크립트가 쓴다. 실제 공격 가능 상태(Idle/Attack)와
// 무관하게 사거리 내 우선순위 대상을 그대로 반환 - 이동 명령 중에도 포탑은 눈에 보이는 적을 계속 쳐다봐야 하므로.
public GameObject GetTrackingTarget() => GetPreferredTarget();
```
기존 `GetPreferredTarget()`(명시 지정 대상 우선, 없으면 최근접 적)을 그대로 재사용 — 포탑이 조준하는 대상과 실제
공격 대상이 항상 일치하게 됨.

### 3.2 신규 파일 `Assets/Scripts/Unit/TurretController.cs`
포탑 오브젝트(`unit_Tank_Heavy_A_Turret_yup` 같은)에 직접 부착:

```csharp
using UnityEngine;
using DG.Tweening;

public class TurretController : MonoBehaviour
{
    [Header("조준")]
    [SerializeField] private float rotationSpeed = 360f; // 초당 회전각(도) - 몸체보다 빠르게

    [Header("반동 (DOTween)")]
    [SerializeField] private Transform recoilPart;              // 뒤로 빠질 파츠(포신). 비우면 이 오브젝트 자신
    [SerializeField] private Vector3 recoilLocalOffset = new Vector3(0f, 0f, -0.3f);
    [SerializeField] private float recoilDuration = 0.08f;      // 뒤로 빠지는 시간
    [SerializeField] private float recoilReturnDuration = 0.18f;// 원위치 복귀 시간
    [SerializeField] private Ease recoilEase = Ease.OutQuad;
    [SerializeField] private Ease recoilReturnEase = Ease.OutBack;

    private AttackRange attackRange;
    private Vector3 recoilRestLocalPosition;
    private Tween recoilTween;

    private void Awake()
    {
        UnitController unitController = GetComponentInParent<UnitController>();
        attackRange = unitController != null ? unitController.GetAttackRange() : null;

        if (recoilPart == null) recoilPart = transform;
        recoilRestLocalPosition = recoilPart.localPosition;
    }

    private void Update()
    {
        if (attackRange == null) return;
        GameObject target = attackRange.GetTrackingTarget();
        if (target == null) return;

        Vector3 dir = target.transform.position - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, rotationSpeed * Time.deltaTime);
    }

    // UnitController.Attack()이 데미지를 실제로 입힌 순간 호출 (UnitEffects.PlayAttack와 동일한 훅 지점)
    public void FireRecoil()
    {
        recoilTween?.Kill();
        recoilPart.localPosition = recoilRestLocalPosition;

        recoilTween = recoilPart.DOLocalMove(recoilRestLocalPosition + recoilLocalOffset, recoilDuration)
            .SetEase(recoilEase)
            .OnComplete(() => recoilTween = recoilPart
                .DOLocalMove(recoilRestLocalPosition, recoilReturnDuration)
                .SetEase(recoilReturnEase));
    }

    private void OnDestroy() => recoilTween?.Kill();
}
```

- `rotationSpeed`: 초당 회전 각도. 몸체의 `RotateYOnly`는 `Slerp(..., Time.deltaTime * 10f)`라 정확한 도/초 환산은
  아니지만 대략 빠른 편 — 포탑은 그보다 눈에 띄게 빠르게(기본 360°/s = 1초에 한 바퀴) 잡아뒀고, 인스펙터에서 자유 조정 가능.
- 반동: `recoilPart`(비우면 포탑 자신, 보통은 포신 오브젝트를 지정)를 로컬 좌표 기준 `recoilLocalOffset`만큼 뒤로
  뺐다가(`recoilDuration`) 원위치로 복귀(`recoilReturnDuration`) — 오프셋/시간/Ease 전부 인스펙터 노출.
- Y축만 참조하는 `dir.y = 0`은 `RotateYOnly`와 동일하게 수평 조준만 함(포탑이 상하로 안 꺾임 — 필요하면 나중에 별도 피치 축 추가 가능).

### 3.3 `UnitController.cs` 수정
```csharp
private TurretController turretController; // Awake에서 캐싱, 없으면 null(일반 유닛은 영향 없음)

// Awake()
turretController = GetComponentInChildren<TurretController>();

// Attack() 안, 기존 RotateYOnly(end); 를
if (turretController == null)
    RotateYOnly(end); // 포탑 유닛은 몸체를 안 돌림 - 포탑이 대신 조준

// 데미지 적용 블록 안, 기존 두 줄 다음에 한 줄 추가
GetComponent<UnitEffects>()?.PlayAttack();
GetComponent<LaserBeamAttack>()?.Fire(enemy.transform);
turretController?.FireRecoil();

// 새 public 게터 추가 (TurretController가 AttackRange를 얻을 때 사용)
public AttackRange GetAttackRange() => attackRange;
```

## 4. 남은 수동 작업 (Unity 에디터, 프리팹 못 특정해서 자동 배선 불가)
1. 포탑을 쓸 유닛 프리팹(예: `unit_Tank_Heavy_A_yup` 계열)의 **Turret 오브젝트**에 `TurretController` 컴포넌트 추가.
2. `Recoil Part`에 포신(Gun) 오브젝트 할당(비우면 포탑 전체가 반동함).
3. `Rotation Speed`/반동 오프셋·시간·Ease 값을 원하는 대로 조정.

## 5. 확인 필요 사항
1. "시야 범위"를 별도로 둘지, 아니면 기존 `AttackRange`의 사거리(트리거 콜라이더)를 그대로 조준 감지 범위로 쓸지 —
   지금 코드베이스엔 공격 사거리 외에 별도 "시야" 개념이 없어서, 이번 제안은 기존 `AttackRange`를 그대로 재사용함
   (더 넓은 "시야만 있고 사거리 밖" 개념이 필요하면 별도 콜라이더/필드를 추가해야 함 — 원하면 이어서 설계).
2. 포탑이 상하로도 움직여야 하는지(현재는 Y축 수평 회전만).
3. 이대로 구현 진행해도 될지.

## 6. 적용 완료

질문 답변: 감지 범위 = **기존 AttackRange 그대로 재사용**, 포탑 축 = **Y축 좌우 회전만**. 3절 설계 그대로 구현함.

1. `Assets/Scripts/Unit/AttackRange.cs` — `GetTrackingTarget()` 공개 메서드 추가(기존 private `GetPreferredTarget()` 재사용).
2. `Assets/Scripts/Unit/TurretController.cs` 신규 작성 — 3.2절 코드 그대로.
3. `Assets/Scripts/Unit/UnitController.cs`
   - `turretController` 필드 추가, `Awake()`에서 `GetComponentInChildren<TurretController>()`로 캐싱(없으면 null, 일반 유닛엔 영향 없음).
   - `Attack()`의 `RotateYOnly(end);` 호출을 `if (turretController == null) RotateYOnly(end);`로 변경.
   - 데미지 적용 블록의 기존 `UnitEffects`/`LaserBeamAttack` 훅 옆에 `turretController?.FireRecoil();` 추가.
   - `public AttackRange GetAttackRange() => attackRange;` 게터 추가.

### 7. 남은 수동 작업 (Unity 에디터)
4절 그대로: 포탑을 쓸 유닛 프리팹의 Turret 오브젝트에 `TurretController` 컴포넌트를 추가하고, `Recoil Part`에 포신
오브젝트를 할당한 뒤 `Rotation Speed`/반동 값들을 원하는 대로 조정. 플레이 모드에서 적이 사거리에 들어왔을 때
포탑이 몸체보다 빠르게 돌아가는지, 이동 명령 중엔 몸체만 돌고 포탑은 계속 적을 쳐다보기만 하는지, 공격 시
포신이 반동으로 뒤로 빠졌다 돌아오는지 확인 부탁.
