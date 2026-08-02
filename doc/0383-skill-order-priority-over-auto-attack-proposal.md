# 0383 - 스킬 사용 명령이 자동교전보다 우선하도록

## 질문/요청

> 고급 유닛의 스킬 사용이 공격명령보다 더 우선순위였으면 좋겠어
> 전투중에 스킬을 사용해도 사거리 안에 드는게 아닌 이상 지정한 유닛,위치에 대한 스킬 사용을 무시하네
> 어떤 상태든 스킬 사용 명령을 내리게 되면 스킬을 사용하기 위해 나머지를 무시하고 사용하러 갔으면 좋겠어

## 원인 조사

지정형 액티브 스킬(단일 유닛/범위, doc/0323)은 `UnitController.MoveToUseSkillOnUnit` /
`MoveToUseSkillOnArea`가 담당한다. 이 두 메서드는 이미 `CancelAttackOrder()`로 기존 추격
대상(`orderedTarget`)/아군 강제공격(`friendlyTarget`)/공격-이동(`attackMoveDestination`)을
전부 비우고, `hasPendingSkillUnitOrder`/`hasPendingSkillAreaOrder`를 세운 뒤 `MoveAgentTo`로
스킬 사거리까지 이동을 시작한다 (UnitController.cs:1914-1935). 이동 자체는 정상적으로 시작된다.

문제는 별도 컴포넌트인 `AttackRange.Update()`(자식 오브젝트, 매 프레임 독립 실행)에 있다.
이 메서드는 `hasPendingSkillUnitOrder`/`hasPendingSkillAreaOrder`를 전혀 모른 채, 유닛 상태가
`Attack` 또는 `Idle`이기만 하면 감지 범위 안의 적을 계속 확인해서 사거리 안이면
`unitController.Attack(...)`을 호출한다 (AttackRange.cs:71-91). `Attack()`은 즉시
`navMeshAgent.isStopped = true`로 이동을 멈춘다.

`MoveToUseSkillOnUnit`/`MoveToUseSkillOnArea`가 `UnitcurrentState`를 바꾸지 않기 때문에
(직전이 전투 중이었다면 `Attack` 상태 그대로 남음), 스킬 명령 직후에도 여전히 사거리 안의 적이
있으면 `AttackRange.Update()`가 매 프레임 다시 `Attack()`을 호출해 이동을 멈춰 세운다. 그 결과
- 스킬 대상이 이미 스킬 사거리 안이면: `SkillOrderTick()`이 즉시 스킬을 발동시켜 버그가 안 보임.
- 스킬 대상이 스킬 사거리 밖이면: 이동을 시작하자마자 근처 적과의 교전으로 다시 멈춰서, 유닛이
  제자리에서 계속 싸우기만 하고 스킬 사거리에는 영원히 도달하지 못함.

즉 "사거리 안에 드는게 아닌 이상 무시된다"는 증상은 AttackRange가 대기 중인 스킬 명령의 존재를
모르고 매 프레임 자동교전으로 이동을 가로채기 때문이다. `AttackOrderTick`/`FriendlyAttackTick`/
`FollowTick`은 이미 `CancelAttackOrder()`가 각자의 가드 변수를 비워서 문제 없음 - 유일하게
자동교전 진입점인 `AttackRange.Update()` 한 곳만 이 상태를 모른다.

## 수정 방향

- `UnitController`에 대기 중인 스킬 명령 여부를 묻는 읽기 전용 프로퍼티 추가:
  ```csharp
  public bool HasPendingSkillOrder => hasPendingSkillUnitOrder || hasPendingSkillAreaOrder;
  ```
- `AttackRange.Update()` 맨 앞에서 이 값을 확인해서 대기 중인 스킬 명령이 있으면 자동교전 로직
  전체(추격/공격 모두)를 건너뛴다:
  ```csharp
  private void Update()
  {
      enemiesInRange.RemoveAll(enemy => enemy == null);

      if (unitController.HasPendingSkillOrder)
          return; // 스킬 사용 명령이 우선 - 사거리 밖 적 자동교전으로 이동을 가로채지 않는다

      GameObject target = GetPreferredTarget();
      ...
  ```

이렇게 하면 스킬 명령이 들어온 순간부터(전투 중이었든 아니든) 실제로 스킬이 발동될 때까지
자동교전이 완전히 비활성화되어, 유닛이 다른 적에게 한눈팔지 않고 지정한 대상/위치로 곧장
이동해 스킬을 사용한다. 스킬 발동 시점에 `SkillOrderTick()`이 `StopUnit()`(→`CancelAttackOrder()`
→`HaltInPlace()`로 `UnitcurrentState = Idle`)을 호출하므로, 스킬 사용 직후에는 자동교전이 다시
정상적으로 재개된다.

수정 대상 파일: `Assets/Scripts/Unit/UnitController.cs` (프로퍼티 1개 추가),
`Assets/Scripts/Unit/AttackRange.cs` (Update() 가드 1줄 추가). 다른 파일은 건드릴 필요 없음.

## 적용 결과

사용자 승인 후 위 방향대로 수정 완료.

### UnitController.cs (`CanUseSkill()` 바로 위에 추가)

```diff
+    // 대기 중인 지정형 스킬 명령(단일 유닛/범위)이 있는지. AttackRange.Update()가 이 값을 확인해서
+    // 스킬 사용 명령 중엔 사거리 밖 적 자동교전으로 이동을 가로채지 않게 하는 데 쓴다 (doc/0383).
+    public bool HasPendingSkillOrder => hasPendingSkillUnitOrder || hasPendingSkillAreaOrder;
+
     public bool CanUseSkill() => skillCooldownRemaining <= 0f;
```

### AttackRange.cs (`Update()` 도입부)

```diff
         enemiesInRange.RemoveAll(enemy => enemy == null); // 이미 죽어서 destroy된 대상 정리

+        if (unitController.HasPendingSkillOrder)
+            return; // 스킬 사용 명령이 우선 - 사거리 밖 적 자동교전으로 이동을 가로채지 않는다 (doc/0383)
+
         GameObject target = GetPreferredTarget();
```

`npx uloop-cli compile` 확인: Success=true, ErrorCount=0 (WarningCount=33은 전부 기존에 있던
`FindObjectOfType` 계열 obsolete 경고로 이번 수정과 무관).
