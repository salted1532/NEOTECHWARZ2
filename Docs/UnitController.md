# UnitController

`Assets/Scripts/Unit/UnitController.cs`

## 개요

개별 유닛(일꾼/전투유닛/공중유닛 포함)의 이동, 전투, 순찰, 자원 채취(일꾼 전용), 아군 따라다니기, 건설 이동/건설 진행 상태머신을 담당하는 핵심 컴포넌트. `NavMeshAgent` 기반 지상 이동과 직접 좌표 보간 기반 공중 이동을 모두 지원하며, `AttackRange`가 사거리 내 적을 감지하면 이 컴포넌트의 `Attack`/`ChaseTarget`을 호출한다.

## 상태 정의

| 상태머신 | 값 | 설명 |
|---|---|---|
| `UnitState` (private enum) | `Idle, Move, Attack` | 유닛의 큰 행동 상태. `Idle`이어야 `AttackRange`가 사거리 내 적을 자동으로 교전(공격/추격)한다 |
| `GatherState` (private enum) | `None, MovingToResource, WaitingInQueue, Gathering, MovingToBase, Depositing` | 일꾼 전용 자원 채취 세부 상태 |

## 주요 필드

| 필드 | 설명 |
|---|---|
| `unitMarker` | 선택 표시 마커 (공격 대상 지정/따라다니기 지정 시 깜빡임에도 재사용) |
| `icon`, `unitID` | Squad_panel 등 선택 UI 아이콘, `RTSUnitController.GetUnitName`으로 이름 조회 시 쓰는 ID |
| `attackDamage`, `armor` | 전투 스탯. Info_panel 호버 툴팁에도 사용 |
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
| `airSeparationRadius`, `airSeparationSpeed` | 정지/공격 중인 공중 유닛끼리 겹치지 않게 밀어내는 반경/속도 |
| `chaseLoseSightRange` | 지정 추격 대상과 이 거리 이상 벌어지면 "시야 이탈"로 간주 |
| `orderedTarget`, `hasEngagedOrderedTarget` | 우클릭/A모드로 지정된 적 추격 대상, 한 번이라도 사거리 접촉했는지 |
| `attackMoveDestination` | 공격-이동 목적지 / 추격 중 마지막 확인 위치(교전 후 복귀 지점) |
| `attackRange` | 자식 `AttackRange` 캐시 |
| `friendlyTarget`, `hasFriendlyOrder` | 아군 강제 공격 대상(A모드 아군 좌클릭). `UnitController` 또는 `BuildingController` 모두 받을 수 있게 `MonoBehaviour` 타입 |
| `markerFlashRoutine`, `markerFlashInterval`, `markerFlashCount` | 공격/따라다니기 대상 지정 피드백 마커 깜빡임 |
| `followTarget`, `hasFollowOrder` | 아군 유닛 우클릭 "계속 따라다니기" 대상 (공격 명령이 아니라 Idle 상태 유지) |
| `buildInteractRange`, `buildDestination`, `onBuildArrived`, `onBuildCancelled`, `hasBuildOrder` | 건설모드에서 건물 위치로 이동하는 "건설 이동" 상태 |
| `attachedStructure`, `isConstructing` | 현재 붙어서 건설 중인 `BaseStructure` 및 건설 잠금 플래그 |

## 메소드

### 생명주기 / 이동 기반
| 메소드 | 설명 |
|---|---|
| `Awake()` | Worker 태그 판정, 자식 `AttackRange` 캐싱, 지상 유닛이면 `NavMeshAgent` 캐싱, 공중 유닛이면 초기 목표 위치 설정 |
| `Start()` | 마커 비활성화, 일꾼이면 자원 표시 오브젝트 비활성화, `RTSUnitController.UnitList`에 등록 |
| `Update()` | 공중 유닛 이동 보간(`Vector3.MoveTowards` + 회전) + 도착 판정, 지상 유닛 도착 판정, `GatherTick`/`PatrolTick`/`AttackOrderTick`/`FriendlyAttackTick`/`FollowTick`/`BuildTick`을 매 프레임 호출 후 공중 유닛이면 `SeparateFromOverlappingAirUnits()` |
| `SeparateFromOverlappingAirUnits()` (private) | 정지/공격 중인(이동 중이 아닌) 공중 유닛끼리만 겹친 만큼 수평으로 밀어냄 |

