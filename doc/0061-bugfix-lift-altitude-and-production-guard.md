# 0061. 버그수정: 이륙 직후 이동 시 고도 하락 고정 + 생산 중 이륙 차단

**날짜:** 2026-07-12

## 요청 내용
> 건물 이동 버그: (1) 이륙 후 바로 이동 명령을 내리면 그 높이가 이륙 높이(5) 이하인 상태로 이동되고 계속 그 상태로 유지된다. (2) 유닛을 생산 대기열에 넣어놓고 이륙하면 공중에서도 계속 유닛 생산이 진행된다 → 생산 중일 땐 이륙을 막아달라. 그리고 이륙 후 이동하더라도 공중유닛의 이동 방식처럼 "목적지 좌표 + 이륙 높이값"으로 계속 그 고도를 유지하며 날아다니도록 해달라.

## 원인 분석

### 버그 1: 이륙 직후 이동하면 낮은 고도로 고정됨
`BuildingController.MoveWhileLifted()`가 새 수평 목표의 고도를 **"현재(transform.position.y)"** 로 고정해서 저장한다.
```csharp
flightDestination = new Vector3(groundDestination.x, transform.position.y, groundDestination.z);
```
`LiftOff()` 직후엔 `isAscending`이 아직 목표 고도(`liftHeight`)까지 다 오르지 못한 상태일 수 있는데, 이 시점에 이동 명령이 들어오면 그 "다 오르지 못한 낮은 현재 높이"가 그대로 새 비행 목표의 고도로 굳어버린다. 게다가 `UpdateLiftedMovement()`의 수평 비행 처리도 Y값은 그대로 두고 X/Z만 이동시키므로, 한 번 낮은 고도로 굳으면 착륙(공식 착륙 비행)하기 전까지는 절대 다시 원래 고도로 회복되지 않는다 — 정확히 사용자가 관찰한 증상과 일치한다.

### 버그 2: 생산 중에도 이륙 가능
[[0057-lift-freeflight-land-lock-shortcut|0057]]에서 "공중이면 생산 패널을 숨긴다"는 UI 레이어만 막았을 뿐, `BuildingController.LiftOff()` 자체엔 생산 대기열 상태를 확인하는 가드가 없었다. `UnitSpawner`는 `BuildingController`와 별개로 자기 `Update()`에서 대기열을 계속 진행시키므로, 이미 대기열에 유닛이 있는 상태로 이륙해버리면 UI만 안 보일 뿐 생산은 그대로 계속된다.

## 수정 내용

### 1. 이동 고도를 "목적지 + 이륙 높이"로 고정 (공중유닛과 동일한 패턴)
`UnitController`의 공중유닛이 `targetPosition = destination + Vector3.up * 5f`로 항상 목적지 기준 고정 오프셋을 쓰는 것과 동일하게, `MoveWhileLifted()`도 클릭한 목적지 좌표에 `liftHeight`를 더한 값을 비행 목표로 삼도록 바꾼다. 착륙 비행(`BeginRelocationFlight`)의 "수평 이동 구간"도 같은 방식으로 통일하고, 실제 착륙 지면 좌표는 별도 필드(`landingGroundDestination`)로 분리해서 하강 단계에서만 쓰도록 한다. 그 결과 `UpdateLiftedMovement()`의 수평 비행 처리가 더 이상 "현재 Y값 유지"가 아니라 "목표 고도까지 함께 수렴"하게 되어, 이륙 도중(고도가 덜 오른 상태)에 이동 명령이 들어와도 비행하면서 자연스럽게 목표 고도까지 다시 올라간다.

```csharp
// 기존 코드
    private Vector3 verticalTarget;    // 상승 목표(현재 위치 기준 + liftHeight)
    private Vector3 flightDestination; // 착륙 목표(최종 정착 월드 좌표, 수평이동+하강 공통 목표)
```
```csharp
// 변경 코드
    private Vector3 verticalTarget;    // 상승 목표(현재 위치 기준 + liftHeight)
    private Vector3 flightDestination; // 수평 비행 목표(목적지 좌표 + liftHeight 고도) - 자유이동/착륙 비행 공통
    private Vector3 landingGroundDestination; // 착륙 최종 지면 좌표 (pendingLanding일 때만 유효, 하강 단계 전용)
```

```csharp
// 기존 코드
        if (isFlyingToDestination)
        {
            Vector3 flatTarget = new Vector3(flightDestination.x, transform.position.y, flightDestination.z);
            transform.position = Vector3.MoveTowards(transform.position, flatTarget, liftMoveSpeed * Time.deltaTime);

            Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatDest = new Vector3(flightDestination.x, 0f, flightDestination.z);

            if (Vector3.Distance(flatPos, flatDest) < 0.05f)
            {
                isFlyingToDestination = false;

                if (pendingLanding)
                    isDescending = true;
                // else: 우클릭/Move버튼 자유이동 도착 - 착륙하지 않고 그 자리에서 계속 공중에 떠 있는다
            }

            return;
        }

        if (isDescending)
        {
            transform.position = Vector3.MoveTowards(transform.position, flightDestination, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, flightDestination) < 0.05f)
            {
                transform.position = flightDestination;
                Land();
            }
        }
```
```csharp
// 변경 코드
        if (isFlyingToDestination)
        {
            // flightDestination은 항상 "목적지 좌표 + liftHeight"로 미리 계산돼 있으므로, 그대로 목표로 삼으면
            // 이륙 도중(고도가 덜 오른 상태)에 새 이동 명령이 들어와도 비행하면서 자연스럽게 목표 고도까지 수렴한다.
            transform.position = Vector3.MoveTowards(transform.position, flightDestination, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, flightDestination) < 0.05f)
            {
                isFlyingToDestination = false;

                if (pendingLanding)
                    isDescending = true;
                // else: 우클릭/Move버튼 자유이동 도착 - 착륙하지 않고 이 고도(목적지 + liftHeight)에서 계속 공중에 떠 있는다
            }

            return;
        }

        if (isDescending)
        {
            transform.position = Vector3.MoveTowards(transform.position, landingGroundDestination, liftMoveSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, landingGroundDestination) < 0.05f)
            {
                transform.position = landingGroundDestination;
                Land();
            }
        }
```

