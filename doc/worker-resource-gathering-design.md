# 일꾼 채취(Gather) / 일반 유닛 이동 설계 (Ore, Gas 우클릭)

작성일: 2026-07-03 (개정)
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

> **개정 사항**: 최초안은 일꾼 여부 판단과 채취 로직을 별도 `WorkerController`
> 컴포넌트로 분리했었다. 이번 개정에서는 요청에 따라 **일꾼인지 아닌지 판단하는
> 로직 자체를 `UnitController` 내부로 통합**한다. `UserControl`/`RTSUnitController`는
> "채취해라"라는 명령만 그대로 전달하고, "나는 일꾼인가 아닌가 → 그래서 채취할지
> 이동만 할지"는 전부 `UnitController.Gather()` 안에서 결정한다.

## 1. 목표

`UserControl.HandleRightClick()`(`UserControl.cs`)의 빈 `clickedOre`/`clickedGas`
블록에서, 선택된 유닛들에게 "이 자원을 채취(또는 이동)하라"는 명령 하나만 내려보낸다.

- `UserControl` → `RTSUnitController.GatherSelectedUnits(node)` 호출만 함. 일꾼인지
  아닌지 전혀 모름.
- `RTSUnitController` → 선택된 유닛 각각에게 `unit.Gather(node)`만 그대로 전달.
  역시 일꾼인지 아닌지 모름 (분기 없음).
- `UnitController.Gather(node)` → **여기서만** 자기 자신이 일꾼인지 판단:
  - 일꾼이면 채취 상태로 들어가서 "자원까지 이동 → 채취 대기 → 기지로 복귀 →
    자원 반납"을 반복한다.
  - 일꾼이 아니면(전투 유닛 등) 그냥 `MoveTo(node 위치)`로 처리한다.

[[resource-manager-design]]에서 만든 `ResourceManager.AddOre/AddGas`가 실제 자원
반납 지점에서 호출된다.

## 2. 일꾼 판별 기준

기존 `RTSUnitController.SelectUnit()`(`RTSUnitController.cs:136-144`)이 이미
`unit.tag == "Worker"`로 일꾼 여부를 판별하고 있으므로, 동일한 기준을 그대로
`UnitController` 안에서 재사용한다 (새로운 판별 규칙을 만들지 않음).

```csharp
private bool isWorker;

private void Awake()
{
    isWorker = CompareTag("Worker");
    ...
}
```

## 3. 필요한 데이터 컴포넌트: `ResourceNode`

Ore/Gas 오브젝트에 부착. 지금은 레이어(`layerOre`, `layerGas`)로만 구분되고 있어서
"이 노드에 자원이 얼마나 남았는지"를 들고 있을 곳이 없다.

```csharp
using UnityEngine;

public enum ResourceType { Ore, Gas }

public class ResourceNode : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int remainingAmount = 1000;

    public ResourceType Type => resourceType;
    public bool IsDepleted => remainingAmount <= 0;

    public int Extract(int amountPerTrip)
    {
        int taken = Mathf.Min(amountPerTrip, remainingAmount);
        remainingAmount -= taken;
        return taken;
    }
}
```

## 4. `UnitController` 내부 통합 로직

기존 `PatrolTick()`(`UnitController.cs:300`)이 `Update()`에서 매 프레임 호출되며
자기 상태(`patrolling`)를 스스로 갱신하는 것과 동일한 패턴으로 `GatherTick()`을
추가한다. 채취 진행 상태(`GatherState`)는 기존 `UnitState`(Idle/Move/Attack)와
별개로 둔다 — `MoveTo()`가 이동 도중 `UnitcurrentState`를 Move↔Idle로 자동
전환시키는 기존 로직(`UnitController.cs:107-115`)을 그대로 두고, `GatherTick()`은
그 결과(`IsIdle()`)를 "도착 신호"로만 관찰한다.

