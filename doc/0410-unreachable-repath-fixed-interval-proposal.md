# 0410 - 도달 불가 재확인 간격을 지수 백오프에서 고정 1초로 변경 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

사용자가 제시한 "WaitingReachable 상태" 참고 설계는 [[0403]]/[[0406]]으로 이미 구현된 것과 사실상
동일했다(대응 관계는 아래 표). 유일한 차이는 재확인 간격 - 참고 설계는 "0.5~1초 고정", 현재 코드는
"0.2초로 시작해 실패마다 2배, 최대 4초"인 지수 백오프. 사용자가 **고정 간격으로 교체**를 선택.

| 참고 설계 | 현재 코드 |
|---|---|
| `Reachable?` 판정 | `IsPositionReachable()` ([[0403]]) |
| `WaitingReachable` 상태 | `chaseIsUnreachable` 플래그 ([[0406]]) |
| 대기 중 `MoveAgentTo()`/재탐색 호출 안 함 | `Time.time < nextUnreachableRepathTime`이면 그대로 `return false` |
| 재확인 간격 | (참고) 0.5~1초 고정 vs (현재) 0.2초→4초 지수 백오프 |

## 제안하는 수정

`UnreachableRepathInitialDelay`/`UnreachableRepathMaxDelay`/`unreachableRepathDelay`(2배씩 늘던
백오프 변수)를 없애고, 고정 간격 상수 하나(`UnreachableRepathInterval = 1f`)로 교체한다. 실패해도
간격이 안 늘어나므로 "성공 시 초기화"할 것도 없어져 관련 필드 3개 → 상수 1개로 단순해진다.

### `Assets/Scripts/Unit/UnitController.cs`

```csharp
private const float UnreachableRepathInterval = 1f;
private bool chaseIsUnreachable;
private float nextUnreachableRepathTime;
```

`justLeftAttackRange`/`!hasPath`/`targetMoved` 세 분기에서 `unreachableRepathDelay = ...` 초기화
라인 제거, `nextUnreachableRepathTime = Time.time + UnreachableRepathInterval;`로 통일 (백오프 2배
계산 부분도 제거).

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

동일하게 수정.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`UpdateUnreachableChase()` + 필드)
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (`ChaseTarget()` + 필드)

## 요약

- `UnreachableRepathInitialDelay`/`UnreachableRepathMaxDelay`/`unreachableRepathDelay`(지수 백오프
  변수 3개)를 제거하고 `UnreachableRepathInterval = 1f`(고정 상수 1개)로 교체.
- 도달 불가 상태 유지 중이면 실패해도 간격이 안 늘어나고 항상 1초마다 재확인.
- 명령 시작 지점(`CancelAttackOrder`/`AttackUnitTarget`/`AttackFriendlyTarget`)의 백오프 초기화
  라인도 함께 제거 - 더 이상 초기화할 게 없음.
- 플레이어(`UnitController`)/적(`EnemyUnitController`) 양쪽 다 적용.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
