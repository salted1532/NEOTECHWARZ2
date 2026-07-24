# 0225 - Sharpshooter 공격 애니메이션 연속 루프 (제안)

날짜: 2026-07-24

## 요청 내용

Sharpshooter가 공격 중일 때 공격 애니메이션이 계속 반복 재생되었으면 하는데, 지금은 공격
시작 시 잠깐 공격 모션이 나왔다가 곧바로 idle로 돌아가버려서 부자연스럽다는 요청. 사용자가
공격 모션 클립 자체의 Loop 설정은 이미 켰고, 스크립트/애니메이터 쪽에서도 "공격 중"인 동안은
계속 공격 상태를 유지하도록 고쳐달라는 요청.

## 조사 내용 (원인)

`Assets\Animation\Sharpshooter.controller`의 상태머신 구성 (`doc/0221`, `doc/0222`에서 만든 것):

- 파라미터: `IsMoving` (Bool), `Fire` (**Trigger**)
- `idle --[Fire 트리거]--> Fire상태` : idle에서 Fire 트리거가 뜨면 Fire 상태로 전이.
- `Fire상태 --[조건 없음, ExitTime 0.9]--> idle` : **조건 없이** Fire 상태 재생이 90% 진행되면
  무조건 idle로 돌아가는 전이가 걸려 있음 (fileID `1330897869674452680`).

즉 클립 자체의 Loop를 켜도, 상태머신이 "Fire 상태에 진입한 지 얼마 안 됐으면 90% 지점에서
무조건 idle로 나가라"는 전이를 그대로 갖고 있어서, 첫 재생 사이클의 90%만 돌고 강제로 idle로
빠져나가는 것 — 이게 사용자가 말한 "잠깐 공격 애니메이션이 나왔다가 idle로 넘어가는" 증상의
원인.

`Assets\Scripts\Animation\UnitAnimatorDriver.cs`도 이 트리거 방식에 맞춰 만들어져 있음:

```csharp
public void PlayFire()
{
    if (animator == null)
        return;

    animator.SetTrigger(FireParam);
}
```

그리고 `UnitController.Attack()` 안에서 실제 데미지가 들어가는 순간(공격 쿨다운 주기당 1회)에만
`GetComponent<UnitAnimatorDriver>()?.PlayFire();`를 호출함 (line 858). 트리거는 "1회성 이벤트"라
공격 중이라는 지속 상태를 표현하기에 안 맞고, 애니메이터 쪽 ExitTime 전이와 겹쳐서 문제를 더
키움.

한편 `UnitController`에는 이미 "지금 공격 상태인지"를 지속적으로 알려주는 훅이 있음
(line 1400): `public bool IsAttack() => UnitcurrentState == UnitState.Attack;`
— `IsCurrentlyMoving()`과 같은 패턴으로, 매 프레임 폴링해서 `IsMoving`처럼 Bool로 넘기기 적합.

## 제안하는 변경

### 방향

- `Fire` 파라미터를 Trigger → **Bool**로 바꾸고, `UnitAnimatorDriver.Update()`에서 매 프레임
  `unitController.IsAttack()` 값을 그대로 `SetBool`로 흘려보낸다 (IsMoving과 완전히 동일한 패턴).
- Fire 상태 → idle 전이의 **무조건 ExitTime을 제거**하고, `Fire == false`일 때만 idle로 나가도록
  조건을 건다. 즉 공격 상태가 유지되는 동안은 Fire 상태에 계속 머무르며, 클립 Loop 설정 덕분에
  모션이 끊김 없이 반복 재생된다. 공격이 끝나 `IsAttack()`이 false가 되는 순간에만 idle로 복귀.
- idle → Fire 전이는 조건을 트리거 대신 `Fire == true`(Bool)로 바꾼다.
- `UnitController.Attack()`의 `PlayFire()` 호출은 제거 — 더 이상 "공격 성공 이벤트" 시점에 1회
  트리거할 필요가 없고, `Update()`의 매 프레임 폴링이 상태를 전부 담당하게 됨. (`IsCurrentlyMoving`을
  건드리는 이동 로직도 이벤트 콜백 없이 폴링만으로 동작하는 것과 동일한 패턴으로 통일.)

