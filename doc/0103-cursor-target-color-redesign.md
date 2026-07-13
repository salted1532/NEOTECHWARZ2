# 0103 - 마우스 커서 상태 재설계: 기본/선택/명령 + 적·아군·중립 3색

**날짜:** 2026-07-13

## 요청 내용

> 마우스 커서를 조금더 상황에 수정하려고해
> 기본, 선택, 명령 이렇게 3개로 구분하고
> 선택 커서= 적 선택, 아군 선택, 중립자원 선택으로 3가지 선택이고
> 공격이랑 이동은 서로 같은 텍스처로 처리하려고 하는데 이것도 적, 아군, 중립으로 3가지 색깔로 나타나게 하려고 3가지텍스처로

즉 [[0102-mouse-cursor-controller]]에서 만든 "기본/선택/이동/공격" 4분류 구조를 버리고, 모양(Shape) 기준 "기본/선택/명령" 3분류 + 각 선택·명령 모양마다 대상 진영(적/아군/중립) 3색으로 재구성. 결과적으로 텍스처는 기본 1개 + 선택 3개(적/아군/중립) + 명령 3개(적/아군/중립) = 총 7개.

## 조사 내용

- 현재(0102에서 구현) `UserControl.cs`에 있는 커서 구조:
  - `CursorState` enum: `Default, Select, Move, Attack` (4가지, Attack/Move가 별도 모양+별도 텍스처)
  - `UpdateCursor()`: UI 위 → Default, `OrderState.Attack` → Attack, `Move/Patrol/Rally/BuildingMove` → Move, 그 외 `IsHoveringSelectable()`(유닛/적/건물/광물/가스 통합 레이어마스크 히트)이면 Select, 아니면 Default.
  - `IsHoveringSelectable()`은 대상 종류(적/아군/자원)를 구분하지 않고 "선택 가능한 무언가"인지만 판별했음 - 이번 요청은 이걸 진영별로 쪼개야 함.
- 레이어마스크는 이미 `UserControl`에 다 있음: `layerUnit`(아군 유닛), `layerBuilding`(아군 건물, BaseStructure 포함), `layerEnemy`(적), `layerOre`/`layerGas`(중립 자원). 아군 판정은 `layerUnit | layerBuilding`, 적은 `layerEnemy`, 중립자원은 `layerOre | layerGas`로 매핑하면 기존 마스크 재사용만으로 충분.
- "명령(Command)" 모양은 기존 Attack/Move가 텍스처만 같게 합쳐지는 것이 아니라, **적용되는 주문 상태 범위도 그대로 유지**(Attack뿐 아니라 Move/Patrol/Rally/BuildingMove까지 전부 "명령 대기 중" 모양으로 취급 - 0102에서 사용자가 이미 이렇게 확장하기로 확정했었음). 다만 이번엔 그 모양 안에서 마우스 아래 대상(적/아군/중립)에 따라 3가지 색 중 하나로 갈린다.
  - 명령 대기 중 적 위에 호버 → 명령-적 텍스처 (공격 대상 표시)
  - 명령 대기 중 아군(유닛/건물) 위에 호버 → 명령-아군 텍스처 (예: Attack 모드에서 아군 강제공격 대상 표시)
  - 명령 대기 중 땅/자원/그 외 → 명령-중립 텍스처 (이동 지점, 공격-이동 지점 등)
- "선택(Select)" 모양은 명령 대기 상태가 아닐 때(`OrderState.None`)만 등장하며, 마찬가지로 호버 대상에 따라 선택-적/선택-아군/선택-중립자원 3색. 아무 것도 안 걸리면(빈 땅 등) 기본 화살표.

## 계획

`UserControl.cs`의 커서 관련 코드(0102에서 추가된 `CursorState` enum, `cursorSelectTexture`/`cursorMoveTexture`/`cursorAttackTexture` 필드, `UpdateCursor()`/`IsHoveringSelectable()`/`ApplyCursor()`)를 아래처럼 교체한다.

### 1. 필드/enum 교체

**기존 코드:**
```csharp
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
```

**변경 코드:**
```csharp
    [Header("Mouse Cursor")]
    [SerializeField]
    private Texture2D cursorDefaultTexture; // 비워두면 OS 기본 화살표 사용
    [SerializeField]
    private Texture2D cursorSelectEnemyTexture;
    [SerializeField]
    private Texture2D cursorSelectAllyTexture;
    [SerializeField]
    private Texture2D cursorSelectNeutralTexture;
    [SerializeField]
    private Texture2D cursorCommandEnemyTexture;
    [SerializeField]
    private Texture2D cursorCommandAllyTexture;
    [SerializeField]
    private Texture2D cursorCommandNeutralTexture;
    [SerializeField]
    private Vector2 cursorHotspot = Vector2.zero;

    // 마우스 아래에 있는 대상의 진영 (선택/명령 커서의 색을 고른다)
    private enum CursorTarget { None, Enemy, Ally, Neutral }

    private Texture2D currentCursorTexture; // 직전 프레임에 적용한 텍스처 (같으면 SetCursor 재호출 생략)
```

