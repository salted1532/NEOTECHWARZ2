# 0102 - 마우스 커서 상태 전환 스크립트

**날짜:** 2026-07-13

## 요청 내용

> 마우스 커서를 제어하는 스크립트가 있으면 좋겠어 마우스 커서 종류는 기본 화살표, 선택, 이동, 공격 정도인거 같아
> 각 선택 = 유닛, 건물, 자원, 적 위로 마우스 호버 시,
> 이동 = m키를 눌러 이동 위치 설정모드 진입,
> 공격 = a키를 눌러 공격 위치 설정모드로 진입 시
> 그리고 나머지 상황에선 기본 화살표로 돌아가는

## 조사 내용

- 현재 `UserControl.cs`가 이미 마우스 좌/우클릭 처리, 레이어마스크(`layerUnit/layerEnemy/layerBuilding/layerOre/layerGas/layerGround`), 명령 대기 상태(`OrderState: None/Attack/Move/Patrol/Rally/BuildingMove`)를 전부 들고 있음. `Update()`에서 매 프레임 `HandleMouse()` → `HandlekeyBoard()` → `UpdatePointer()` 순으로 호출.
- `OrderState`는 A/M 버튼(또는 단축키)을 누르면 `RTSUnitController.EnterAttackMode()/EnterMoveMode()` → `UserControl.SetOrderState("Attack"/"Move")`로 전환된다. M/A 키 자체는 이제 `ProductionSlot.cs`가 각 버튼에 등록된 단축키를 감지해 버튼을 대신 눌러주는 방식이라(코드 906-940번째 줄 `KeyCode.M`, `KeyCode.A` 참고), `UserControl`에서 새로 키 입력을 감지할 필요 없이 `UsercurrentState`만 읽으면 된다.
- 현재는 "커서"가 아니라 월드 공간 마커(`movePointer`/`attackPointer` 프리팹)로만 상태를 표시하고, OS/UI 레벨의 실제 마우스 커서 이미지는 항상 기본값 그대로였음 (`Cursor.SetCursor` 호출 없음).
- 프로젝트에 이미 커서용 아트 에셋이 존재함: `Assets/AssetFolder/Pixel Cursors/Cursors/*.png` (예: `basic_01.png`, `Bonus_XX.png` 등 다수). 이번 작업은 이 중 원하는 이미지를 인스펙터에서 끼워 넣는 것을 전제로 함 (어떤 이미지를 기본/선택/이동/공격에 쓸지는 사용자가 직접 지정).

## 계획

`UserControl.cs`에 커서 전용 필드/메서드를 추가하고 `Update()`에서 매 프레임 호출한다 (새 스크립트 파일을 따로 만들지 않고 기존 컨트롤러에 얹는 이유: 레이캐스트용 레이어마스크와 `OrderState`를 이미 이 클래스가 갖고 있어서 중복 없이 재사용 가능).

우선순위:
1. UI 위에 마우스가 있으면 → 기본 화살표 (커서 텍스처는 항상 UI보다 게임 월드 상태를 반영해야 하므로, UI 위에서는 커스텀 커서를 끄고 OS 기본으로 둠)
2. `OrderState.Attack` → 공격 커서
3. `OrderState.Move` → 이동 커서
4. 그 외 상태에서 유닛/적/건물/자원(광물·가스) 위에 호버 → 선택 커서
5. 나머지 전부 → 기본 화살표

**사용자 확인 결과 (AskUserQuestion):**
1. `OrderState.Patrol`/`Rally`/`BuildingMove`도 "이동" 커서와 동일하게 취급 (이미 `movePointer` 월드 마커도 이 세 상태에서 같이 쓰이므로 일관성 있게 포함).
2. 커서 이미지 4개(기본/선택/이동/공격)는 코드에서 특정 파일을 기본값으로 미리 연결하지 않고, `Texture2D` 필드만 노출해서 Unity 인스펙터에서 `Pixel Cursors` 폴더의 원하는 파일을 직접 끼워 넣는 방식으로 결정.

## 코드 변경 (적용 완료)

### 1. 필드 추가

**기존 코드** (`Assets/Scripts/UserControl/UserControl.cs` 30-46번째 줄 부근):
```csharp
    private GameObject pointer;

    private GameObject attackPointer;
    private GameObject movePointer;

    [SerializeField]
    private GameObject pointerPrefab;
    [SerializeField]
    private GameObject attackPointerPrefab;

    private RTSUnitController rtsUnitController;
```

