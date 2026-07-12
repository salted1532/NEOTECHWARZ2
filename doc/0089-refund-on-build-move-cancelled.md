# 0089. 건설 이동 중(도착 전) 이동/공격 명령으로 취소 시 건물 가격 환불 (제안)

**날짜:** 2026-07-12

## 요청 내용
> 현재 일꾼이 건설모드에서 건물을 선택하고 건설할 위치를 지정하면 자원을 쓰는데 중간에 이동명령이나 공격등으로 건설 명령을 취소했을때 건물의 가격만큼 환불 되도록해줘

## 조사 내용

### 현재 흐름 (`PlacementSystem.cs`)
- `PlaceStructure()`에서 클릭 즉시 `rtsController.TryConstructBuilding(data.ID)`로 **자원을 바로 차감**한다 (`PlacementSystem.cs:157`).
- 그 다음 `worker.GoBuild(spawnPos, onArrived: StartConstruction, onCancelled: CancelReservedConstruction)`를 호출해 일꾼을 건설 위치로 이동시킨다 (`PlacementSystem.cs:172-175`).
- 일꾼이 도착하기 **전에** 다른 명령(이동/공격 등)이 들어오면 `UnitController`의 `MoveTo`/`AttackUnitTarget`/`AttackMoveTo`/`AttackFriendlyTarget`/`FollowUnit` 등이 `CancelAttackOrder()` → `CancelBuildOrder()`를 호출하고, 이게 `onBuildCancelled` 콜백(`= CancelReservedConstruction`)을 실행한다 (`UnitController.cs:632-644`).
- 그런데 `CancelReservedConstruction(gridPos, ghost)`는 **그리드 예약 해제 + 고스트 제거만** 하고, **자원 환불은 하지 않는다** (`PlacementSystem.cs:204-210`).

→ 즉, 일꾼이 건설 위치로 이동하는 도중 이동/공격 명령으로 그 이동이 취소되면, 이미 차감된 건물 가격이 그대로 날아가는 버그(요청하신 미구현 상태)가 맞습니다.

### 이미 구현된 것과의 구분 ([[0044-production-and-construction-refunds]])
- 0044는 일꾼이 **도착해서 `BaseStructure`가 생성된 이후** 플레이어가 Info_panel의 "취소" 버튼/단축키(T)로 명시적으로 취소하는 경우만 환불한다 (`BaseStructure.CancelConstruction()` → `rtsController.RefundBuilding(buildingID)`).
- 이번 요청은 그보다 **이전 단계** — 아직 `BaseStructure`도 생성되기 전, 일꾼이 건설 위치로 걸어가는 중에 다른 명령으로 취소되는 경우를 다룬다. 서로 다른 코드 경로라 겹치거나 충돌하지 않는다.
- 주의: `BaseStructure.Initialize()`에 넘기는 `onCancelledByPlayer` 콜백도 내부적으로 같은 `CancelReservedConstruction(gridPos, null)`을 호출하는데(`PlacementSystem.cs:196`), 이 경로는 `BaseStructure.CancelConstruction()`이 **이미 자체적으로 환불**을 한 뒤에 호출하는 것이므로, `CancelReservedConstruction` 함수 자체에 환불 로직을 넣으면 이 경로에서 **이중 환불**이 발생한다. 따라서 환불은 `CancelReservedConstruction` 공용 함수가 아니라, `GoBuild`의 `onCancelled` 콜백(건설 이동 중 취소 전용)에만 추가한다.

## 설계안

**`PlacementSystem.cs`** — `PlaceStructure()`의 `GoBuild` 호출부만 수정 (건물 가격 환불 추가):

```csharp
// 기존 코드
        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, groundPos, gridPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));
```
```csharp
// 변경 코드
        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, groundPos, gridPos, placedIndex, ghost, worker),
            onCancelled: () =>
            {
                // 일꾼이 건설 위치에 도착하기 전(BaseStructure 생성 전)에 이동/공격 등 다른 명령으로
                // 건설 이동이 취소된 경우: 클릭 시 이미 차감된 건물 가격(광물/가스)을 전액 환불한다.
                CancelReservedConstruction(gridPos, ghost);
                rtsController?.RefundBuilding(data.ID);
            });
```

`CancelReservedConstruction` 함수 자체, `BaseStructure.Initialize()`에 넘기는 다른 취소 콜백(`() => CancelReservedConstruction(gridPos, null)`, `PlacementSystem.cs:196`), `RefundBuilding()`(0044에서 이미 구현됨, `RTSUnitController.cs:790`)은 변경 없음.

## 결정한 세부 동작
- **환불액**: 0044와 동일하게 진행률 무관 전액(광물/가스) 환불. 인구수는 애초에 건설 중 소모하지 않으므로 대상 아님.
- **적용 범위**: `MoveTo`, `AttackUnitTarget`, `AttackMoveTo`, `AttackFriendlyTarget`, `FollowUnit` 등 `CancelBuildOrder()`를 경유하는 모든 명령이 자동으로 이 환불을 트리거함 (별도 분기 불필요 — 전부 같은 `onBuildCancelled` 콜백을 탐).
- **이중 환불 방지**: `CancelReservedConstruction` 공용 함수는 손대지 않고, `GoBuild`의 `onCancelled`에서만 환불을 추가해 0044의 취소/파괴 경로와 겹치지 않게 함.

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs` (1곳)

## 상태
**적용 완료** — 제안대로 `Assets/Scripts/BuildSystem/PlacementSystem.cs`의 `GoBuild` 취소 콜백에 `RefundBuilding` 호출을 추가함.
