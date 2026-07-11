# 0066. 부대 지정 선택 시 취소 범위를 A/M/P 모드로 한정 (0065 수정)

**날짜:** 2026-07-12

## 요청 내용
> [[0065-control-group-select-cancels-order-state|0065]]에서 만든 "부대 지정 선택 시 명령 대기 상태 해제"가 "명령 상태를 벗어나는" 식이 아니라, 정확히는 A키/M키/P키를 눌렀을 때 들어가는 "공격 위치·순찰·이동 위치 지정" 모드에서만 벗어나야 한다. 0065에서 수정한 내용을 되돌리고 다시 수정해달라.

## 조사 결과
0065는 숫자로 부대를 선택할 때 `UsercurrentState`(`OrderState`)를 조건 없이 `None`으로 초기화했다. 그런데 `OrderState`에는 `Attack`/`Move`/`Patrol`(A/M/P 버튼·단축키로 진입) 외에도 `Rally`(Y 단축키, 건물 랠리 포인트 지정 대기)와 `BuildingMove`([[0057-lift-freeflight-land-lock-shortcut|리프트 중인 건물의 M 이동 버튼]] 대기)가 있다. 요청하신 범위는 정확히 `Attack`/`Move`/`Patrol` 세 가지("공격위치, 순찰, 이동위치 정하는 모드")만이고, `Rally`/`BuildingMove`는 대상이 아니다.

## 수정 내용
`Assets/Scripts/UserControl/UserControl.cs`의 `HandleControlGroupInput()`에서, 무조건 초기화하던 부분을 `UsercurrentState`가 `Attack`/`Move`/`Patrol`일 때만 초기화하도록 좁혔다.

```csharp
// 기존 코드 (0065)
            else
            {
                rtsUnitController.SelectControlGroup(i);
                UsercurrentState = OrderState.None; // 공격/이동/순찰 등 대기 중이던 명령 상태를 빠져나온다
            }
```
```csharp
// 변경 코드
            else
            {
                rtsUnitController.SelectControlGroup(i);

                // A/M/P로 들어간 "공격 위치/순찰/이동 위치 지정" 대기 모드에서만 빠져나온다 (Rally/BuildingMove는 그대로 유지)
                if (UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol)
                    UsercurrentState = OrderState.None;
            }
```

## 이번 수정에서 결정한 세부 동작
- **`Rally`/`BuildingMove` 대기 중에 부대를 선택해도 그 대기 상태는 유지됩니다** - 요청 범위에 명시적으로 포함되지 않아서 그대로 뒀습니다. 필요하시면 별도 요청 주시면 추가하겠습니다.

## 변경 예정 파일
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 완료.**
