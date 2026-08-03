# 0416 - 도달 가능/불가 확인 결과를 매번 디버그 로그로 남기기 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 추적자가 목적지가 도달 가능한지 불가능한지에 대한 결과를 디버그 로그 남겨줘

지금은 [[0407]]에서 넣은 로그가 "상태가 바뀌는 순간"에만 찍힌다(도달 불가로 막 전환됐을 때 /
도달 불가 모드에서 다시 가능해졌을 때). 이번 요청은 `IsPositionReachable()`을 호출할 때마다
**그 결과 자체**(가능/불가)를 매번 로그로 남기라는 것으로 이해했다.

## 참고 - 호출 빈도

[[0415]]의 "도달 가능 모드"는 게이트 없이 **매 프레임** `IsPositionReachable()`을 부르므로,
그 결과를 매번 로그로 남기면 추격 중엔 콘솔에 로그가 초당 수십 줄씩 쌓인다. 디버깅 목적이라
일단 요청하신 대로 넣고, 나중에 시끄러우면 빈도를 줄이자고 제안한다.

## 코드 변경

`IsPositionReachable()` 호출 지점 2곳(도달 불가 모드에서 도착 시 재확인 / 도달 가능 모드에서
매 프레임 재확인) 각각에서 결과를 변수로 받아 로그를 남긴 뒤 그 값을 그대로 쓴다.

### `Assets/Scripts/Unit/UnitController.cs`

```csharp
            // 도착(또는 더 갈 수 없어 멈춤) - 여기서만 재탐색(도달 가능 여부 재확인)한다.
            bool reachableOnArrival = IsPositionReachable(targetPos);
            Debug.Log($"{name}: [도달 불가 추격] 재탐색 결과 - {(reachableOnArrival ? "도달 가능" : "도달 불가")}");
            if (reachableOnArrival)
            {
                chaseIsUnreachable = false;
                MoveAgentTo(targetPos, false);
                return false;
            }
```

```csharp
        // 도달 가능 모드: 게이트 없이 매 프레임 실시간으로 계속 추적/재확인한다. ...
        bool reachableNow = IsPositionReachable(targetPos);
        Debug.Log($"{name}: [추격] 재탐색 결과 - {(reachableNow ? "도달 가능" : "도달 불가")}");
        if (!reachableNow)
        {
            chaseIsUnreachable = true; // 방금 도달 불가로 전환
        }

        MoveAgentTo(targetPos, false);
```

(기존 "도달 가능해짐 - 재탐색"/"도달 불가로 전환" 로그는 정보가 겹치므로 위 결과 로그로 대체)

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

같은 두 지점에 동일하게 적용.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()`)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()`)

## 요약

- `IsPositionReachable()` 호출 지점 2곳(도달 불가 모드 도착 시 재확인 / 도달 가능 모드 매 프레임
  재확인) 각각에서 결과를 `Debug.Log($"{name}: [...] 재탐색 결과 - 도달 가능/도달 불가")`로 매번
  남기도록 변경. 기존 전환 전용 로그는 이 결과 로그로 대체.
- 플레이어(`UnitController`)/적(`EnemyUnitController`) 양쪽 다 적용.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
