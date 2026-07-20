# 0181 - 부대 지정(컨트롤 그룹) 더블클릭 시 카메라 이동 설계 및 적용

## 요청

"부대지정 후 부대 버튼 1~0까지 각 키보드를 더블클릭 시 해당하는 유닛 위치로 카메라 이동하는걸
구현하고 싶어. 만약 유닛이 하나가 아니라 여러 유닛이면 그 리스트에 있는 유닛중 가장 앞에 [0]번
인덱스에 있는 유닛의 위치로 카메라가 이동했으면 좋겠어" — 컨트롤 그룹(1~9,0)을 숫자 키 두 번 빠르게
누르면(더블클릭) 그 그룹에 저장된 유닛 리스트의 `[0]`번째 유닛 위치로 카메라를 이동시키고 싶다는 요청.

## 현재 구조 확인

- `Assets/Scripts/UserControl/UserControl.cs`
  - `controlGroupKeys` = `Alpha1~Alpha9, Alpha0` (인덱스 0~9가 그대로 그룹 번호).
  - `HandleControlGroupInput()`: 매 프레임 `Input.GetKeyDown`으로 각 그룹 키를 검사.
    - Ctrl+숫자 → `AssignControlGroup(i)` (덮어쓰기 저장)
    - Shift+숫자 → `AddSelectedToControlGroup(i)` (병합 추가)
    - 숫자만 → `SelectControlGroup(i)` (저장된 그룹 선택) ← **여기에 더블클릭 감지를 추가**
- `Assets/Scripts/System/RTSUnitController.cs`
  - `controlGroupUnits`/`controlGroupBuildings`: `List<UnitController>[10]` / `List<BuildingController>[10]`.
  - `SelectControlGroup(groupIndex)`: null이 된(죽은/파괴된) 대상을 걸러내고 선택 상태로 되돌림.
- `Assets/Scripts/Camera/CameraControl.cs`
  - `JumpToWorldXZ(Vector3 worldPoint)`: 이미 존재하는 공개 메서드. X/Z만 즉시 순간이동시키고 높이(Y)/회전은
    그대로 유지 — 미니맵 클릭(`MinimapController.cs`)이 이미 이 메서드로 카메라를 이동시키는 데 쓰고 있음.
    이번 기능도 이걸 그대로 재사용하면 됨(새 카메라 이동 로직 불필요).

즉 "그룹의 [0]번 유닛 위치 구하기"와 "더블클릭 감지"만 추가하면, 실제 카메라 이동은 기존
`JumpToWorldXZ`로 충분함.

## 제안하는 변경

### 1. `RTSUnitController.cs` — 그룹의 포커스 위치(0번 인덱스) 조회 메서드 추가

`SelectControlGroup` 바로 아래(같은 `#region 부대 지정(컨트롤 그룹)` 안)에 추가:

```csharp
// 더블클릭 시 카메라가 이동할 기준 좌표. 유닛이 여러 개면 리스트의 [0]번째(가장 먼저 저장/추가된) 유닛을 우선하고,
// 유닛이 하나도 없으면(건물만 지정된 그룹) 건물 [0]번째를 대신 사용한다.
public bool TryGetControlGroupFocusPosition(int groupIndex, out Vector3 position)
{
    position = default;

    if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
        return false;

    controlGroupUnits[groupIndex].RemoveAll(unit => unit == null);
    controlGroupBuildings[groupIndex].RemoveAll(building => building == null);

    if (controlGroupUnits[groupIndex].Count > 0)
    {
        position = controlGroupUnits[groupIndex][0].transform.position;
        return true;
    }

    if (controlGroupBuildings[groupIndex].Count > 0)
    {
        position = controlGroupBuildings[groupIndex][0].transform.position;
        return true;
    }

    return false;
}
```

### 2. `UserControl.cs` — 더블클릭 감지 + 카메라 이동 호출

필드 추가 (기존 `mainCamera` 근처):

```csharp
[SerializeField]
private CameraControl mainCameraControl; // 더블클릭 시 카메라 이동에 사용 (MinimapController와 동일한 참조 방식)
```

`controlGroupKeys` 근처에 더블클릭 판정용 상태 추가:

```csharp
private const float ControlGroupDoubleClickThreshold = 0.3f; // 이 시간(초) 안에 같은 그룹 키를 다시 누르면 더블클릭으로 간주
private readonly float[] lastControlGroupPressTime = new float[10];
```

`Awake()`에 초기화 추가(게임 시작 직후 첫 입력이 실수로 더블클릭 처리되지 않도록):

```csharp
for (int i = 0; i < lastControlGroupPressTime.Length; i++)
    lastControlGroupPressTime[i] = float.NegativeInfinity;
```

`HandleControlGroupInput()`의 else 분기(현재 90번째 줄대) 수정:

Before:
```csharp
            else
            {
                rtsUnitController.SelectControlGroup(i);

                // A/M/P로 들어간 "공격 위치/순찰/이동 위치 지정" 대기 모드에서만 빠져나온다 (Rally/BuildingMove는 그대로 유지)
                if (UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol)
                {
                    UsercurrentState = OrderState.None;

                    // 명령을 취소했으니 마우스를 따라다니던 대기 중 마커(공격/이동 포인터)도 그 자리에 남지 않도록 끈다
                    attackPointer.SetActive(false);
                    movePointer.SetActive(false);
                }
            }
```

