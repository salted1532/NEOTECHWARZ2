# 0307. VehicleIdleAnimation - UnitController 없을 때 수동 제어 (제안)

날짜: 2026-07-30

## 요청 내용

> 차량 떨림 애니메이션의 idle 상태일때 작동하는거 잖아 그걸 내가 직접 조절할수 있도록 해줄래 그래서 뭐
> unitcontroller가 없더라도 내가 조절해서 작동시킬수 있도록

`VehicleIdleAnimation.IsIdle()`은 지금 `unitController`도 `enemyUnitController`도 못 찾으면 무조건
`false`를 반환한다 — 즉 유닛 프리팹 구조가 아닌 오브젝트(예: 쇼케이스용 차량, 데모 씬, 아직 유닛
컴포넌트가 안 붙은 프리팹)에 이 스크립트만 붙이면 엔진 떨림/포탑 방황이 절대 재생되지 않는다. 이걸
인스펙터 체크박스 + 코드에서 호출 가능한 메서드로 직접 켜고 끌 수 있게 해달라는 요청.

## 설계

- 두 컨트롤러가 다 없을 때만 쓰는 폴백 필드 `manualIdle`을 추가한다 (컨트롤러가 있으면 지금처럼 자동
  폴링이 우선 — 이 요청은 "컨트롤러가 없을 때"에 한정된 것이므로 기존 자동 동작은 건드리지 않는다).
- 인스펙터에서 체크박스로 바로 켜고 끌 수 있고, `SetManualIdle(bool)` 공개 메서드로 다른 스크립트나
  이벤트에서도 호출할 수 있게 한다.

## 코드 변경

### `Assets/Scripts/Animation/VehicleIdleAnimation.cs`

**기존 코드**:
```csharp
    [Header("엔진 떨림 (DOTween)")]
    [SerializeField] private float idleShakeStrength = 0.01f;
    [SerializeField] private int idleShakeVibrato = 10;
    [SerializeField] private float idleShakeCycleDuration = 0.15f;
```
```csharp
    private bool IsIdle()
    {
        if (unitController != null)
            return !unitController.IsCurrentlyMoving() && !unitController.IsAttack();
        if (enemyUnitController != null)
            return !enemyUnitController.IsCurrentlyMoving() && !enemyUnitController.IsAttack();
        return false;
    }
```

**변경 코드**:
```csharp
    [Header("엔진 떨림 (DOTween)")]
    [SerializeField] private float idleShakeStrength = 0.01f;
    [SerializeField] private int idleShakeVibrato = 10;
    [SerializeField] private float idleShakeCycleDuration = 0.15f;

    [Header("수동 제어 (UnitController/EnemyUnitController가 없을 때만 사용됨)")]
    [SerializeField] private bool manualIdle = false; // 인스펙터에서 직접 켜고 끌 수 있다 - SetManualIdle()로도 제어 가능
```
```csharp
    private bool IsIdle()
    {
        if (unitController != null)
            return !unitController.IsCurrentlyMoving() && !unitController.IsAttack();
        if (enemyUnitController != null)
            return !enemyUnitController.IsCurrentlyMoving() && !enemyUnitController.IsAttack();
        return manualIdle; // 유닛 컨트롤러가 없는 오브젝트에서는 이 값으로 직접 idle 여부를 제어한다
    }

    // UnitController/EnemyUnitController가 없는 오브젝트(쇼케이스, 데모 씬 등)에서 외부 스크립트나
    // 이벤트로 idle 애니메이션을 직접 켜고 끌 때 쓴다.
    public void SetManualIdle(bool idle) => manualIdle = idle;
```

## 요약

- 유닛 컴포넌트가 붙어있는 일반적인 경우는 동작 변화 없음(자동 폴링이 그대로 우선).
- 유닛 컴포넌트가 없는 오브젝트에서는 인스펙터의 `Manual Idle` 체크박스나 `SetManualIdle(bool)` 호출로
  엔진 떨림 + 포탑 방황을 직접 켜고 끌 수 있다.

## 영향받는 파일

- `Assets/Scripts/Animation/VehicleIdleAnimation.cs` (수정)

## 다음 단계

이대로 수정해도 될지 확인 부탁드립니다.
