# 0382 - 일꾼이 도달할 수 없는 건설 위치로 보내지면 자동 취소 + 실패 음성

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 일꾼이 도달할수 없는 위치나 도달할수 없는 언덕의 위치로 건설하러 갈때 자동 취소
> ㄴ 일꾼이 도달할수 없는 위치에 건설명령에 대한 처리를 했으면 좋겠어 도착할수 없는 위치라 높이가
> 안맞거나 경사로가 없는 언덕위일 경우 가장 가까운 위치까지 갔는데도 도달할 수 없는 경우 건설명령을
> 취소하고 건설 실패 음성이 나오도록 해줬으면 좋겠어.

## 조사 결과

- 일꾼의 건설 이동 도착 판정은 `UnitController.BuildTick()`(`UnitController.cs:923`)이 매 프레임
  담당한다: 현재 위치와 `buildDestination`(건물 스폰 지점) 사이의 실제 거리가 `buildInteractRange`
  (기본 2m) 안에 들어오는지만 확인하고, 아니면 그냥 `return`한다.
- [[0375]]에서 `MoveAgentTo()`에 추가한 fallback 덕분에, 경사로 없는 언덕처럼 도달 불가능한 위치로
  보내져도 일꾼은 "갈 수 있는 데까지"(NavMesh Partial Path의 끝)까지는 이동하고 거기서 멈춘다.
  문제는 그 지점이 `buildDestination`에서 `buildInteractRange`보다 멀면, `BuildTick()`은 이 상황을
  전혀 구분하지 못하고 **매 프레임 조용히 `return`만 반복** - 일꾼이 그 자리에 영원히 멈춰 선 채,
  건설 명령도 취소되지 않고 건물 그리드 예약(고스트)도 계속 남아있는 상태가 된다.
- 반면 "취소" 자체의 인프라는 이미 있음: `CancelBuildOrder()`(`UnitController.cs:909`)를 호출하면
  `hasBuildOrder=false` 처리 + `onBuildCancelled` 콜백(그리드 예약 해제 + 건물 가격 환불,
  `PlacementSystem.cs:196~199`)이 그대로 실행된다. 실패 음성도 `UnitAudio.PlayBuildFailVoice()`가
  이미 있고(다른 건설 실패 케이스 - 도착 시 장애물 발견 - 에서 이미 쓰이는 중, `PlacementSystem.cs:215`),
  `UnitController`도 이미 `unitAudio` 캐시 필드를 갖고 있음(`UnitController.cs:180`). 즉 필요한 조각은
  다 있고, "일꾼이 더 이상 못 간다"는 것만 감지해서 이어붙이면 됨.
- "더 이상 못 간다"는 것은 NavMeshAgent 자신이 이미 알고 있음: `!navMeshAgent.pathPending &&
  navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance`이면 지금 잡혀 있는 경로(도달
  가능한 한도까지의 Partial Path 포함)의 끝에 도달해서 멈춘 상태라는 뜻 - 목적지가 진짜 가까워서
  멈춘 경우는 이미 `buildInteractRange` 체크(2m > `stoppingDistance` 0.5m)에서 먼저 걸러지므로, 이
  조건까지 내려온다는 것 자체가 "더는 못 간다"는 신호.

## 코드 변경 (제안)

`UnitController.cs:922~944`

기존 코드:
```csharp
    // 건설 이동을 매 프레임 갱신한다: 목적지 근접 반경 안에 들어오면 도착 콜백을 실행하고 Idle로 전환한다.
    private void BuildTick()
    {
        if (!hasBuildOrder)
            return;

        if ((transform.position - buildDestination).sqrMagnitude > buildInteractRange * buildInteractRange)
            return;

        hasBuildOrder = false;

        if (!isAirUnit)
            navMeshAgent.ResetPath();

        arrived = true;
        UnitcurrentState = UnitState.Idle;

        System.Action arrivedCallback = onBuildArrived;
        onBuildArrived = null;
        onBuildCancelled = null;

        arrivedCallback?.Invoke();
    }
```

변경 코드:
```csharp
    // 건설 이동을 매 프레임 갱신한다: 목적지 근접 반경 안에 들어오면 도착 콜백을 실행하고 Idle로 전환한다.
    private void BuildTick()
    {
        if (!hasBuildOrder)
            return;

        if ((transform.position - buildDestination).sqrMagnitude > buildInteractRange * buildInteractRange)
        {
            // 목적지에 아직 못 왔는데 NavMeshAgent가 갈 수 있는 데까지 다 가서 멈춘 경우
            // (경사로 없는 언덕 위 등 도달 불가능한 위치, doc/0375 fallback으로 가장 가까운 지점까지만
            // 이동한 경우 포함) - 건설 명령을 취소하고 실패 음성을 재생한다 (doc/0382).
            if (!isAirUnit && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= navMeshAgent.stoppingDistance)
            {
                unitAudio?.PlayBuildFailVoice();
                HaltInPlace();
                CancelBuildOrder();
            }
            return;
        }

        hasBuildOrder = false;

        if (!isAirUnit)
            navMeshAgent.ResetPath();

        arrived = true;
        UnitcurrentState = UnitState.Idle;

        System.Action arrivedCallback = onBuildArrived;
        onBuildArrived = null;
        onBuildCancelled = null;

        arrivedCallback?.Invoke();
    }
```

`CancelBuildOrder()`가 실행하는 `onBuildCancelled` 콜백은 이미 `PlacementSystem.cs`에서
그리드 예약 해제 + 건물 가격 환불까지 처리하므로 별도 손볼 필요 없음.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs`
