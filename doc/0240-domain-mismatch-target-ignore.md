# 0240 - 공격 불가능한 도메인의 대상은 감지 단계에서부터 완전히 무시

## 요청

지상 공격만 가능한 유닛이 공중 유닛을 만나면(반대로 공중 전용 유닛이 지상 유닛/건물을 만나면) 그 대상을
완전히 무시해야 함. 그런데 현재는 땅 공격-이동(A + 클릭) 중에 공격 불가능한 도메인의 대상을 만나면,
공격은 못 하면서도 그 자리에 멈춰서 계속 공격하려는 것처럼 굳어버림. 무시하고 원래 목적지까지 계속
이동하다가, 실제로 공격 가능한(같은 도메인) 대상을 만났을 때만 멈춰서 공격해야 함. 아군/적 유닛 둘 다
적용.

## 원인

`AttackRange.cs`(아군)/`EnemyAttackRange.cs`(적)의 "가장 가까운 대상 찾기"(`GetClosestEnemy`/
`GetClosestTarget`)가 도메인(지상/공중)을 전혀 따지지 않고 감지된 대상 중 가장 가까운 것을 무조건
반환하고 있었음. 도메인 체크는 `UnitController.Attack()`/`EnemyUnitController.Attack()` 내부에만 있었는데,
거기서도 데미지 적용만 막을 뿐 그 앞에서 이미 `navMeshAgent.isStopped = true`(또는 공중 유닛은
`isMovingAirUnit = false`)로 멈추는 부분이 먼저 실행된 뒤였음. 그래서:

- 공격 사거리 안에 도메인이 안 맞는 대상이 들어오면 → `Attack()`이 호출되어 이동을 멈추지만 데미지는 안 들어감(제자리에 멈춰서 헛손질하는 것처럼 보임)
- 공격 사거리 밖(감지 범위 안)이면 → `Idle` 상태라 오히려 `ChaseTarget()`으로 쫓아가기까지 함 (더 심각)

## 수정 내용

`GetClosestEnemy()`(아군) / `GetClosestTarget()`(적)에서 대상을 고를 때, 이 유닛이 공격 가능한 도메인이
아니면 **후보에서 아예 제외**하도록 필터를 추가함 (`CanEngage()` 헬퍼로 유닛/건물의 공중 여부를 판정하고
`unitController.CanAttackDomain()` / `enemyUnit.CanAttackDomain()`으로 비교).

이제 도메인이 안 맞는 대상은 트리거에 감지는 되어도 "타겟"으로 선택되지 않으므로, `Attack()`/
`ChaseTarget()` 둘 다 호출되지 않는다 - 공격-이동 중이면 원래 목적지로 방해 없이 계속 이동하고, 실제로
공격 가능한 대상을 만났을 때만 멈춰서 교전한다.

## 변경 파일

- `Assets/Scripts/Unit/AttackRange.cs`
- `Assets/Scripts/Enemy/EnemyAttackRange.cs`