```csharp
// 기존 코드
    public void BeginRelocationFlight(Vector3Int newGridPos, Vector3 destination, System.Action onLanded, System.Action onCancelled)
    {
        pendingGridPosition = newGridPos;
        onRelocationLanded = onLanded;
        onRelocationCancelled = onCancelled;

        isAscending = false;
        isDescending = false;
        pendingLanding = true;
        flightDestination = destination;
        isFlyingToDestination = true;
    }
```
```csharp
// 변경 코드
    public void BeginRelocationFlight(Vector3Int newGridPos, Vector3 destination, System.Action onLanded, System.Action onCancelled)
    {
        pendingGridPosition = newGridPos;
        onRelocationLanded = onLanded;
        onRelocationCancelled = onCancelled;

        isAscending = false;
        isDescending = false;
        pendingLanding = true;
        landingGroundDestination = destination; // 최종 착륙 지면 좌표 (하강 단계에서 사용)
        flightDestination = destination + Vector3.up * liftHeight; // 수평 비행 중엔 착륙지점 상공(liftHeight 고도)까지만 이동
        isFlyingToDestination = true;
    }
```

```csharp
// 기존 코드
    public void MoveWhileLifted(Vector3 groundDestination)
    {
        if (!isLifted)
            return;

        CancelPendingLandingFlight();

        isAscending = false;
        isDescending = false;
        pendingLanding = false;
        flightDestination = new Vector3(groundDestination.x, transform.position.y, groundDestination.z);
        isFlyingToDestination = true;
    }
```
```csharp
// 변경 코드
    public void MoveWhileLifted(Vector3 groundDestination)
    {
        if (!isLifted)
            return;

        CancelPendingLandingFlight();

        isAscending = false;
        isDescending = false;
        pendingLanding = false;
        flightDestination = groundDestination + Vector3.up * liftHeight; // 공중유닛과 동일한 패턴: 목적지 + 이륙 높이
        isFlyingToDestination = true;
    }
```

### 2. 생산 중일 땐 이륙 차단
```csharp
// 기존 코드
    public void LiftOff()
    {
        if (!canLift || isLifted)
            return;

        if (hasGridPosition)
```
```csharp
// 변경 코드
    public void LiftOff()
    {
        if (!canLift || isLifted)
            return;

        if (HasActiveProductionQueue()) // 생산 대기열에 뭔가 있으면 이륙 불가(공중에서 생산이 계속되는 것 방지)
            return;

        if (hasGridPosition)
```

**새 메서드 추가** (`LiftOff()` 위):
```csharp
    // 생산 대기열에 하나라도 남아있는지 (UnitSpawner가 없는 건물은 항상 false). LiftOff() 가드용.
    private bool HasActiveProductionQueue()
    {
        return UnitSpawner != null && UnitSpawner.GetProductionQueue().Count > 0;
    }
```
(`GetProductionQueue()`(public) 대신 이 헬퍼를 새로 만든 이유: 기존 `GetProductionQueue()`는 `UnitSpawner.GetProductionQueue()`를 null 체크 없이 그대로 호출해서, `UnitSpawner`가 없는 건물(SupplyDepot/Lab 등)에서 호출하면 `NullReferenceException`이 난다. `LiftOff()`는 그런 건물에서도 호출될 수 있으므로 안전하게 null 체크를 포함한 별도 메서드를 쓴다.)

## 이번 수정에서 결정한 세부 동작
- **생산 중 이륙 시도 시 피드백**: 별도 UI 피드백(버튼 비활성화 표시 등) 없이 조용히 무시합니다 - 기존 `UnitController`의 `isConstructing` 가드 등 이 코드베이스의 기존 "막힌 명령은 조용히 무시" 패턴과 동일하게 맞췄습니다. 필요하시면 버튼을 회색으로 비활성화하는 것도 추가로 처리해드릴 수 있습니다.
- **대기열에 남은 유닛 취소 후 이륙 재시도**: 별도 처리 없이도 자연스럽게 됩니다 - 대기열을 취소(`CancelProduction`)하면 `HasActiveProductionQueue()`가 다시 `false`가 되어 이륙이 정상적으로 가능해집니다.

## 변경 예정 파일
- `Assets/Scripts/Building/BuildingController.cs`

## 상태
**적용 완료.**
