# 0065. 부대 지정 선택 시 대기 중인 명령(공격/이동/순찰) 모드 해제

**날짜:** 2026-07-12

## 요청 내용
> 유닛을 선택하고 공격/순찰/이동 명령을 내리려는 상황(버튼을 눌러 대상/목적지 클릭을 기다리는 중)에서 부대 지정으로 다른 부대를 선택하면, 그 공격/명령 대기 상태를 빠져나오도록 해달라.

## 조사 결과 (현재 코드 상태)
- `UserControl.UsercurrentState`(`OrderState`)가 "다음 클릭을 무엇으로 처리할지"를 결정한다. `Attack`/`Move`/`Patrol`/`Rally`/`BuildingMove` 버튼을 누르면 이 상태로 들어가고, 그 다음 클릭(땅/적/아군 등)에서 실제 명령이 실행되며 다시 `None`으로 돌아간다.
- [[0059-control-group-assignment|0059]]에서 만든 `UserControl.HandleControlGroupInput()`은 숫자만 눌렀을 때 `RTSUnitController.SelectControlGroup()`을 호출해 선택 목록만 바꿀 뿐, `UsercurrentState`는 전혀 건드리지 않는다. 그 결과, 예를 들어 "공격" 버튼을 눌러 `Attack` 대기 상태로 들어간 뒤 숫자키로 다른 부대를 선택하면, 새로 선택된 부대가 여전히 `Attack` 대기 상태를 물려받아서 다음 클릭에 의도치 않게 공격 명령이 나가버린다.
- `Ctrl+숫자`(저장)/`Shift+숫자`(병합 추가)는 현재 선택을 바꾸지 않고 그냥 저장만 하므로 이 문제와 무관하다 - 오직 "숫자만 눌러서 선택"하는 경우에만 해당된다.

## 수정 내용
`Assets/Scripts/UserControl/UserControl.cs`의 `HandleControlGroupInput()`에서, 숫자만 눌러 부대를 선택하는 분기에 `UsercurrentState = OrderState.None;`을 추가한다.

```csharp
// 기존 코드
            if (ctrlHeld)
                rtsUnitController.AssignControlGroup(i);
            else if (shiftHeld)
                rtsUnitController.AddSelectedToControlGroup(i);
            else
                rtsUnitController.SelectControlGroup(i);

            break; // 한 프레임에 숫자 키 하나만 처리하면 충분
```
```csharp
// 변경 코드
            if (ctrlHeld)
                rtsUnitController.AssignControlGroup(i);
            else if (shiftHeld)
                rtsUnitController.AddSelectedToControlGroup(i);
            else
            {
                rtsUnitController.SelectControlGroup(i);
                UsercurrentState = OrderState.None; // 공격/이동/순찰 등 대기 중이던 명령 상태를 빠져나온다
            }

            break; // 한 프레임에 숫자 키 하나만 처리하면 충분
```

## 이번 수정에서 결정한 세부 동작
- **빈 부대(저장된 적 없거나 전멸)를 숫자로 눌렀을 때도** 명령 대기 상태를 해제합니다 - `SelectControlGroup`이 실제로 선택을 바꿨는지와 무관하게, 숫자키를 누르는 행위 자체를 "명령 대기 취소"로 취급하는 게 더 예측 가능한 동작이라고 판단했습니다(빈 부대 번호를 눌렀는데 공격 대기 상태가 계속 남아있으면 오히려 헷갈릴 수 있음).
- **Ctrl+숫자/Shift+숫자는 그대로 둡니다**: 이 둘은 현재 선택을 바꾸지 않고 저장만 하므로, 공격 대상을 고르던 중에 실수로 다른 부대에 현재 선택을 저장(Ctrl/Shift)해도 원래 하려던 공격 명령은 그대로 이어집니다.

## 변경 예정 파일
- `Assets/Scripts/UserControl/UserControl.cs`

## 상태
**적용 후 범위 수정됨 — 자세한 내용은 [[0066-control-group-select-cancels-order-state-scoped|0066]] 참고.**
사용자 피드백: "명령상태를 벗어나는게 아니라 A/M/P로 들어가는 공격위치·순찰·이동위치 지정 모드에서만 벗어나야 한다"는 요청에 따라, `UsercurrentState`를 무조건 `None`으로 초기화하던 부분을 `Attack`/`Move`/`Patrol`일 때만 초기화하도록 범위를 좁혔다(Rally/BuildingMove는 그대로 유지).