```csharp
// ===== 필드 추가 =====
private bool isWorker;

private enum GatherState { None, MovingToResource, Gathering, MovingToBase, Depositing }
private GatherState gatherState = GatherState.None;

[SerializeField] private int amountPerTrip = 8;
[SerializeField] private float gatherDuration = 3f;

private ResourceNode gatherTargetNode;
private float gatherTimer;
private int carryingAmount;

private RTSUnitController rtsController;   // 기존 Start()에서 지역변수였던 것을 필드로 승격
private ResourceManager resourceManager;

// ===== Awake / Start 수정 =====
private void Awake()
{
    isWorker = CompareTag("Worker");

    if (!isAirUnit) { navMeshAgent = GetComponent<NavMeshAgent>(); }
    else { targetPosition = transform.position + Vector3.up * 5f; isMovingAirUnit = true; }
}

void Start()
{
    unitMarker.SetActive(false);

    rtsController = FindFirstObjectByType<RTSUnitController>();
    rtsController.UnitList.Add(this);

    resourceManager = FindFirstObjectByType<ResourceManager>();
}

// ===== Update() 안에 한 줄 추가 =====
// PatrolTick();
// GatherTick();   // 이 줄 추가

// ===== 외부에서 호출하는 유일한 진입점 =====
public void Gather(ResourceNode node)
{
    if (!isWorker)
    {
        MoveTo(node.transform.position); // 전투 유닛은 그냥 이동 명령으로 처리
        return;
    }

    patrolling = false;
    gatherTargetNode = node;
    MoveTo(node.transform.position);
    gatherState = GatherState.MovingToResource;
}

// ===== 채취 상태 머신 =====
private void GatherTick()
{
    if (gatherState == GatherState.None)
        return;

    switch (gatherState)
    {
        case GatherState.MovingToResource:
            if (IsIdle()) // MoveTo()가 도착 시 자동으로 Idle 전환시키는 걸 그대로 관찰
            {
                gatherTimer = gatherDuration;
                gatherState = GatherState.Gathering;
            }
            break;

        case GatherState.Gathering:
            gatherTimer -= Time.deltaTime;
            if (gatherTimer <= 0f)
            {
                carryingAmount = gatherTargetNode.Extract(amountPerTrip);

                Transform depositTarget = FindNearestDepositBuilding();
                if (depositTarget == null)
                {
                    gatherState = GatherState.None; // 반납할 건물이 없으면 채취 중단
                    return;
                }

                MoveTo(depositTarget.position);
                gatherState = GatherState.MovingToBase;
            }
            break;

        case GatherState.MovingToBase:
            if (IsIdle())
                gatherState = GatherState.Depositing;
            break;

        case GatherState.Depositing:
            Deposit();
            break;
    }
}

private void Deposit()
{
    if (gatherTargetNode.Type == ResourceType.Ore)
        resourceManager.AddOre(carryingAmount);
    else
        resourceManager.AddGas(carryingAmount);

    carryingAmount = 0;

    if (gatherTargetNode == null || gatherTargetNode.IsDepleted)
    {
        gatherState = GatherState.None; // 노드 고갈 → 대기 (7절 열린 질문 참고)
        return;
    }

    MoveTo(gatherTargetNode.transform.position);
    gatherState = GatherState.MovingToResource;
}

private Transform FindNearestDepositBuilding()
{
    BuildingController nearest = null;
    float nearestSqrDist = float.MaxValue;

    foreach (BuildingController building in rtsController.BuildingList)
    {
        if (building == null) continue;

        float sqrDist = (building.transform.position - transform.position).sqrMagnitude;
        if (sqrDist < nearestSqrDist)
        {
            nearestSqrDist = sqrDist;
            nearest = building;
        }
    }

    return nearest != null ? nearest.transform : null;
}
```

## 5. `RTSUnitController` — 그냥 전달만 함 (분기 없음)

```csharp
public void GatherSelectedUnits(ResourceNode node)
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].Gather(node);
    }
}
```

기존 `MoveSelectedUnits`/`AttackSelectedUnits`/`PatrolSelectedUnits`
(`RTSUnitController.cs:167-195`)과 동일한 모양 — 전부 그냥 반복문으로 각 유닛의
메소드 하나씩 호출하는 패턴을 그대로 따른다.

## 6. `UserControl` 연결

`HandleRightClick()`의 빈 블록 채우기 (`clickedOre`, `clickedGas` 아래):

```csharp
if (clickedOre)
{
    ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();
    if (node != null)
        rtsUnitController.GatherSelectedUnits(node);
}

if (clickedGas)
{
    ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();
    if (node != null)
        rtsUnitController.GatherSelectedUnits(node);
}
```

## 7. 전체 흐름 요약

```
우클릭 (Ore/Gas)
  └─ UserControl.HandleRightClick()
       └─ RTSUnitController.GatherSelectedUnits(node)   ← 그냥 전달만
            └─ 각 UnitController.Gather(node)            ← 일꾼 판단은 여기서만
                 ├─ isWorker == false → MoveTo(node 위치)
                 └─ isWorker == true  → GatherTick() 상태머신 시작
                       MovingToResource → Gathering → MovingToBase → Depositing
                       → ResourceManager.AddOre/AddGas → 다시 MovingToResource (반복)
```

