# 0384 - 도달 불가능한 대상에 대한 공격 명령 자동 취소

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 올라갈수 없는 언덕에 대한 처리의 연장선으로 올라갈수 없는 언덕위에 있는 대상을 공격하러 갈때
> 유닛들이 고장나는데 올라갈수 없거나 도달할수 없는 대상에 공격명령에 대한 경우 최대한 가까운 곳까지
> 이동하고 사거리 안에 들면 공격. 도저히 도달할수 없고 사거리 안에도 들지 않으면 공격명령 취소

[[0375]](0375-unreachable-hill-navmesh-fallback-proposal.md)에서 이동 자체는 "갈 수 있는 데까지"
가도록 고쳤지만, 그 뒤 사거리에 끝내 안 들어오는 경우를 아무도 감지하지 않아 유닛이 그 자리에 멈춘 채
공격 명령만 영원히 남아있는 문제. [[0382]](0382-auto-cancel-unreachable-build-order-proposal.md)에서
건설 이동에 적용한 것과 같은 패턴을 공격 명령에도 적용한다.

## 조사 결과

- 지정 공격(우클릭 적 클릭)은 `AttackOrderTick()`(`UnitController.cs:986`)이 매 프레임 갱신한다.
  대상이 `attackRange.UnitRange` 밖이면 그냥 매 프레임 `MoveAgentTo(target position)`만 호출한다
  (`UnitController.cs:1011~1012`). 대상이 경사로 없는 언덕 위 등 도달 불가능한 위치면, 0375 fallback
  덕분에 유닛은 가장 가까운 지점까지는 가서 멈추지만, 그 지점이 사거리 밖이면 `hasEngagedOrderedTarget`이
  단 한 번도 `true`가 되지 않아 "시야 이탈" 판정(996번째 줄)도 걸리지 않는다 - 즉 영원히 else 분기에
  머물며 이미 도달한 지점으로 `MoveAgentTo`만 헛되이 반복 호출한다. 공격 명령이 취소되지 않으니 유닛은
  그 자리에 멈춘 채 다른 자동 행동(예: 근처에 다른 적이 지나가도 무시)도 하지 못하는 "고장난" 것처럼
  보이는 상태로 남는다.
- 아군 강제공격(A 모드로 아군 좌클릭)은 `FriendlyAttackTick()`(`UnitController.cs:737`)이 담당한다.
  "시야 이탈" 개념 자체가 없이 사거리 밖이면 거리 상관없이 무조건 계속 추격하도록 설계돼 있어서
  (706번째 줄 주석), 도달 불가능한 대상이면 역시 영원히 제자리에서 `MoveAgentTo`만 반복한다.
- "더 이상 갈 수 없다"는 판정은 이미 같은 파일 안에 검증된 패턴이 있다: `BuildTick()`
  (`UnitController.cs:933`, doc/0382)과 `PatrolTick()`(`UnitController.cs:1305~1308`)이 둘 다
  `!navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance`로
  "지금 잡혀있는 경로(Partial Path 포함)의 끝에 도달해서 멈췄다"를 판정한다. 두 곳 모두 목적지가 사거리/
  상호작용 반경보다 먼 상태에서 이 조건이 참이면 "더는 다가갈 수 없다"로 해석한다. 공격 명령도 정확히
  같은 상황(목적지=대상 위치, 판정 반경=사거리)이라 동일한 조건을 그대로 재사용할 수 있음.
- 공격 명령 취소 자체의 인프라도 이미 있음: `CancelAttackOrder()`(`UnitController.cs:634`)가
  `orderedTarget`/`friendlyTarget`/`attackMoveDestination` 등 관련 상태를 전부 정리하고
  `unitEffects?.StopAttackEffects()`까지 처리한다. 정지 후 Idle 전환은 `HaltInPlace()`
  (`UnitController.cs:1257`)가 이미 하는 일(`UnitcurrentState = Idle` + `navMeshAgent.isStopped = true`
  / 공중 유닛은 제자리 고도 유지)이라 그대로 재사용하면 됨. `MoveTo()`/`StopUnit()` 등 기존 명령
  진입점들도 전부 `CancelAttackOrder()` 호출 뒤 상태 전환을 직접 하는 동일한 패턴을 쓰고 있음
  (`UnitController.cs:544~553`).
- 정상적으로 도달 가능한 대상(경사로로 연결된 언덕 포함)은 원래 사거리 안까지 걸어 들어갈 수 있으므로
  이 판정에 걸리지 않는다 - `remainingDistance <= stoppingDistance`가 참이 되는 시점은 오직 "더 갈 수
  없어서 멈췄는데 아직도 사거리 밖"인 경우뿐(사거리가 정지거리보다 큰 것이 일반적인 유닛 스펙이라 실질적
  충돌 없음, 0382와 동일한 전제).