**변경 코드:**
```csharp
    private GameObject pointer;

    private GameObject attackPointer;
    private GameObject movePointer;

    [SerializeField]
    private GameObject pointerPrefab;
    [SerializeField]
    private GameObject attackPointerPrefab;

    [Header("Mouse Cursor")]
    [SerializeField]
    private Texture2D cursorDefaultTexture; // 비워두면 OS 기본 화살표 사용
    [SerializeField]
    private Texture2D cursorSelectTexture;
    [SerializeField]
    private Texture2D cursorMoveTexture;
    [SerializeField]
    private Texture2D cursorAttackTexture;
    [SerializeField]
    private Vector2 cursorHotspot = Vector2.zero;

    private enum CursorState { Default, Select, Move, Attack }
    private CursorState currentCursorState = CursorState.Default;

    private RTSUnitController rtsUnitController;
```

### 2. `Update()`에서 커서 갱신 호출 추가

**기존 코드** (85-94번째 줄):
```csharp
    private void Update()
    {
        //마우스 입력 관리
        HandleMouse();
        //키보드 입력 관리
        HandlekeyBoard();

        // 입력 상황에 따라 포인터 생성
        UpdatePointer();
    }
```

**변경 코드:**
```csharp
    private void Update()
    {
        //마우스 입력 관리
        HandleMouse();
        //키보드 입력 관리
        HandlekeyBoard();

        // 입력 상황에 따라 포인터 생성
        UpdatePointer();

        // 입력 상황에 따라 마우스 커서 아이콘 갱신
        UpdateCursor();
    }
```

### 3. 커서 갱신 로직 추가 (`UpdatePointer()` 메서드 바로 뒤, 656번째 줄 부근에 새 메서드 삽입)

```csharp
    // 상황(UI 위/공격 대기/이동 대기/선택 가능 대상 호버)에 맞춰 실제 마우스 커서 아이콘을 바꾼다.
    private void UpdateCursor()
    {
        CursorState desired;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            desired = CursorState.Default;
        }
        else if (UsercurrentState == OrderState.Attack)
        {
            desired = CursorState.Attack;
        }
        else if (UsercurrentState == OrderState.Move || UsercurrentState == OrderState.Patrol ||
                 UsercurrentState == OrderState.Rally || UsercurrentState == OrderState.BuildingMove)
        {
            desired = CursorState.Move;
        }
        else if (IsHoveringSelectable())
        {
            desired = CursorState.Select;
        }
        else
        {
            desired = CursorState.Default;
        }

        if (desired == currentCursorState)
            return;

        currentCursorState = desired;
        ApplyCursor(desired);
    }

    // 유닛/적/건물/광물/가스 중 하나 위에 마우스가 올라가 있는지 검사한다.
    private bool IsHoveringSelectable()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        LayerMask selectableMask = layerUnit | layerEnemy | layerBuilding | layerOre | layerGas;

        return Physics.Raycast(ray, Mathf.Infinity, selectableMask);
    }

    private void ApplyCursor(CursorState state)
    {
        Texture2D texture = state switch
        {
            CursorState.Select => cursorSelectTexture,
            CursorState.Move => cursorMoveTexture,
            CursorState.Attack => cursorAttackTexture,
            _ => cursorDefaultTexture,
        };

        Cursor.SetCursor(texture, cursorHotspot, CursorMode.Auto);
    }
```

## 영향받는 파일 (예정)

- `Assets/Scripts/UserControl/UserControl.cs` (필드 추가, `Update()` 수정, 새 메서드 3개 추가)
- Unity 에디터에서 `UserControl` 컴포넌트의 새 인스펙터 필드(`Cursor Default/Select/Move/Attack Texture`)에 `Assets/AssetFolder/Pixel Cursors/Cursors/*.png` 중 원하는 이미지를 직접 연결해야 실제로 커서가 바뀜 (텍스처를 비워두면 해당 상태에서 OS 기본 화살표로 표시됨).

## 요약/남은 작업

- `UserControl.cs`에 커서 텍스처 필드 5개(기본/선택/이동/공격/핫스팟), `CursorState` enum, `UpdateCursor()`/`IsHoveringSelectable()`/`ApplyCursor()` 메서드를 추가하고 `Update()`에서 매 프레임 `UpdateCursor()`를 호출하도록 반영 완료.
- 남은 작업은 프로젝트 코드가 아니라 에디터 작업: Unity에서 `UserControl` 컴포넌트를 선택해 `Cursor Default/Select/Move/Attack Texture` 4개 슬롯에 `Assets/AssetFolder/Pixel Cursors/Cursors/` 폴더의 원하는 이미지를 직접 끌어다 놓아야 실제로 커서가 바뀐다 (비워두면 해당 상태에서 OS 기본 화살표 유지).
- 텍스처를 Read/Write 가능하게 만들거나 `Cursor.SetCursor`가 요구하는 포맷(32bit RGBA)에 안 맞으면 Unity가 경고를 띄울 수 있으니, 실제 이미지를 연결한 뒤 플레이 모드에서 각 상태(호버/M/A)를 눈으로 확인해볼 것을 권장.