### 선택 / 피드백
| 메소드 | 설명 |
|---|---|
| `SelectUnit()` / `DeselectUnit()` | 선택 마커 on/off |
| `FlashMarker()` | 공격/따라다니기 대상으로 지정됐을 때 마커를 0.3초 간격 3회 깜빡임 (끝나면 실제 선택 상태로 복원) |

### 이동 / 일반 명령 (건설 중엔 전부 무시됨 — `isConstructing` 가드)
| 메소드 | 설명 |
|---|---|
| `MoveTo(end)` | 채취/공격 명령 취소 후 이동 상태로 전환 |
| `StopUnit()` | 채취/공격 명령 취소 후 Idle로 전환, 이동 정지 |
| `PatrolUnit(end)` / `PatrolTick()` (private) | 현재 위치 ↔ 지정 위치 사이 순찰 시작/진행 |
| `HoldUnit()` | 채취/공격 명령 취소, Attack 상태로 전환하되 이동은 정지 (제자리에서 사거리 내 적만 공격) |
| `MoveAgentTo(destination)` (private) | 지상/공중 이동 로직 공용 헬퍼 |
| `RotateYOnly(target)` (private) | Y축만 회전시켜 대상을 바라보게 함 |

### 공격 명령 (우클릭 적 지정 / A 모드, 건설 중엔 무시됨)
| 메소드 | 설명 |
|---|---|
| `AttackUnitTarget(target)` | 특정 적을 추격 공격 대상으로 지정 |
| `AttackMoveTo(destination)` | 공격-이동(A모드 땅 클릭): Idle 상태를 유지해 도중에 만나는 적도 자동 교전 |
| `AttackFriendlyTarget(target)` | 아군 유닛/건물 강제 공격(A모드 아군 좌클릭). 시야 이탈 개념 없이 대상이 죽을 때까지 추격 |
| `FriendlyAttackTick()` (private) | 아군 강제 공격을 매 프레임 갱신(사거리 안이면 공격, 밖이면 계속 추격, 대상 사망 시 Idle 복귀) |
| `AttackOrderTick()` (private) | 지정 추격 대상/공격-이동을 매 프레임 갱신 — 시야 이탈 판정, 교전 종료 후 이동 재개 |
| `GetOrderedTarget()` | 현재 지정 추격 대상 반환 (`AttackRange`에서 조회) |
| `ChaseTarget(pos)` | 사거리 밖 Idle 유닛을 `AttackRange`가 대상 쪽으로 추격 이동시킬 때 호출 |
| `Attack(end, enemy)` | 이동 정지, 대상 방향 회전, 쿨다운 확인 후 `HealthManager.GetDamage(attackDamage)` 호출 |
| `ResetAttack()` (private) | 공격 쿨다운 해제 |

### 아군 따라다니기 (우클릭 아군 유닛, 건설 중엔 무시됨)
| 메소드 | 설명 |
|---|---|
| `FollowUnit(target)` | 지정한 아군을 계속 따라다님 — Attack이 아니라 Idle 상태를 유지해 도중에 만나는 적은 `AttackRange`가 알아서 자동 교전 |
| `FollowTick()` (private) | 매 프레임 대상 위치로 이동 갱신, 교전 중(`AttackRange.HasEnemyInRange`)이면 유지, 대상이 죽으면 그 자리에 정지 |

