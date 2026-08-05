# 0438. Stage1~5Objectives 스크립트 신설 (제안)

**날짜:** 2026-08-05

## 요청 내용
> 각 스테이지 별로 미션 목표를 구성해줘 Campaign.md파일을 참고하여 Stage0Objectives와 같이 1~5까지
> 스테이지오브젝티브스 스크립를 구성해줘
>
> 1 스테이지 OC 전초기지(메인기지) 파괴 -> 게임오브젝트 연결(내가 직접할거임), 광물 2000 확보
> 레이더 기지 점령 -> 레이더 기지 연결(내가 직접할거임),
> 적 건물 모두 파괴 -> 적 건물 리스트에 있는거 확인(매 프레임 하지말고 부서져서 리스트에 변화가
> 있을때마다 갱신
>
> 2 스테이지
> 외계 유물 1개 확보 -> 정확한 로직은 일꾼이 유물 근처로 가면 유물을 획득(유물이 트리거 범위 안에
> 있는 일꾼중 가장 가까운 일꾼을 따라감) -> 획득한 유물을 비콘(트리거) 로 가져가면 승리
> OC 연구 데이터 확보 -> 같은 원리로 데이터 오브젝트가 있고 그게 비콘으로 가져오면 서브 목표 완료
>
> 3 스테이지
> 외계 전초기지 제거 -> 게임 오브젝트 연결(내가 직접 할거임)
> 생존한 OC 병사 구조 -> 맵에 존재하는 비콘(트리거)를 아무 유닛으로 가서 밟으면 클리어
>
> 4 스테이지
> 외계 사령기지 파괴 -> 게임 오브젝트 연결(내가 직접 할거임)
> OC 사령부 생존 -> 게임 오브젝트 연결(내가 직접 할거임) destory 됬는지 확인 부서지면 서브 목표
> 실패 생존시에는 계속 완료 상태였다가 부서지면 다시 깰수 없음
>
> 5 스테이지
> 에너지 코어 3개 파괴 -> 게임 오브젝트 리스트로 연결(내가 직접할거임)
> 외계 지휘 코어 제거 -> 게임 오브젝트 연결(내가 직접할거임)
>
> OC 사령부 생존 -> 4스테이지와 동일
>
> 각 스테이지별 스크립트 5개가 만들어야함

## 현재 구조

`Assets/Scripts/System/Stage0Objectives.cs`가 유일한 선례: 매 프레임 조건을 재평가해 텍스트를
`<s>`(취소선)로 감싸고, 주목표가 전부 완료되면 `StageManager.Instance.ReportVictory()`를 호출한다.
서브목표는 체크리스트 표시만 하고 승리 조건에는 포함하지 않는다. `Docs/Campaign.md`가 미션별
메인/서브 목표 텍스트의 원본이다.

관련 기존 컴포넌트:
- `RTSUnitController` — `UnitList`(아군 유닛 전체), `GetOre()`, `BuildingList` 등 허브.
- `TerritoryZone.Owner == CaptureOwner.Ally` — 거점 점령 판정(Stage0의 레이더 기지 점령과 동일 패턴).
- `HealthManager`/`IDestructible.Die()` → 결국 `Destroy(gameObject)` — "파괴됐는지"는 연결해둔
  GameObject 참조가 `null`이 됐는지로 판정 가능(유니티의 페이크-null 비교).
- `EnemyBuildingController` — 적 건물 하나하나의 껍데기 컴포넌트. 지금은 씬 전체의 적 건물을 모아
  놓은 리스트가 없음 → "적 건물 모두 파괴"를 매 프레임 스캔 없이 판정하려면 이 리스트가 새로 필요.

## 제안하는 변경

### 1) `EnemyBuildingController.cs`에 정적 리스트 + 변경 이벤트 추가

적 건물이 `Start()`에서 스스로 등록하고 `OnDestroy()`(=`Die()`로 파괴되든, 씬 언로드로 파괴되든
공통 경로)에서 스스로 등록 해제하면서 `OnActiveBuildingsChanged` 이벤트를 쏜다. Stage1Objectives는
이 이벤트를 구독해서, 이벤트가 올 때만 "리스트가 비었는지"를 다시 계산한다 — 매 프레임 스캔하지
않음(요청사항).

### 2) 체크리스트 텍스트 헬퍼를 `ObjectiveTextUtil`로 추출

Stage0Objectives의 `SetObjectiveText` 오버로드 2개(불리언형/개수비교형)를 그대로 쓰는 스크립트가
이제 6개(Stage0~5)가 되므로, 정적 유틸 클래스 하나로 옮기고 Stage0Objectives도 그걸 호출하도록
바꾼다. 동작은 완전히 동일 — 중복 제거만.

### 3) `Stage1Objectives.cs` ~ `Stage5Objectives.cs` 신설

Stage0과 동일하게 "매 프레임 재평가 + 주목표 완료 시 승리"가 기본 틀. 스테이지마다 다른 부분:

- **1스테이지** — 주목표: OC 전초기지(메인기지) GameObject 파괴 확인. 서브: 광물 2000, 레이더 기지
  `TerritoryZone` 점령, 적 건물 전멸(이벤트 구독).
- **2스테이지** — 유물/연구데이터 확보. 트리거 콜라이더를 새로 만들지 않고, 이 스크립트가 매 프레임
  "일꾼이 아이템 `pickupRadius` 안에 있는지" 직접 거리 판정으로 대신한다(요청한 "5개 스크립트만"
  틀에 맞추기 위해 아이템/비콘에 별도 컴포넌트를 붙이지 않음). 로직: 아직 아무도 안 들었으면 범위
  안의 가장 가까운 일꾼을 찾아 "든 상태"로 만들고, 든 동안은 아이템이 그 일꾼 위치를 매 프레임
  따라간다. 아이템이 비콘 `deliverRadius` 안에 들어오면 반납 완료 처리 후 아이템을 비활성화한다.
  든 일꾼이 죽으면(참조가 null) 자동으로 "아직 안 든" 상태로 돌아가 다시 주울 수 있다. 유물 확보 =
  주목표(승리), 연구 데이터 확보 = 서브목표(체크리스트만).
- **3스테이지** — 주목표: 외계 전초기지 파괴 확인. 서브: 구조 비콘 `rescueRadius` 안에 아군 유닛
  아무거나 들어오면 완료 — 트리거 대신 거리 판정, 한 번 완료되면 영구 고정(다시 벗어나도 안 풀림).
- **4스테이지** — 주목표: 외계 사령기지 파괴 확인. 서브: OC 사령부 생존 — 살아있는 동안 계속
  "완료" 표시, 파괴되는 순간부터는 영구히 "미완료"로 고정(요청사항 - 되살아나서 다시 깰 수 없음).
- **5스테이지** — 주목표 2개: 에너지 코어 리스트(연결된 것 중 파괴된 개수, 개수비교형 텍스트로
  "N/3" 표시) + 외계 지휘 코어 파괴. 서브: OC 사령부 생존(4스테이지와 동일 규칙). 주목표 2개가
  모두 완료돼야 승리.

공통 규칙: "GameObject 연결 → 파괴 확인" 계열은 전부 `Start()`에서 처음 연결 여부(`null`이 아니었는지)를
저장해두고, 그 값이 `true`인 상태에서 나중에 `null`이 되면 "파괴됨"으로 판정한다 — 아직 인스펙터에서
연결하지 않은 상태(처음부터 null)를 파괴된 것으로 착각하지 않기 위함.

버튼/오브젝트/텍스트 연결은 전부 요청대로 직접 하실 것이므로 씬/프리팹 파일은 건드리지 않음 —
스크립트만 추가/수정.

## 구현 (승인 후 적용됨)

### `EnemyBuildingController.cs` (일부)

**Before:**
```csharp
using System.Collections;
using UnityEngine;
using FischlWorks_FogWar;
...
public class EnemyBuildingController : MonoBehaviour, IDestructible
{
    [SerializeField] private GameObject buildingMarker;
    ...
    private void Start()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        healthManager = GetComponent<HealthManager>();
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
        fogWar = FindFirstObjectByType<csFogWar>();
        ...
    }
    ...
    public void Die()
    {
        rtsController?.ClearSelectedEnemyBuildingIfMatches(this);

        Destroy(gameObject);
    }
}
```

**After:**
```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FischlWorks_FogWar;
...
public class EnemyBuildingController : MonoBehaviour, IDestructible
{
    // 씬에 존재하는 모든 적 건물 목록. 등록/해제(=파괴) 시에만 갱신되고 이벤트로 알리므로, "적 건물
    // 모두 파괴" 같은 스테이지 목표는 매 프레임 스캔하지 않고 이 이벤트를 구독해서 필요할 때만
    // 다시 계산하면 된다.
    public static readonly List<EnemyBuildingController> ActiveBuildings = new List<EnemyBuildingController>();
    public static event System.Action OnActiveBuildingsChanged;

    [SerializeField] private GameObject buildingMarker;
    ...
    private void Start()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);

        healthManager = GetComponent<HealthManager>();
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
        fogWar = FindFirstObjectByType<csFogWar>();

        ActiveBuildings.Add(this);
        OnActiveBuildingsChanged?.Invoke();
        ...
    }

    private void OnDestroy()
    {
        ActiveBuildings.Remove(this);
        OnActiveBuildingsChanged?.Invoke();
    }
    ...
    public void Die()
    {
        rtsController?.ClearSelectedEnemyBuildingIfMatches(this);

        Destroy(gameObject); // OnDestroy()에서 리스트 해제/이벤트가 자동으로 따라온다
    }
}
```

### `ObjectiveTextUtil.cs` (신규)

```csharp
using TMPro;
using UnityEngine;

// 스테이지 목표 체크리스트 텍스트 표시 공통 헬퍼(Stage0~5Objectives 공유). 완료 시 <s>(취소선)로
// 감싸고, 미완료면 그대로 표시한다. 매 프레임 다시 호출되는 것을 전제로 하므로 "한 번 완료되면
// 고정"하지 않는다 - 조건이 다시 깨지면 취소선도 자동으로 사라진다.
public static class ObjectiveTextUtil
{
    public static void SetObjectiveText(TextMeshProUGUI text, string description, bool complete)
    {
        if (text == null) return;
        text.text = complete ? $"<s>{description}</s>" : description;
    }

    // 개수 비교형 목표용 오버로드 - "설명 (현재/목표)" 형식으로 표시(요청사항: 9/10 형식).
    // 현재값이 목표를 넘어도 표시는 목표치에서 고정(예: 1050/1000이 아니라 1000/1000으로 표시).
    public static void SetObjectiveText(TextMeshProUGUI text, string description, int current, int target)
    {
        if (text == null) return;
        bool complete = current >= target;
        string content = $"{description} ({Mathf.Min(current, target)}/{target})";
        text.text = complete ? $"<s>{content}</s>" : content;
    }
}
```

### `Stage0Objectives.cs` (일부, 리팩터링만 — 동작 동일)

**Before:**
```csharp
        SetObjectiveText(captureZoneText, "거점 1개 점령하기", zoneCaptured);
        SetObjectiveText(produceTroopersText, "어썰트 트루퍼 생산하기", trooperCount, RequiredTrooperCount);
        SetObjectiveText(buildBarracksText, "병영 건설하기", barracksBuilt);
        SetObjectiveText(clearEnemiesText, "(서브) 주변 적 유닛 모두 제거", enemiesCleared);
        SetObjectiveText(secureOreText, "(서브) 광물 확보", oreAmount, RequiredOre);
...
    // 완료 시 텍스트를 <s>(취소선)로 감싸고, 미완료면 그대로 표시 - 매 프레임 다시 호출되므로
    // 조건이 다시 깨지면 취소선도 자동으로 사라진다.
    private static void SetObjectiveText(TextMeshProUGUI text, string description, bool complete)
    {
        if (text == null) return;
        text.text = complete ? $"<s>{description}</s>" : description;
    }

    // 개수 비교형 목표용 오버로드 - "설명 (현재/목표)" 형식으로 표시(요청사항: 9/10 형식).
    // 현재값이 목표를 넘어도 표시는 목표치에서 고정(예: 1050/1000이 아니라 1000/1000으로 표시).
    private static void SetObjectiveText(TextMeshProUGUI text, string description, int current, int target)
    {
        if (text == null) return;
        bool complete = current >= target;
        string content = $"{description} ({Mathf.Min(current, target)}/{target})";
        text.text = complete ? $"<s>{content}</s>" : content;
    }
}
```

**After:**
```csharp
        ObjectiveTextUtil.SetObjectiveText(captureZoneText, "거점 1개 점령하기", zoneCaptured);
        ObjectiveTextUtil.SetObjectiveText(produceTroopersText, "어썰트 트루퍼 생산하기", trooperCount, RequiredTrooperCount);
        ObjectiveTextUtil.SetObjectiveText(buildBarracksText, "병영 건설하기", barracksBuilt);
        ObjectiveTextUtil.SetObjectiveText(clearEnemiesText, "(서브) 주변 적 유닛 모두 제거", enemiesCleared);
        ObjectiveTextUtil.SetObjectiveText(secureOreText, "(서브) 광물 확보", oreAmount, RequiredOre);
...
}
```
(두 `private static SetObjectiveText` 오버로드는 삭제 — `ObjectiveTextUtil`로 이전됨)

### `Stage1Objectives.cs` (신규)

```csharp
using TMPro;
using UnityEngine;

// 1스테이지("국경 분쟁") 임무 목표 체크리스트. Stage0Objectives와 동일한 패턴 - 완료 조건은 매 프레임
// 다시 평가해 취소선을 표시하고, 주목표(OC 전초기지 파괴)가 완료되면 StageManager.ReportVictory()를
// 호출한다. 서브목표(광물/레이더 기지/적 건물 전멸)는 체크리스트 표시만 하고 승리 조건에는
// 포함하지 않는다 (Docs/Campaign.md 미션 1).
//
// "적 건물 모두 파괴"만 예외적으로 매 프레임 스캔하지 않는다 - EnemyBuildingController.ActiveBuildings가
// 등록/파괴될 때만 이벤트를 쏘므로, 그 이벤트가 올 때만 다시 계산한다(요청사항).
public class Stage1Objectives : MonoBehaviour
{
    private const int RequiredOre = 2000;

    [Header("주목표")]
    [SerializeField] private GameObject ocMainBase; // OC 전초기지(메인기지) - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyMainBaseText;

    [Header("서브목표")]
    [SerializeField] private TerritoryZone radarBaseZone; // 점령해야 할 레이더 기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI secureOreText;
    [SerializeField] private TextMeshProUGUI captureRadarBaseText;
    [SerializeField] private TextMeshProUGUI destroyAllEnemyBuildingsText;

    private RTSUnitController rtsController;
    private bool ocMainBaseAssigned;
    private bool allEnemyBuildingsDestroyed;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        ocMainBaseAssigned = ocMainBase != null;
    }

    private void OnEnable()
    {
        EnemyBuildingController.OnActiveBuildingsChanged += RefreshAllEnemyBuildingsDestroyed;
        RefreshAllEnemyBuildingsDestroyed();
    }

    private void OnDisable()
    {
        EnemyBuildingController.OnActiveBuildingsChanged -= RefreshAllEnemyBuildingsDestroyed;
    }

    private void RefreshAllEnemyBuildingsDestroyed()
    {
        allEnemyBuildingsDestroyed = EnemyBuildingController.ActiveBuildings.Count == 0;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool mainBaseDestroyed = ocMainBaseAssigned && ocMainBase == null;
        int oreAmount = rtsController != null ? rtsController.GetOre() : 0;
        bool radarCaptured = radarBaseZone != null && radarBaseZone.Owner == CaptureOwner.Ally;

        ObjectiveTextUtil.SetObjectiveText(destroyMainBaseText, "OC 전초기지(메인기지) 파괴", mainBaseDestroyed);
        ObjectiveTextUtil.SetObjectiveText(secureOreText, "(서브) 광물 확보", oreAmount, RequiredOre);
        ObjectiveTextUtil.SetObjectiveText(captureRadarBaseText, "(서브) 레이더 기지 점령", radarCaptured);
        ObjectiveTextUtil.SetObjectiveText(destroyAllEnemyBuildingsText, "(서브) 적 건물 모두 파괴", allEnemyBuildingsDestroyed);

        if (mainBaseDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
```

### `Stage2Objectives.cs` (신규)

```csharp
using TMPro;
using UnityEngine;

// 2스테이지("미지의 신호") 임무 목표 체크리스트. 외계 유물/OC 연구 데이터는 트리거 콜라이더를 따로
// 붙이지 않고, 이 스크립트가 매 프레임 거리 판정으로 "줍기 → 따라가기 → 반납"을 전부 처리한다
// (스테이지당 스크립트 1개로 완결시키기 위함, 요청사항).
//
// 로직: item을 아직 아무도 안 들었으면 pickupRadius 안의 가장 가까운 일꾼을 찾아 든 상태로 만든다.
// 든 동안은 item이 매 프레임 그 일꾼 위치(+carryOffset)를 따라가고, item이 비콘 deliverRadius
// 안에 들어오면 반납 완료 처리 후 item을 비활성화한다. 든 일꾼이 죽으면(참조 null) 자동으로
// 다시 주울 수 있는 상태로 돌아간다. 유물 확보 = 주목표(승리), 연구 데이터 확보 = 서브목표.
public class Stage2Objectives : MonoBehaviour
{
    [Header("주목표 - 외계 유물")]
    [SerializeField] private Transform artifact; // 유물 오브젝트 - 직접 연결
    [SerializeField] private Transform artifactBeacon; // 유물 반납 지점 - 직접 연결
    [SerializeField] private TextMeshProUGUI collectArtifactText;

    [Header("서브목표 - OC 연구 데이터")]
    [SerializeField] private Transform researchData; // 연구 데이터 오브젝트 - 직접 연결
    [SerializeField] private Transform researchDataBeacon; // 데이터 반납 지점 - 직접 연결
    [SerializeField] private TextMeshProUGUI collectResearchDataText;

    [Header("판정 설정")]
    [SerializeField] private float pickupRadius = 3f;  // 일꾼이 이 범위 안에 들어오면 자동으로 듦
    [SerializeField] private float deliverRadius = 2f; // 비콘까지 이 범위 안이면 반납 완료
    [SerializeField] private Vector3 carryOffset = Vector3.up; // 들린 동안 일꾼 기준 오프셋

    private RTSUnitController rtsController;

    private UnitController artifactCarrier;
    private bool artifactDelivered;

    private UnitController dataCarrier;
    private bool dataDelivered;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        UpdateCarry(artifact, artifactBeacon, ref artifactCarrier, ref artifactDelivered);
        UpdateCarry(researchData, researchDataBeacon, ref dataCarrier, ref dataDelivered);

        ObjectiveTextUtil.SetObjectiveText(collectArtifactText, "외계 유물 확보", artifactDelivered);
        ObjectiveTextUtil.SetObjectiveText(collectResearchDataText, "(서브) OC 연구 데이터 확보", dataDelivered);

        if (artifactDelivered)
            StageManager.Instance?.ReportVictory();
    }

    private void UpdateCarry(Transform item, Transform beacon, ref UnitController carrier, ref bool delivered)
    {
        if (delivered || item == null || rtsController == null)
            return;

        if (carrier == null)
            carrier = FindNearestWorkerInRange(item.position, pickupRadius);

        if (carrier == null)
            return;

        item.position = carrier.transform.position + carryOffset;

        if (beacon != null && Vector3.Distance(item.position, beacon.position) <= deliverRadius)
        {
            delivered = true;
            item.gameObject.SetActive(false);
            carrier = null;
        }
    }

    private UnitController FindNearestWorkerInRange(Vector3 position, float radius)
    {
        UnitController nearest = null;
        float nearestDistSqr = radius * radius;

        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit == null || !unit.CompareTag("Worker"))
                continue;

            float distSqr = (unit.transform.position - position).sqrMagnitude;
            if (distSqr <= nearestDistSqr)
            {
                nearestDistSqr = distSqr;
                nearest = unit;
            }
        }

        return nearest;
    }
}
```

### `Stage3Objectives.cs` (신규)

```csharp
using TMPro;
using UnityEngine;

// 3스테이지("침공") 임무 목표 체크리스트. 주목표(외계 전초기지 제거)가 완료되면 승리 처리한다.
// 서브목표(생존자 구조)는 맵의 구조 비콘 rescueRadius 안에 아무 아군 유닛이나 들어오면 완료로
// 처리하고, 한 번 완료되면 되돌리지 않는다("구조했다"는 사실은 유닛이 다시 벗어나도 취소되지
// 않아야 하므로 - Stage0/1의 "재평가" 목표들과 다름).
public class Stage3Objectives : MonoBehaviour
{
    [Header("주목표")]
    [SerializeField] private GameObject alienOutpost; // 외계 전초기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyOutpostText;

    [Header("서브목표")]
    [SerializeField] private Transform rescueBeacon; // 생존자 구조 지점 - 직접 연결
    [SerializeField] private float rescueRadius = 2f;
    [SerializeField] private TextMeshProUGUI rescueSurvivorsText;

    private RTSUnitController rtsController;
    private bool alienOutpostAssigned;
    private bool survivorsRescued;

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        alienOutpostAssigned = alienOutpost != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool outpostDestroyed = alienOutpostAssigned && alienOutpost == null;

        if (!survivorsRescued && rescueBeacon != null && rtsController != null)
            survivorsRescued = IsAnyUnitWithinRadius(rescueBeacon.position, rescueRadius);

        ObjectiveTextUtil.SetObjectiveText(destroyOutpostText, "외계 전초기지 제거", outpostDestroyed);
        ObjectiveTextUtil.SetObjectiveText(rescueSurvivorsText, "(서브) 생존한 OC 병사 구조", survivorsRescued);

        if (outpostDestroyed)
            StageManager.Instance?.ReportVictory();
    }

    private bool IsAnyUnitWithinRadius(Vector3 position, float radius)
    {
        float radiusSqr = radius * radius;
        foreach (UnitController unit in rtsController.UnitList)
        {
            if (unit != null && (unit.transform.position - position).sqrMagnitude <= radiusSqr)
                return true;
        }
        return false;
    }
}
```

### `Stage4Objectives.cs` (신규)

```csharp
using TMPro;
using UnityEngine;

// 4스테이지("공동 전선") 임무 목표 체크리스트. 주목표(외계 사령기지 파괴)가 완료되면 승리 처리한다.
// 서브목표(OC 사령부 생존)는 살아있는 동안 계속 완료 상태로 표시되다가, 파괴되는 순간부터는
// 다시 살아나지 않으므로 그 이후로는 영구히 미완료로 고정한다(요청사항).
public class Stage4Objectives : MonoBehaviour
{
    [Header("주목표")]
    [SerializeField] private GameObject alienCommandBase; // 외계 사령기지 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyCommandBaseText;

    [Header("서브목표")]
    [SerializeField] private GameObject ocCommandCenter; // OC 사령부 - 직접 연결
    [SerializeField] private TextMeshProUGUI survivalOcCommandText;

    private bool alienCommandBaseAssigned;
    private bool ocCommandCenterAssigned;
    private bool ocCommandCenterDestroyedPermanently;

    private void Start()
    {
        alienCommandBaseAssigned = alienCommandBase != null;
        ocCommandCenterAssigned = ocCommandCenter != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        bool commandBaseDestroyed = alienCommandBaseAssigned && alienCommandBase == null;

        if (!ocCommandCenterDestroyedPermanently && ocCommandCenterAssigned && ocCommandCenter == null)
            ocCommandCenterDestroyedPermanently = true;

        bool ocCommandSurvived = !ocCommandCenterDestroyedPermanently;

        ObjectiveTextUtil.SetObjectiveText(destroyCommandBaseText, "외계 사령기지 파괴", commandBaseDestroyed);
        ObjectiveTextUtil.SetObjectiveText(survivalOcCommandText, "(서브) OC 사령부 생존", ocCommandSurvived);

        if (commandBaseDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
```

### `Stage5Objectives.cs` (신규)

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 5스테이지("최후의 원정") 임무 목표 체크리스트. 주목표 2개(에너지 코어 3개 파괴 + 외계 지휘 코어
// 제거)가 모두 완료되면 승리 처리한다. 서브목표(OC 사령부 생존)는 Stage4Objectives와 동일한 규칙
// (파괴되면 영구히 미완료로 고정)을 따른다.
public class Stage5Objectives : MonoBehaviour
{
    [Header("주목표 - 에너지 코어")]
    [SerializeField] private List<GameObject> energyCores; // 파괴해야 할 에너지 코어들 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyEnergyCoresText;

    [Header("주목표 - 외계 지휘 코어")]
    [SerializeField] private GameObject alienCommandCore; // 외계 지휘 코어 - 직접 연결
    [SerializeField] private TextMeshProUGUI destroyCommandCoreText;

    [Header("서브목표")]
    [SerializeField] private GameObject ocCommandCenter; // OC 사령부 - 직접 연결
    [SerializeField] private TextMeshProUGUI survivalOcCommandText;

    private List<GameObject> trackedEnergyCores;
    private bool alienCommandCoreAssigned;
    private bool ocCommandCenterAssigned;
    private bool ocCommandCenterDestroyedPermanently;

    private void Start()
    {
        trackedEnergyCores = energyCores.FindAll(core => core != null);
        alienCommandCoreAssigned = alienCommandCore != null;
        ocCommandCenterAssigned = ocCommandCenter != null;
    }

    private void Update()
    {
        if (StageManager.Instance != null && StageManager.Instance.Result != StageManager.StageResult.InProgress)
            return;

        int destroyedCoreCount = trackedEnergyCores.FindAll(core => core == null).Count;
        bool allCoresDestroyed = trackedEnergyCores.Count > 0 && destroyedCoreCount == trackedEnergyCores.Count;

        bool commandCoreDestroyed = alienCommandCoreAssigned && alienCommandCore == null;

        if (!ocCommandCenterDestroyedPermanently && ocCommandCenterAssigned && ocCommandCenter == null)
            ocCommandCenterDestroyedPermanently = true;

        bool ocCommandSurvived = !ocCommandCenterDestroyedPermanently;

        ObjectiveTextUtil.SetObjectiveText(destroyEnergyCoresText, "에너지 코어 파괴", destroyedCoreCount, trackedEnergyCores.Count);
        ObjectiveTextUtil.SetObjectiveText(destroyCommandCoreText, "외계 지휘 코어 제거", commandCoreDestroyed);
        ObjectiveTextUtil.SetObjectiveText(survivalOcCommandText, "(서브) OC 사령부 생존", ocCommandSurvived);

        if (allCoresDestroyed && commandCoreDestroyed)
            StageManager.Instance?.ReportVictory();
    }
}
```

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`, `WarningCount: 37`(기존 34개 + 새
  Stage1/2/3의 `FindFirstObjectByType` 사용 3개 — 코드베이스 전역에 이미 있는 동일한 obsolete-API
  경고 패턴이라 새로운 문제 아님).
- GameObject/TerritoryZone/Transform 연결은 요청대로 전부 직접 하실 것이므로 씬/프리팹 파일은
  변경하지 않음.

## 영향받는 파일

- `Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs` (정적 리스트/이벤트 추가)
- `Assets/Scripts/System/ObjectiveTextUtil.cs` (신규)
- `Assets/Scripts/System/Stage0Objectives.cs` (리팩터링만, 동작 동일)
- `Assets/Scripts/System/Stage1Objectives.cs` (신규)
- `Assets/Scripts/System/Stage2Objectives.cs` (신규)
- `Assets/Scripts/System/Stage3Objectives.cs` (신규)
- `Assets/Scripts/System/Stage4Objectives.cs` (신규)
- `Assets/Scripts/System/Stage5Objectives.cs` (신규)
