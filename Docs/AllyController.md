# AllyController

`Assets/Scripts/FogOfWar/Ally/AllyController.cs`

## 개요

아군 OC(플레이어에게 적대적이지 않은 진영, 예: 구조된 OC 병사) 유닛 컨트롤러. `EnemyUnitController`를 상속하지 않고 이동/전투 AI 로직을 통째로 복제한 완전 독립 클래스다 — 아군 AI를 적 AI와 별개로 자유롭게 발전시킬 수 있도록 상속으로 묶어두지 않았다(doc/0452). `EnemyUnitController`와 기능적으로 거의 동일하며, 다른 부분은 피아식별 방향이 반대인 두 곳뿐이다: `Attack()`의 `isEnemyAttacker` 값과 `HandleAttacked()`의 반격 조건.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `enemyUnitID` | `EnemyUnitDataSO`(OC Unit Data SO)의 `UnitData.ID`와 매칭 — 아군 OC도 같은 로스터/SO를 재사용(doc/0447/0448) |
| `attackDamage`, `armor`, `attackType`, `armorType`, `sizeType` | 전투 스탯 — `ApplyUnitData()`로 SO 값을 덮어씀 |
| `canAttackGround`, `canAttackAir` | 공격 가능 도메인 제한 |
| `attackDelivery` | 공격 전달 방식(Hitscan/Projectile) |
| `isAirUnit`, `moveSpeed`, `airCruiseAltitude` | 공중 유닛 전용 이동 파라미터(`UnitController`와 동일한 방식 — NavMesh 미사용, 직접 좌표 보간) |
| `attackRange` | 자식의 `EnemyAttackRange`(실제로는 `AllyAttackRange`) — 사거리 내 대상 감지 |
| `attackMoveDestination` | 공격-이동 목적지, null이면 공격-이동 중이 아님 |
| `chaseIsUnreachable` | `ChaseTarget()`이 마지막으로 도달 불가로 판정했는지 — 도달 가능/불가 두 추격 모드를 가르는 값 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 자식/형제 컴포넌트 캐싱, 공중 유닛이면 시작하자마자 목표 고도로 떠오르도록 초기화 |
| `Start()` | `enemyUnitID`로 OC Unit Data SO를 조회해 `ApplyUnitData()`로 스스로 스탯 적용 |
| `HandleAttacked(damage, attackerPosition, attackType, isEnemyAttacker)` | 감지 범위 밖에서 공격받으면 공격 위치로 반격하러 감(`AttackMoveTo`) — `isEnemyAttacker=true`(실제 적대 세력)일 때만 반격, 플레이어의 오인사격에는 반격하지 않음(`EnemyUnitController`와 반대 조건) |
| `MoveTo(destination)` | 단순 이동 |
| `AttackMoveTo(destination)` | 공격-이동 — 이동 중 사거리에 들어오면 교전, 끝나면 원위치 이동 재개 |
| `ChaseTarget(pos)` | 사거리 밖 감지 대상 추격 — 도달 가능하면 매 프레임 재확인, 도달 불가면 가장 가까운 위치로 이동 후 도착 시에만 재확인(doc/0415) |
| `Attack(end, target)` | 사거리 내 대상 공격 — 데미지 계산·적용, 이펙트/SFX 재생, `isEnemyAttacker: false`로 데미지 전달 |
| `CalculateFinalDamage(target, targetArmor)` | `UnitController.CalculateFinalDamage`와 동일한 공식(장갑×크기 배율 + 고유 보너스), 연구소 전역 보너스는 미적용(OC 쪽 연구 시스템 없음) |
| `ApplyUnitData(data)` | OC Unit Data SO 값으로 스탯 덮어씀 — 이름/설명은 `LocalizationManager.GetTextOrFallback`으로 번역 조회(doc/0487) |
| `Die()` | 선택 목록에서 제거 후 파괴 |
| `IsIdle()` / `IsMove()` / `IsAttack()` / `CanAttackDomain()` | `IAttackRangeUnit` 계약 구현 — `AllyAttackRange`가 상태 조회/명령에 사용 |

## 연관 컴포넌트

- **AllyAttackRange**: 자식 오브젝트에서 사거리 내 적대 세력(외계종족)을 감지해 `Attack`/`ChaseTarget` 호출
- **EnemyUnitController**: 로직을 복제한 원본 — 상속 없이 별도 유지(피아식별 반대 방향)
- **AllyBuildingController**: 아군 OC 건물(단순 껍데기라 `EnemyBuildingController`를 그대로 상속)
- **RTSUnitController**: `selectedAllyList`로 아군 OC 선택 상태 관리, `GetEnemyUnitData`로 SO 조회 위임
