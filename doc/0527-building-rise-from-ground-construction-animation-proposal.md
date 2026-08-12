# 0527 - 건설 완료 시 건물이 땅속에서 떠오르는 DOTween 애니메이션

## 결과
사용자 확인 후 제안대로 구현 완료. `PlacementSystem.cs`/`PreviewSystem.cs`/`BaseStructure.cs` 3개 파일
모두 위 "코드 변경" 섹션과 동일하게 반영됨. Unity 컴파일 확인 완료(에러 0, 기존에도 있던 무관한 경고만 존재).

## 날짜
2026-08-12

## 요청 내용
건물 건설 중(BaseStructure가 서 있는 동안) 완공될 실제 건물이 땅속에서부터 서서히 떠올라
건설시간에 맞춰 최종 위치까지 올라오는 애니메이션을 DOTween으로 추가해달라는 요청.

## 조사 내용
- `Assets\Scripts\Building\BaseStructure.cs`: 건설 기반 오브젝트. `Initialize()`에서 건설시간/건물종류를
  받고, `Update()`에서 `builder == null`이거나 영토 밖이면 건설을 일시정지(시간/체력 모두 멈춤), 시간이
  다 되면 `CompleteConstruction()`에서 완공 건물을 `Instantiate`하고 자신은 파괴됨.
- `Assets\Scripts\BuildSystem\PreviewSystem.cs`: 배치 중 마우스를 따라다니는 반투명 프리뷰와, 일꾼이
  도착하기 전까지 자리에 남아있는 정적 고스트(`SpawnConstructionGhost`)를 이미 만들고 있음. 두 경우 모두
  `DisableGameplayComponents()`(콜러이더/리지드바디/NavMeshObstacle/HealthManager 표시/FogRevealerAgent/
  BuildingController 비활성화)와 `SetLayerRecursively(Indicators 레이어)`를 거쳐 "허상" 오브젝트로 만듦.
  다만 기존 두 프리뷰는 전용 반투명 머티리얼(`ApplyGhostMaterial`)로 덮어써서, 요청하신 "실제 건물처럼
  보여야 하는" 상승 애니메이션에는 그대로 재사용할 수 없음(머티리얼 교체 단계만 스킵하면 됨).
- `Assets\Scripts\BuildSystem\PlacementSystem.cs`의 `GetGroundOffsetY(prefab)`: 프리팹의 피벗이 지면에
  닿도록 하는 정적 헬퍼로, `BaseStructure.CompleteConstruction()`이 이미 재사용 중. 다만 이 값은 "피벗→
  바닥까지 거리"라서, 건물을 땅속에 파묻을 때 필요한 "전체 높이"는 별도로 필요함(신규 헬퍼 필요).
- `Assets\Scripts\Animation\AutoRotate.cs`: 이 프로젝트에서 DOTween을 쓰는 기존 패턴 확인
  (`private Tween xxxTween;` 필드 + `OnDestroy() => xxxTween?.Kill();`).

## 설계
1. **PlacementSystem.cs**: `GetGroundOffsetY` 옆에 프리팹의 전체 높이를 구하는 `GetBuildingHeight()` 정적
   헬퍼를 추가. (땅속에 얼마나 파묻어서 시작할지 계산용)
2. **PreviewSystem.cs**: 기존 `SpawnConstructionGhost`를 본떠, 머티리얼은 원본 그대로 두고 게임플레이
   컴포넌트만 비활성화하는 `SpawnRisingBuildingPreview()`를 추가.
3. **BaseStructure.cs**:
   - `Initialize()`에서 완공될 건물 프리팹으로 위 프리뷰를 생성하되, 최종 위치에서 프리팹 전체 높이만큼
     아래(땅속)에 배치.
   - `DOMoveY(최종 Y, buildTime)`로 건설시간과 정확히 같은 길이의 트윈을 걺(`Ease.Linear`).
   - `Update()`의 기존 일시정지 분기(일꾼 없음/영토 상실)에서 트윈도 함께 `Pause()`/`Play()`시켜, 건설이
     멈추면 상승도 같이 멈추고 재개되면 같이 재개되도록 함 → 진행률(체력)과 항상 정확히 동기화됨.
   - `OnDestroy()`에서 트윈 Kill + 프리뷰 오브젝트 Destroy (완공/취소/파괴 등 어떤 경로로 사라지든 한 곳에서
     정리 - `CompleteConstruction`/`CancelConstruction`/`Die` 각각에 따로 정리 코드를 넣지 않아도 됨).

건설이 끝나는 순간(`CompleteConstruction`)엔 실제 완공 건물이 같은 위치에 새로 `Instantiate`되고, 그 직후
`Destroy(gameObject)`가 호출되어 프레임 끝에 상승 프리뷰가 함께 사라짐 - 겹쳐 보이는 구간은 최대 1프레임.

