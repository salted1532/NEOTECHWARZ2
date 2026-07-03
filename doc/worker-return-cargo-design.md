# 일꾼 "자원 반환(Return Cargo)" 모드 설계 — `EnterReturnMode`

작성일: 2026-07-03
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 0. 현재 문제

`RTSUnitController.cs:466`에서 이미 `EnterReturnMode`를 워커 패널 콜백으로 넘기고
있는데, 정작 `EnterReturnMode` 메소드 자체가 어디에도 정의돼 있지 않다.

```csharp
uIController.ShowWorkerPanel(
    EnterMoveMode,
    EnterAttackMode,
    StopSelectedUnits,
    EnterPatrolMode,
    HoldSelectedUnits,
    EnterReturnMode,   // ← 정의 없음, 지금 컴파일 에러 상태
    BuildModeOn);
```

`UIController.ShowWorkerPanel`(`UIController.cs:286-307`)의 `onReturn` 파라미터도
`Action`(인자 없음)이라 UI 쪽 아이콘(`returnIcon`)까지 이미 준비돼 있다. 즉
**UI는 완성돼 있고, "버튼을 누르면 무슨 일이 일어나야 하는지"만 비어있는 상태.**

## 1. 요구사항 정리

> 일꾼이 자원을 들고 있다(채취 후 다른 명령 때문에 Deposit하지 못한 상태)면,
> 들고 있는 자원을 가까운 기지에 넣고, 가장 가까운 자원으로 다시 캐러 간다.

즉 SC 시리즈의 "Return Cargo" 커맨드와 동일하다:

- 자원을 안 들고 있는 일꾼에게 눌러도 **아무 일도 일어나지 않아야 함** (버튼이
  비활성화되진 않으니, 로직 쪽에서 조용히 무시).
- 자원을 들고 있는 일꾼 → 최근접 기지로 이동 → 반납 → **원래 캐던 노드가 아니라
  그 시점 기준 최근접 자원 노드**로 다시 이동해 채취 재개.
- 여러 일꾼을 동시에 선택했을 수 있으므로 `selectedUnitList` 전체에 대해
  개별 판단(각자 들고 있는지 여부가 다를 수 있음).

## 2. 기존 코드에서 재사용 가능한 부분

`UnitController.Gather()`(`UnitController.cs:406-435`)에 이미 "이미 자원을 들고
있는 상태에서 새 명령을 받으면 일단 반납부터 간다"는 것과 거의 같은 분기가 있다:

```csharp
if (IsCarryingResource())
{
    depositTargetTransform = FindNearestDepositBuilding();
    if (depositTargetTransform == null)
    {
        CancelGathering();
        return;
    }

    patrolling = false;
    MoveTo(depositTargetTransform.position);
    gatherState = GatherState.MovingToBase;
    return;
}
```

`IsCarryingResource()`(`UnitController.cs:437`)는 `DepositOre`/`DepositGas`
오브젝트의 활성 여부로 판정한다 — 정확히 "자원을 들고 있다"는 신호로 쓸 수 있다.

다만 이 분기는 반납 후 **원래 `gatherTargetNode`로 돌아가는** 것을 전제로 짜여
있어서(`Deposit()`, `UnitController.cs:528-553`, 특히 545-552줄), "가장 가까운
자원"이라는 요구사항과는 다르다. 이 부분만 새로 손봐야 한다.

## 3. 설계

### 3-1. `UnitController.ReturnCargo()` — 신규 진입점

`Gather()`와 동일한 위치(자원 관련 public API들 옆)에 추가한다.

```csharp
// ===== Return Cargo 진입점 (UI "반환" 버튼) =====
public void ReturnCargo()
{
    if (!isWorker || !IsCarryingResource())
        return; // 일꾼이 아니거나 들고 있는 자원이 없으면 아무 것도 안 함

    depositTargetTransform = FindNearestDepositBuilding();
    if (depositTargetTransform == null)
    {
        CancelGathering(); // 반납할 건물이 없으면 그 자리에 멈춰서 Idle로
        return;
    }

    patrolling = false;
    gatherTargetNode = null; // "고정 목적지 없음" 신호 → Deposit()이 최근접 노드를 새로 찾게 함
    MoveTo(depositTargetTransform.position);
    gatherState = GatherState.MovingToBase;
}
```

`MoveTo()`가 내부에서 `CancelGatheringForNewCommand()`를 호출해 `gatherState`를
`None`으로 초기화하지만, 바로 다음 줄에서 `gatherState = GatherState.MovingToBase`로
덮어쓰므로 문제 없다 — `Gather()`의 기존 분기(위 2절)와 완전히 같은 패턴이라 이미
검증된 흐름이다.

`gatherTargetNode = null`로 지우는 것이 핵심 트릭이다. 원래 캐던 노드로 돌아가지
않고 "새로 찾아야 한다"는 걸 `Deposit()` 쪽에 알리는 신호로 재사용한다 (3-2절).

### 3-2. `Deposit()` — 목적지가 없으면 최근접 노드 재탐색

기존 코드(`UnitController.cs:528-553`):