## 8. 판단이 필요한 열린 질문

1. **자원 반납 건물을 어떻게 찾을지 (`FindNearestDepositBuilding`).**
   - (a) `rtsController.BuildingList`에서 특정 태그(`"MainBase"`)만 필터링 후 최근접
   - (b) 지금 코드처럼 그냥 아무 건물이나 최근접
   - (c) 일꾼을 생산한 건물(출신 건물)로 고정
2. **자원 노드가 채취 도중 고갈되면?** 현재 설계는 반납 후 고갈 상태면 그냥
   `GatherState.None`으로 멈춘다. 근처 다른 노드를 자동 탐색해 이어가게 하려면
   로직 추가 필요.
3. **여러 일꾼이 같은 노드에 동시에 채취해도 되는지 (슬롯 제한).** 지금은 무제한
   동시 채취 가능. 노드당 동시 채취 인원 제한을 두려면 `ResourceNode`에 예약
   카운트가 필요.
4. **채취 도중 다른 명령(이동/공격 등)을 받으면?** 지금 `Gather()`는 `gatherState`를
   덮어쓰지 않고 그대로 두는 `MoveTo`/`AttackToGround` 등과 상호작용이 정의돼있지
   않다. 예를 들어 채취 중인 일꾼에게 `MoveTo()`를 직접 호출하면 `gatherState`는
   `MovingToResource`/`MovingToBase` 등으로 남아있는 채 다음 `IsIdle()` 타이밍에
   채취 상태머신이 계속 돌아버릴 수 있음. `MoveTo`/`AttackTo*` 계열 메소드 시작
   지점에서 `gatherState = GatherState.None`으로 초기화해주는 처리가 필요.
5. **왕복 이동 없이 즉시 채취(단순화 버전)로 갈지.** 왕복 없이 "자원 앞 도착 시
   일정 시간마다 즉시 반납" 식으로 단순화하면 `MovingToBase`/`Depositing`/
   `FindNearestDepositBuilding`이 통째로 필요 없어짐.

## 9. (개정) 채취 자원 시각 표시 — `DepositOre` / `DepositGas`

현재 `UnitController.cs:67-68`에 필드만 추가되어 있는 상태다.

```csharp
private GameObject DepositOre;
private GameObject DepositGas;
```

원하는 동작: **채취(Extract) 직후, 캐낸 자원 종류에 맞는 오브젝트를 활성화**(예:
일꾼이 광물/가스를 짊어진 모습을 보여주는 자식 오브젝트)하고, **`Deposit()`으로
반납이 끝나면 다시 비활성화**한다. 기본 상태는 둘 다 `false`.

### 3-1. 필드에 `[SerializeField]` 추가 (인스펙터에서 오브젝트 연결 필요)

```csharp
[SerializeField] private GameObject DepositOre;
[SerializeField] private GameObject DepositGas;
```

### 3-2. 기본 상태 `false` 보장 — `Start()`

기존 `unitMarker.SetActive(false)`(`UnitController.cs:89`)와 같은 자리에 추가한다.

```csharp
void Start()
{
    unitMarker.SetActive(false);
    DepositOre.SetActive(false);
    DepositGas.SetActive(false);

    rtsController = FindFirstObjectByType<RTSUnitController>();
    rtsController.UnitList.Add(this);

    resourceManager = FindFirstObjectByType<ResourceManager>();
}
```

### 3-3. 채취 성공 시 활성화 — `GatherTick()`의 `Gathering` 분기

`carryingAmount = gatherTargetNode.Extract(amountPerTrip);` 바로 다음
(`UnitController.cs:415` 인근)에 타입에 따라 활성화한다.

```csharp
case GatherState.Gathering:
    gatherTimer -= Time.deltaTime;
    if (gatherTimer <= 0f)
    {
        carryingAmount = gatherTargetNode.Extract(amountPerTrip);

        if (gatherTargetNode.Type == ResourceType.Ore)
            DepositOre.SetActive(true);
        else
            DepositGas.SetActive(true);

        Transform depositTarget = FindNearestDepositBuilding();
        if (depositTarget == null)
        {
            gatherState = GatherState.None;
            return;
        }

        MoveTo(depositTarget.position);
        gatherState = GatherState.MovingToBase;
    }
    break;
```

