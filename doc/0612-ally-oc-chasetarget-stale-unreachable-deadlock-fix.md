# 0612. 아군 OC(및 적 AI) 공격 별동대가 적 건물/유닛을 마주친 뒤 멈추는 버그 수정

**날짜:** 2026-08-19

## 요청 내용
> 아군OC 유닛들이 적 건물을 공격을 안해 / 유닛 공격도 안하고 멈춰버리는 현상이 보이네 / 멀리서
> 적유닛이 공격해도 반격하러 가지도 않고 / b(공격 별동대가 진격 중 적 건물을 마주하면 그때부터
> 고장나는거 같아)의 경우인거 같아

## 조사 - 라이브 진단

Mission4 Play Mode에서 `execute-dynamic-code`로 실제로 멈춰있는 아군 OC 유닛(Cyborg Soldier
(Ally)(Clone))의 내부 상태를 리플렉션으로 직접 찍어봄(사용자가 Pause로 정확한 순간을 잡아준 덕분에
동일한 프레임을 반복 확인 가능했음 - `t=139.14, frame=14977` 고정):

```
chaseIsUnreachable=True
agent: isStopped=True, hasPath=True, pathPending=False, pathStatus=PathComplete,
       remainingDistance=13.29, stoppingDistance=1, velocity=(0,0,0)
engagedTarget=Spitter(9) @ dist=14.9m (실제 사거리 12 밖, 감지 반경 17 안)
NavMesh.CalculatePath(현재위치 → engagedTarget): ok=True, status=PathComplete
```

### 원인

`AllyController.ChaseTarget()`(`EnemyUnitController.cs`에도 완전히 동일하게 복제된 코드, doc/0452)의
"도달 불가 모드" 처리 구조 때문:

1. 유닛이 건물로 이동하던 중 사거리 안에 들어온 적 유닛(예: Spitter)과 교전 → `Attack()`이
   `navMeshAgent.isStopped = true`로 세팅(진짜 유닛 대상이라 "스쳐가는 건물" 취급 안 됨 - 정상 동작).
2. 그 적 유닛이 사거리 밖으로 빠져나감 → `EnemyAttackRange.Update()`가 `ChaseTarget()` 호출.
3. 그런데 `chaseIsUnreachable`(도달 불가 판정 플래그, `AllyController`/`EnemyUnitController` 인스턴스당
   **하나뿐**)이 **이전에 쫓던 다른 목표 기준으로 이미 `true`로 남아있는 상태** - `AllyAttackRange`는
   "사거리 안 유닛을 건물보다 항상 우선"하도록 설정돼 있어서(doc/0565) 건물↔유닛 타겟 전환이 잦고, 그
   과정에서 이 플래그가 새 목표와 무관하게 계속 재사용됨.
4. `chaseIsUnreachable == true`인 분기는 "아직 가장 가까운 도달 가능 지점에 도착 전"이라고 판단되면
   (`remainingDistance > stoppingDistance`) `hasPath`가 이미 있다는 이유로 `MoveAgentTo()`를 다시
   안 부름(doc/0391 - 매 프레임 재탐색 방지가 목적).
5. 하지만 `isStopped`는 1번에서 세팅된 `true` 그대로 - 실제로는 전혀 움직이지 않는데(`velocity=0`),
   코드는 "이동 중이라 아직 도착 안 함"으로 착각해 영원히 대기 → **완전 교착 상태**. 지금 당장
   `NavMesh.CalculatePath`로 재확인하면 분명히 갈 수 있는 길인데도, 판정 자체가 "이전 목표" 기준이라
   재시도가 아예 안 일어남.

## 수정

`ChaseTarget()` 진입 시, `chaseIsUnreachable`이 켜져 있어도 지금 쫓는 목표 위치(`pos`)가 마지막으로
이동을 지시했던 위치(`lastMoveAgentToDestination`)와 실질적으로 다르면(=대상이 바뀜) 그 판정을
재사용하지 않고 리셋 - `AllyController.cs`/`EnemyUnitController.cs` 두 곳 동일하게 반영(완전히 같은
복제 코드라 적 AI에도 같은 버그가 잠재해 있었음).

```csharp
// PrioritizeUnitTargets 등으로 교전 대상이 완전히 다른 위치로 바뀌었는데 chaseIsUnreachable이
// 이전 대상 기준으로 true인 채 남아있으면, 새 대상이 실제로는 도달 가능해도 "이미 포기한 상태"로
// 취급돼 아래에서 MoveAgentTo가 다시 안 불리고(hasPath만 있으면 재탐색 안 함) navMeshAgent.isStopped가
// 다른 곳(Attack())에서 true로 남은 채 영원히 멈춰버린다 - 새 목표면 이전 판정을 재사용하지 않는다.
if (chaseIsUnreachable && lastMoveAgentToDestination.HasValue &&
    (lastMoveAgentToDestination.Value - pos).sqrMagnitude > RedundantDestinationEpsilon * RedundantDestinationEpsilon)
{
    chaseIsUnreachable = false;
}

if (chaseIsUnreachable)
{
    ...
}
```

리셋 후 `chaseIsUnreachable == false` 분기(도달 가능 모드)로 자연히 떨어지고, 그 분기는
`MoveAgentTo(pos)`를 무조건 호출하므로 `isStopped`가 정상적으로 `false`로 풀리고 새 경로가 잡힌다.

## 부수 작업 - 테스트 편의용 임시 생산 시간 단축

같은 세션에서 요청받아, Play Mode 런타임에서만(에셋 파일 미수정) `OC Unit Data SO`의 9개 유닛
`productionTime`을 전부 1초로 리플렉션으로 낮춰둠(`<productionTime>k__BackingField`) - Play Mode를
끄면 자동으로 원래 값(18~95초)으로 복귀. `EnemyAIDirector`(적대 OC)도 같은 데이터를 참조하므로 영구
반영은 하지 않기로 확인받음.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 49`(기존 베이스라인과 동일 - 새 경고 없음).
- 컴파일에 의한 도메인 리로드 이후에도 Play Mode 유지됨(사용자가 Pause 상태였음) 확인, 임시
  productionTime 오버라이드도 유지됨 확인.
- 사용자가 Pause 해제 후 실제 플레이로 재확인 - 아군 OC 공격 별동대가 적 건물/유닛을 마주친 뒤에도
  더 이상 멈추지 않고 정상적으로 교전/진격함을 확인("잘 반영된거 같네").

## 변경된 파일
- `Assets/Scripts/FogOfWar/Ally/AllyController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