```csharp
private void Deposit()
{
    if (carryingType == ResourceType.Ore) { ... } else { ... }
    carryingAmount = 0;

    if (gatherTargetNode == null || gatherTargetNode.IsDepleted)
    {
        CancelGathering(); // 캐던 노드가 없어졌으면(고갈/파괴) 그 자리에 멈춰서 Idle로
        return;
    }

    MoveTo(gatherTargetNode.transform.position);
    gatherState = GatherState.MovingToResource;
}
```

변경안 — `gatherTargetNode`가 없거나 고갈됐을 때 바로 멈추지 말고, 먼저 최근접
노드를 찾아본다:

```csharp
private void Deposit()
{
    if (carryingType == ResourceType.Ore)
    {
        rtsController.AddOre(carryingAmount);
        DepositOre.SetActive(false);
    }
    else
    {
        rtsController.AddGas(carryingAmount);
        DepositGas.SetActive(false);
    }

    carryingAmount = 0;

    if (gatherTargetNode == null || gatherTargetNode.IsDepleted)
    {
        gatherTargetNode = FindNearestResourceNode();
        if (gatherTargetNode == null)
        {
            CancelGathering(); // 근처에 캘 자원이 아예 없으면 그 자리에 멈춰서 Idle로
            return;
        }
    }

    MoveTo(gatherTargetNode.transform.position);
    gatherState = GatherState.MovingToResource;
}
```