### 건설 이동 / 건설 진행 (`isConstructing` 관련)
| 메소드 | 설명 |
|---|---|
| `GoBuild(destination, onArrived, onCancelled)` | 건설 위치(신규 배치 지점 또는 기존 `BaseStructure`)로 이동. `buildInteractRange` 안에 들어오면 `onArrived` 실행, 도중 다른 명령으로 대체되면 `onCancelled` 실행. 건설 중엔 무시됨 |
| `CancelBuildOrder()` (private) | 진행 중이던 건설 이동을 취소하고 `onCancelled` 콜백 실행 (다른 명령이 들어올 때 `CancelAttackOrder`에서 호출) |
| `BuildTick()` (private) | 건설 이동을 매 프레임 갱신, 목적지 근접 시 `onArrived` 실행 후 Idle 전환 |
| `BeginConstruction(structure)` | `BaseStructure`에 도착해서 건설을 시작(또는 재개)할 때 호출(`GoBuild`의 `onArrived`에서). `isConstructing = true`로 잠그고 `structure.AttachBuilder(this)` 호출. `structure`가 이미 파괴됐으면 아무 것도 안 함 |
| `FinishConstruction()` | 건설이 끝나거나 다른 일꾼으로 교체돼 담당에서 풀렸을 때 `BaseStructure`가 호출 — 잠금 해제 |
| `IsConstructing()` | 건설 중 여부 (건설 중엔 `MoveTo`/`AttackUnitTarget`/`AttackMoveTo`/`AttackFriendlyTarget`/`FollowUnit`/`GoBuild`/`StopUnit`/`PatrolUnit`/`HoldUnit`/`Gather`/`ReturnCargo`/`MoveToBuilding` 12개 명령 메소드가 전부 첫 줄에서 조용히 반환됨) |

### 자원 채취 (일꾼 전용, 건설 중엔 무시됨)
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

### 명령 취소 헬퍼
| 메소드 | 설명 |
|---|---|
| `CancelAttackOrder()` (private) | 지정 추격/아군 강제공격/공격-이동/따라다니기 대상을 전부 초기화하고 `CancelBuildOrder()`도 호출 — 다른 종류의 명령이 새로 들어올 때 공용으로 호출 |

### 사망 / 상태 조회
| 메소드 | 설명 |
|---|---|
| `Die()` | 채취/대기열 중이었다면 자리 반납, `RTSUnitController.UnitList`/`selectedUnitList`에서 제거 후 파괴. `HealthManager`의 `IDestructible` 구현체로 호출됨 |
| `IsIdle()` / `IsMove()` / `IsAttack()` | `UnitState` 확인용 (주로 `AttackRange`에서 사용) |
| `GetIcon()` / `GetUnitID()` / `GetAttackDamage()` / `GetArmor()` | Info_panel/Squad_panel 표시용 조회 |

## 연관 컴포넌트

- **AttackRange**: `Attack`/`ChaseTarget`/`IsAttack`/`IsIdle`/`GetOrderedTarget`/`HasEnemyInRange` 호출·참조
- **HealthManager**: 사망 시 `IDestructible.Die()`로 이 컴포넌트의 `Die()`를 호출, 공격 시 대상의 `GetDamage()` 호출
- **ResourceNode**: 채취 상태머신에서 대기열 등록/채취(`JoinQueue`/`LeaveQueue`/`IsTurnToGather`/`Extract`) 호출
- **RTSUnitController**: `UnitList` 등록/해제, `AddOre`/`AddGas`, `BuildingList` 조회
- **BuildingController**: `MoveToBuilding`에서 목적지로 사용, `AttackFriendlyTarget`의 대상 타입
- **BaseStructure**: `GoBuild`로 이동한 뒤 `BeginConstruction`으로 붙고, 완공/취소/교체 시 `FinishConstruction`으로 풀려남
- **PlacementSystem**: 신규 배치 시 `GoBuild`를 호출해 일꾼을 건설 위치로 보냄
