# UnitController

`Assets/Scripts/Unit/UnitController.cs`

## 개요

개별 유닛(일꾼/전투유닛/공중유닛 포함)의 이동, 전투, 순찰, 자원 채취(일꾼 전용) 상태머신을 담당하는 핵심 컴포넌트. `NavMeshAgent` 기반 지상 이동과 직접 좌표 보간 기반 공중 이동을 모두 지원하며, `AttackRange`가 사거리 내 적을 감지하면 이 컴포넌트의 `Attack`/`ChaseTarget`을 호출한다.

## 상태 정의

| 상태머신 | 값 | 설명 |
|---|---|---|
| `UnitState` (private enum) | `Idle, Move, Attack` | 유닛의 큰 행동 상태 |
| `GatherState` (private enum) | `None, MovingToResource, WaitingInQueue, Gathering, MovingToBase, Depositing` | 일꾼 전용 자원 채취 세부 상태 |

## 주요 필드

| 필드 | 설명 |
|---|---|
| `unitMarker` | 선택 표시 마커 |
| `navMeshAgent` | 지상 유닛의 길찾기 에이전트 (`isAirUnit`이면 사용 안 함) |
| `moveSpeed`, `arriveDistance` | 이동 속도, 도착 판정 거리 |
| `targetPosition`, `isMovingAirUnit`, `isAirUnit` | 공중 유닛의 직접 좌표 이동에 사용 |
| `arrived` | 지상 유닛의 목적지 도착 여부 플래그 |
| `alreadyAttacked`, `timeBetweenAttacks` | 공격 쿨다운 처리 |
| `patrolling`, `goingToEnd`, `startPoint`, `endPoint` | 순찰 상태 |
| `isWorker` | "Worker" 태그 여부 (자원 채취 가능 여부 판정) |
| `amountPerTrip`, `gatherDuration` | 채취량, 채취 소요 시간 |
| `alternateResourceSearchRadius` | 목표 자원 대기열이 꽉 찼을 때 대체 자원을 찾는 반경(10) |
| `gatherTargetNode`, `gatherTimer`, `carryingAmount`, `carryingType` | 채취 진행 상태 |
| `DepositOre`, `DepositGas` | 자원을 들고 있음을 표시하는 오브젝트 (활성화 여부로 `IsCarryingResource` 판정) |
| `gatherInteractRange` | 채취/반납 상호작용 판정 거리 (장애물 특성상 `arriveDistance`보다 넉넉함) |
| `gatherAgentRadius`, `defaultAgentRadius` | 채취 중 서로 부딪히지 않도록 NavMeshAgent 반경을 임시로 축소 |

## 메소드

### 생명주기 / 이동 기반
| 메소드 | 설명 |
|---|---|
| `Awake()` | Worker 태그 판정, 지상 유닛이면 `NavMeshAgent` 캐싱, 공중 유닛이면 초기 목표 위치 설정 |
| `Start()` | 마커 비활성화, 일꾼이면 자원 표시 오브젝트 비활성화, `RTSUnitController.UnitList`에 등록 |
| `Update()` | 공중 유닛 이동 보간(`Vector3.MoveTowards` + 회전), 지상 유닛 도착 판정, 매 프레임 `GatherTick()` + `PatrolTick()` 호출 |

### 이동 / 명령
| 메소드 | 설명 |
|---|---|
| `SelectUnit()` / `DeselectUnit()` | 선택 마커 on/off |
| `MoveTo(end)` | 채취 취소 후 이동 상태로 전환. 지상은 NavMesh 목적지 설정, 공중은 `targetPosition` 갱신 |
| `AttackToGround(end)` | 지정 위치로 이동하되 Idle 상태 유지(적 발견 시 바로 공격하도록) |
| `AttackToUnit(end)` | Attack 상태로 전환하며 목표 위치로 이동 |
| `ChaseTarget(pos)` | 적을 추격하기 위한 이동 (Idle 상태 유지) — `AttackRange`가 사거리 밖의 Idle 유닛에 대해 호출 |
| `Attack(end, damage, enemy)` | 이동 정지, 대상 방향으로 회전, 쿨다운(`alreadyAttacked`) 확인 후 `HealthManager.GetDamage` 호출, `timeBetweenAttacks` 후 `ResetAttack` 예약 |
| `ResetAttack()` (private) | 공격 쿨다운 해제 |
| `RotateYOnly(target)` (private) | Y축만 회전시켜 대상을 바라보게 함 |
| `StopUnit()` | 채취 취소 후 Idle로 전환, 이동 정지 |
| `PatrolUnit(end)` | 현재 위치 ↔ 지정 위치 사이 순찰 시작 |
| `PatrolTick()` (private) | 순찰 중 도착 판정 시 시작점/끝점을 번갈아 목적지로 설정 |
| `HoldUnit()` | 채취 취소, Attack 상태로 전환하되 이동은 정지 (제자리에서 사거리 내 적만 공격) |

