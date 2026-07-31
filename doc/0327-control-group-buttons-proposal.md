# 0327. 부대(컨트롤 그룹) 선택 버튼 UI (설계 제안)

**날짜:** 2026-07-31

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 검토 후 확정되면 코드에 반영한다.

## 요청

> 부대 지정시 그 부대 선택할수 있는 버튼이 Info패널 위에 가장 왼쪽부터 차례대로 생겼으면 좋겠어.
> 1번 부대 설정하고 2번부대 설정하면 1 옆에 2번버튼이 생기고, 만약 부대지정된 유닛들이 모두 죽으면
> 그 부대 리스트는 파기한거로 치고 1번 부대 버튼은 없애줘. 없어지면 2번 버튼이 제일 왼쪽으로 가고,
> 만약 다시 1번 부대를 지정하면 1번이 가장 왼쪽 그 다음 2번으로 이동하게. 버튼은 내가 프리팹으로
> 연결할게, 필요할때마다 생성하고 지워줘. 안에 텍스트는 1, 2, 3 이런식으로 숫자를 넣어서 구별되게.

## 조사 내용

- 부대 지정(컨트롤 그룹) 자체는 [[control-group-assignment]](doc/0059)에서 이미 구현 완료됨 —
  `RTSUnitController`가 `controlGroupUnits[10]`/`controlGroupBuildings[10]`(둘 다 `private`)로 그룹별
  대상을 들고 있고, `Ctrl+숫자`(`AssignControlGroup`)/`Shift+숫자`(`AddSelectedToControlGroup`)로
  저장, 숫자만 누르면(`SelectControlGroup`) 선택 복원. 인덱스 0~9가 각각 키보드 숫자 `1,2,...,9,0`에
  대응(주석에 이미 "0→그룹10"으로 명시돼 있음).
- **죽은 대상 정리는 지금 "지연 정리"뿐**: `SelectControlGroup()`을 실제로 호출할 때(그 그룹 숫자를
  누를 때)만 `RemoveAll(x => x == null)`로 걸러냄 — 그룹을 불러오지 않고 그냥 놔두면, 유닛이 다
  죽어도 리스트 자체는 죽은(파괴된) 참조를 그대로 들고 있음. 이번 버튼 UI는 "전멸하면 버튼을 즉시
  없애야" 하므로, 매 프레임(또는 주기적으로) 죽은 대상을 정리하고 남은 인원수를 확인할 수 있는 질의가
  하나 더 필요함(기존 `controlGroupUnits`/`controlGroupBuildings`는 `private`라 외부에서 못 봄).
- `Info_panel`은 `UIController.infoPanel`(`Assets/Scripts/UI/UIController.cs:240`)로 참조되는 기존
  UI 오브젝트 — "Info패널 위"는 씬의 UI 배치(앵커/좌표) 문제라 코드 변경이 아니라 사용자가 직접
  캔버스에서 배치할 부분으로 남겨둠(요청에도 "버튼은 내가 프리팹으로 연결할게"로 명시).
- 이 프로젝트에 "필요할 때마다 Instantiate/Destroy하는 동적 UI 리스트" 전례가 아직 없음(생산
  대기열/스쿼드 패널 등은 전부 고정 슬롯 풀 방식 - `ProductionSlot.SetData()`/`Clear()` 패턴,
  `Assets/Scripts/UI/ProductionSlot.cs`). 이번 요청은 명시적으로 "생성하고 지워줘"라고 했으므로
  기존 풀링 패턴 대신 실제 Instantiate/Destroy로 구현.
- "왼쪽부터 순서대로" + "하나 없어지면 나머지가 왼쪽으로 당겨짐"은 버튼 각각의 좌표를 직접 계산하는
  대신, **부모 오브젝트에 Unity 표준 `HorizontalLayoutGroup`을 두고 자식의 sibling index만 맞추는
  방식**이 가장 단순함 — 레이아웃 그룹이 알아서 자식 순서대로 왼쪽부터 재배치해주므로, 버튼 하나를
  파괴하면 나머지는 코드 변경 없이 자동으로 당겨짐. 이 `HorizontalLayoutGroup` 설정도 씬/프리팹 쪽
  작업이라 사용자가 직접 구성.

