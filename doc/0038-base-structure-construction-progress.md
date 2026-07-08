# 0038. BaseStructure(건물 기반) 건설 진행 시스템

**날짜:** 2026-07-09

## 요청 내용
> 이제 건물이 지어지기 전에 건물 기반(건설시간에 맞춰서 지어지도록하는)이 먼저 지어지고 내가 이미 BaseStructure라는 프리팹은 만들었는데 BaseStructure 스크립트 하나 만들기 해당 스크립트는 건물의 종류, 건설시간을 받아와 일꾼이 붙어서 건설중일때 건설시간이 줄어들고 일꾼이 건설중이지 않을때 건설시간 퍼즈 그리고 건설시간이 다 지나면 건물의 종류에 맞는 건물을 생성하고 자신은 destroy함. 하도록 구현해줘 그리고 건설중일때는 일꾼이 붙어있도록 그렇게 되면 일꾼 유닛은 건물이 지어질때까지 명령을 못내리도록 해줘. 만약 중간에 일꾼이 죽으면 건설은 퍼즈(중단)되고 다른 일꾼을 BaseStructure에다가 우클릭 시 그 건물로 가서 가까우 붙으면 다시 건설진행(일꾼은 또 명령불가 상태로) 이런식으로 구현해줘

요구사항 정리:
1. 일꾼이 건설 위치에 도착하면 [[0037-build-site-ghost-and-exit-buildmode-on-click|기존처럼 즉시 완성된 건물이 생기는 게 아니라]], 먼저 `BaseStructure`(건물 기반)가 생성된다.
2. `BaseStructure`는 "건물 종류"와 "건설시간"을 받아서, 일꾼이 붙어 있는 동안 건설시간이 줄고, 일꾼이 없으면 퍼즈(정지)된다.
3. 건설시간이 다 되면 해당 건물 종류의 실제 건물을 생성하고 `BaseStructure` 자신은 파괴된다.
4. 건설 중인 일꾼은 건물이 완공될 때까지 다른 명령(이동/공격/정지/채취 등)을 받을 수 없다.
5. 건설 중이던 일꾼이 죽으면 건설이 퍼즈되고, 다른 일꾼을 `BaseStructure`에 우클릭하면 그 자리로 이동해 붙어서 건설을 재개한다(그 일꾼도 다시 명령불가 상태가 됨).

## 조사 결과 (현재 코드 상태)
- `BaseStructure.prefab`(`Assets/prefabs/NTA/Building/BaseStructure.prefab`)은 이미 존재하지만 아직 스크립트가 없는 순수 비주얼 프리팹(Transform + MeshFilter/Renderer + BoxCollider, 레이어 0=Default, 자식으로 작은 "Marker" 오브젝트 포함).
- `BuildingData`(`BuildingDataSO.cs`)에 이미 `productionTime`(건물별 건설시간, 예: CommandCenter=50, SupplyDepot=25) 필드가 존재 — 이걸 그대로 "건설시간"으로 재사용.
- 건물 배치 레이어는 프로젝트 레이어 목록(`ProjectSettings/TagManager.asset`) 기준 `Building = 9`번 레이어. 실제 건물 프리팹(`MainBase.prefab` 등)도 `BuildingController`가 붙은 루트 오브젝트가 레이어 9. `UserControl.cs`의 우클릭 처리(`layerBuilding` 레이캐스트)가 이 레이어를 사용 중이므로, `BaseStructure`도 레이어 9로 맞춰야 우클릭으로 감지 가능.
- [[0036-worker-walks-to-build-site|0036]]에서 만든 `UnitController.GoBuild(destination, onArrived, onCancelled)`을 그대로 재사용 가능 — "목적지 근처 도착 시 콜백 실행" 로직이 "일꾼이 BaseStructure에 도착해서 붙는다"는 이번 요구사항과 동일한 패턴.
- 현재 `UnitController`의 명령 진입점(`MoveTo`, `AttackUnitTarget`, `AttackMoveTo`, `AttackFriendlyTarget`, `FollowUnit`, `GoBuild`, `StopUnit`, `PatrolUnit`, `HoldUnit`, `Gather`, `ReturnCargo`, `MoveToBuilding`)은 전부 별도 잠금 없이 즉시 실행됨 — "건설 중엔 명령 불가"를 위해 이 12개 메서드 전부에 동일한 가드를 추가해야 함.
- 일꾼이 죽으면(`Die()`) `Destroy(gameObject)`가 호출되는데, `BaseStructure`가 들고 있는 `builder`(빌더) 참조는 `UnitController` 타입 변수라서 Unity의 "가짜 null"(`== null`이 파괴 후 자동으로 true가 됨) 덕분에 별도 이벤트 연결 없이도 다음 프레임에 자동으로 "담당자 없음" 상태가 됨 → 건설 자동 퍼즈가 별도 코드 없이 자연스럽게 성립.