이렇게 하면 `ReturnCargo()`가 만든 "목적지 없음" 상태뿐 아니라, 기존
`worker-resource-gathering-design.md` 8절의 열린 질문 2번("자원 노드가 채취
도중 고갈되면?")도 같이 해결된다 — 캐던 노드가 소진돼도 이제 그냥 서 있지 않고
근처 다른 노드를 찾아 계속 채취한다. **동작이 바뀌는 지점이니 의도한 변경인지
확인 필요** (원래는 고갈 시 항상 Idle로 멈추는 게 의도였다면 이 부분은 별도
플래그로 분리해야 함).

### 3-3. `FindNearestResourceNode()` — 신규 헬퍼

기존 `FindNearestDepositBuilding()`(`UnitController.cs:564-582`)이
`rtsController.BuildingList`를 순회하는 것과 정확히 같은 패턴을, 자원 노드
버전으로 하나 더 만든다.

```csharp
private ResourceNode FindNearestResourceNode()
{
    ResourceNode nearest = null;
    float nearestSqrDist = float.MaxValue;

    foreach (ResourceNode node in rtsController.ResourceNodeList)
    {
        if (node == null || node.IsDepleted) continue;

        float sqrDist = (node.transform.position - transform.position).sqrMagnitude;
        if (sqrDist < nearestSqrDist)
        {
            nearestSqrDist = sqrDist;
            nearest = node;
        }
    }

    return nearest;
}
```

`rtsController.ResourceNodeList`는 아직 존재하지 않는다 → 3-4절에서 추가.

### 3-4. `RTSUnitController` — `ResourceNodeList` 추가

`UnitList`/`BuildingList`(`RTSUnitController.cs:17-18`, `82-83`, `88-89`)와
완전히 같은 패턴으로 하나 더 둔다. 지금은 맵의 모든 `ResourceNode`를 들고 있는
곳이 없어서(개별 `UnitController`가 `FindNearestDepositBuilding()`을 쓸 때
쓰는 `BuildingList`에 대응하는 게 없음), 이 리스트가 없으면 매번
`FindObjectsByType<ResourceNode>()`로 씬 전체를 스캔해야 한다.

```csharp
// 맵에 존재하는 모든 유닛/건물/자원 노드
public List<UnitController> UnitList;
public List<BuildingController> BuildingList;
public List<ResourceNode> ResourceNodeList;
```

```csharp
private void Awake()
{
    selectedUnitList = new List<UnitController>();
    selectedBuildingList = new List<BuildingController>();
    UnitList = new List<UnitController>();
    BuildingList = new List<BuildingController>();
    ResourceNodeList = new List<ResourceNode>();
}

private void Update()
{
    UnitList.RemoveAll(unit => unit == null);
    BuildingList.RemoveAll(building => building == null);
    ResourceNodeList.RemoveAll(node => node == null);

    UpdateUI();
}
```

`ResourceNode.cs`에 등록 코드 추가 (지금은 `Awake()`에서 `initialAmount`만
세팅하고 끝 — 등록하는 곳이 없다):

```csharp
private void Start()
{
    RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
    controller?.ResourceNodeList.Add(this);
}
```

`Extract()`(`ResourceNode.cs:24-35`)가 고갈 시 이미 `Destroy(gameObject)`를
호출하므로, 리스트에서의 제거는 `UnitList`/`BuildingList`와 동일하게
`RemoveAll(node => node == null)`이 다음 프레임에 자동으로 처리한다 — 별도
`Die()` 호출 같은 걸 추가할 필요 없음.

> **대안**: `ResourceNodeList`를 새로 안 만들고 싶다면, `FindNearestResourceNode()`
> 안에서 그때그때 `FindObjectsByType<ResourceNode>(FindObjectsSortMode.None)`로
> 스캔해도 결과는 동일하다. 다만 이 프로젝트는 `UnitList`/`BuildingList`처럼
> "자기 자신을 컨트롤러 리스트에 등록"하는 패턴을 이미 일관되게 쓰고 있어서,
> 그 컨벤션을 따르는 쪽을 권장한다. `Deposit()`은 한 번 반납할 때 한 번만
> 호출되는 저빈도 이벤트라 성능 차이는 어느 쪽이든 사실상 없다.

### 3-5. `RTSUnitController.EnterReturnMode()` — 실제로 빠져있던 부분

`Move`/`Attack`/`Patrol`/`Rally`처럼 "버튼 누르고 → 지도 클릭"이 필요한 명령이
아니라, `Stop`/`Hold`처럼 **버튼 한 번으로 즉시 실행되는 명령**이다
(`ShowWorkerPanel`에 `onReturn`이 좌표를 받지 않는 `Action`인 것도 이를
뒷받침한다). 그래서 `userControl.SetOrderState(...)`를 거치는
`EnterMoveMode`류(`RTSUnitController.cs:356-374`)가 아니라,
`StopSelectedUnits`/`HoldSelectedUnits`(`RTSUnitController.cs:205-218`)와 같은
모양으로 만든다.

```csharp
public void EnterReturnMode()
{
    for (int i = 0; i < selectedUnitList.Count; ++i)
    {
        selectedUnitList[i].ReturnCargo();
    }
}
```

`#region Unit선택 관련`(`RTSUnitController.cs:95-237`) 안, `HoldSelectedUnits`
바로 아래에 추가하는 걸 권장 (같은 성격의 메소드끼리 묶임). `GatherSelectedUnits`
(`RTSUnitController.cs:229-235`)와도 정확히 같은 반복문 패턴.

`ReturnCargo()` 안에서 `!isWorker`면 바로 return하므로, 워커/전투유닛이 섞여
선택된 상태에서 호출돼도 전투유닛은 조용히 무시된다 — 별도 필터링 불필요.

## 4. 전체 흐름 요약

```
"반환" 버튼 클릭 (워커 패널)
  └─ RTSUnitController.EnterReturnMode()
       └─ 각 UnitController.ReturnCargo()
            ├─ 자원 안 들고 있음 → 아무 것도 안 함
            └─ 자원 들고 있음
                 ├─ 최근접 기지 없음 → CancelGathering() (Idle)
                 └─ 최근접 기지 있음
                      gatherTargetNode = null (신호)
                      MoveTo(기지) + gatherState = MovingToBase
                        └─ GatherTick()이 기지 도착 감지 → Depositing
                             └─ Deposit()
                                  ├─ 자원 반납 (AddOre/AddGas)
                                  └─ gatherTargetNode == null
                                       └─ FindNearestResourceNode()
                                            ├─ 없음 → CancelGathering() (Idle)
                                            └─ 있음 → 그 노드로 MoveTo, MovingToResource
                                                       (이후 기존 채취 사이클 그대로 반복)
```

## 5. 적용 순서 제안

1. `RTSUnitController`에 `ResourceNodeList` 필드 추가 + `Awake()`/`Update()` 갱신
2. `ResourceNode.cs`에 `Start()` 추가해 `ResourceNodeList`에 자기 등록
3. `UnitController.Deposit()` 수정 — 목적지 없을 때 `FindNearestResourceNode()` 시도
4. `UnitController`에 `FindNearestResourceNode()`, `ReturnCargo()` 추가
5. `RTSUnitController.EnterReturnMode()` 추가 (이게 있어야 현재 컴파일 에러가
   해소됨)

## 6. 열린 질문

1. **최근접 노드 검색 시 자원 타입(Ore/Gas)을 가릴지.** 지금 설계는 타입 무관
   최근접이다. 예를 들어 가스를 캐던 일꾼이 반환 후 바로 옆 미네랄 노드로
   갈아타는 게 이상하다면, `FindNearestResourceNode(carryingType)`처럼 타입
   필터를 추가해야 한다.
2. **3-2절에서 언급한 "고갈 시 자동으로 다른 노드 탐색" 동작이 `ReturnCargo()`
   호출 여부와 무관하게 항상 적용된다는 점.** 원래 채취 사이클(자기가 캐던
   노드가 도중에 고갈)에서도 이제 자동으로 다음 노드를 찾아간다. 의도한
   범위 확장이 맞는지 확인 필요 — 원치 않으면 `ReturnCargo()`가 세팅한
   "명시적 null"과 "그냥 고갈된 것"을 구분할 별도 플래그(`bool
   findNearestOnDeposit`)가 필요하다.
3. **여러 노드가 동시에 최근접일 때(동타 거리) 여러 일꾼이 같은 노드로 몰릴 수
   있음.** `worker-resource-gathering-design.md` 8절 열린 질문 3번(동시 채취
   인원 제한)과 동일한 이슈라 여기서 새로 생기는 문제는 아니지만, `ReturnCargo`
   특성상 여러 일꾼을 한 번에 선택해서 누르는 경우가 많아 노출 빈도가 높아질
   수 있다.
