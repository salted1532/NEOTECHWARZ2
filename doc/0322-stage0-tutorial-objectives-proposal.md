# 0322. 0스테이지(튜토리얼) 임무 목표 체크리스트

**날짜:** 2026-07-31

## 요청 내용

> 0스테이지 (튜토리얼) 스테이지 임무 목표는 1개의 거점 점령하기(해당 거점 gameobject를 연결 할수 있도록) 10마리의 어썰트트루퍼 생산하기(유닛 리스트중에 어썰트트루퍼가 10기 이상있는지 확인), 병영 건설하기(병영 Tier1 건물이 존재하는지 확인) 서브목표: 주변 적 유닛 모두 제거(적유닛 다 죽었는지 적유닛 리스트로 확인). 광물 1000 확보(자원 ore 1000있는지 확인). 해당 목표들은 화면에서 텍스트로 알려주고 목표 해결시 글자 가운데에 - 선 추가해서 해결된거로 보이기 만약 도중에 목표 달성 조건이 미달되면 다시 - 선 제거하기

## 조사 내용

기존 코드에서 각 조건을 판정할 수 있는 API를 확인함(전부 이미 존재 — 새 시스템을 만들 필요 없음):

- **거점 점령**: `CaptureSystem/TerritoryZone.cs`의 `TerritoryZone.Owner`(`CaptureOwner` enum: `Neutral/Ally/Enemy`) — `Owner == CaptureOwner.Ally`면 점령됨. 인스펙터에 `TerritoryZone` 참조 필드를 두면 "해당 거점 gameobject 연결"이 그대로 됨.
- **어썰트 트루퍼 10기**: `Assets/Scripts/ScriptableObject/Data/NTA Unit Data SO.asset`에서 `unitName: 'Assault Trooper '`의 `ID: 2` — 코드상 상수는 `RTSUnitController.UnitID.Marine`(`System/RTSUnitController.cs:104`, 이름은 Marine이지만 실제 표시 데이터가 Assault Trooper로 재정의됨). 살아있는 아군 유닛 전체 목록은 `RTSUnitController.UnitList`(유닛 스폰/사망 시 자동으로 추가/제거됨, `Unit/UnitController.cs:246,1441`) — 여기서 `GetUnitID() == 2`인 유닛 수를 세면 됨.
- **병영 건설**: `RTSUnitController.HasCompletedBuilding(int buildingID)`(`System/RTSUnitController.cs:1245`)가 이미 있음 — `BuildingID.Barracks`(=3)로 바로 호출 가능.
- **적 유닛 전멸(서브)**: 아군과 달리 `EnemyUnitController`는 전역 리스트로 등록되지 않음 — 대신 유니티 기본 API `FindObjectsByType<EnemyUnitController>()`로 씬에 남은 적 유닛 수를 그대로 셀 수 있음(새 레지스트리를 만들 필요 없음). "주변"이라는 표현은 이번 튜토리얼 맵 특성상 적이 한 구역에만 있다고 보고 "씬 전체 적 유닛 수"로 단순화함 — 만약 특정 구역 안의 적만 판정해야 한다면 범위(Collider/Zone)가 하나 더 필요하니 알려주세요.
- **광물 1000**: `RTSUnitController.GetOre()`(`System/RTSUnitController.cs:1776`, 내부적으로 `ResourceManager.GetOre()`를 그대로 위임) 이미 존재.
- **취소선 표시**: TextMeshPro가 리치 텍스트 태그 `<s>...</s>`(strikethrough)를 기본 지원 — 별도로 선을 그리는 UI를 만들 필요 없이 텍스트를 `<s>내용</s>`로 감싸면 그대로 가운데 취소선이 표시됨. 이 프로젝트 UI는 전부 TextMeshPro(`TMPro`) 기반(`UI/UIController.cs` 등)이라 그대로 재사용.
- 조건이 "도중에 다시 미달되면 취소선도 다시 제거"돼야 하므로, 한 번 완료 판정하고 끝나는 게 아니라 **매 프레임 5개 조건을 전부 다시 평가**해서 텍스트를 갱신하는 방식으로 설계함(스테이지 하나 규모라 매 프레임 재계산 비용은 무시 가능).
- [[stage-manager-skeleton]](`doc/0321`)에서 만든 `StageManager.ReportVictory()`를 주목표 3개(거점 점령/트루퍼 10기/병영 건설)가 모두 완료된 순간 호출하도록 연결함. **서브목표(적 전멸/광물 1000)는 체크리스트에는 표시하지만 승리 조건에는 포함하지 않음** — 요청에서 "서브목표"로 따로 분류했으므로 필수 승리조건이 아니라고 해석함. 이 해석이 틀렸다면 알려주세요.
- 이번 5개 목표는 스테이지 0 전용 고정 목록이라, 범용 "임무 목표 프레임워크"(오브젝트 타입 계층, ScriptableObject 조건 등)를 새로 만들지 않고 이 스테이지 하나를 위한 컴포넌트 하나로 직접 작성함 — 다음 스테이지가 생기면 같은 패턴으로 별도 컴포넌트를 하나 더 만들면 됨(지금 억지로 공용화하면 조건 형태가 제각각이라 오히려 복잡해짐).