### 3-4. 반납 완료 시 비활성화 — `Deposit()`

`resourceManager.AddOre/AddGas` 호출 직후(`UnitController.cs:442-445` 인근)에
같은 타입 판정으로 꺼준다.

```csharp
private void Deposit()
{
    if (gatherTargetNode.Type == ResourceType.Ore)
    {
        resourceManager.AddOre(carryingAmount);
        DepositOre.SetActive(false);
    }
    else
    {
        resourceManager.AddGas(carryingAmount);
        DepositGas.SetActive(false);
    }

    carryingAmount = 0;

    if (gatherTargetNode == null || gatherTargetNode.IsDepleted)
    {
        gatherState = GatherState.None;
        return;
    }

    MoveTo(gatherTargetNode.transform.position);
    gatherState = GatherState.MovingToResource;
}
```

### 열린 질문

- **채취 중단 시나리오.** 8절 열린 질문 4번(채취 도중 다른 명령을 받는 경우)이
  아직 해결 안 된 상태라, 만약 `DepositOre/Gas`가 켜진 채로(=자원을 짊어진 채로)
  `MoveTo`/`Attack` 등으로 상태가 강제 전환되면 오브젝트가 켜진 채로 남아있을 수
  있다. 채취 상태를 벗어나는 모든 경로(피격으로 사망 등 포함)에서 두 오브젝트를
  같이 꺼주는 공통 처리(`ResetGatherVisual()` 같은 헬퍼)가 필요할 수 있음.
- **일꾼이 아닌데도 필드가 존재.** `DepositOre`/`DepositGas`는 `isWorker`가
  아닌 유닛에는 의미가 없는데, 지금 필드는 `UnitController` 전체에 붙어있어
  전투 유닛 프리팹에도 인스펙터에 노출된다. 기능상 문제는 없지만, 전투 유닛
  프리팹에는 값을 비워둘지(`null`이면 `Deposit`/`GatherTick`에서 절대 접근 안
  하므로 안전) 확인 필요.

## 10. (개정) NavMeshObstacle 때문에 목적지에 절대 "도착"하지 못하는 문제

### 원인

`ResourceNode`(Ore/Gas)와 건물은 `NavMeshObstacle`로 설정돼 있다
(`PlacementSystem.cs:85`에서 건물 배치 시 `obstacle.enabled = true`로 다시 켜는
코드가 있는 것으로 확인됨). `NavMeshObstacle`은 그 위치 주변의 NavMesh를
"carve"(구멍을 뚫듯 깎아냄)해서, 에이전트가 실제로는 그 지점까지 절대 들어올 수
없게 만든다.

그런데 `Gather()`(`UnitController.cs:385`)와 `Deposit()`(`UnitController.cs:470`)은
`MoveTo(node.transform.position)` / `MoveTo(depositTarget.position)`로 **장애물의
정확한 좌표 자체**를 목적지로 잡는다. `NavMeshAgent`는 이 목적지에 실제로 도달할 수
없으므로 carve된 경계 언저리에서 계속 맴돌게 되고, `Update()`의 도착 판정
(`UnitController.cs:132-140`)

```csharp
if (!arrived &&
    !navMeshAgent.pathPending &&
    navMeshAgent.remainingDistance <= arriveDistance)
{
    arrived = true;
    ...
    UnitcurrentState = UnitState.Idle;
}
```

은 `remainingDistance`가 `arriveDistance`(기본 0.5) 밑으로 절대 안 내려가서
`arrived`가 `true`가 되는 일이 없다. 결과적으로 `IsIdle()`이 계속 `false`라서,
`GatherTick()`의 `MovingToResource`/`MovingToBase` 분기(`UnitController.cs:407-408`,
`438-439`)가 다음 단계로 절대 못 넘어간다 — 채취/반납이 시작조차 안 되는 게 이
때문이다.

### 해결 방향: "정확히 도착"이 아니라 "충분히 가까워지면" 판정으로 교체

장애물 목적지에 대해서는 애초에 `IsIdle()`(=정확히 도착)을 기다리지 말고,
**채취/반납 로직 자신이 거리로 직접 판정**하도록 바꾼다. 일반 이동 명령
(`MoveTo`, `PatrolTick` 등)의 도착 판정 로직은 그대로 둔다 — 걷는 목적지는
장애물이 아니므로 문제 없음.

```csharp
// ===== 필드 추가 =====
[SerializeField] private float gatherInteractRange = 2f; // 장애물 특성상 arriveDistance보다 넉넉하게

private Transform depositTargetTransform; // Gathering 단계에서만 쓰던 지역변수를 필드로 승격
```