## 설계안

### 1. `Assets/Scripts/Building/BaseStructure.cs` (신규)
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// 건물 기반(파운데이션) 오브젝트. PlacementSystem이 일꾼 도착 시 이 프리팹을 생성하고 Initialize()로
// 지어질 건물 종류/건설시간을 넘겨준다. 담당 일꾼(builder)이 붙어있는 동안에만 건설시간이 줄어들고,
// 담당 일꾼이 없으면(사망 등으로 Unity의 가짜 null이 되면) 건설이 자동으로 일시정지된다.
// 건설시간이 다 되면 해당 건물을 생성하고 자신은 파괴된다.
public class BaseStructure : MonoBehaviour
{
    [SerializeField] private BuildingDataSO buildingDatabase; // 완공 시 생성할 건물 프리팹을 buildingID로 조회

    [SerializeField] private GameObject buildingMarker; // 우클릭 피드백용 마커(Marker 자식) - 평소엔 꺼져있음
    [SerializeField] private float markerFlashInterval = 0.3f;
    [SerializeField] private int markerFlashCount = 3;
    private Coroutine markerFlashRoutine;

    private int buildingID;
    private float remainingBuildTime;

    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)

    private void Awake()
    {
        if (buildingMarker != null)
            buildingMarker.SetActive(false);
    }

    // PlacementSystem이 스폰 직후 호출해 지어질 건물 종류와 건설시간을 설정한다.
    public void Initialize(int buildingID, float buildTime)
    {
        this.buildingID = buildingID;
        remainingBuildTime = buildTime;
    }

    private void Update()
    {
        if (builder == null)
            return; // 담당 일꾼이 없음(교체 대기 중이거나 방금 사망) - 건설 일시정지

        remainingBuildTime -= Time.deltaTime;

        if (remainingBuildTime <= 0f)
            CompleteConstruction();
    }

    // 일꾼이 도착해서 건설을 시작(또는 재개)할 때 호출된다. 이미 다른 일꾼이 담당 중이었다면 그 일꾼의 건설 담당을 풀어준다.
    public void AttachBuilder(UnitController worker)
    {
        if (builder != null && builder != worker)
            builder.FinishConstruction();

        builder = worker;
    }

    public int GetBuildingID() => buildingID;

    // 다른 일꾼을 이 건물 기반에 우클릭했을 때(UserControl) 피드백으로 마커를 짧게 깜빡인다. (Enemy/Building 등과 동일한 패턴)
    public void FlashMarker()
    {
        if (buildingMarker == null)
            return;

        if (markerFlashRoutine != null)
            StopCoroutine(markerFlashRoutine);

        markerFlashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(markerFlashInterval);

        for (int i = 0; i < markerFlashCount; i++)
        {
            buildingMarker.SetActive(true);
            yield return wait;
            buildingMarker.SetActive(false);
            yield return wait;
        }

        markerFlashRoutine = null;
    }

    private void CompleteConstruction()
    {
        BuildingData data = buildingDatabase != null
            ? buildingDatabase.buildingData.Find(d => d.ID == buildingID)
            : null;

        if (data != null && data.Prefab != null)
        {
            GameObject obj = Instantiate(data.Prefab, transform.position, transform.rotation);

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;
        }

        if (builder != null)
            builder.FinishConstruction();

        Destroy(gameObject);
    }
}
```

### 2. `UnitController.cs` — 건설 중 명령 잠금

**필드 추가** (`hasBuildOrder` 아래):
```csharp
// 변경 코드 (추가)
    // ===== 건설 진행 (BaseStructure에 붙어서 건설 중일 때는 다른 명령을 받을 수 없다) =====
    private BaseStructure attachedStructure;
    private bool isConstructing;
