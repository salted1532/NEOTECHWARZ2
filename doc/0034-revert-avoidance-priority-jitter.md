# 0034. avoidancePriority 랜덤 지터 되돌림 (진짜 원인은 NavMesh Bake 미세 언덕)

- 날짜: 2026-07-08

## 요청 내용

> avoidancePriority + - 5 랜덤 관련해서 수정해달라고 한거 다시 되돌려줘 문제를 발견했는데 navmesh surface로 bake 했을때 미세한 언덕이 발생했어 그래서 그거때문에 발생한거 같아 우선순위 문제가 아니였어 그래서 다시 되돌려줘

사용자가 직접 원인을 재확인한 결과, [[0031-ramp-unit-jitter-investigation]]에서 추정했던 avoidancePriority 동률/회피 교착이 아니라 **NavMesh Surface를 bake할 때 경사로에 미세한 언덕(요철)이 생긴 것**이 실제 원인이었음. [[0033-avoidance-priority-jitter-implementation]]에서 적용한 랜덤 지터는 이 문제와 무관하므로 되돌림.

## 코드 변경

### 기존 코드 (되돌리기 전, [[0033-avoidance-priority-jitter-implementation]] 적용 상태)

```csharp
[SerializeField] private int avoidancePriorityJitter = 5; // 생성 시 프리팹 기본값에서 +-이 범위로 무작위화 (동일 우선순위 유닛끼리의 회피 교착/진동 방지)

private void Awake()
{
    isWorker = CompareTag("Worker");
    attackRange = GetComponentInChildren<AttackRange>();

    if (!isAirUnit)
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        defaultAgentRadius = navMeshAgent.radius;

        int jitter = Random.Range(-avoidancePriorityJitter, avoidancePriorityJitter + 1); // -5~5 (양끝 포함)
        navMeshAgent.avoidancePriority = Mathf.Clamp(navMeshAgent.avoidancePriority + jitter, 0, 99);
    }
    else
    {
        targetPosition = transform.position + Vector3.up * 5f;
        isMovingAirUnit = true;
    }

}
```

### 변경 코드 (되돌린 후, `Assets/Scripts/Unit/UnitController.cs`)

```csharp
private void Awake()
{
    isWorker = CompareTag("Worker");
    attackRange = GetComponentInChildren<AttackRange>();

    if (!isAirUnit)
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        defaultAgentRadius = navMeshAgent.radius;
    }
    else
    {
        targetPosition = transform.position + Vector3.up * 5f;
        isMovingAirUnit = true;
    }

}
```

`avoidancePriorityJitter` 필드와 랜덤 지터 로직 전부 제거, [[0033-avoidance-priority-jitter-implementation]] 이전 상태로 완전 복원.

## 요약 / 남은 작업

- 실제 원인이 NavMesh Bake 시 경사로에 생긴 미세 언덕(요철)으로 밝혀짐 — 유닛이 그 요철을 타고 넘으며 y가 흔들리고, 여러 유닛이 겹쳐 보이면 "머리 위로 올라타는" 것처럼 보였던 것.
- 다음 조사/수정 방향은 NavMesh Bake 설정(Voxel Size, Agent Climb, Max Slope 등) 쪽이 될 가능성이 높음. 필요 시 후속 요청으로 진행.
- [[0031-ramp-unit-jitter-investigation]]에서 제안했던 formation 오프셋 수정은 이 되돌림과 별개 사안 — 사용자가 별도로 적용 여부 결정.

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` — `avoidancePriorityJitter` 필드 및 관련 로직 제거, [[0033-avoidance-priority-jitter-implementation]] 이전 상태로 복원
