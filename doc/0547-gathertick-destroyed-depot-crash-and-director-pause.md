# 0547 - GatherTick MissingReferenceException 근본 수정 + EnemyAIDirector 목표 소진 시 보류

## 날짜
2026-08-13

## 요청 내용
```
MissingReferenceException: The object of type 'UnityEngine.Transform' has been destroyed but you are
still trying to access it.
...
UnitController.GatherTick () (at Assets/Scripts/Unit/UnitController.cs:1850)
UnitController.Update () (at Assets/Scripts/Unit/UnitController.cs:465)
```
"이런식으로 아마 공격할곳이 없어서 그런거 같은데 그렇게 되면 찾을게 없으면 공격웨이브도 정지하고
점령도 정지하고 스크립트가 보류 상태로 넘어가도록 예외처리해줘"

## 원인 조사
크래시 지점은 **플레이어 쪽** `UnitController.GatherTick()`, `GatherState.MovingToBase` 케이스
(`UnitController.cs:1850`) - 일꾼이 채취한 자원을 반납하러 메인기지로 걸어가는 중에 그 메인기지가
파괴되면, 캐싱해둔 `depositTargetTransform`(Transform 참조)이 Unity가 파괴한 오브젝트를 가리키게 된다.
바로 아래 "이륙 중" 케이스(1851~1866줄)는 대체 기지를 찾는 재탐색 로직이 있는데, **파괴된 경우엔 이
재탐색 로직에 도달하기도 전에** `depositTargetTransform.GetComponent<BuildingController>()`가 죽은
참조에 접근해서 바로 예외가 남 - null 체크가 아예 없었음.

**왜 지금 발생했는지**: EnemyAIDirector가 이번에 doc/0534에서 플레이어 `MainBase`를 무작위로 골라
공격하고 파괴되면 즉시 다른 곳으로 재조준하는 기능이 생겼다 - 그 전엔 미션 중 플레이어 메인기지가
"일꾼이 반납하러 걸어가는 도중에" 파괴되는 상황 자체가 거의 없었을 것이라 이 구멍이 안 드러났던 것으로
보임. 즉 사용자가 지목한 "공격할 곳이 없어서"라기보다는, **"반납하러 가던 건물이 마침 EnemyAIDirector에게
파괴됐다"**는 타이밍 문제 - 근본 원인은 EnemyAIDirector가 아니라 `UnitController`의 null 체크 누락.

두 가지를 각각 고침:
1. **근본 수정(크래시 자체)**: `UnitController.GatherTick()`의 `MovingToBase` 케이스에 null 체크 추가.
2. **요청하신 부가 조치**: `EnemyAIDirector`가 플레이어 건물이 하나도 안 남으면(더 공격/점령할 대상
   자체가 없으면) 웨이브/별동대 스케줄을 더 돌리지 않고 보류 상태로 넘어가게 함.

## 수정 1 - `UnitController.GatherTick()` null 체크 (근본 원인)
`MovingToBase` 케이스 진입 시, 바로 아래 "이륙 중" 케이스가 이미 쓰고 있는 "대체 반납처 재탐색"
패턴(`FindNearestDepositBuilding()`)을 "파괴된 경우"에도 그대로 적용 - 대체 기지가 있으면 갈아타고,
없으면 기존과 동일하게 화물을 든 채 멈춘다(`CancelGathering()`).

### 기존 코드
```csharp
case GatherState.MovingToBase:
    BuildingController depositBuilding = depositTargetTransform.GetComponent<BuildingController>();
    if (depositBuilding != null && depositBuilding.IsLifted())
    {
        ...
```

### 변경 코드
```csharp
case GatherState.MovingToBase:
    if (depositTargetTransform == null)
    {
        // 반납하러 가던 건물이 이동 중 파괴됨 - "이륙 중" 케이스와 동일한 재탐색 패턴으로 다른
        // 메인기지가 있으면 갈아타고, 없으면 화물을 든 채 멈춘다(doc/0547).
        Transform alt = FindNearestDepositBuilding();
        if (alt == null)
        {
            CancelGathering();
            return;
        }

        depositTargetTransform = alt;
        MoveToDepositTargetOrWait();
        break;
    }

    BuildingController depositBuilding = depositTargetTransform.GetComponent<BuildingController>();
    if (depositBuilding != null && depositBuilding.IsLifted())
    {
        ...
```

## 수정 2 - `EnemyAIDirector` 플레이어 전멸 시 웨이브/별동대 보류
`rtsController.BuildingList`(플레이어 건물 전체)가 완전히 비면 "더 공격/점령할 대상이 없다"는 뜻으로
보고, `AttackWaveRoutine`/`RaidRoutine` 둘 다 그 시점부터 더 이상 다음 웨이브/별동대를 스케줄하지 않고
코루틴을 끝낸다(`yield break`) - 재시작 로직은 없음(플레이어가 전멸했으면 사실상 미션이 끝난 상태라
다시 살아날 걸 기다릴 필요가 없다고 보고 단순하게 처리). `ReinforceRoutine`(보충 생산)과 기지 방어
반응은 그대로 유지 - "공격/점령만" 멈추라는 요청이라 다른 기능까지 끌 필요는 없음.

### 코드 변경
```csharp
// 플레이어 건물이 하나도 안 남았는지 - 더 공격/점령할 대상 자체가 없다는 신호로 쓴다(doc/0547).
private bool IsPlayerDefeated() =>
    rtsController == null || rtsController.BuildingList.FindAll(b => b != null).Count == 0;
```
`AttackWaveRoutine()`의 `for`/`while(true)` 루프 시작 지점과, `RaidRoutine()`의 `while(true)` 루프
시작 지점에 각각 `if (IsPlayerDefeated()) yield break;`를 추가 - 다음 카운트다운을 시작하기 전에 먼저
확인해서, 플레이어가 전멸한 순간부터는 카운트다운조차 돌지 않고 바로 멈춘다.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존 베이스라인과 동일).

## 영향받는 파일
- 변경: `Assets\Scripts\Unit\UnitController.cs` (크래시 근본 수정)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs` (요청하신 부가 조치)
