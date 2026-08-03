# 0422 - 아군 따라가기(FollowTick)도 도달 불가 시 포기 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 34개(`FollowTick` 관련 새 경고
  없음 - 기존에 이미 있던 `ResourceNode.cs` 변경분에서 나온 경고).

## 요청 내용

> 문서 0417에서 아군강제공격, 적 강제공격과 같이 포기하는 부분을 추가했으면 좋겠어 아군
> 따라가기는 현재 포기하지 않고 계속 경로를 찾아서 이동한단 말이지. 강제공격 명령과 같이
> 포기하는 로직을 추가했으면 좋겠어

[[0417]]에서 `FollowTick()`에 `UpdateUnreachableChase()`를 연결하면서, 반환값(최종 도달
불가 판정)은 "따라가기는 끝까지 포기하지 않는다"는 기존 설계를 지키려고 **의도적으로
무시**하도록 만들었다 (`UnitController.cs:968~971`). 이번 요청은 그 설계를 뒤집어서,
`FriendlyAttackTick`(아군 강제공격, `UnitController.cs:897~903`) /
`AttackOrderTick`(적 강제공격, `UnitController.cs:1164~1168` 부근)과 동일하게 반환값이
`true`면 명령을 취소하고 제자리에 정지시키자는 것.

## 제안하는 수정

`FollowTick()`도 두 강제공격 Tick과 완전히 같은 패턴을 그대로 적용한다.

### `Assets/Scripts/Unit/UnitController.cs` (`FollowTick()`, 968번째 줄)

기존:
```csharp
        // 대상이 도달 불가 지형에 있어도(가장 가까운 위치로 이동 후 도착 시에만 재확인) 처리되도록
        // 강제공격과 같은 도달 가능/불가 로직을 재사용한다. 따라가기는 끝까지 포기하지 않으므로
        // 반환값(최종 도달 불가 판정)은 무시한다 (doc/0417).
        UpdateUnreachableChase(followTarget.transform.position, followTarget.isAirUnit, false);
```

변경:
```csharp
        // 대상이 도달 불가 지형에 있어도(가장 가까운 위치로 이동 후 도착 시에만 재확인) 처리되도록
        // 강제공격과 같은 도달 가능/불가 로직을 재사용한다. 강제공격과 동일하게, 재탐색을 거듭해도
        // 계속 도달 불가로 판정되면 따라가기 명령도 포기한다 (doc/0422 - doc/0417의 "끝까지 포기하지
        // 않는다" 설계를 뒤집음).
        if (UpdateUnreachableChase(followTarget.transform.position, followTarget.isAirUnit, false))
        {
            CancelAttackOrder();
            HaltInPlace();
        }
```

`CancelAttackOrder()`는 `hasFollowOrder`/`followTarget`을 포함해 명령 관련 상태 전체를
초기화하는 공용 취소 지점이라 그대로 재사용 가능하다 (`UnitController.cs:754~777`).
`HaltInPlace()`는 두 강제공격 Tick이 이미 같은 상황에서 쓰고 있는 정지 처리 함수
(`UnitController.cs:1408`).

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`FollowTick()`)

## 요약

- `FollowTick()`이 `UpdateUnreachableChase()`의 반환값을 더 이상 무시하지 않고, 아군/적
  강제공격과 동일하게 최종 도달 불가 판정 시 `CancelAttackOrder()` + `HaltInPlace()`로
  따라가기 명령을 포기하도록 변경.
- 도달 가능 모드(매 프레임 재확인)와 도달 불가 모드(가장 가까운 위치로 이동 후 도착 시에만
  재확인, 대상이 그 사이 움직였으면 계속 추격)는 [[0415]]/[[0417]] 로직 그대로 - 이번 변경은
  "최종 판정 후 어떻게 하느냐"만 강제공격과 통일한다.
