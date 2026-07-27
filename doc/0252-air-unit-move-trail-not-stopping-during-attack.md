# 0252 - 공중 유닛 이동 이펙트가 공격 중에도 멈추지 않음 (적용 완료)

## 요청

> 적 공중유닛의 이동 이펙트가 공격중일때도 안멈추고 계속 나오네 이것좀 확인해줘

## 조사 내용 - 원인: Attack()이 매 프레임 isMovingAirUnit을 무조건 true로 켬

`UnitEffects`의 이동 트레일은 `IsCurrentlyMoving()`을 매 프레임 폴링해서 켜고 끈다:

```csharp
// UnitEffects.cs:69-90
private void Update()
{
    bool moving = (unitController != null && unitController.IsCurrentlyMoving())
        || (enemyUnitController != null && enemyUnitController.IsCurrentlyMoving());
    SetMoveTrail(moving);
}

private void SetMoveTrail(bool moving)
{
    if (moving && activeTrails.Count == 0 && moveTrailPrefab != null) { ... 트레일 생성 ... }
    else if (!moving && activeTrails.Count > 0) { ... 트레일 파괴 ... }
}
```

```csharp
// EnemyUnitController.cs:420-426
public bool IsCurrentlyMoving()
{
    if (isAirUnit)
        return isMovingAirUnit;   // 공중 유닛은 이 플래그 하나로 결정됨
    ...
}
```

문제는 `Attack()`이 공중 유닛인 경우 **매번 호출될 때마다 조건 없이** `isMovingAirUnit = true`를 설정한다는 것:

```csharp
// EnemyUnitController.cs:226-240 (Attack, EnemyAttackRange가 사거리 내 대상에게 매 프레임 호출)
public void Attack(Vector3 end, GameObject target)
{
    if (!isAirUnit)
    {
        navMeshAgent.isStopped = true;
    }
    else
    {
        // 공격 중에도 목표 고도(airCruiseAltitude)까지는 계속 상승한다 - 생성되자마자 근처에 상대가
        // 있어서 뜨기도 전에 공격을 시작하면 그대로 바닥에 눌러붙은 채로 싸우는 문제가 있었다 (doc/0241).
        // 수평 이동 없이(현재 XZ 그대로) 수직으로만 계속 목표 고도로 수렴시킨다.
        float groundBelow = SampleGroundHeight(transform.position, transform.position.y - airCruiseAltitude);
        targetPosition = new Vector3(transform.position.x, groundBelow + airCruiseAltitude, transform.position.z);
        isMovingAirUnit = true;   // ← 이미 순항 고도에 도달했어도 공격할 때마다 매번 true로 켜짐
    }
    ...
}
```

원래 이 코드의 의도(doc/0241)는 "생성 직후 아직 순항 고도까지 안 떴는데 근처에 적이 있어 바로 공격을
시작하는 경우, 공격 중에도 계속 떠오르게 하자"였다. 그런데 조건 없이 매번 `true`로 켜버려서, **이미
순항 고도에 도달한 뒤에도** `EnemyAttackRange`가 사거리 내 대상에게 매 프레임 `Attack()`을 호출할 때마다
`isMovingAirUnit`이 계속 `true`로 다시 켜진다.

`Update()`의 도착 판정(`arrivedHorizontally && arrivedVertically`이면 `isMovingAirUnit = false`)이
따로 있긴 하지만, `Attack()`이 매 프레임 다시 `true`로 덮어써버리므로 실질적으로 공격이 지속되는 동안
`isMovingAirUnit`이 거의 계속 `true`로 유지되거나(또는 프레임마다 false↔true를 반복) - 결과적으로
`IsCurrentlyMoving()`이 계속 `true`를 반환해 이동 트레일이 파괴되지 않거나 파괴→즉시 재생성을 반복해서
"공격 중에도 이동 이펙트가 안 멈추고 계속 나온다"는 증상으로 보인다.

**참고 - 아군(플레이어) 쪽에도 동일한 코드가 있음**: `Assets/Scripts/Unit/UnitController.cs:841-855`의
`Attack()`도 완전히 같은 패턴(`isMovingAirUnit = true`를 조건 없이 설정)이라, 아군 공중 유닛도 같은
증상을 겪을 가능성이 높다(아직 보고되지 않았을 뿐). 이번 요청은 적 유닛만 언급하셔서 우선 적만
수정안에 넣었고, 아래 "적용 범위" 질문에서 아군도 같이 고칠지 여쭤봄.

## 제안하는 수정 - 이미 목표 고도에 도달했으면 isMovingAirUnit을 다시 켜지 않음

목표 Y(`groundBelow + airCruiseAltitude`)와 현재 Y의 차이가 `Update()`의 도착 판정과 동일한 임계값
(0.1) 이상일 때만 "아직 상승/하강 중"으로 보고 `isMovingAirUnit`을 켠다. 이미 도달했으면 그대로 두어
`IsCurrentlyMoving()`이 `false`를 유지하게 한다(공격 중 몸통 회전 등 다른 동작에는 영향 없음 - 이
필드는 순수히 "고도까지 수직 이동 중인가"만 나타냄).

**`Assets/Scripts/Enemy/EnemyUnitController.cs`**
```csharp
// 기존 코드
        else
        {
            // 공격 중에도 목표 고도(airCruiseAltitude)까지는 계속 상승한다 - 생성되자마자 근처에 상대가
            // 있어서 뜨기도 전에 공격을 시작하면 그대로 바닥에 눌러붙은 채로 싸우는 문제가 있었다 (doc/0241).
            // 수평 이동 없이(현재 XZ 그대로) 수직으로만 계속 목표 고도로 수렴시킨다.
            float groundBelow = SampleGroundHeight(transform.position, transform.position.y - airCruiseAltitude);
            targetPosition = new Vector3(transform.position.x, groundBelow + airCruiseAltitude, transform.position.z);
            isMovingAirUnit = true;
        }
```
```csharp
// 변경 코드
        else
        {
            // 공격 중에도 목표 고도(airCruiseAltitude)까지는 계속 상승한다 - 생성되자마자 근처에 상대가
            // 있어서 뜨기도 전에 공격을 시작하면 그대로 바닥에 눌러붙은 채로 싸우는 문제가 있었다 (doc/0241).
            // 수평 이동 없이(현재 XZ 그대로) 수직으로만 계속 목표 고도로 수렴시킨다.
            float groundBelow = SampleGroundHeight(transform.position, transform.position.y - airCruiseAltitude);
            float desiredY = groundBelow + airCruiseAltitude;
            targetPosition = new Vector3(transform.position.x, desiredY, transform.position.z);

            // 이미 목표 고도에 도달했으면 "이동 중"으로 다시 켜지 않는다 - 여기서 매 프레임 무조건 true로
            // 켜면 공격이 지속되는 내내 IsCurrentlyMoving()이 true가 되어 이동 이펙트(엔진 트레일)가
            // 멈추지 않는다(doc/0252). 임계값은 Update()의 도착 판정(arrivedVertically)과 동일하게 0.1.
            if (Mathf.Abs(transform.position.y - desiredY) >= 0.1f)
                isMovingAirUnit = true;
        }
```

## 적용 범위

사용자 확인 결과 **적 + 아군 둘 다** 수정 (동일한 코드 패턴이라 아군 공중 유닛도 같은 버그를 겪고
있었음).

## 변경된 파일

- `Assets/Scripts/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/Unit/UnitController.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함(설계와 구현 간 차이 없음).
