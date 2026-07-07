# 0033. avoidancePriority 개체별 랜덤 지터 적용

- 날짜: 2026-07-08

## 요청 내용

> 그럼 avoidancePriority값을 현재 내가 설정해둔 avoidancePriority값에서 + - 5정도로 우선순위를 랜덤으로 만드는 코드를 UnitController에 추가해줘 이 우선순위 조정은 각 유닛별로 생성된 시점에 적용되었으면 좋겠어

([[0032-avoidance-priority-recommendation]]에서 논의한 대로, 같은 타입 유닛끼리는 avoidancePriority가 동일해서 타입 계층만으로는 진동 문제가 재발한다는 점을 확인 → 개체별 랜덤 편차를 코드로 추가)

## 조사 내용

- `UnitController.Awake()`에서 지상 유닛일 때 `navMeshAgent`를 캐싱하는 부분이 유닛 생성 시점에 한 번만 실행되는 지점이라, 여기서 `navMeshAgent.avoidancePriority`에 랜덤 편차를 더하면 "생성된 시점에 적용"이라는 요구사항과 맞음.
- 프리팹에 타입별로 미리 설정해둔 `avoidancePriority`(예: 차량 20~30, 보병 50, 일꾼 60~80)를 기준값으로 삼아 그 위에 `±5`를 더하고, 0~99 범위를 벗어나지 않도록 `Mathf.Clamp`로 보정.
- 공중 유닛(`isAirUnit`)은 `navMeshAgent` 자체가 없으므로(회피는 `SeparateFromOverlappingAirUnits`가 별도 처리) 이 로직에서 제외.

## 코드 변경 (제안)

### 기존 코드 (`Assets/Scripts/Unit/UnitController.cs`)

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

### 변경 코드 (제안)

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

- `avoidancePriorityJitter`는 인스펙터에 노출되는 `[SerializeField]`라 유닛 타입별 프리팹에서 값을 조정할 수 있음(기본 5).
- `Random.Range(int, int)`는 최댓값을 포함하지 않으므로 `avoidancePriorityJitter + 1`을 넘겨 `-5 ~ 5`가 모두 나오도록 함.

## 요약 / 남은 작업

- 사용자 확인 후 `Assets/Scripts/Unit/UnitController.cs`에 실제 반영 완료 (제안 코드와 동일하게 적용).
- 이 변경으로 같은 타입 유닛끼리도 개체별로 미세하게 다른 avoidancePriority를 갖게 되어, 동률로 인한 회피 교착/진동이 줄어듦.
- 전제조건: 프리팹별 기준 `avoidancePriority` 값(차량/보병/일꾼 계층)은 사용자가 별도로 설정.
- 실제 효과는 좁은 경사로에서 다수 유닛을 이동시켜 플레이 테스트로 확인 필요.

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` — `Awake()`에 `avoidancePriorityJitter` 필드 및 생성 시 랜덤 지터 적용 로직 추가