After:
```csharp
            else
            {
                rtsUnitController.SelectControlGroup(i);

                bool isDoubleClick = Time.time - lastControlGroupPressTime[i] <= ControlGroupDoubleClickThreshold;
                lastControlGroupPressTime[i] = Time.time;

                if (isDoubleClick && mainCameraControl != null &&
                    rtsUnitController.TryGetControlGroupFocusPosition(i, out Vector3 focusPosition))
                {
                    mainCameraControl.JumpToWorldXZ(focusPosition);
                }

                // A/M/P로 들어간 "공격 위치/순찰/이동 위치 지정" 대기 모드에서만 빠져나온다 (Rally/BuildingMove는 그대로 유지)
                if (UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol)
                {
                    UsercurrentState = OrderState.None;

                    // 명령을 취소했으니 마우스를 따라다니던 대기 중 마커(공격/이동 포인터)도 그 자리에 남지 않도록 끈다
                    attackPointer.SetActive(false);
                    movePointer.SetActive(false);
                }
            }
```

### 3. 씬 와이어링 — `mainCameraControl` 필드 연결

`MinimapController`가 이미 각 씬에서 Main Camera의 `CameraControl` 컴포넌트를 참조하고 있어서
(`TestScene.unity`: fileID `19481817`, `SampleScene.unity`: fileID `1535061840`), 새로 추가하는
`UserControl.mainCameraControl` 필드도 같은 컴포넌트를 가리키도록 두 씬의 `UserControl`
MonoBehaviour 블록에 아래 줄을 추가:

- `TestScene.unity` (UserControl, fileID `2101171184`): `mainCameraControl: {fileID: 19481817}`
- `SampleScene.unity` (UserControl, fileID `953871856`): `mainCameraControl: {fileID: 1535061840}`

(에디터에서 직접 드래그해 연결해도 동일 — 원하시면 이 부분만 직접 하셔도 됩니다.)

## 확인이 필요한 부분

1. **더블클릭 판정 시간(0.3초)** — Windows 기본 더블클릭 속도와 비슷한 값으로 임의 설정. 더 여유
   있게/빡빡하게 원하시면 값을 조정하겠습니다.
2. **그룹이 유닛+건물 혼합일 때 우선순위** — 유닛 리스트가 있으면 무조건 유닛 `[0]`을 우선하고, 유닛이
   하나도 없을 때만 건물 `[0]`을 씀. 이 우선순위가 맞는지 확인 부탁드립니다.
3. **씬 파일(.unity) 직접 편집 여부** — 위 3번처럼 YAML에 한 줄 추가하는 방식으로 진행해도 되는지,
   아니면 코드만 바꾸고 필드 연결은 직접 에디터에서 하실지 알려주세요.

## 적용 결과

사용자가 위 설계에 동의(더블클릭 0.3초 유지, 씬 YAML 직접 편집)하여 아래 항목을 그대로 적용함.

- `Assets/Scripts/System/RTSUnitController.cs` — `SelectControlGroup` 아래에 `TryGetControlGroupFocusPosition(int groupIndex, out Vector3 position)` 추가 (설계안 그대로).
- `Assets/Scripts/UserControl/UserControl.cs`
  - `mainCameraControl` (`CameraControl`) 필드 추가
  - `ControlGroupDoubleClickThreshold`(0.3f) 상수와 `lastControlGroupPressTime[10]` 배열 추가, `Awake()`에서 전부 `float.NegativeInfinity`로 초기화
  - `HandleControlGroupInput()`의 숫자만-누름(else) 분기에 더블클릭 판정 + `mainCameraControl.JumpToWorldXZ(focusPosition)` 호출 추가
- `Assets/Scenes/TestScene.unity` — `UserControl`(fileID `2101171184`)에 `mainCameraControl: {fileID: 19481817}` 추가
- `Assets/Scenes/SampleScene.unity` — `UserControl`(fileID `953871856`)에 `mainCameraControl: {fileID: 1535061840}` 추가

Ctrl/Shift 조합 분기는 건드리지 않았고, 더블클릭은 "숫자만 누르는" 선택 분기에서만 동작한다.

## 후속 요청 — z값 -30 보정

"이제 카메라 이동은 잘하는데 여기서 z값 -30정도로 해줘" — `MinimapController.cs`의 미니맵 클릭 이동이
이미 `groundPoint.z -= 30f;` 보정을 쓰고 있는 것과 동일하게, 더블클릭 카메라 이동도 유닛 위치 그대로가
아니라 z를 -30만큼 보정한 좌표로 이동하도록 요청.

`Assets/Scripts/UserControl/UserControl.cs`의 더블클릭 처리부 수정:

Before:
```csharp
                if (isDoubleClick && mainCameraControl != null &&
                    rtsUnitController.TryGetControlGroupFocusPosition(i, out Vector3 focusPosition))
                {
                    mainCameraControl.JumpToWorldXZ(focusPosition);
                }
```

After:
```csharp
                if (isDoubleClick && mainCameraControl != null &&
                    rtsUnitController.TryGetControlGroupFocusPosition(i, out Vector3 focusPosition))
                {
                    // MinimapController의 클릭 이동과 동일하게 z를 -30 보정해 카메라가 유닛 바로 위가 아니라 살짝 아래쪽에서 비추게 한다
                    focusPosition.z -= 30f;
                    mainCameraControl.JumpToWorldXZ(focusPosition);
                }
```
