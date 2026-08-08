# 0464. 이동 명령이 자동교전을 못 끊는 문제 - 원인 확인 및 수정 제안

**날짜:** 2026-08-08

## 요청 내용
> 현재 구조 된 유닛에 경우 공격 명령에 들어가면 이동명령을 내려도 공격이 무시가 되지 않고 그냥
> 계속 공격하는데 이것좀 확인해줘

사용자 확인: "이미 구조 완료된" 유닛(`isRescueUnit=false`, 일반 유닛과 동일하게 동작해야 하는 상태)
얘기임 - 즉 명령 진입점 자체가 막히는 케이스(doc/0458/0459, 구조 전 전용)가 아니라 진짜 버그.

## 조사 - 재현 확인 (Play Mode, Mission3)

정적 코드 분석으로는 `MoveTo()` → `CancelAttackOrder()`가 `orderedTarget`/`friendlyTarget`/
`attackMoveDestination`을 전부 비우고 `UnitcurrentState = UnitState.Move`로 바꾸므로 문제가 없어
보였음. 그런데 Play Mode에서 실제 구조 유닛(`Cyborg Soldier (Rescue)`)을 대상으로 통제된 재현을
해보니 실제로 재현됨:

```
afterMoveTo: state=Move isStopped=False IsAttack=True
AttackRange.Update() 한 번 수동 실행 후: state=Move isStopped=True IsAttack=True
```

`MoveTo()` 직후엔 정상적으로 `isStopped=False`(이동 시작)이 되지만, 그 다음 `AttackRange.Update()`가
**단 한 번**만 더 돌아도 `isStopped`가 다시 `True`로 강제로 되돌아간다 - 즉 실제 위치 이동이 매
프레임 다시 멈춰지는 것과 동일한 상황.

### 원인

`AttackRange.Update()`의 게이트:
```csharp
if (unitController.IsAttack() || unitController.IsIdle())
{
    if (sqrDistance <= UnitRange * UnitRange)
        unitController.Attack(target.transform.position, target);
    ...
}
```

`UnitController.IsAttack()`은 `doc/0451`(전투 중 두리번 애니메이션 억제)에서 이렇게 넓게
재정의됐음:
```csharp
public bool IsAttack() => UnitcurrentState == UnitState.Attack || (attackRange != null && attackRange.HasEnemyInRange);
```

`HasEnemyInRange`는 "사거리 트리거(감지 콜라이더, `UnitRange+5` 이상) 안에 살아있는 적이 하나라도
있는가"만 보고, **현재 유닛의 명령 상태(Move/Idle/Attack)는 전혀 고려하지 않는다.** 그 결과
`MoveTo()`로 `UnitcurrentState`를 `Move`로 바꿔도, 방금까지 싸우던 적이 여전히 그 넓은 감지 범위
안에 남아있는 동안은 `IsAttack()`이 계속 `true`를 반환해서 `AttackRange.Update()`의 게이트가 계속
통과해버림 - `Attack()`이 매 프레임 다시 호출되고, `Attack()` 맨 위의 `navMeshAgent.isStopped = true`
(공격 쿨다운과 무관하게 무조건 실행됨)가 이동 명령이 방금 풀어둔 `isStopped = false`를 즉시 다시
덮어써서 유닛이 사실상 멈춘 채로 남는다.

