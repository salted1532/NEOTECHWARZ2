# 0418 - 일꾼이 캐던 자원을 기억하지 못하는 문제 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 0개.

## 요청 내용

> 일꾼이 자원을 가지고 있는 상태에서 건물 or 광물 우클릭 시 가까운 메인기지로 자원을 리턴하고
> 메인기지에서 가장 가까운 자원으로 혹은 광물 우클릭 시에 지정된 광물을 캐러 가도록 재게
> 자신이 자원 캔 그 자원을 기억하고 있다가 건물 우클릭시 자원 리턴 후 그 자원으로 캐러 돌아감
> 만약 다른 자원을 우클릭 시 다른 자원으로 이동한 다음 자신이 자원을 들고 있으면 리턴하러
> 메인기지로 가고 우클릭한 자원을 캐러 감
> 현재 로직을 확인하고 자신이 캔 자원을 기억하고 있는 이 부분은 새로 추가해야할거 같아 확인해줘

## 조사 결과 - "기억" 자체는 이미 있다, 근데 명령 경로에서 지워버린다

일꾼이 캐던 자원은 이미 `gatherTargetNode` 필드에 저장돼 있고, 반납 완료 시점(`Deposit()`,
`UnitController.cs:1862~1895`)에 정확히 사용자가 원하는 동작이 이미 구현돼 있다:

```csharp
if (gatherTargetNode != null && !gatherTargetNode.IsDepleted)
{
    // 원래 캐던 노드가 아직 남아있으면 그대로 복귀한다
    MoveTo(gatherTargetNode.transform.position);
    gatherState = GatherState.MovingToResource;
    return;
}
```

문제는 **"건물 우클릭"으로 반납을 트리거하는 두 경로가 이 기억을 반납 완료 전에 미리
지워버린다**는 것:

- `ReturnCargo()`(`UnitController.cs:1648~1665`, `MoveToBuilding()`이 일반 건물 우클릭 시 호출):
  `gatherTargetNode = null; // 고정 목적지 없음 신호`
- `ReturnCargoTo()`(`UnitController.cs:1690~1696`, `MoveToBuilding()`이 메인기지 우클릭 시 호출):
  동일하게 `gatherTargetNode = null;`

그래서 건물을 우클릭해서 반납을 시작하면, 반납이 끝난 시점엔 이미 `gatherTargetNode`가
`null`이라 `Deposit()`은 "기억"을 쓸 수 없고, 대신 `TryRedirectToNearbyResource(null)`로
**반납한 건물(메인기지) 기준 10 이내**에서 아무 자원이나 새로 찾는다 - 원래 캐던 자원이
아니라 엉뚱한(혹은 가까운 게 없으면 아예 못 찾는) 자원으로 가버리는 게 지금 버그다.

또 하나, **"광물 우클릭으로 다른 자원 지정" 쪽도 새 지정을 기억에 반영하지 않는** 문제가 있다.
`Gather(node)`(`UnitController.cs:1528~1566`)가 이미 자원을 들고 있는 상태에서 호출되면:

```csharp
if (IsCarryingResource())
{
    depositTargetTransform = FindNearestDepositBuilding();
    if (depositTargetTransform == null) { CancelGathering(); return; }
    patrolling = false;
    MoveToDepositTargetOrWait();
    return;   // <- 여기서 그냥 반납만 시키고 끝 - gatherTargetNode를 새로 클릭한 node로 안 바꿈
}
```

`gatherTargetNode`를 새로 우클릭한 `node`로 갱신하지 않고 그대로 반환하기 때문에, 반납이
끝나면 `Deposit()`은 (아직 안 지워졌으면) **원래 캐던 옛날 자원**으로 돌아간다 - 사용자가
방금 새로 지정한 자원이 아니라.

## 제안하는 수정

두 곳 다 "기억"을 건드리는 방식만 고치면 된다 - `Deposit()`의 기존 복귀 로직은 그대로 재사용.

### 1. 건물 우클릭(`ReturnCargo`/`ReturnCargoTo`) - 기억을 지우지 않는다

`Assets/Scripts/Unit/UnitController.cs`

```csharp
        patrolling = false;
        gatherTargetNode = null; // 고정 목적지 없음 신호 → Deposit()이 최근접 노드를 새로 찾게 함
        MoveToDepositTargetOrWait();
```
->
```csharp
        patrolling = false;
        // gatherTargetNode는 그대로 둔다 - 반납이 끝나면 Deposit()이 원래 캐던 자원으로 돌아간다 (doc/0418)
        MoveToDepositTargetOrWait();
```

`ReturnCargoTo()`도 동일하게 `gatherTargetNode = null;` 라인 삭제.

### 2. 광물 우클릭(`Gather`, 이미 자원을 든 상태) - 새로 지정한 자원으로 기억을 갱신한다

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
            MoveToDepositTargetOrWait();
            return;
        }
```
->
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
            gatherTargetNode = node; // 새로 지정한 자원으로 기억을 갱신 - 반납 후 이 자원으로 캐러 감 (doc/0418)
            MoveToDepositTargetOrWait();
            return;
        }
```

## 요청하신 흐름과의 대조

| 요청하신 동작 | 원인/수정 |
|---|---|
| 자원 들고 건물 우클릭 → 가까운 메인기지로 반납 | 이미 정상 동작(`FindNearestDepositBuilding()`) - 손 안 댐 |
| 반납 후 원래 캐던 자원으로 복귀 | `Deposit()`에 이미 있음 - `ReturnCargo`/`ReturnCargoTo`가 기억을 미리 지우던 것만 제거 |
| 자원 들고 다른 광물 우클릭 → 반납 후 그 광물로 | `Gather()`가 새 노드를 기억에 반영하지 않던 것을 수정(`gatherTargetNode = node` 추가) |

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`ReturnCargo()`, `ReturnCargoTo()`, `Gather()`)

## 요약

- `ReturnCargo()`/`ReturnCargoTo()`에서 `gatherTargetNode = null;`을 제거 - 반납해도 원래 캐던
  자원 기억을 지우지 않음.
- `Gather()`가 이미 자원을 든 상태에서 새 노드로 호출되면 `gatherTargetNode = node;`로 기억을
  갱신하도록 추가 - 반납 후 새로 지정한 자원으로 감.
- `Deposit()`의 기존 복귀 로직은 그대로 재사용, 별도 수정 없음.
- 컴파일 확인 완료(에러 0, 경고 0).
