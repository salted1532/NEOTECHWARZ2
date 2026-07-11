# 0067. 부대 지정으로 A/M/P 모드 취소 시 포인터 마커도 함께 숨김

**날짜:** 2026-07-12

## 요청 내용
> [[0066-control-group-select-cancels-order-state-scoped|0066]] 이후 모드는 잘 바뀌는데, 마커(공격/이동 포인터)가 `false`로 안 꺼져서 그 자리에 남아있다. 명령 모드가 취소된 거고 부대 지정으로 선택도 바뀐 거니까 마커도 안 보이게 해달라.

## 원인
`UserControl.UpdatePointer()`는 `UsercurrentState`가 `Attack`일 때 `attackPointer`를, `Move`/`Patrol`/`Rally`/`BuildingMove`일 때 `movePointer`를 매 프레임 마우스 위치로 계속 켜놓고 따라다니게 한다. 그런데 `None`으로 바뀌었을 때를 처리하는 `else` 분기는 비어있어서, 상태가 `None`으로 바뀌어도 이미 켜져 있던 포인터를 끄는 코드가 없었다. [[0066-control-group-select-cancels-order-state-scoped|0066]]에서 부대 지정 시 `UsercurrentState`만 `None`으로 되돌렸을 뿐 포인터 오브젝트 자체는 그대로 뒀기 때문에, 마우스를 따라다니던 마커가 마지막 위치에 멈춘 채로 남아있었다.

## 수정 내용
`Assets/Scripts/UserControl/UserControl.cs`의 `HandleControlGroupInput()`에서, A/M/P 모드를 취소하는 시점에 `attackPointer`/`movePointer`를 함께 꺼준다.

```csharp
// 기존 코드
                rtsUnitController.SelectControlGroup(i);

                // A/M/P로 들어간 "공격 위치/순찰/이동 위치 지정" 대기 모드에서만 빠져나온다 (Rally/BuildingMove는 그대로 유지)
                if (UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol)
                    UsercurrentState = OrderState.None;
```
```csharp
// 변경 코드
                rtsUnitController.SelectControlGroup(i);

                // A/M/P로 들어간 "공격 위치/순찰/이동 위치 지정" 대기 모드에서만 빠져나온다 (Rally/BuildingMove는 그대로 유지)
                if (UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol)
                {
                    UsercurrentState = OrderState.None;

                    // 명령을 취소했으니 마우스를 따라다니던 대기 중 마커(공격/이동 포인터)도 그 자리에 남지 않도록 끈다
                    attackPointer.SetActive(false);
                    movePointer.SetActive(false);
                }
```

## 변경 예정 파일
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료.**