- 공중 유닛은 애초에 지형을 무시하고 날아가므로(`AirTargetPosition`) 이 문제 자체가 발생하지 않는다 -
  두 틱 함수 모두 `!isAirUnit` 조건으로 지상 유닛에만 적용.
- 적 AI(`EnemyUnitController`/`EnemyAttackRange`)는 대상 추격이 "명령"이 아니라 `EnemyAttackRange.Update()`
  가 매 프레임 `Idle` 상태일 때만 `ChaseTarget()`을 호출하는 자동 감지 방식(`EnemyAttackRange.cs:109~119`)
  이라, 취소할 "명령" 개념이 없음. 도달 불가능한 대상 근처에서 멈춰도 매 프레임 같은 처리를 계속 반복할
  뿐 다른 상태를 막지 않으므로(0375에서 이미 성능 관점으로만 검토하고 문제없다고 판단한 부분과 동일)
  이번 변경 대상에서 제외.

## 코드 변경 (제안)

### `Assets/Scripts/Unit/UnitController.cs` - `AttackOrderTick()` (1005~1015번째 줄)

기존 코드:
```csharp
            else
            {
                attackMoveDestination = orderedTarget.transform.position;

                // 다른 적이 근처에 있어도 그건 무시하고, 오직 "지정한 대상"과의 거리로만 교전 여부를 판단한다
                // (attackRange.HasEnemyInRange를 쓰면 무관한 다른 적 때문에 추격이 멈춰버릴 수 있음).
                if (!inAttackRange)
                    MoveAgentTo(attackMoveDestination.Value); // 사거리 밖: 계속 추격 이동

                return;
            }
```

변경 코드:
```csharp
            else
            {
                attackMoveDestination = orderedTarget.transform.position;

                // 다른 적이 근처에 있어도 그건 무시하고, 오직 "지정한 대상"과의 거리로만 교전 여부를 판단한다
                // (attackRange.HasEnemyInRange를 쓰면 무관한 다른 적 때문에 추격이 멈춰버릴 수 있음).
                if (!inAttackRange)
                {
                    // 갈 수 있는 데까지 다 가서 멈췄는데도 사거리 밖(경사로 없는 언덕 위 등 도달 불가능한
                    // 대상, doc/0375 fallback으로 가장 가까운 지점까지만 이동한 경우 포함) - 공격 명령을
                    // 취소한다 (doc/0384).
                    if (!isAirUnit && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
                    {
                        CancelAttackOrder();
                        HaltInPlace();
                        return;
                    }

                    MoveAgentTo(attackMoveDestination.Value); // 사거리 밖: 계속 추격 이동
                }

                return;
            }
```

### `Assets/Scripts/Unit/UnitController.cs` - `FriendlyAttackTick()` (754~765번째 줄)

기존 코드:
```csharp
        float sqrDistance = (transform.position - friendlyTarget.transform.position).sqrMagnitude;

        if (attackRange != null && sqrDistance <= attackRange.UnitRange * attackRange.UnitRange)
        {
            Attack(friendlyTarget.transform.position, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
        else
        {
            MoveAgentTo(friendlyTarget.transform.position, IsAirborne(friendlyTarget)); // 사거리 밖: 거리 상관없이 끝까지 추격
        }
```

변경 코드:
```csharp
        float sqrDistance = (transform.position - friendlyTarget.transform.position).sqrMagnitude;

        if (attackRange != null && sqrDistance <= attackRange.UnitRange * attackRange.UnitRange)
        {
            Attack(friendlyTarget.transform.position, friendlyTarget.gameObject); // 내부에서 정지 처리까지 함께 해준다
        }
        else if (!isAirUnit && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
        {
            // 갈 수 있는 데까지 다 가서 멈췄는데도 사거리 밖(경사로 없는 언덕 위 등 도달 불가능한 대상) -
            // 공격 명령을 취소한다 (doc/0384).
            CancelAttackOrder();
            HaltInPlace();
        }
        else
        {
            MoveAgentTo(friendlyTarget.transform.position, IsAirborne(friendlyTarget)); // 사거리 밖: 거리 상관없이 끝까지 추격
        }
```

## 열린 질문

- 실패 음성/효과음은 요청에 없어서 추가하지 않음(0382의 건설 실패 음성과 달리 공격 취소는 조용히
  Idle로 돌아간다) - 필요하면 나중에 `UnitAudio`에 별도 보이스를 추가하면 됨.
- `HaltInPlace()`는 `navMeshAgent.ResetPath()`를 호출하지 않고 `isStopped = true`만 설정한다(기존
  `StopUnit()`/`BuildTick()` 취소 경로와 동일한 방식) - 취소 직후 새 명령이 오면 어차피
  `MoveAgentTo()`가 새 목적지로 덮어쓰므로 문제없음.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