### 자원 채취 (일꾼 전용)
| 메소드 | 설명 |
|---|---|
| `Gather(node)` | 외부에서 호출하는 채취 명령 진입점. 전투 유닛이면 단순 이동으로 처리. 이미 자원을 들고 있으면 바로 반납하러 이동, 아니면 목표 노드로 이동 시작(`MovingToResource`) |
| `TryRedirectToNearbyResource(exclude)` (private) | 목표 노드 대기열이 꽉 찼거나 사라졌을 때, 반경 내 대기열 여유 있는 다른 노드로 재이동. 성공 시 true |
| `FindNearestAvailableResourceNode(maxDistance, exclude)` (private) | 반경 내에서 고갈되지 않고 혼잡하지 않은 가장 가까운 자원 노드 탐색 |
| `IsCarryingResource()` (private) | `DepositOre`/`DepositGas` 활성 여부로 자원 소지 여부 판정 |
| `ReturnCargo()` | UI "반환" 버튼 진입점. 자원을 들고 있을 때만 동작, 가장 가까운 건물로 이동(`MovingToBase`) |
| `MoveToBuilding(building)` | 건물 우클릭 명령: 자원을 들고 있으면 `ReturnCargo()`, 아니면 건물로 단순 이동 |
| `CancelGathering()` (private) | 채취 중단 + 제자리에 멈춰서 Idle로 (반납 건물이 없거나 노드가 사라진 경우) |
| `CancelGatheringForNewCommand()` (private) | 다른 명령이 들어와 채취를 중단시킬 때 호출. 대기열 등록 상태(`WaitingInQueue`/`Gathering`)면 자리를 비워주고, NavMeshAgent 반경을 원상복구 |
| `GatherTick()` (private) | 채취 상태머신의 매 프레임 처리: 이동 중 대기열 확인 → 대기 → 채취(타이머) → 채취 완료 후 반납 위치로 이동 → 반납. 노드가 채취 도중 사라지면 대체 자원 탐색 후 없으면 중단 |
| `Deposit()` (private) | 반납 처리: 캐싱해둔 `carryingType`으로 `RTSUnitController.AddOre/AddGas` 호출. 원래 노드가 남아있으면 복귀, 없으면 근처 새 자원 탐색, 둘 다 실패하면 `CancelGathering` |
| `DistanceToTarget(target)` (private) | 콜라이더가 있는 대상(건물 등)은 피벗이 아닌 표면(가장 가까운 지점) 기준으로 거리 판정 |
| `FindNearestDepositBuilding()` (private) | `RTSUnitController.BuildingList` 중 가장 가까운 건물을 반환 (자원 반납 대상) |

### 사망 / 상태 조회
| 메소드 | 설명 |
|---|---|
| `Die()` | 채취/대기열 중이었다면 자리 반납, `RTSUnitController.UnitList`에서 제거 후 파괴. `HealthManager`의 `IDestructible` 구현체로 호출됨 |
| `IsIdle()` / `IsMove()` / `IsAttack()` | `UnitState` 확인용 (주로 `AttackRange`에서 사용) |

## 연관 컴포넌트

- **AttackRange**: `Attack`/`ChaseTarget`/`IsAttack`/`IsIdle` 호출
- **HealthManager**: 사망 시 `IDestructible.Die()`로 이 컴포넌트의 `Die()`를 호출
- **ResourceNode**: 채취 상태머신에서 대기열 등록/채취(`JoinQueue`/`LeaveQueue`/`IsTurnToGather`/`Extract`) 호출
- **RTSUnitController**: `UnitList` 등록/해제, `AddOre`/`AddGas`, `BuildingList` 조회
- **BuildingController**: `MoveToBuilding`에서 목적지로 사용
