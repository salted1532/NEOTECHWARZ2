# 0420 - 메인기지 이/착륙 시 자원 든 일꾼 재라우팅 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 메인기지 착륙시 모든 일꾼들에게 자원을 가지고 있는 일꾼들은 리턴명령(자신을 기준으로 가장
> 가까운 메인기지를 다시찾는)을 내리도록 하면 좋을거 같아
> 현재는 A메인기지와 B메인기지가 있으면 일꾼이 A메인기지로 자원을 들고 가고 있다가 A가
> 이륙을 하게 되면 그자리에서 멈추게 돼. B메인기지가 지상에 착륙해 있는 상태인데도 B
> 메인기지로 자원 채취를 하던 일꾼들도 B를 이륙하면 그자리에 멈추고 일단 일꾼이 자원
> 채취하던 기지가 이륙해서 없어지면 가까운 메인기지를 다시 찾아서 리턴하도록 하고 어느
> 메인기지든 착륙하게 되면 일꾼들에게 가까운 메인기지를 찾는 로직을 다시 작동시켜서
> 갱신하도록 해줘

## 조사 결과 - 지금은 반납 대상이 이륙하면 그 건물 하나만 계속 기다림

`GatherTick()`의 `MovingToBase` 케이스(`UnitController.cs:1815~1823`):

```csharp
case GatherState.MovingToBase:
    BuildingController depositBuilding = depositTargetTransform.GetComponent<BuildingController>();
    if (depositBuilding != null && depositBuilding.IsLifted())
    {
        // 반납 대상 건물이 공중에 뜬 동안은 도달할 수 없으므로 제자리에서 착륙을 기다린다.
        if (!isAirUnit)
            navMeshAgent.isStopped = true;
        break;
    }
    ...
```

반납하러 가던 메인기지(A)가 이륙하면, **A가 다시 착륙할 때까지** 제자리에서 무한정 대기한다.
이 시점에 B가 이미 착륙해 있어도 전혀 확인하지 않는다 - 오직 "지금 향하던 그 건물"만 본다.
사용자가 말한 두 증상 다 이 한 곳이 원인이다(A로 가다가 A 이륙 / B로 가다가 B 이륙 - 둘 다
"지금 향하던 건물이 이륙하면 그 건물만 계속 기다림"이라는 같은 코드 경로).

참고로 최초 반납 목적지를 정하는 `FindNearestDepositBuilding()`(`UnitController.cs:1914~1921`)은
이미 "착륙한 곳 우선, 하나도 없으면 그중 가장 가까운 곳"으로 잘 고르고 있다 - 문제는 그 이후,
이동 도중에 사정이 바뀌었을 때(내가 향하던 곳이 이륙, 혹은 다른 곳이 새로 착륙) 다시 반영을
안 한다는 것.

## 제안하는 수정

### 1. 이동 중이던 반납 대상이 이륙하면, 그 자리에서 기다리지 않고 다른 착륙 기지가 있는지 즉시 확인

`GatherTick()`의 `MovingToBase` 케이스 수정:

```csharp
case GatherState.MovingToBase:
    BuildingController depositBuilding = depositTargetTransform.GetComponent<BuildingController>();
    if (depositBuilding != null && depositBuilding.IsLifted())
    {
        // 반납 대상 건물이 이륙했다 - 착륙해 있는 다른 메인기지가 있으면 그쪽으로 갈아탄다.
        // 착륙한 곳이 하나도 없으면(전부 이륙 중) 기존처럼 제자리에서 대기한다 (doc/0420).
        Transform alt = FindNearestDepositBuilding();
        if (alt != null && alt != depositTargetTransform)
        {
            depositTargetTransform = alt;
            MoveToDepositTargetOrWait();
            break;
        }

        if (!isAirUnit)
            navMeshAgent.isStopped = true;
        break;
    }
    ...
```

### 2. 메인기지가 착륙할 때마다, 자원을 든 모든 일꾼에게 반납 재탐색을 걸어준다

`BuildingController.Land()`(`BuildingController.cs:358~382`)가 착륙을 완료하는 지점에 정적
이벤트를 하나 추가하고, `RTSUnitController`가 구독해서 메인기지가 착륙할 때마다 전체 유닛
목록 중 자원을 든 일꾼에게 `ReturnCargo()`를 호출한다(그 안에서 `FindNearestDepositBuilding()`이
다시 최근접 착륙 기지를 계산해줌 - 이미 딱 맞는 곳으로 향하던 일꾼은 같은 목적지로 재계산되어
사실상 아무 변화 없음, 대상 없이 멈춰있던 일꾼만 실질적으로 갱신됨).

`Assets/Scripts/Building/BuildingController.cs`:

```csharp
    // 이 건물이 착륙을 완료할 때마다 발행 (doc/0420) - 메인기지 착륙 시 자원 든 일꾼들의 반납
    // 목적지를 다시 계산시키기 위한 용도. 정적 이벤트라 개별 건물 참조 없이 어디서든 구독 가능.
    public static event System.Action<BuildingController> OnLanded;
```

`Land()` 끝(`landed?.Invoke();` 다음)에 추가:

```csharp
        OnLanded?.Invoke(this);
```

`Assets/Scripts/System/RTSUnitController.cs`:

```csharp
    private void Awake()
    {
        ...
        BuildingController.OnLanded += HandleMainBaseLanded;
    }

    private void OnDestroy()
    {
        BuildingController.OnLanded -= HandleMainBaseLanded;
    }

    // 메인기지가 착륙할 때마다 자원을 든 모든 일꾼의 반납 목적지를 다시 계산시킨다 (doc/0420).
    private void HandleMainBaseLanded(BuildingController building)
    {
        if (!building.CompareTag("MainBase"))
            return;

        foreach (UnitController unit in UnitList)
        {
            if (unit != null)
                unit.ReturnCargo(); // 내부에서 isWorker/자원 소지 여부를 스스로 확인하므로 안전하게 전체 호출
        }
    }
```

## 요청하신 흐름과의 대조

| 요청하신 동작 | 수정 |
|---|---|
| A로 반납 가던 중 A 이륙 → 제자리 멈춤 | `MovingToBase`에서 이륙 감지 시 다른 착륙 기지로 즉시 갈아탐(1번) |
| B가 착륙해 있어도 반영 안 됨 | 같은 수정(1번) - `FindNearestDepositBuilding()`이 착륙한 기지를 우선 고름 |
| 메인기지가 착륙하면 일꾼들에게 재탐색을 다시 걸어줌 | `BuildingController.OnLanded` 이벤트 + `RTSUnitController`가 전체 일꾼에 `ReturnCargo()`(2번) |

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`GatherTick()`의 `MovingToBase` 케이스)
- `Assets/Scripts/Building/BuildingController.cs` (`OnLanded` 이벤트 추가, `Land()`에서 발행)
- `Assets/Scripts/System/RTSUnitController.cs` (구독/해제, `HandleMainBaseLanded()`)

## 요약

- `GatherTick()`의 `MovingToBase`에서 반납 대상이 이륙하면 다른 착륙 기지가 있는지 즉시
  확인해서 갈아타도록 수정 - 없으면 기존처럼 대기.
- `BuildingController`에 정적 이벤트 `OnLanded`를 추가하고 `Land()` 완료 시점에 발행.
- `RTSUnitController`가 `Awake()`에서 구독, `OnDestroy()`에서 해제, 메인기지 착륙 시 전체
  유닛에 `ReturnCargo()`를 호출(내부에서 일꾼/자원 소지 여부를 스스로 걸러냄).
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
