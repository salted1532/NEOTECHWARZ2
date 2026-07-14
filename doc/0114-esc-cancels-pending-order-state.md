# 0114 - ESC로 대기 중인 명령(공격A/이동M/순찰P 등) 취소

## 1. 요청
"A나 M키로 명령을 내리는거 ESC로 취소 할수 있게좀 해줘" → "키보드로 명령 내리는거 있잖아 위치 지정하는
명령등 공격A, 이동M, 순찰P 등"

## 2. 배경
`UserControl.cs`는 A(공격)/M(이동)/P(순찰)/Y(랠리) 등을 누르면 즉시 명령이 실행되는 게 아니라
`UsercurrentState`(`OrderState` enum: Attack/Move/Patrol/Rally/BuildingMove)를 그 값으로 바꿔서 "다음
클릭으로 위치·대상을 지정해달라"는 대기 상태로 들어간다(`UpdatePointer()`가 이 상태에 맞는 포인터를
마우스 위치에 그려줌). 지금까지는 이 대기 상태를 취소하는 방법이 컨트롤 그룹 숫자키를 누르는 것뿐이었다
(`HandleControlGroupInput()`에 이미 있던 로직, 단 Attack/Move/Patrol만 취소하고 Rally/BuildingMove는
의도적으로 유지).

## 3. 변경
`UserControl.HandlekeyBoard()`에 ESC 처리를 추가했다. 기존 그룹키 취소 로직과 달리 Rally/BuildingMove를
포함한 **모든** 대기 상태를 취소한다 — ESC는 범용 "취소" 키라 특정 두세 개만 봐줄 이유가 없다고 판단.

```csharp
// Assets/Scripts/UserControl/UserControl.cs, HandlekeyBoard() 안, 기존 빌드모드 ESC 처리 바로 다음
if (Input.GetKeyDown(KeyCode.Escape) && UsercurrentState != OrderState.None)
{
    UsercurrentState = OrderState.None;

    attackPointer.SetActive(false);
    movePointer.SetActive(false);
}
```

빌드 모드(`rtsUnitController.IsBuildMode()`)의 기존 ESC 처리(`ReturnState()`)는 `UsercurrentState`와
별개의 상태라 그대로 두고 건드리지 않았다 — 같은 프레임에 두 조건이 동시에 참이어도 서로 독립적으로
동작하므로 문제없다.