## 코드 변경 (제안)

### Assets\Scripts\BuildSystem\PlacementSystem.cs
기존 코드 (`GetGroundOffsetY` 바로 아래):
```csharp
    public static float GetGroundOffsetY(GameObject prefab)
    {
        if (prefab == null)
            return 1f;

        if (!prefab.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            return 1f;

        Bounds bounds = meshFilter.sharedMesh.bounds;
        return (bounds.extents.y - bounds.center.y) * prefab.transform.localScale.y;
    }
```

변경 코드:
```csharp
    public static float GetGroundOffsetY(GameObject prefab)
    {
        if (prefab == null)
            return 1f;

        if (!prefab.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            return 1f;

        Bounds bounds = meshFilter.sharedMesh.bounds;
        return (bounds.extents.y - bounds.center.y) * prefab.transform.localScale.y;
    }

    // 건물 프리팹의 전체 높이(로컬 메쉬 바운드 기준) - 건설 상승 애니메이션에서 건물을 땅속에 얼마나
    // 파묻어 시작할지 계산하는 데 쓰인다 (doc/0527). GetGroundOffsetY와 마찬가지로 루트의 MeshFilter만 본다.
    public static float GetBuildingHeight(GameObject prefab)
    {
        if (prefab == null)
            return 2f;

        if (!prefab.TryGetComponent<MeshFilter>(out var meshFilter) || meshFilter.sharedMesh == null)
            return 2f;

        return meshFilter.sharedMesh.bounds.size.y * prefab.transform.localScale.y;
    }
```

### Assets\Scripts\BuildSystem\PreviewSystem.cs
기존 코드 (`SpawnConstructionGhost` 바로 아래):
```csharp
        return ghost;
    }

    // 프리뷰 표시 종료: 셀 커서를 숨기고 프리뷰 오브젝트를 파괴한다.
    public void StopShowingPreview()
```

변경 코드:
```csharp
        return ghost;
    }

    // 건설 상승 애니메이션(BaseStructure)용: 실제 건물 프리팹을 원래 머티리얼 그대로 생성하되,
    // 게임플레이에 영향을 주는 컴포넌트만 비활성화한다 (doc/0527). SpawnConstructionGhost와 달리
    // 반투명 고스트 머티리얼로 바꾸지 않는다 - "실제 건물이 올라오는" 것처럼 보여야 하기 때문.
    public GameObject SpawnRisingBuildingPreview(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        GameObject obj = Instantiate(prefab, position, rotation);

        DisableGameplayComponents(obj);
        SetLayerRecursively(obj, indicatorsLayer);

        return obj;
    }

    // 프리뷰 표시 종료: 셀 커서를 숨기고 프리뷰 오브젝트를 파괴한다.
    public void StopShowingPreview()
```

### Assets\Scripts\Building\BaseStructure.cs
기존 코드 (상단 using/필드):
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
```
```csharp
    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)
```

변경 코드:
```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using DG.Tweening;
```
```csharp
    private UnitController builder; // 현재 건설 담당 일꾼 (null이면 건설 일시정지)
    private HealthManager healthManager; // 같은 오브젝트에 붙어있는 HealthManager (체력 표시/증가를 여기에 위임)
    private RTSUnitController rtsController;
    private PreviewSystem previewSystem;
    private System.Action onCancelledByPlayer; // 플레이어가 직접 취소했을 때 그리드 예약을 풀어주는 콜백(PlacementSystem 제공)

    private GameObject risingBuilding; // 지면 아래에서 서서히 떠오르는 완공될 건물 프리뷰 (doc/0527)
    private Tween risingTween;
```

기존 코드 (`Start()`):
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```

변경 코드:
```csharp
    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();
        previewSystem = FindFirstObjectByType<PreviewSystem>();
    }
```

기존 코드 (`Initialize()` 안, 완공 건물 데이터 읽는 부분):
```csharp
        BuildingData data = GetBuildingData();

        if (data != null && data.Prefab != null)
        {
            if (data.Prefab.TryGetComponent<HealthManager>(out var finishedHealth))
                finalMaxHealth = finishedHealth.GetMaxHealth();

            if (data.Prefab.TryGetComponent<BuildingController>(out var controller))
                icon = controller.GetIcon();
        }
```

변경 코드:
```csharp
        BuildingData data = GetBuildingData();

        if (data != null && data.Prefab != null)
        {
            if (data.Prefab.TryGetComponent<HealthManager>(out var finishedHealth))
                finalMaxHealth = finishedHealth.GetMaxHealth();

            if (data.Prefab.TryGetComponent<BuildingController>(out var controller))
                icon = controller.GetIcon();

            SpawnRisingBuilding(data.Prefab, buildTime);
        }
```

