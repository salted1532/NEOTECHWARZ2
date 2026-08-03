# 0419 - 건물 우클릭 특수 처리는 메인기지만, 나머지는 그냥 이동 명령 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 0개.

## 요청 내용

> 건물 우클릭이 메인기지만을 말하는거야 다른 건물을 우클릭했을땐 그냥 이동명령으로만
> 작동하게 해줘

[[0418]]에서 다룬 "건물 우클릭 시 자원 반납" 로직을 메인기지 우클릭에만 한정하고, 그 외
건물(메인기지가 아닌 모든 건물)은 자원 소지 여부와 상관없이 그냥 일반 이동 명령으로 처리해
달라는 요청.

## 현재 로직

`MoveToBuilding()`(`UnitController.cs:1670~1686`, `RTSUnitController.MoveToBuildingSelectedUnits()`가
건물 우클릭 시 호출):

```csharp
public void MoveToBuilding(BuildingController building)
{
    if (isConstructing) return;

    if (isWorker && IsCarryingResource())
    {
        if (building.CompareTag("MainBase"))
            ReturnCargoTo(building);
        else
            ReturnCargo(); // <- 메인기지가 아니어도 자원 들고 있으면 "가장 가까운 기지로 반납"
        return;
    }

    FollowBuilding(building); // <- 자원 없으면(또는 전투유닛) 건물을 계속 따라다님
}
```

지금은 메인기지가 아닌 건물을 우클릭해도, 자원을 들고 있으면 반납 로직(`ReturnCargo()` -
가장 가까운 기지로)이 걸리고, 자원이 없으면 그 건물을 계속 쫓아다니는(`FollowBuilding`) 동작을
한다. 요청하신 건 이 둘 다 없애고, **메인기지가 아닌 건물은 항상 그냥 그 자리로 이동만** 하게
하는 것.

## 사용자 재확인 - "이동 명령"은 사실 기존 FollowBuilding 그대로를 말한 것

1차 제안(위, `MoveTo`로 교체 + `FollowBuilding` 삭제)에 대해 사용자가 정정:

> 자원을 들고 있는 일꾼이 메인기지에 우클릭만 리턴및 캐왔던 자원으로 재취하러가기이고
> 나머지 건물은 자원을 들고 있던 안들고 있던 건물 따라가기 명령으로 처리해줘

즉 특수 처리(반납)는 "메인기지 + 자원 소지" 조합에만 걸리고, 그 외에는(다른 건물이든,
메인기지라도 자원이 없든) **기존 `FollowBuilding()` 그대로** 유지한다. `MoveTo`로 바꾸는
것도, `FollowBuilding()`을 지우는 것도 필요 없다 - 원래 있던 `else` 분기(자원 들고 다른
건물 클릭 시 `ReturnCargo()`로 가까운 기지에 반납하던 것)만 없애고 `FollowBuilding()`으로
떨어지게 하면 된다.

## 제안하는 수정 (최종)

```csharp
public void MoveToBuilding(BuildingController building)
{
    if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

    if (building.CompareTag("MainBase") && isWorker && IsCarryingResource())
    {
        ReturnCargoTo(building); // 메인기지 우클릭 + 자원 소지 - 그 기지로 직접 반납 후 캐던 자원으로 복귀
        return;
    }

    FollowBuilding(building); // 그 외(다른 건물, 또는 메인기지라도 자원 없음) - 기존 그대로 계속 따라다니기
}
```

`FollowBuilding()`/`FollowBuildingTick()`은 계속 쓰이므로 삭제하지 않는다.

## 영향받는 파일 (예정)

- `Assets/Scripts/Unit/UnitController.cs` (`MoveToBuilding()`, 부수 효과로
  `FollowBuilding()`/`FollowBuildingTick()`/관련 필드 삭제 여부)

## 요약

- `MoveToBuilding()`을 "메인기지 + 자원 소지"일 때만 `ReturnCargoTo()`로 반납하도록 하고, 그
  외(다른 건물, 또는 메인기지라도 자원 없음)는 기존 `FollowBuilding()` 그대로 유지.
- `FollowBuilding()`/`FollowBuildingTick()`은 계속 쓰이므로 삭제하지 않음.
- 컴파일 확인 완료(에러 0, 경고 0).
