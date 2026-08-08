# 0455. 미션 오브젝트(유물/데이터베이스)를 중립 Ore처럼 선택 가능하게 - 제안

**날짜:** 2026-08-08

## 요청 내용
> MissionObject 폴더안에 DataBase와 Artifact를 만들어서 스테이지에도 추가했는데 해당 오브젝트도
> 중립 Ore와 같이 선택 가능하도록 해줘 선택시 Ore와 같이 나오는데 체력이나 이런건 존재하지 않아서
> 그냥 오브젝트 이름이랑 이미지만 나오면 될거같아

## 조사 내용

### 현재 상태

`Assets/prefabs/MissionObject/Artifact.prefab`/`Database.prefab`는 `BoxCollider`(트리거 아님) +
`FogRevealerAgent` + `ItemHover`(방금 만든 것)만 붙어 있는 순수 장식용 오브젝트 - 선택/클릭 관련
컴포넌트, 전용 Layer, Tag 전부 없음(Layer는 기본값 `Default`). `Stage2Objectives.cs`가 이 둘을
`Transform` 참조로만 들고 매 프레임 거리 판정으로 줍기/반납을 처리함(트리거 콜라이더도 안 씀) - 즉
지금은 "클릭해서 선택"이라는 개념 자체가 전혀 없는 상태.

### Ore 선택이 어떻게 동작하는지 (그대로 따라갈 패턴)

- `UserControl.cs`: 전용 `layerOre` 레이어로 레이캐스트 → `ResourceNode` 컴포넌트를 찾아
  `rtsUnitController.ClickSelectResource(node)` 호출. 커서 판정(`GetHoveredTarget`)에도
  `layerOre | layerGas | layerAllyOC`가 포함돼 있어 호버 시 노란(중립) 커서가 뜸.
- `RTSUnitController.cs`: `selectedResourceNode` 단일 선택 필드 + `SelectState.OreSelect` +
  `ClickSelectResource`/`SelectResource`/`ClearSelectedResourceIfMatches` + Info Panel 표시
  (`ShowResourceInfoPanel(icon, name, remainingAmount)` - 광물은 "남은 채취량"까지 같이 보여줌).

### "체력도 남은 양도 없이 이름+이미지만" 표시할 방법 - 이미 있음

`UIController.ShowInfoPanel(Sprite icon, string unitName, HealthManager health)` 오버로드가 정확히
이 용도임(건물 등 "공격력/방어력 개념이 없는 대상"용으로 이미 존재, doc/0249) - 내부적으로
`SetCombatStatsVisible(false)`로 공격/방어 아이콘을 숨기고, `BindInfoHealth(health)`에 `null`을
넘기면 체력 텍스트도 빈 문자열로 비워짐(`BindInfoHealth` 코드 확인함). 즉 새 UI 패널 메서드를 만들
필요 없이, 이 오버로드에 `health: null`만 넘기면 요청하신 "이름+이미지만" 표시가 정확히 됨.

## 제안하는 변경

### 1) 새 Layer `MissionObject` 추가 (`ProjectSettings/TagManager.asset`, 14번 슬롯)

`AllyOC`(13번, doc/0447)와 동일한 목적 - 클릭 판정을 위한 전용 물리 레이어.

### 2) 신규 컴포넌트 `Assets/Scripts/System/MissionItem.cs`

```csharp
using UnityEngine;

// 유물/데이터베이스 등 "줍기/반납"은 개별 스테이지 스크립트(Stage2Objectives 등)가 거리 판정으로
// 직접 처리하고, 이 컴포넌트는 순수하게 "좌클릭으로 선택했을 때 Info Panel에 무엇을 보여줄지"만
// 담당한다(체력/전투 스탯 없음 - ResourceNode의 선택 관련 부분만 떼어낸 축소판, doc/0455).
public class MissionItem : MonoBehaviour
{
    [SerializeField] private string itemName;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject selectionMarker; // 선택 시 표시할 마커 (없으면 그냥 표시 없이 선택만 됨)

    public void SelectItem()
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(true);
    }

    public void DeselectItem()
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
    }

    public Sprite GetIcon() => icon;
    public string GetItemName() => itemName;
}
```

### 3) `RTSUnitController.cs` - Ore와 동일한 패턴으로 병행 추가

```csharp
public MissionItem selectedMissionItem; // 미션 오브젝트도 항상 단일 선택 (selectedResourceNode와 동일 패턴)

// SelectState에 MissionItemSelect 추가

public void ClickSelectMissionItem(MissionItem item) { DeselectAll(); SelectMissionItem(item); }
private void SelectMissionItem(MissionItem item) { if (IsBuildMode()) return; RTScurrentSate = SelectState.MissionItemSelect; item.SelectItem(); selectedMissionItem = item; }
public void ClearSelectedMissionItemIfMatches(MissionItem item) { ... } // ClearSelectedResourceIfMatches와 동일 패턴

// Info Panel switch문에 케이스 추가
case SelectState.MissionItemSelect:
    if (selectedMissionItem != null)
        uIController.ShowInfoPanel(selectedMissionItem.GetIcon(), selectedMissionItem.GetItemName(), null); // 체력 없음 - 이름+이미지만
    else
        uIController.HideInfoPanel();
    uIController.ClearPanel();
    uIController.HideProductionUI();
    uIController.HideSquadPanel();
    break;
```

`DeselectAll()`에도 `selectedMissionItem` 정리 로직을 다른 단일-선택 필드들과 나란히 추가.

