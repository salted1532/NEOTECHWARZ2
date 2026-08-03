# 0407 - 재탐색/가까운 위치 이동 시 디버그 로그 추가 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 경로 재탐색 하는 경우와 가까운 위치 계산하는 부분이 작동할때마다 디버그 로그 남기도록 해줘

[[0406]]에서 넣은 쿨다운/백오프 로직이 실제로 어떻게 동작하는지(언제 진짜 재탐색을 하고, 언제
"가까운 위치"로만 이동시키는지) 눈으로 확인하기 위한 로그.

## 적용 위치

`UpdateUnreachableChase()`(`UnitController.cs`)와 `ChaseTarget()`(`EnemyUnitController.cs`)의
`targetMoved` 분기 안, `MoveAgentTo()`를 부르는 4곳 각각에 `Debug.Log` 추가:

1. 쿨다운 만료 후 재확인해서 **도달 가능해짐** → 진짜 재탐색.
2. 쿨다운 만료 후 재확인해도 **여전히 도달 불가** → 가장 가까운 위치로 갱신 이동.
3. (쿨다운 상태 아님) 대상이 움직였는데 **도달 가능** → 재탐색.
4. (쿨다운 상태 아님) 대상이 움직였는데 **도달 불가로 막 전환** → 가장 가까운 위치로 1회 이동,
   쿨다운 진입.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs`

```csharp
        if (targetMoved)
        {
            if (chaseIsUnreachable)
            {
                if (Time.time < nextUnreachableRepathTime)
                    return false;

                if (IsPositionReachable(targetPos))
                {
                    chaseIsUnreachable = false;
                    unreachableRepathDelay = UnreachableRepathInitialDelay;
                    Debug.Log($"{name}: [도달 불가 추격] 도달 가능해짐 - 재탐색");
                    MoveAgentTo(targetPos, false);
                }
                else
                {
                    Debug.Log($"{name}: [도달 불가 추격] 여전히 도달 불가 - 가까운 위치로 이동 (다음 쿨다운 {unreachableRepathDelay * 2f}s)");
                    MoveAgentTo(targetPos, false);
                    unreachableRepathDelay = Mathf.Min(unreachableRepathDelay * 2f, UnreachableRepathMaxDelay);
                    nextUnreachableRepathTime = Time.time + unreachableRepathDelay;
                }

                return false;
            }

            if (IsPositionReachable(targetPos))
            {
                Debug.Log($"{name}: [도달 불가 추격] 재탐색");
                MoveAgentTo(targetPos, false);
            }
            else
            {
                Debug.Log($"{name}: [도달 불가 추격] 도달 불가로 전환 - 가까운 위치로 이동");
                MoveAgentTo(targetPos, false);
                chaseIsUnreachable = true;
                unreachableRepathDelay = UnreachableRepathInitialDelay;
                nextUnreachableRepathTime = Time.time + unreachableRepathDelay;
            }

            return false;
        }
```

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

같은 4곳에 동일하게 추가.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()`)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()`)

## 요약

- `UnitController.UpdateUnreachableChase()`/`EnemyUnitController.ChaseTarget()`의 `MoveAgentTo()`
  호출 4곳(재탐색 2곳 + 가까운 위치 이동 2곳) 각각에 `Debug.Log($"{name}: [도달 불가 추격] ...")`
  추가.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
