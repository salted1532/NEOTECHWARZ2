# 0396 - "재탐색 3회 누적 시 포기" 임시 비활성화 (확인용)

**날짜:** 2026-08-03

**구현 완료. 이후 [[0397]]에서 이 상태를 최종본으로 확정 - "복구 방법"은 더 이상 유효하지 않음
(죽은 카운트 로직 자체를 삭제했음).**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 3회 쌓이는건 한번 빼볼래 확인해보게

[[0395]]에서 넣은 "`chaseStuckCount`가 `ChaseUnreachableRepathLimit`(3)에 도달하면 도달 불가로
포기" 판정 2곳(도착 후 대상 이동 감지 / 이동 중 진행도 정체 감지)을 확인 목적으로 임시 비활성화.
카운트 자체는 그대로 계속 누적하되, 그 값으로 취소하지는 않도록 `return false;`로 바꿔뒀다.

**주의**: 도착했는데 대상이 그 자리에 그대로 멈춰있는 경우의 **즉시 포기**([[0393]]에서 정한 별개
규칙, `return true;`)는 "3회 쌓이는 것"이 아니라서 이번 요청 범위에 포함하지 않고 그대로 뒀다.

## 코드 변경

### `Assets/Scripts/Unit/UnitController.cs` - `UpdateUnreachableChase()`

두 지점 모두 `return chaseStuckCount >= ChaseUnreachableRepathLimit;` → `return false;`로 교체,
`ponytail:` 주석으로 임시 상태와 복구 방법을 남겨둠:

```csharp
                chaseStuckCount++;
                // ponytail: 3회 누적 포기를 임시로 비활성화(확인 차 요청) - 카운트는 계속 쌓되 취소는
                // 안 함. 확인 끝나면 `return chaseStuckCount >= ChaseUnreachableRepathLimit;`로 복구 (doc/0396).
                return false;
```

(도착 후 대상 이동 감지 지점 / 이동 중 진행도 정체 감지 지점 두 군데 동일)

## 복구 방법

확인이 끝나면 위 두 곳의 `return false;`를 다시 `return chaseStuckCount >= ChaseUnreachableRepathLimit;`
로 되돌리면 [[0395]] 상태로 복귀한다.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