```

**진입점 추가** (`BuildTick()` 아래):
```csharp
    // BaseStructure에 도착해서 건설을 시작(또는 재개)할 때 호출된다(GoBuild의 onArrived에서 호출).
    // structure가 이미 파괴된 경우(도착 전에 다른 일꾼이 먼저 완공한 경우 등)는 그냥 아무 것도 하지 않고 자유 상태로 남는다.
    public void BeginConstruction(BaseStructure structure)
    {
        if (structure == null)
            return;

        attachedStructure = structure;
        isConstructing = true;

        structure.AttachBuilder(this);
    }

    // 건설이 끝나거나(완공) 다른 일꾼으로 교체되어 담당에서 풀렸을 때 BaseStructure가 호출한다.
    public void FinishConstruction()
    {
        isConstructing = false;
        attachedStructure = null;
    }

    public bool IsConstructing() => isConstructing;
```

**명령 진입점 12곳에 가드 추가** (각 메서드 맨 첫 줄에 `if (isConstructing) return;`):
`MoveTo`, `AttackUnitTarget`, `AttackMoveTo`, `AttackFriendlyTarget`, `FollowUnit`, `GoBuild`, `StopUnit`, `PatrolUnit`, `HoldUnit`, `Gather`, `ReturnCargo`, `MoveToBuilding`.

예시(`MoveTo`):
```csharp
// 기존 코드
    public void MoveTo(Vector3 end)
    {
        CancelGatheringForNewCommand();
        CancelAttackOrder();
```
```csharp
// 변경 코드
    public void MoveTo(Vector3 end)
    {
        if (isConstructing) return; // 건설 중엔 다른 명령을 받지 않는다

        CancelGatheringForNewCommand();
        CancelAttackOrder();
```
(나머지 11개 메서드도 동일하게 첫 줄에 `if (isConstructing) return;` 추가.)

### 3. `RTSUnitController.cs`

**`GetSelectedWorker()`가 건설 중인 일꾼은 "가용하지 않음"으로 취급하도록 수정** (건설 중인 일꾼에게 새 건설 명령이 몰래 들어가 그리드/고스트만 예약되고 실제로는 무시되는 사고 방지):
```csharp
// 기존 코드
    public UnitController GetSelectedWorker()
    {
        if (selectedUnitList.Count == 0)
            return null;

        UnitController unit = selectedUnitList[0];
        return unit != null && unit.CompareTag("Worker") ? unit : null;
    }
```
```csharp
// 변경 코드
    public UnitController GetSelectedWorker()
    {
        if (selectedUnitList.Count == 0)
            return null;

        UnitController unit = selectedUnitList[0];
        return unit != null && unit.CompareTag("Worker") && !unit.IsConstructing() ? unit : null;
    }
```

**`BaseStructure` 우클릭 시 선택된 일꾼을 보내는 진입점 추가**:
```csharp
    // BaseStructure(건설 중단된 건물 기반) 우클릭: 선택된 일꾼을 보내 붙여서 건설을 재개시킨다.
    public void AssignBuilderToStructure(BaseStructure structure)
    {
        UnitController worker = GetSelectedWorker();
        if (worker == null)
            return;

        worker.GoBuild(
            structure.transform.position,
            onArrived: () => worker.BeginConstruction(structure),
            onCancelled: null);
    }
```

### 4. `UserControl.cs` — BaseStructure 우클릭 처리

`HandleRightClick()`의 "건물 우클릭" 블록에 추가:
```csharp
// 기존 코드
        // 건물 우클릭
        if(clickedBuilding)
        {
            BuildingController building = BuildingHit.transform.GetComponent<BuildingController>();

            if (building != null && rtsUnitController.IsUnitSelect())
            {
                rtsUnitController.MoveToBuildingSelectedUnits(building);

                UsercurrentState = OrderState.Move;
                UpdatePointer();
                movePointer.transform.position = building.transform.position;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;
            }
        }
```
```csharp
// 변경 코드
        // 건물 우클릭
        if(clickedBuilding)
        {
            BuildingController building = BuildingHit.transform.GetComponent<BuildingController>();

            if (building != null && rtsUnitController.IsUnitSelect())
            {
                rtsUnitController.MoveToBuildingSelectedUnits(building);

                UsercurrentState = OrderState.Move;
                UpdatePointer();
                movePointer.transform.position = building.transform.position;
                movePointer.SetActive(true);

                UsercurrentState = OrderState.None;
            }

            // 건설이 중단된 BaseStructure 우클릭 = 선택된 일꾼을 보내 건설 재개
            BaseStructure baseStructure = BuildingHit.transform.GetComponent<BaseStructure>();
            if (baseStructure != null && rtsUnitController.IsUnitSelect())
            {
                rtsUnitController.AssignBuilderToStructure(baseStructure);
                baseStructure.FlashMarker();

                movePointer.transform.position = baseStructure.transform.position;
                movePointer.SetActive(true);
            }
        }
```

### 5. `PlacementSystem.cs` — 도착 시 완성 건물 대신 BaseStructure 생성

**필드 추가**:
```csharp
// 기존 코드
    private RTSUnitController rtsController;
```
```csharp
// 변경 코드
    private RTSUnitController rtsController;

    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹
```
(⚠️ 이 필드는 씬 파일을 직접 손대지 않고는 자동으로 연결할 수 없어서, **적용 후 유니티 에디터에서 `Placement System` 컴포넌트의 `Base Structure Prefab` 슬롯에 `BaseStructure` 프리팹을 직접 드래그해서 넣어주셔야 합니다.**)

**`PlaceStructure()`의 콜백 및 `CompleteConstruction` → `StartConstruction`으로 교체**:
```csharp
// 기존 코드
        worker.GoBuild(
            spawnPos,
            onArrived: () => CompleteConstruction(data, spawnPos, placedIndex, ghost),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));

        // 클릭 한 번으로 배치를 확정했으므로 건설모드는 여기서 종료한다 (기존 "취소" 버튼과 동일한 종료 방식)
        StopPlacement();
        rtsController?.ReturnState();
    }

    // 일꾼이 건설 위치에 도착했을 때(GoBuild 콜백) 고스트를 지우고 실제 건물을 생성한다.
    private void CompleteConstruction(BuildingData data, Vector3 spawnPos, int placedIndex, GameObject ghost)
    {
        if (ghost != null)
            Destroy(ghost);

        GameObject obj = Instantiate(data.Prefab);

        var obstacle = obj.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.enabled = true;
        }
        obj.transform.position = spawnPos;

        placedGameObject[placedIndex] = obj;
    }
