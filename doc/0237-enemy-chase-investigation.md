# 0237 - 추적 안 되는 문제 추가 조사 (공중 유닛 Rigidbody 수정 + 감지 반경 vs 사거리 역전 버그 수정)

## 요청

[[0236]] 수정 이후에도: 지상 유닛 중 특정 유닛은 여전히 추적할 생각을 안 하고, 공중 유닛은 사거리에
들어왔다 나가도 전혀 추적하지 않음. 원인 확인 요청.

## 코드 재검증 (상태머신)

`EnemyUnitController`/`EnemyAttackRange`의 상태 판단 로직(`IsIdle()` 게이트, `ChaseTarget()`/`Attack()`
호출 흐름)을 처음부터 다시 읽어봤는데, [[0236]]에서 제거한 `currentState = EnemyState.Attack;` 관련
코드는 지상/공중 유닛에 동일하게 적용되는 공용 로직이라 - 코드만 놓고 보면 양쪽 다 정상 동작해야 함.
그래서 추가로 다른 원인을 찾음.

## 발견: 공중 유닛 Rigidbody가 Kinematic이 아니었음

`Raven`/`Strike Drone`(및 다른 모든 적 프리팹)의 `Rigidbody`가 `m_IsKinematic: 0`(비-Kinematic)으로
설정되어 있었음. 지상 유닛은 `NavMeshAgent`가 이동을 전담해서 큰 문제가 없지만, 공중 유닛은
`EnemyUnitController.Update()`에서 **`transform.position`을 매 프레임 직접 대입**해서 움직인다
(`UnitController`의 공중 이동 로직을 그대로 이식한 것, [[0231]]).

Unity 공식 권장사항: 스크립트로 `transform.position`을 직접 바꿔서 움직이는 오브젝트는 Rigidbody를
**Kinematic**으로 설정해야 트리거 감지(`OnTriggerEnter`/`OnTriggerExit`)가 안정적으로 작동한다.
비-Kinematic Rigidbody는 물리 엔진이 직접 제어하도록 설계되어 있어서, 스크립트가 Transform을 강제로
덮어쓰면 물리 시뮬레이션과 어긋나 트리거 이벤트가 누락되거나 불안정해질 수 있음 - 특히 오래 정지해있다가
(Sleep 상태) Transform만 외부에서 바뀌는 경우 더 잘 발생하는 문제. 공중 유닛은 정확히 이 패턴(장시간
제자리 유지 → Transform만 스크립트로 갱신)에 해당해서, 감지 자체가 처음부터 불안정했을 가능성이 큼.

## 수정 내용

`Raven.prefab`, `Strike Drone.prefab`의 `Rigidbody.m_IsKinematic`을 `0` → `1`로 변경. (트리거
감지/발생 자체는 Kinematic이어도 그대로 작동함 - 물리 힘(중력/충돌 반응)만 안 받게 되는 것이라 부작용
없음.) 지상 유닛(`NavMeshAgent` 기반)은 이 문제의 대상이 아니라고 판단해 건드리지 않음.

## 진짜 원인 발견: 감지 반경이 사거리보다 좁았음

사용자 확인 결과 안 쫓아오는 지상 유닛은 **Heavy Assault Tank(사거리 20), Railgunner(사거리 20)** -
정확히 사거리가 가장 긴 유닛들이었음(공중 유닛 Raven=18, Strike Drone=20도 마찬가지로 김). 반면 정상
동작하는 Nanobot Repair(4), Brute Mech(2)는 사거리가 짧음. 이 패턴이 결정적 단서였음.

`EnemyAttackRange`의 감지(트리거) 콜라이더 반경은 모든 프리팹에서 **`10`으로 고정**돼 있었음
(TestEnemy를 복제한 흔적 - `UnitRange` 데이터 값과 전혀 연동되지 않는 별개의 값). 그런데 사거리
(`UnitRange`)가 18~20인 유닛은 감지 반경(10)보다 사거리가 더 넓다는 뜻 - 즉 "감지는 되는데 사거리
밖"인 구간이 아예 존재하지 않음. 대상은 감지 반경(10) 안에 들어오는 순간 이미 무조건 사거리(18~20) 안에도
들어와 있으므로 곧바로 `Attack()`이 호출되고, `ChaseTarget()`으로 가는 `else` 분기는 수학적으로 절대
실행될 수 없었음. 반대로 대상이 감지 반경(10) 밖으로 나가면 애초에 트리거 감지 자체가 끊겨서 마지막
위치를 쫓아갈 실마리도 없어짐. 사거리가 짧은 유닛(Nanobot Repair 4, Brute Mech 2)은 10보다 한참 작아서
"감지되지만 사거리 밖"인 구간이 충분히 넓게 존재해 정상 동작했던 것.

### 수정

`Assets/Scripts/Enemy/EnemyAttackRange.cs`
- `chaseDetectionMargin`(기본 2, 사용자 요청으로 10→2 축소) 필드 추가, `SyncDetectionRadius()` 메서드
  추가 - 감지 콜라이더 반경을 `UnitRange + chaseDetectionMargin`으로(현재보다 작아지지 않는 선에서)
  다시 계산해 적용.

`Assets/Scripts/Enemy/EnemyUnitController.cs`
- `ApplyUnitData()`에서 `UnitRange`를 SO 값으로 덮어쓴 직후 `attackRange.SyncDetectionRadius()` 호출.
- `Start()` 끝에도 `attackRange?.SyncDetectionRadius();`를 한 번 더 호출 - `RTSUnitController`에
  `Enemy Unit Database`가 아직 연결 안 돼 있어서(doc/0232) `ApplyUnitData`가 조용히 아무 것도 안 해도,
  프리팹에 직접 박혀있는 `UnitRange` 기준으로는 항상 감지 반경이 맞춰지도록 함.

이제 어떤 유닛이든(앞으로 추가될 유닛 포함) 사거리가 얼마든 감지 반경이 항상 사거리보다 넓게 자동으로
유지돼서, "사거리 밖이지만 감지되는" 추적 구간이 항상 존재함.

## 되돌림 (사용자 요청)

감지 반경을 사거리에 맞춰 자동으로 키우는 방식(`SyncDetectionRadius`)이 접근이 아닌 것 같다고 판단하셔서
`EnemyAttackRange.cs`/`EnemyUnitController.cs`의 관련 코드를 전부 되돌림 - `chaseDetectionMargin` 필드,
`detectionCollider` 참조, `SyncDetectionRadius()` 메서드, 그리고 `ApplyUnitData()`/`Start()`에서의 호출
전부 제거. 원인 진단(감지 반경 10 고정 vs 사거리 18~20) 자체는 doc 내용대로 유효하니, 다른 방식으로
해결할 때 참고할 것. 공중 유닛 Rigidbody Kinematic 수정과 doc/0236의 상태머신 수정은 되돌리지 않고 유지.

## 변경 파일 (최종)

- `Assets/prefabs/OC/Unit/Tier3/Raven.prefab` (Rigidbody Kinematic - 유지)
- `Assets/prefabs/OC/Unit/Tier3/Strike Drone.prefab` (Rigidbody Kinematic - 유지)
- `Assets/Scripts/Enemy/EnemyAttackRange.cs` (감지 반경 자동 확장 - 되돌림)
- `Assets/Scripts/Enemy/EnemyUnitController.cs` (감지 반경 동기화 호출 - 되돌림)