### 4) `UserControl.cs` - 전용 레이어 필드 + 클릭 분기 + 커서 분기

```csharp
[SerializeField]
private LayerMask layerMissionObject; // 유물/데이터베이스 등 선택 전용 레이어 (doc/0455)

// HandleLeftClick() 상단 레이캐스트 묶음에 추가
bool clickedMissionObject = Physics.Raycast(ray, out missionObjectHit, Mathf.Infinity, layerMissionObject);

// "5. 광물/가스 클릭" 블록 옆에 나란히 추가
if (clickedMissionObject)
{
    MissionItem item = missionObjectHit.transform.GetComponent<MissionItem>();
    if (item != null)
    {
        pendingLeftClickSelect = () => { if (item != null) rtsUnitController.ClickSelectMissionItem(item); };
        return;
    }
}

// GetHoveredTarget()의 Neutral 판정에도 포함
Physics.Raycast(ray, out RaycastHit resourceHit, Mathf.Infinity, layerOre | layerGas | layerAllyOC | layerMissionObject)
```

### 5) 프리팹/씬 설정

- `Artifact.prefab`/`Database.prefab` 루트 Layer → `MissionObject`로 변경.
- 두 프리팹 루트에 `MissionItem` 컴포넌트 추가: `itemName`은 "Artifact"/"Database"로 채움(더 보기
  좋은 표시명으로 바꾸고 싶으면 나중에 인스펙터에서 수정 가능), `icon`은 지금 준비된 아이콘 에셋이
  없어 비워둠(비워두면 `ShowInfoPanel`이 `infoIcon.enabled = false`로 처리해서 조용히 안 보이기만 함 -
  나중에 아이콘 에셋이 생기면 그때 연결).
- `GameManager.prefab`의 `UserControl` 컴포넌트에 새 `layerMissionObject` 필드를 `MissionObject`
  레이어로 연결(`layerAllyOC`를 `AllyOC`로 연결했던 것과 동일한 자리, doc/0448).

## 확인하고 싶은 점 (승인됨)

1. 이대로 진행해도 될까요? (Layer 1개 추가, 신규 컴포넌트 1개, `RTSUnitController.cs`/
   `UserControl.cs` 수정, 프리팹 2개 + GameManager 필드 연결)
2. `itemName` 표시값을 "Artifact"/"Database" 그대로 둘지, 더 보기 좋은 이름(예: "외계 유물"/"OC 연구
   데이터" - `Stage2Objectives`의 목표 텍스트와 맞춤)으로 할지 알려주세요.

사용자가 "적용시켜줘"로 승인함. 표시 이름은 답이 없어 `Stage2Objectives`의 목표 텍스트와 맞춰
"외계 유물"/"OC 연구 데이터"로 정함.

## 구현 결과

제안 그대로 적용함.

- `ProjectSettings/TagManager.asset` - Layer 14번에 `MissionObject` 추가.
- `Assets/Scripts/System/MissionItem.cs` (신규) - 제안 그대로.
- `Assets/Scripts/System/RTSUnitController.cs` - `selectedMissionItem` 필드, `SelectState.MissionItemSelect`,
  `ClickSelectMissionItem`/`SelectMissionItem`/`ClearSelectedMissionItemIfMatches`, `DeselectAll()`에
  정리 로직 추가, Info Panel switch문에 `ShowInfoPanel(icon, name, null)` 케이스 추가.
- `Assets/Scripts/UserControl/UserControl.cs` - `layerMissionObject` 필드, 레이캐스트, "5.5 미션
  오브젝트 클릭" 분기, `GetHoveredTarget()`의 Neutral 판정에 포함.
- `Assets/prefabs/MissionObject/Artifact.prefab` - 루트 Layer → `MissionObject`(14), `MissionItem`
  추가(`itemName: "외계 유물"`).
- `Assets/prefabs/MissionObject/Database.prefab` - 루트 Layer → `MissionObject`(14), `MissionItem`
  추가(`itemName: "OC 연구 데이터"`).
- `Assets/prefabs/Game/GameManager.prefab` - `UserControl.layerMissionObject`를 `MissionObject`
  레이어로 연결(`m_Bits: 16384` = 1<<14).

Unity Editor 다이나믹 코드로 프리팹 3개를 `PrefabUtility.LoadPrefabContents`/`SaveAsPrefabAsset`으로
직접 편집(Ally 컨트롤러 교체 때와 동일한 방식) - `icon`/`selectionMarker`는 준비된 에셋이 없어
비워둠(나중에 인스펙터에서 연결 가능).

`Assets/prefabs/MissionObject/`(구 `Assets/prefabs/Maps/MissionObject/`) 폴더 이동과 `Mission2.unity`
씬 변경은 이번 작업 이전에 사용자가 직접 해둔 것으로 확인됨(git status로 확인, 내가 건드리지 않음) -
그대로 둠.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔 Error 0건.
- 프리팹 직접 확인: `Artifact.prefab`/`Database.prefab` 둘 다 루트 Layer override가 `14`로 정확히
  저장됨, `MissionItem` 컴포넌트에 `itemName` 정상 반영됨.
- 부수 변경(워터 메시 재직렬화 등) 없음 - `git status`로 확인.

## 변경된 파일

- `ProjectSettings/TagManager.asset`
- `Assets/Scripts/System/MissionItem.cs` (신규)
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
- `Assets/prefabs/MissionObject/Artifact.prefab`
- `Assets/prefabs/MissionObject/Database.prefab`
- `Assets/prefabs/Game/GameManager.prefab`