## 설계안

### 신규 파일: `Assets/Scripts/System/Stage0Objectives.cs`
```csharp
using TMPro;
using UnityEngine;

// 0스테이지(튜토리얼) 임무 목표 체크리스트.
// 목표별 완료 조건은 매 프레임 다시 평가한다 - 자원을 다시 쓰거나 유닛이 죽는 등으로 조건이
// 깨지면 취소선도 다시 사라져야 하므로(요청사항), "한 번 완료되면 고정"하지 않는다.
// 주목표(거점 점령/트루퍼 10기/병영 건설) 3개가 모두 완료되면 StageManager.ReportVictory()를 호출한다.
// 서브목표(적 전멸/광물 1000)는 체크리스트 표시만 하고 승리 조건에는 포함하지 않는다.
public class Stage0Objectives : MonoBehaviour
{
    private const int AssaultTrooperUnitID = RTSUnitController.UnitID.Marine; // 데이터상 표시명은 "Assault Trooper"
    private const int RequiredTrooperCount = 10;
    private const int BarracksBuildingID = RTSUnitController.BuildingID.Barracks;
    private const int RequiredOre = 1000;

    [Header("주목표")]
    [SerializeField] private TerritoryZone targetZone; // 점령해야 할 거점 (씬의 TerritoryZone 오브젝트를 연결)
    [SerializeField] private TextMeshProUGUI captureZoneText;
    [SerializeField] private TextMeshProUGUI produceTroopersText;
    [SerializeField] private TextMeshProUGUI buildBarracksText;

    [Header("서브목표")]
    [SerializeField] private TextMeshProUGUI clearEnemiesText;
    [SerializeField] private TextMeshProUGUI secureOreText;

    private RTSUnitController rtsController;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return; // 이미 승패가 갈렸으면 더 이상 갱신하지 않음

        bool zoneCaptured = targetZone != null && targetZone.Owner == CaptureOwner.Ally;
        bool troopersReady = CountAliveUnits(AssaultTrooperUnitID) >= RequiredTrooperCount;
        bool barracksBuilt = rtsController != null && rtsController.HasCompletedBuilding(BarracksBuildingID);
        bool enemiesCleared = FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None).Length == 0;
        bool oreSecured = rtsController != null && rtsController.GetOre() >= RequiredOre;

        SetObjectiveText(captureZoneText, "거점 1개 점령하기", zoneCaptured);
        SetObjectiveText(produceTroopersText, $"어썰트 트루퍼 {RequiredTrooperCount}기 생산하기", troopersReady);
        SetObjectiveText(buildBarracksText, "병영 건설하기", barracksBuilt);
        SetObjectiveText(clearEnemiesText, "(서브) 주변 적 유닛 모두 제거", enemiesCleared);
        SetObjectiveText(secureOreText, $"(서브) 광물 {RequiredOre} 확보", oreSecured);

        if (zoneCaptured && troopersReady && barracksBuilt)
            StageManager.Instance?.ReportVictory();
    }

    private int CountAliveUnits(int unitID)
    {
        if (rtsController == null) return 0;

        int count = 0;
        foreach (UnitController unit in rtsController.UnitList)
            if (unit != null && unit.GetUnitID() == unitID)
                count++;

        return count;
    }

    // 완료 시 텍스트를 <s>(취소선)로 감싸고, 미완료면 그대로 표시 - 매 프레임 다시 호출되므로
    // 조건이 다시 깨지면 취소선도 자동으로 사라진다.
    private static void SetObjectiveText(TextMeshProUGUI text, string description, bool complete)
    {
        if (text == null) return;
        text.text = complete ? $"<s>{description}</s>" : description;
    }
}
```

### 씬 작업 (스크립트 생성 후 별도로 필요, 코드로는 할 수 없음)
- 튜토리얼(0스테이지) 씬에 빈 GameObject를 만들어 `Stage0Objectives` 컴포넌트 부착
- 목표 텍스트 5개를 표시할 `TextMeshProUGUI` UI 오브젝트 5개를 만들어 각 필드에 연결
- 점령 대상 `TerritoryZone` 오브젝트를 `targetZone`에 연결

## 확인 결과

1. 이 설계(주목표 3개 완료 시 승리)로 진행 확정
2. "주변 적 유닛" 판정은 씬 전체 적 유닛 수(`FindObjectsByType<EnemyUnitController>()`)로 확정
3. 서브목표(적 전멸/광물 1000)는 승리 조건에 포함하지 않는 것으로 확정 — 체크리스트 표시만 함

## 검증

- 사용자 확인 후 위 설계안 그대로 `Assets/Scripts/System/Stage0Objectives.cs` 생성
- `uloop compile`: `Success: true, ErrorCount: 0` (Stage0Objectives.cs 관련 에러/경고 없음)
- 씬에 `Stage0Objectives` 오브젝트 배치, 목표 텍스트 UI 5개 연결, `targetZone` 연결은 다음 작업으로 남김(코드로 할 수 없는 씬 편집)

## 영향받는 파일

- `Assets/Scripts/System/Stage0Objectives.cs` (신규, 생성 완료)
