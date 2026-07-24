# 0222 - UnitAnimatorDriver 추가 (idle/Walk/Fire 파라미터 연동)

날짜: 2026-07-24

## 요청 내용

`doc/0221`에서 고친 Sharpshooter.controller의 `IsMoving`/`Fire` 파라미터를 실제로 채워주는
컴포넌트를 만들어달라는 요청. 추가 조건: 모든 유닛이 애니메이션을 가지는 건 아니므로,
Animator가 없는 유닛에서는 예외(NullReferenceException) 없이 그냥 아무 동작도 안 하도록
처리해달라는 요청.

(이전 턴에서 "원하시면 지금 만들어드릴까요?"라고 물었고, 이번 요청이 그에 대한 명시적 승인이라
바로 구현.)

## 조사 내용

- `Assets\Scripts\Unit\UnitController.cs`에 이미 존재하는 훅:
  - `IsCurrentlyMoving()` (line 1403) — `UnitEffects` 등 다른 컴포넌트가 매 프레임 폴링하는 패턴.
  - `Attack(Vector3 end, GameObject enemy)` (line 821) 안에서, 실제로 공격이 성공한 순간
    (`targetHealth.GetDamage(...)` 직후) `GetComponent<UnitEffects>()?.PlayAttack()`,
    `GetComponent<LaserBeamAttack>()?.Fire(...)`, `turretController?.FireRecoil()`을
    옵셔널 컴포넌트 패턴(`GetComponent<T>()?.Method()`)으로 호출하고 있음 (line 855-857).
    → "Fire" 트리거는 이 자리에 같은 패턴으로 추가하는 게 기존 코드 스타일과 가장 일치함
    (매 프레임 폴링이 아니라 실제 공격 이벤트 시점에 1회 호출).
- `Assets\Scripts\Animation\` 폴더에 `HoverBob.cs`, `AutoRotate.cs`, `VehicleShake.cs`가 있고
  전부 비주얼/이펙트 전용 컴포넌트라 새 컴포넌트도 이 폴더에 두는 게 일관됨.
- Animator는 유닛의 비주얼 모델(예: `unit_Infantry_Light_A_yup`)에 붙어 있고, 이 모델은
  UnitController가 있는 루트의 자식으로 들어가는 구조 → `GetComponent<Animator>()`가 아니라
  `GetComponentInChildren<Animator>()`로 찾아야 함. 못 찾으면 null이 되고, 그 이후 모든
  호출부에서 null 체크로 조용히 스킵.

## 코드 변경 (기존 코드 → 변경 코드)

### 1) 신규 파일: `Assets/Scripts/Animation/UnitAnimatorDriver.cs`

```csharp
using UnityEngine;

// 유닛의 이동/공격 상태를 Animator 파라미터(IsMoving/Fire)에 반영한다.
// 비주얼 모델에 Animator가 없는 유닛(정적 메쉬만 쓰는 유닛 등)도 있으므로, Animator를 못 찾으면
// 아무 동작도 하지 않고 조용히 넘어간다 - 모든 유닛이 애니메이션을 갖는 것은 아니기 때문.
public class UnitAnimatorDriver : MonoBehaviour
{
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");
    private static readonly int FireParam = Animator.StringToHash("Fire");

    private UnitController unitController;
    private Animator animator;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        animator = GetComponentInChildren<Animator>(); // 비주얼 모델 자식에 붙어있는 Animator
    }

    private void Update()
    {
        if (animator == null || unitController == null)
            return;

        animator.SetBool(IsMovingParam, unitController.IsCurrentlyMoving());
    }

    // UnitController.Attack()이 실제로 공격에 성공했을 때 호출한다 (doc/0222).
    public void PlayFire()
    {
        if (animator == null)
            return;

        animator.SetTrigger(FireParam);
    }
}
```

### 2) `Assets\Scripts\Unit\UnitController.cs` (line 855 부근)

기존 코드:
```csharp
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(enemy.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0219)
```

변경 코드:
```csharp
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(enemy.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0219)
            GetComponent<UnitAnimatorDriver>()?.PlayFire(); // Animator가 있는 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0222)
```

## 요약/남은 작업

- `UnitAnimatorDriver`는 `UnitController`와 같은 GameObject(유닛 루트)에 붙이는 걸 전제로 함.
  Animator가 있는 유닛(현재는 Sharpshooter)에만 이 컴포넌트를 추가하면 되고, 없는 유닛은
  아예 컴포넌트를 안 붙이거나 붙여도 `animator == null`이라 안전하게 아무 일도 안 함.
- 실제 Sharpshooter 프리팹(`Assets\prefabs\NTA\Unit\Tier1\Sharpshooter.prefab`)에 이 컴포넌트를
  붙이는 작업과, Animator가 있는 비주얼 모델(`unit_Infantry_Light_A_yup`)을 그 프리팹 하위에
  실제로 연결하는 작업은 사용자가 에디터에서 직접 진행 필요 (프리팹 계층 구성은 GUI 작업 영역).

## 변경된 파일

- `Assets/Scripts/Animation/UnitAnimatorDriver.cs` (신규)
- `Assets/Scripts/Unit/UnitController.cs` (Attack() 안에 1줄 추가)