신규 메서드 추가 (`Initialize()` 아래):
```csharp
    // 완공될 건물을 미리(진짜 머티리얼로) 생성해 지면 아래 파묻어두고, 건설시간에 맞춰 서서히
    // 지면 위 최종 위치까지 떠오르게 한다 (doc/0527). Update()의 건설 일시정지 로직과 Pause()/Play()로
    // 맞물려, 건설이 멈추면 상승도 함께 멈춘다.
    private void SpawnRisingBuilding(GameObject finishedPrefab, float buildTime)
    {
        if (previewSystem == null)
            return;

        Vector3 finalPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(finishedPrefab);
        float buriedDepth = PlacementSystem.GetBuildingHeight(finishedPrefab);
        Vector3 startPos = finalPos + Vector3.down * buriedDepth;

        risingBuilding = previewSystem.SpawnRisingBuildingPreview(finishedPrefab, startPos, transform.rotation);
        risingTween = risingBuilding.transform.DOMoveY(finalPos.y, buildTime).SetEase(Ease.Linear);
    }
```

기존 코드 (`Update()`):
```csharp
    private void Update()
    {
        if (builder == null)
            return; // 담당 일꾼이 없음(교체 대기 중이거나 방금 사망) - 건설 일시정지

        if (!TerritoryManager.IsInsideAlliedTerritory(transform.position))
            return; // 영토를 잃으면 건설 진행(및 그에 딸린 체력 회복)도 함께 일시정지

        remainingBuildTime -= Time.deltaTime;
```

변경 코드:
```csharp
    private void Update()
    {
        if (builder == null)
        {
            risingTween?.Pause();
            return; // 담당 일꾼이 없음(교체 대기 중이거나 방금 사망) - 건설 일시정지
        }

        if (!TerritoryManager.IsInsideAlliedTerritory(transform.position))
        {
            risingTween?.Pause();
            return; // 영토를 잃으면 건설 진행(및 그에 딸린 체력 회복)도 함께 일시정지
        }

        risingTween?.Play();

        remainingBuildTime -= Time.deltaTime;
```

신규 메서드 추가 (클래스 맨 아래, `Die()` 다음):
```csharp
    // 완공/취소/파괴 등 어떤 경로로 사라지든, 상승 중이던 건물 프리뷰와 트윈을 한 곳에서 정리한다.
    private void OnDestroy()
    {
        risingTween?.Kill();

        if (risingBuilding != null)
            Destroy(risingBuilding);
    }
```

## 추가 확인 (선택/생산/명령 불가 요구사항)
사용자가 "해당 고스트 건물은 선택되거나 생산+명령 등 안 되도록 해야한다"고 요청 — 이미 위 설계로 충족됨을 확인:
- `Assets\Scripts\UserControl\UserControl.cs`의 클릭 판정은 `layerBuilding` 레이어로만 레이캐스트한다
  (`Physics.Raycast(ray, out BuildingHit, Mathf.Infinity, layerBuilding)`). `SpawnRisingBuildingPreview()`가
  오브젝트를 `Indicators` 레이어로 옮기고 콜라이더도 전부 꺼버리므로(`DisableGameplayComponents`), 클릭/레이캐스트
  어느 쪽으로도 맞을 수 없음.
- `DisableGameplayComponents()`가 `BuildingController.enabled = false`로 꺼버려 `Start()` 자체가 돌지 않음 →
  `RTSUnitController`의 건물 목록(생산 큐/커맨드 패널/테크트리 조건이 참조)에 등록되지 않아 생산·명령 대상이 될 수 없음.
- 유닛 드래그 박스 선택(`RTSUnitController.DragSelectUnit`)은 유닛 전용이라 건물류는 애초에 대상이 아님.
- 추가 코드 변경 불필요 — 기존 설계 그대로 진행.

## 요약
- 새 파일 없음. 기존 `PlacementSystem`/`PreviewSystem`의 기존 프리뷰/고스트 메커니즘을 재사용해
  최소 변경으로 구현.
- 건설 일시정지(일꾼 이탈/영토 상실)와 자동으로 동기화됨 - 별도 진행률 계산 불필요.
- 완공/취소/파괴 어떤 경로든 `OnDestroy()` 한 곳에서 정리되므로 누수 없음.
- 씬의 `PreviewSystem`이 정확히 하나 존재한다고 가정(기존 `RTSUnitController` 탐색과 동일한 패턴).

## 영향받는 파일 (구현 시)
- `Assets\Scripts\BuildSystem\PlacementSystem.cs`
- `Assets\Scripts\BuildSystem\PreviewSystem.cs`
- `Assets\Scripts\Building\BaseStructure.cs`

## 확인 필요
이대로 구현해도 될지 확인 부탁드립니다. (이징을 `Ease.Linear`가 아닌 다른 곡선으로 바꾸거나, 파묻는
깊이를 조정하는 등 원하시면 말씀해주세요.)
