# EnemyAttackRange

`Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`

## 개요

적 유닛(`EnemyUnitController`)의 자식 오브젝트(트리거 콜라이더)에 부착되어 사거리 내 상대를 자동 감지·공격·추격한다. 플레이어 쪽 `AttackRange`를 반대 방향(플레이어 유닛/건물을 감지)으로 뒤집은 축소판 — 지정 대상 강제 추격 개념은 없고, 항상 "사거리 안의 가장 가까운 대상"만 본다(doc/0231). `AllyAttackRange`가 이 클래스를 그대로 상속해서 감지 대상 태그만 바꿔 재사용한다(doc/0448).

## IAttackRangeUnit 인터페이스

`EnemyUnitController`와 `AllyController`(서로 비상속)가 각자 구현하는 계약 — 하나의 `EnemyAttackRange`/`AllyAttackRange` 감지 로직을 두 컨트롤러 타입 모두에 재사용할 수 있게 한다: `IsAttack()`, `IsIdle()`, `Attack(end, target)`, `ChaseTarget(pos)`, `CanAttackDomain(targetIsAirUnit)`.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `UnitRange` | 실제 공격 사거리 |
| `targetTags` | 감지 대상 태그 목록(protected, 하위 클래스가 재정의 가능) — 기본값은 플레이어 진영 + 아군 OC("AllyOC"), `AllyAttackRange`는 `["Enemy"]`로 재정의 |
| `engagedTarget` | 한 번 교전을 시작한 대상 — 감지 반경+완충 밖으로 완전히 벗어나기 전까지 계속 우선시(멈칫거림 방지, doc/0388) |
| `unreachableTarget` | `ChaseTarget()`이 도달 불가로 포기한 대상 — 감지 범위를 완전히 벗어나기 전까지 후보에서 제외(doc/0398) |
| `DetectionRangeMargin` | 감지 콜라이더 반경이 `UnitRange + margin` 이상이 되도록 보장하는 안전장치(doc/0239) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 부모의 `IAttackRangeUnit` 캐싱, 감지 콜라이더 반경 보정 |
| `EnsureDetectionRadius()` | `UnitRange`가 바뀔 때마다 함께 호출해 감지 콜라이더 최소 반경 유지 |
| `Update()` | 매 프레임 대상 조회 → 사거리 안이면 `Attack()`, 밖이지만 Idle이면 `ChaseTarget()` → 도달 불가 판정되면 `unreachableTarget`으로 기록 |
| `GetEngagedOrClosestTarget()` | 이미 물고 있던 대상을 우선(멈칫거림 방지), 없으면 `GetClosestTarget()` |
| `GetClosestTarget()` | 감지된 대상 중 최근접(공격 불가 도메인/도달 불가 대상은 후보 제외) |
| `GetTrackingTarget()` | 포탑(`TurretController`)이 조준 대상을 물어볼 때 사용 |
| `CanEngage(target)` | 대상이 공격 도메인(지상/공중)에 맞는지, 은신 유닛은 감지 자체를 못 함(doc/0323) |
| `HasTargetInRange` / `HasTargetInAttackRange` | 각각 "감지 콜라이더 안(교전 판정용)" / "실제 사거리 안(애니메이션 Fire 파라미터용, doc/0253)" |

## 연관 컴포넌트

- **EnemyUnitController / AllyController**: `IAttackRangeUnit` 구현체로서 이 컴포넌트에게 상태 조회/명령을 받음
- **AllyAttackRange**: 이 클래스를 상속해 `targetTags` 기본값만 재정의
- **TurretController**: `GetTrackingTarget()`으로 조준 대상 조회