`GatherTick()` 수정:

```csharp
private void GatherTick()
{
    if (gatherState == GatherState.None)
        return;

    switch (gatherState)
    {
        case GatherState.MovingToResource:
            if (Vector3.Distance(transform.position, gatherTargetNode.transform.position) <= gatherInteractRange)
            {
                if (!isAirUnit)
                    navMeshAgent.isStopped = true; // 장애물 경계에서 계속 재탐색하며 맴도는 것 방지

                gatherTimer = gatherDuration;
                gatherState = GatherState.Gathering;
            }
            break;

        case GatherState.Gathering:
            gatherTimer -= Time.deltaTime;
            if (gatherTimer <= 0f)
            {
                carryingAmount = gatherTargetNode.Extract(amountPerTrip);

                if (gatherTargetNode.Type == ResourceType.Ore)
                    DepositOre.SetActive(true);
                else
                    DepositGas.SetActive(true);

                depositTargetTransform = FindNearestDepositBuilding();
                if (depositTargetTransform == null)
                {
                    gatherState = GatherState.None;
                    return;
                }

                MoveTo(depositTargetTransform.position);
                gatherState = GatherState.MovingToBase;
            }
            break;

        case GatherState.MovingToBase:
            if (Vector3.Distance(transform.position, depositTargetTransform.position) <= gatherInteractRange)
            {
                if (!isAirUnit)
                    navMeshAgent.isStopped = true;

                gatherState = GatherState.Depositing;
            }
            break;

        case GatherState.Depositing:
            Deposit();
            break;
    }
}
```

`Deposit()`은 그대로 두되, 다음 채취 이동(`MoveTo(gatherTargetNode.transform.position)`)
호출은 변경 없음 — 다시 `MovingToResource`로 돌아가면 위에서 고친 거리 판정이
그대로 적용된다.

### 주의할 점

- `gatherInteractRange`는 건물/자원 노드의 `NavMeshObstacle` carve 반경보다
  커야 한다. 너무 작으면 여전히 "가까워졌다"는 판정이 안 나고, 너무 크면
  실제로 멀리서도 채취/반납이 시작되는 것처럼 보인다. 건물 크기가 제각각이면
  (`BuildingData.Size` 참고, `BuildingDataSO.cs:20`) 고정값 하나로는 부족할 수
  있어서, 건물/노드마다 "상호작용 반경"을 따로 들고 있게 할지도 고려 대상.
- `navMeshAgent.isStopped = true`를 안 해주면, 상태는 `Gathering`/`Depositing`으로
  넘어갔는데 에이전트는 여전히 도달 불가능한 목적지를 향해 경로 재탐색을 계속
  시도해서 자원 캐는 동안 유닛이 미세하게 흔들리거나(carve 경계를 파고들었다
  밀렸다 반복) 이상하게 보일 수 있다.
- 공중 유닛(`isAirUnit`)은 애초에 `NavMeshAgent`를 안 쓰고 직접
  `transform.position`을 옮기는 방식(`UnitController.cs:104-127`)이라 이
  문제와는 무관하다 — 위 코드에서 `if (!isAirUnit)`으로 감싼 이유.

### 대안(채택 안 함, 참고용): 트리거 콜라이더 기반 "닿음" 판정

`AttackRange`가 이미 트리거 콜라이더로 "사거리 안"을 판정하는 것과 동일한
패턴으로, `ResourceNode`/건물 쪽에 트리거 콜라이더를 두고 유닛이 그 콜라이더에
`OnTriggerEnter`할 때 도착 신호를 주는 방식도 가능하다. 다만:

- 트리거 이벤트가 발생하려면 두 콜라이더 중 한쪽 오브젝트(또는 그 부모)에
  `Rigidbody`가 있어야 하는데, 유닛/건물 프리팹에 `Rigidbody`가 실제로 붙어있는지
  확인이 필요하다 (없으면 `OnTriggerEnter`가 아예 호출되지 않는다).
- `NavMeshObstacle`이 carve만 할 뿐 별도 `Collider`를 갖는지, 그 `Collider`가
  `IsTrigger`로 설정 가능한지도 프리팹 구조를 봐야 확정할 수 있다.

물리 콜라이더/리지드바디 설정에 대한 확인이 더 필요해서, 확인 없이 바로 되는
거리 기반 판정(위 본안)을 우선 제안한다.