### 1) `Assets\Scripts\Animation\UnitAnimatorDriver.cs`

기존 코드:
```csharp
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
```

변경 코드:
```csharp
    private void Update()
    {
        if (animator == null || unitController == null)
            return;

        animator.SetBool(IsMovingParam, unitController.IsCurrentlyMoving());
        // 공격 중인 동안은 계속 true를 흘려보내 Fire 상태에 머무르게 한다 (doc/0225).
        // 공격이 끝나면 false가 되어 애니메이터가 자체적으로 idle로 돌아간다.
        animator.SetBool(FireParam, unitController.IsAttack());
    }
```

`PlayFire()` 메서드는 삭제 (더 이상 트리거 방식으로 호출하지 않음).

### 2) `Assets\Scripts\Unit\UnitController.cs` (line 858 부근)

기존 코드:
```csharp
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(enemy.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0219)
            GetComponent<UnitAnimatorDriver>()?.PlayFire(); // Animator가 있는 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0222)
```

변경 코드:
```csharp
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(enemy.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0218)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (doc/0219)
```

(공격 애니메이션은 이제 `UnitAnimatorDriver.Update()`가 `IsAttack()`을 폴링해서 처리하므로,
공격 성공 이벤트 시점에 별도로 트리거할 필요가 없어짐.)

### 3) `Assets\Animation\Sharpshooter.controller` (에디터에서 직접 수정 필요 — 아래는 수정될 내용의 요약)

- `Fire` 파라미터 타입: Trigger → Bool
- idle → Fire 전이 조건: `Fire` 트리거 → `Fire == true`
- Fire → idle 전이: `Has Exit Time` 체크 해제, 조건에 `Fire == false` 추가
  (현재 조건 없음 + ExitTime 0.9로 되어 있는 것이 원인이므로 반드시 같이 고쳐야 함)

`.controller` 파일은 YAML이라 텍스트로 직접 값을 바꾸는 것도 가능하지만, 이 프로젝트에서는
지금까지 애니메이터 상태머신 관련 편집을 전부 Unity 에디터 GUI로 진행해왔음 (`doc/0220`,
`doc/0221` 참고 — 연결 가이드만 문서로 제공하고 실제 작업은 사용자가 에디터에서 수행). 이번에도
같은 방식을 따를지, 아니면 `.controller` YAML을 직접 수정해도 괜찮을지 확인 필요.

## 확인 결과 및 실제 적용

사용자가 "전부 진행 (컨트롤러 YAML도 직접 수정)"으로 확인 → 위 제안 그대로 3개 파일 모두 적용함.

`Sharpshooter.controller` YAML 실제 변경:
- `Fire` 파라미터: `m_Type: 9` (Trigger) → `m_Type: 4` (Bool)
- Fire → idle 전이 (fileID `1330897869674452680`): `m_Conditions: []` + `m_HasExitTime: 1`
  → `m_Conditions: [{m_ConditionMode: 2, m_ConditionEvent: Fire, m_EventTreshold: 0}]`
  (ConditionMode 2 = IfNot, 즉 `Fire == false`일 때만 전이) + `m_HasExitTime: 0`
- idle → Fire 전이 (fileID `649983045404128812`)는 기존 `m_ConditionMode: 1` (If) 조건을 그대로
  유지 — Trigger의 "발동됨"과 Bool의 "true"가 같은 ConditionMode 값(1)을 쓰므로 조건 자체는
  안 바꿔도 됨. 파라미터 타입만 Bool로 바뀌면서 자동으로 `Fire == true` 의미가 됨.

## 변경된 파일

- `Assets/Scripts/Animation/UnitAnimatorDriver.cs` (Fire를 매 프레임 SetBool로 폴링, `PlayFire()` 삭제)
- `Assets/Scripts/Unit/UnitController.cs` (`Attack()`에서 `PlayFire()` 호출 제거)
- `Assets/Animation/Sharpshooter.controller` (Fire 파라미터 Trigger→Bool, Fire→idle 전이 조건 변경)