`doc/0451` 스스로도 이 영향을 검토했었지만("이 함수 안에서 target이 non-null인 시점은 이미
enemiesInRange에서 뽑아온 대상이라 HasEnemyInRange도 자연히 true가 되므로, 기존에 이 분기를 타던
케이스에서 동작이 달라지지 않는다") - 그 검토는 "이미 게이트를 통과하던 케이스"만 확인했을 뿐,
**"게이트를 원래 막았어야 했는데(Move 상태) 이제 새로 통과하게 된 케이스"를 놓쳤음.** `doc/0451`
이전엔 `IsAttack()`이 순수 상태값(`UnitcurrentState == UnitState.Attack`)이라 `Move` 상태에서는
항상 `false`였고, 그래서 `MoveTo()`가 확실하게 자동교전을 끊었음 - 이번이 바로 그 회귀.

(참고: 구조 유닛이 아니어도 똑같이 재현되는 일반 버그다. 다만 구조 유닛은 Stage3에서 적 밀집
지역에 배치돼 있어 감지 트리거 안에 적이 거의 항상 남아있는 상태라 유독 눈에 잘 띄었을 뿐.)

## 제안하는 수정

`AttackRange.Update()`의 게이트가 원래 의도한 건 "상태가 `Attack`(명시 지정 공격 명령 추격/교전)
이거나 `Idle`(패시브 자동교전)일 때만 자동으로 쏘거나 쫓아간다"였다 - `doc/0451`이 만든 애니메이션용
넓은 정의(`HasEnemyInRange` 포함)는 이 게이트엔 원래 맞지 않았다. `IsAttack()` 자체는 애니메이션
소비처(`VehicleIdleAnimation`/`InfantryIdleLookAround`/`UnitAnimatorDriver`, 전부 doc/0451 의도대로
그대로 유지)에 필요하니 그대로 두고, `AttackRange.Update()`만 순수 상태값 기반의 좁은 접근자로
바꾼다 (root-cause 위치인 게이트 자체를 고치는 게 맞고, 각 명령 진입점마다 따로 손대지 않음):

```csharp
// UnitController.cs
// AttackRange.Update()의 자동교전 게이트 전용 - IsAttack()과 달리 실제 명령 상태(UnitcurrentState)만
// 본다. IsAttack()은 애니메이션용으로 사거리 내 적 존재 여부까지 넓게 판정해서(doc/0451), 이동 명령
// (Move) 중에도 직전까지 싸우던 적이 감지 범위 안에 남아있으면 true가 되어 MoveTo()로 공격을
// 끊으려 해도 AttackRange가 매 프레임 다시 Attack()을 호출해 이동을 계속 막는 문제가 있었다(doc/0464).
public bool IsAttackOrderState() => UnitcurrentState == UnitState.Attack;
```

```csharp
// AttackRange.cs, Update()
if (unitController.IsAttackOrderState() || unitController.IsIdle())
```

`UnitState.Attack`/`Idle` 두 케이스 모두 기존과 동일하게 동작(순수 상태값 비교라 `doc/0451` 이전
동작 그대로 복원)하고, `Move`/`Gather`/`Build` 등 다른 상태에서는 감지 범위 안에 적이 남아있어도
더 이상 게이트를 통과하지 못해 이동 명령이 확실히 자동교전을 끊는다.

## 영향 범위
- **수정**: `MoveTo()`(및 `UnitState.Move`로 전이하는 다른 명령)가 자동교전 중이던 유닛에도 확실히
  먹힌다.
- **변경 없음**: 두리번 애니메이션 억제(`InfantryIdleLookAround`/`VehicleIdleAnimation`), Fire
  애니메이션 파라미터(`UnitAnimatorDriver`) - 전부 기존 `IsAttack()`을 그대로 계속 씀.
- **변경 없음**: 명시 지정 공격(`AttackUnitTarget`/`AttackFriendlyTarget`)이 사거리 밖에서 추격하는
  동안(`UnitState.Attack`) 자동으로 공격 판정하는 동작 - `IsAttackOrderState()`가 여전히 `true`.

## 변경 예정 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/AttackRange.cs`

## 후속 확인
> 정확히 확인해보니 유닛이 공격중일땐 이동명령이 무시되어버리네 기존처럼 공격중에도 바로바로 다른
> 명령으로 공격을 끊을 수 있게끔 생각해줘야겠어

사용자가 직접 재현 확인 - 위 제안대로 구현 진행.

## 구현 결과

제안한 그대로 적용:
- `UnitController.cs`: `IsAttackOrderState()` 추가 (`UnitcurrentState == UnitState.Attack`만 확인).
- `AttackRange.cs`: `Update()`의 게이트를 `unitController.IsAttack() || unitController.IsIdle()`에서
  `unitController.IsAttackOrderState() || unitController.IsIdle()`로 변경.

## 검증

`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0` 확인 후,
Play Mode(Mission3)에서 수정 전과 동일한 통제 재현 절차를 다시 실행:

**수정 전 (버그 재현):**
```
MoveTo() 직후: state=Move isStopped=False
AttackRange.Update() 1회 실행 후: state=Move isStopped=True   ← 이동이 다시 막힘
```

**수정 후:**
```
MoveTo() 직후: state=Move isStopped=False IsAttack=True IsAttackOrderState=False
AttackRange.Update() 1회 + 추가 3회 실행 후: state=Move isStopped=False   ← 이동 유지됨
```

`IsAttack()`(애니메이션용, doc/0451)은 여전히 `true`를 반환하지만(변경 없음, 의도대로),
`AttackRange.Update()`는 이제 `IsAttackOrderState()`(순수 상태값)를 봐서 `Move` 상태에서는 더 이상
게이트를 통과하지 않음 - 이동 명령이 확실히 자동교전을 끊는다.

패시브 자동교전(`Idle` 상태)/명시 지정 공격(`UnitState.Attack`) 경로는 `IsIdle()`/
`IsAttackOrderState()`가 이전과 동일하게 각각 그대로 `true`이므로 게이트 통과 여부에 변화 없음(코드
비교로 no-op 확인) - 실제로 `Idle` 상태에서 사거리 내 적 자동교전(Attack/ChaseTarget)도 Play Mode에서
정상 동작 확인됨.

## 변경된 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/AttackRange.cs`