## 설계안

### 1. `Assets/Scripts/System/RTSUnitController.cs` — 그룹 생존 인원 질의 추가

기존 `SelectControlGroup()`이 하던 "죽은 대상 정리"를 공용 메서드로 뽑아서, 새 질의 메서드와 함께
재사용한다(로직 중복 없음).

Before:
```csharp
    // 숫자만 누르면: 저장된 그룹의 유닛/건물을 선택 상태로 되돌린다. 그 사이 죽거나 파괴된 대상은 자동으로 걸러진다.
    // 그룹이 비어있으면(저장한 적 없거나 전부 사라짐) 기존 선택을 그대로 둔다.
    public void SelectControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return;

        controlGroupUnits[groupIndex].RemoveAll(unit => unit == null);
        controlGroupBuildings[groupIndex].RemoveAll(building => building == null);

        if (controlGroupUnits[groupIndex].Count == 0 && controlGroupBuildings[groupIndex].Count == 0)
            return;

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);
    }
```

After:
```csharp
    // 숫자만 누르면: 저장된 그룹의 유닛/건물을 선택 상태로 되돌린다. 그 사이 죽거나 파괴된 대상은 자동으로 걸러진다.
    // 그룹이 비어있으면(저장한 적 없거나 전부 사라짐) 기존 선택을 그대로 둔다.
    public void SelectControlGroup(int groupIndex)
    {
        if (PurgeAndCountControlGroup(groupIndex) == 0)
            return;

        DeselectAll();

        foreach (UnitController unit in controlGroupUnits[groupIndex])
            DragSelectUnit(unit);

        foreach (BuildingController building in controlGroupBuildings[groupIndex])
            SelectBuilding(building);
    }

    // 그룹 내 죽은/파괴된 대상을 정리하고 남은 인원수를 반환한다.
    // ControlGroupPanel(UI)이 "전멸해서 버튼을 없애야 하는지" 매 프레임 확인하는 용도로도 쓴다.
    public int PurgeAndCountControlGroup(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= controlGroupUnits.Length)
            return 0;

        controlGroupUnits[groupIndex].RemoveAll(unit => unit == null);
        controlGroupBuildings[groupIndex].RemoveAll(building => building == null);

        return controlGroupUnits[groupIndex].Count + controlGroupBuildings[groupIndex].Count;
    }
```

### 2. `Assets/Scripts/UI/ControlGroupPanel.cs` (신규)

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 부대(컨트롤 그룹) 선택 버튼을 그룹이 생기고/전멸할 때마다 생성/파괴한다.
// buttonContainer에 HorizontalLayoutGroup을 둬서(씬에서 직접 구성) sibling index만 그룹 번호 오름차순으로
// 맞추면, "왼쪽부터 그룹번호 순" 배치와 "하나 없어지면 나머지가 왼쪽으로 당겨지는" 동작이 레이아웃
// 그룹에 의해 공짜로 처리된다 - 좌표를 직접 계산하지 않는다.
public class ControlGroupPanel : MonoBehaviour
{
    [SerializeField] private GameObject buttonPrefab; // Button + 자식에 TextMeshProUGUI 하나 필요
    [SerializeField] private Transform buttonContainer; // HorizontalLayoutGroup이 달린 부모 (Info_panel 위)

    private RTSUnitController rtsController;
    private readonly GameObject[] groupButtons = new GameObject[10];

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (rtsController == null || buttonPrefab == null || buttonContainer == null)
            return;

        bool changed = false;

        for (int i = 0; i < groupButtons.Length; i++)
        {
            bool hasMembers = rtsController.PurgeAndCountControlGroup(i) > 0;
            bool hasButton = groupButtons[i] != null;

            if (hasMembers && !hasButton)
            {
                CreateButton(i);
                changed = true;
            }
            else if (!hasMembers && hasButton)
            {
                Destroy(groupButtons[i]);
                groupButtons[i] = null;
                changed = true;
            }
        }