```
```csharp
// 변경 코드
        worker.GoBuild(
            spawnPos,
            onArrived: () => StartConstruction(data, spawnPos, placedIndex, ghost, worker),
            onCancelled: () => CancelReservedConstruction(gridPos, ghost));

        // 클릭 한 번으로 배치를 확정했으므로 건설모드는 여기서 종료한다 (기존 "취소" 버튼과 동일한 종료 방식)
        StopPlacement();
        rtsController?.ReturnState();
    }

    // 일꾼이 건설 위치에 도착했을 때(GoBuild 콜백) 고스트를 지우고 BaseStructure(건물 기반)를 생성해 일꾼을 붙인다.
    // 실제 완성된 건물은 BaseStructure 자신이 건설시간이 다 되면 생성한다.
    private void StartConstruction(BuildingData data, Vector3 spawnPos, int placedIndex, GameObject ghost, UnitController worker)
    {
        if (ghost != null)
            Destroy(ghost);

        GameObject obj = Instantiate(baseStructurePrefab, spawnPos, Quaternion.identity);

        BaseStructure structure = obj.GetComponent<BaseStructure>();
        structure.Initialize(data.ID, data.productionTime);

        placedGameObject[placedIndex] = obj;

        worker.BeginConstruction(structure);
    }
```
(`CancelReservedConstruction`은 변경 없음 - 도착 전 취소 시나리오는 그대로 유지.)

### 6. `BaseStructure.prefab` 수정 (기존 프리팹에 스크립트/레이어 연결)
- 루트 `BaseStructure` 오브젝트의 `m_Layer`를 `0`(Default) → `9`(Building)로 변경 — 실제 건물과 같은 레이어라 `UserControl`의 건물 우클릭 레이캐스트(`layerBuilding`)로 감지됨.
- 루트 오브젝트에 새 `BaseStructure` 컴포넌트를 추가하고, `buildingDatabase` 필드를 기존 다른 건물들이 쓰는 것과 동일한 `New Building Data SO.asset`(guid `6f69615904d465747a380db568c38542`)으로, `buildingMarker` 필드를 이미 있는 자식 "Marker" 오브젝트로 연결.

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **건설 중 명령 차단 범위**: 플레이어가 내리는 실제 명령 메서드 12곳만 막음. `AttackRange`가 자동으로 호출하는 `Attack`/`ChaseTarget`은 건드리지 않음 — 일꾼은 보통 공격 사거리(AttackRange)가 없어서 실질적으로 해당 없는 경우로 판단, 필요시 별도 요청으로 처리.
- **건설 중이던 일꾼이 죽으면**: `BaseStructure.builder`가 Unity의 가짜 null이 되어 자동으로 건설이 퍼즈됨 (별도 이벤트 연결 불필요).
- **다른 일꾼을 새 건설 명령(건설모드)에 보내려 할 때, 그 일꾼이 이미 건설 중이면**: `GetSelectedWorker()`가 이를 "가용한 일꾼 없음"으로 취급해 아예 배치가 시작되지 않음(그리드/고스트가 쓸데없이 예약되는 사고 방지).
- **건설 도중 다른 일꾼으로 교체(우클릭)**: 새 일꾼이 붙으면(`AttachBuilder`) 기존 담당 일꾼은 자동으로 해제(`FinishConstruction`)되어 다시 명령을 받을 수 있는 상태로 돌아감.
- **건설 중인 BaseStructure에 도착하기 전에 그 BaseStructure가 이미 다른 일꾼에 의해 완공되어 사라진 경우**: `BeginConstruction`에서 null 체크로 방어 — 그 일꾼은 그냥 그 자리에 서서 다시 명령 받을 수 있는 자유 상태가 됨.
- **BaseStructure 자체의 NavMesh 장애물 처리**: 이번엔 추가하지 않음 — 건설 중엔 유닛이 그 자리를 그냥 통과해서 지나갈 수 있음(완공된 실제 건물이 생성되는 순간부터는 기존처럼 NavMeshObstacle이 활성화됨). 필요하면 별도 요청으로 추가.
- **우클릭 피드백**: 기존 Enemy/Building/Unit/ResourceNode와 동일한 "마커 0.3초 간격 3회 깜빡임" 패턴을 BaseStructure에도 추가(요청엔 명시 안 됐지만, 프리팹에 이미 준비된 "Marker" 자식 오브젝트가 있고 다른 모든 우클릭 대상이 이 패턴을 쓰고 있어 일관성 차원에서 포함).
- **⚠️ 프리팹/씬 수동 확인 필요**: `BaseStructure.prefab`은 이번에 직접 스크립트/레이어를 연결해서 커밋하지만, `PlacementSystem`이 있는 씬(`SampleScene.unity`)은 직접 손대지 않음 — 적용 후 유니티 에디터에서 `Placement System` 컴포넌트의 새 `Base Structure Prefab` 슬롯에 `BaseStructure` 프리팹을 한 번 드래그해서 연결해주셔야 실제로 동작합니다. 또한 프리팹을 코드로 직접 수정하는 것이라, 적용 후 유니티에서 열어 `BaseStructure` 컴포넌트와 필드들이 정상적으로 연결됐는지 한 번 확인해주시면 좋겠습니다.

## 변경 예정 파일
- `Assets/Scripts/Building/BaseStructure.cs` (신규)
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
- `Assets/Scripts/UserControl/UserControl.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
- `Assets/prefabs/NTA/Building/BaseStructure.prefab`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드/프리팹에 반영함 (설계와 구현 간 차이 없음). `PlacementSystem`의 `Base Structure Prefab` 필드는 씬 파일을 직접 건드리지 않았으므로 유니티 에디터에서 직접 연결 필요.