### 2. 커서 갱신 로직 교체

**기존 코드:**
```csharp
    // 상황(UI 위/공격 대기/이동·순찰·랠리·건물이동 대기/선택 가능 대상 호버)에 맞춰 실제 마우스 커서 아이콘을 바꾼다.
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

**변경 코드:**
```csharp
    // 상황(UI 위/명령 대기 중 호버 대상/선택 가능 대상 호버)에 맞춰 실제 마우스 커서 아이콘을 바꾼다.
    // 모양(기본/선택/명령)은 OrderState가, 색(적/아군/중립)은 마우스 아래 대상의 진영이 결정한다.
    private void UpdateCursor()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetCursorTexture(cursorDefaultTexture);
            return;
        }

        bool commandPending =
            UsercurrentState == OrderState.Attack || UsercurrentState == OrderState.Move ||
            UsercurrentState == OrderState.Patrol || UsercurrentState == OrderState.Rally ||
            UsercurrentState == OrderState.BuildingMove;

        CursorTarget target = GetHoveredTarget();
        Texture2D texture;

        if (commandPending)
        {
            texture = target switch
            {
                CursorTarget.Enemy => cursorCommandEnemyTexture,
                CursorTarget.Ally => cursorCommandAllyTexture,
                _ => cursorCommandNeutralTexture, // 땅/자원/빈 곳은 전부 중립 취급 (이동/공격-이동 지점 지정)
            };
        }
        else if (target != CursorTarget.None)
        {
            texture = target switch
            {
                CursorTarget.Enemy => cursorSelectEnemyTexture,
                CursorTarget.Ally => cursorSelectAllyTexture,
                _ => cursorSelectNeutralTexture,
            };
        }
        else
        {
            texture = cursorDefaultTexture;
        }

        SetCursorTexture(texture);
    }

    // 마우스 아래에 있는 대상의 진영(적/아군/중립자원)을 판별한다. 아무 것도 없으면 None(땅 등).
    private CursorTarget GetHoveredTarget()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, Mathf.Infinity, layerEnemy))
            return CursorTarget.Enemy;

        if (Physics.Raycast(ray, Mathf.Infinity, layerUnit | layerBuilding))
            return CursorTarget.Ally;

        if (Physics.Raycast(ray, Mathf.Infinity, layerOre | layerGas))
            return CursorTarget.Neutral;

        return CursorTarget.None;
    }

    private void SetCursorTexture(Texture2D texture)
    {
        if (texture == currentCursorTexture)
            return;

        currentCursorTexture = texture;
        Cursor.SetCursor(texture, cursorHotspot, CursorMode.Auto);
    }
```

## 영향받는 파일 (예정)

- `Assets/Scripts/UserControl/UserControl.cs` (0102에서 추가했던 커서 필드/enum/메서드 3개를 전부 교체)
- Unity 에디터: `UserControl` 컴포넌트의 커서 텍스처 슬롯이 4개(`Default/Select/Move/Attack`)에서 7개(`Default/Select Enemy/Select Ally/Select Neutral/Command Enemy/Command Ally/Command Neutral`)로 바뀌므로, 기존에 `Select/Move/Attack` 슬롯에 이미 텍스처를 연결해뒀다면 새 슬롯에 다시 연결해야 함 (필드 이름이 바뀌면서 기존 연결은 끊어짐 - Unity 인스펙터에서 재할당 필요).

## 사용자 확인 결과 (AskUserQuestion)

1. "명령" 모양 범위: Attack/Move/Patrol/Rally/BuildingMove 5가지 상태 전부 유지 (0102 결정과 동일하게 계속 확장 범위 유지).
2. Move 대기 중 아군 유닛을 클릭했을 때의 실제 동작(현재는 이동 명령이 아니라 그 유닛이 재선택되는 기존 로직)은 이번 작업 범위에서 건드리지 않음 - 이번엔 커서 색만 바꾸고, 클릭 결과 동작은 나중에 별도로 다루기로 함.

## 적용 완료

`Assets/Scripts/UserControl/UserControl.cs`에 위 "코드 변경" 섹션 그대로 반영함. `CursorState` enum과 `cursorSelectTexture`/`cursorMoveTexture`/`cursorAttackTexture`/`ApplyCursor()`/`IsHoveringSelectable()`는 삭제되고, `CursorTarget` enum·7개 텍스처 필드·`GetHoveredTarget()`/`SetCursorTexture()`로 교체됨.

## 요약/남은 작업

- 남은 작업은 에디터 쪽: `UserControl` 컴포넌트의 커서 텍스처 슬롯이 `Default / Select Enemy / Select Ally / Select Neutral / Command Enemy / Command Ally / Command Neutral` 7개로 바뀌었으므로, 0102에서 이미 연결해뒀던 `Select/Move/Attack` 슬롯은 끊어졌고 새 7개 슬롯에 `Pixel Cursors` 폴더 이미지를 다시 연결해야 실제로 보인다.
- 클릭 시 실제 동작(예: Move 대기 중 아군 클릭)은 이번에 손대지 않았음 - 필요하면 별도 요청으로 진행.