        if (changed)
            ReorderButtons();
    }

    private void CreateButton(int groupIndex)
    {
        GameObject buttonObj = Instantiate(buttonPrefab, buttonContainer);

        TextMeshProUGUI label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.text = DisplayNumber(groupIndex);

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(() => rtsController.SelectControlGroup(groupIndex));

        groupButtons[groupIndex] = buttonObj;
    }

    // 그룹번호 오름차순으로 sibling index를 다시 매긴다 - HorizontalLayoutGroup이 그 순서대로 왼쪽부터 배치.
    private void ReorderButtons()
    {
        int siblingIndex = 0;

        for (int i = 0; i < groupButtons.Length; i++)
        {
            if (groupButtons[i] != null)
                groupButtons[i].transform.SetSiblingIndex(siblingIndex++);
        }
    }

    // 인덱스 0~8은 키보드 1~9, 인덱스 9(키보드 0)는 "10번째 그룹"이라 10으로 표시 (doc/0059 매핑과 동일).
    private static string DisplayNumber(int groupIndex) => groupIndex == 9 ? "10" : (groupIndex + 1).ToString();
}
```

## 결정이 필요한 부분

1. **10번째 그룹(키보드 `0`) 표시 텍스트**: 위 제안은 `"10"` — 기존 코드 주석(doc/0059)이 "0→그룹10"으로
   이미 그렇게 부르고 있어서 맞췄음. `"0"`으로 그대로 표시하길 원하면 알려주면 그렇게 바꿈.
2. **버튼 클릭 동작**: 버튼을 클릭하면 숫자 키를 누른 것과 동일하게 `SelectControlGroup()`(그 부대
   선택)을 호출하도록 넣었음 — 요청 문구("그 부대 선택할수 있는 버튼")에 맞춘 해석. 다른 동작(예:
   호버 시 부대 미리보기 등)을 원하면 알려주세요.
3. **버튼 프리팹/컨테이너 구성**: 코드는 `buttonPrefab`(자식에 `TextMeshProUGUI` + 루트에 `Button`
   필요)과 `buttonContainer`(그 자식에 `HorizontalLayoutGroup` 필요, Info_panel 위에 배치)를
   인스펙터에서 연결하는 것을 전제로 함 — 씬/프리팹 구성은 요청대로 직접 하는 것으로 이해했고, 이번
   세션에서는 스크립트만 추가함.

## 다음 단계

위 내용에 이견 없으면 `RTSUnitController.cs` 리팩터(`PurgeAndCountControlGroup` 추가) +
`Assets/Scripts/UI/ControlGroupPanel.cs` 신규 파일을 그대로 반영한다.

## 확인 결과 및 구현

사용자가 2가지 결정 사항 답변: (1) 10번째 그룹(키보드 `0`) 표시는 **"0"**(권장안 "10" 대신 이걸로
선택), (2) 버튼 클릭 시 그 부대를 선택(권장안 그대로).

설계안대로 적용:
- `RTSUnitController.cs`: `PurgeAndCountControlGroup(int)` 추가, `SelectControlGroup()`과
  `TryGetControlGroupFocusPosition()`이 중복하던 `RemoveAll` 정리 로직을 이 메서드로 공용화.
- `Assets/Scripts/UI/ControlGroupPanel.cs`(신규): `DisplayNumber()`는 인덱스 9(키보드 `0`)일 때
  `"0"`을 반환하도록 반영.
- `npx uloop-cli compile --wait-for-domain-reload true`로 컴파일 확인 — 에러 0개(신규 경고 1개는
  기존 코드베이스 전역에 이미 있는 `FindFirstObjectByType` deprecated 경고와 동일한 패턴).

## 남은 수동 작업 (사용자가 직접)

- 버튼 프리팹: 루트에 `Button`, 자식 어딘가에 `TextMeshProUGUI` 하나 필요.
- `Info_panel` 위에 `HorizontalLayoutGroup`이 달린 빈 컨테이너 오브젝트 배치.
- 씬의 `ControlGroupPanel` 컴포넌트(신규로 아무 오브젝트에나 붙여야 함)에 위 프리팹/컨테이너를
  인스펙터에서 연결.
- PlayMode 테스트(부대 지정 → 버튼 생성 → 전멸 → 버튼 소멸 → 재지정 → 순서 복원)는 이번 세션에서
  하지 않음.
