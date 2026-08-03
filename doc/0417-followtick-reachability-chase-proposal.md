# 0417 - 아군 우클릭 따라가기(FollowTick)에도 도달 가능/불가 로직 적용 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 아군 강제 공격에 관해서는 잘 작동하는데 아군 유닛 우클릭을 통해서 따라가기일때는 적용이
> 안된거야?

확인 결과 맞다 - `FollowTick()`(`UnitController.cs:926~969`)은 `UpdateUnreachableChase()`/
`IsPositionReachable()`를 전혀 거치지 않고, 정지 거리보다 멀면 매 프레임 그냥
`MoveAgentTo(followTarget.transform.position, ...)`만 호출한다. [[0415]]의 도달 가능/불가
두 모드 로직은 `FriendlyAttackTick`/`AttackOrderTick`에만 적용돼 있었다. 사용자가 이어서
"진행시켜줘"로 적용을 요청.

## 제안하는 수정

`FollowTick()` 마지막의 `MoveAgentTo(...)` 호출을 `UpdateUnreachableChase(...)` 호출로
교체한다. `UpdateUnreachableChase`는 이미 범용(공격 관련 필드에 의존하지 않고 `targetPos`만
받음)이라 그대로 재사용 가능하다. 차이점: 따라가기는 "끝까지 포기하지 않고 계속 따라간다"는
기존 설계([[0388]] 근처 주석 - 아군 강제공격과 동일하게 "죽을 때까지 추격")를 그대로 유지해야
하므로, 반환값(도달 불가 최종 판정)은 **무시**한다 - 대상이 도달 불가 지형에서 정지해 있어도
따라가기 명령 자체를 취소하지 않고, 도착한 자리에서 매 프레임 재확인만 계속하다가(비용은
가벼운 `IsPositionReachable` 조회뿐) 대상이 다시 도달 가능한 곳으로 오면 자동으로 재개된다.

`justLeftAttackRange` 인자는 따라가기에 해당 개념이 없으므로 `false`로 고정 전달한다
("교전 중이면 그대로 둔다" 분기가 이 호출보다 앞에서 이미 걸러줌).

### `Assets/Scripts/Unit/UnitController.cs` (`FollowTick()`, 968번째 줄)

기존:
```csharp
        MoveAgentTo(followTarget.transform.position, followTarget.isAirUnit);
```

변경:
```csharp
        // 대상이 도달 불가 지형에 있어도(가장 가까운 위치로 이동 후 도착 시에만 재확인) 처리되도록
        // 강제공격과 같은 도달 가능/불가 로직을 재사용한다. 따라가기는 끝까지 포기하지 않으므로
        // 반환값(최종 도달 불가 판정)은 무시한다 (doc/0417).
        UpdateUnreachableChase(followTarget.transform.position, followTarget.isAirUnit, false);
```

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`FollowTick()`)

## 요약

- `FollowTick()`의 `MoveAgentTo()` 직접 호출을 `UpdateUnreachableChase()` 호출로 교체 - 강제공격과
  같은 도달 가능/불가 두 모드 로직을 그대로 재사용.
- 따라가기는 대상이 죽을 때까지 포기하지 않는다는 기존 설계를 유지하기 위해 반환값(최종 도달
  불가 판정)은 무시 - 도달 불가 지형에서도 명령이 취소되지 않고 도착한 자리에서 계속 재확인만
  하다가 대상이 도달 가능해지면 자동 재개된다.
- 컴파일 확인 완료(에러 0, 경고 33 - 기존과 동일).
