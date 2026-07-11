# 0055. 시작 위치(StartPoint) 메인기지 자동 생성

**날짜:** 2026-07-12

## 요청 내용
> 시작위치 정하기 — `startPoint`라는 게임오브젝트를 넣을 수 있는 필드를 `PlacementSystem`에 추가해달라. 빈 오브젝트(Empty GameObject)를 시작 위치에 배치한 뒤 직접 연결할 것이니, 게임 시작 시 그 위치에 메인기지를 1개 바로 생성해달라. 그리드에 맞춰서 설치되도록 해야 하고, 그리드에도 반드시 등록되어야 한다.

## 조사 결과 (현재 코드 상태)
- 지금까지는 시작 메인기지가 **씬에 미리 배치된 완성 프리팹**이라고 가정하고 있었다([[0054-building-lift-relocate|0054]]의 "에디터에 미리 배치된 시작 건물" 항목 참고). 이 경우 `PlacementSystem`의 `StructureData`(그리드 점유 정보)에 전혀 등록돼 있지 않아서, 그 자리 위에 다른 건물을 지을 수 있는 등 그리드 정합성이 깨져 있었다. 이번 요청대로 `PlacementSystem`이 직접 스폰하면 이 문제도 자연스럽게 해결된다(그리드 등록 + [[0054-building-lift-relocate|리프트]]용 `gridPosition`까지 정상적으로 채워짐).
- `PlacementSystem.PlaceStructure()`가 이미 "그리드 셀 계산 → 배치 가능 검사 → 정확한 지면 좌표 계산 → 그리드 등록" 파이프라인을 갖고 있다. 다만 실제 완성된 건물이 아니라 `BaseStructure`(건설 진행 중 상태)를 먼저 만드는 경로라, 이번 요청("바로 생성")과는 다르다. 대신 `BaseStructure.CompleteConstruction()`에서 "완성된 건물을 그 자리에 `Instantiate`하고 `NavMeshObstacle`을 켠 뒤 `BuildingController.SetGridInfo()`로 그리드 좌표를 알려준다"는 부분을 그대로 재사용하면 된다(자원 차감/일꾼/건설시간 없이 즉시 완성 상태로).
- 그리드 좌표 계산은 `grid.WorldToCell(worldPos)`, 지면 월드 좌표는 `GetGroundPosition(gridPos, size)`, 건물 높이 오프셋은 `GetGroundOffsetY(prefab)` — 전부 `PlacementSystem`에 이미 있는 private 메서드라 새 메서드 하나에서 그대로 재사용 가능하다.
- 메인기지(커맨드센터)의 건물 ID는 `RTSUnitController.BuildingID.CommandCenter`(`= 1`)이고, `BuildingDataSO.buildingData`에서 `ID == 1`인 항목의 `Prefab`/`Size`/`maxpopulationamount`를 그대로 쓰면 된다.
- 인구수 한도 반영(`rtsController.AddMaxPopulation`)은 기존 `BaseStructure.CompleteConstruction()`에서도 완공 시점에 호출하던 것과 동일한 처리라 여기서도 빠뜨리면 안 된다(안 하면 시작하자마자 유닛을 못 뽑음).
- 체력은 `HealthManager.Awake()`가 `currentHp = maxHealth`로 자동 초기화하므로 별도 처리가 필요 없다(씬에 미리 배치된 건물과 동일하게 항상 풀피로 시작).
- `PlacementSystem.cs`엔 아직 `using UnityEngine.AI;`가 없어서 `NavMeshObstacle`을 쓰려면 이번에 추가해야 한다.

## 설계안

### `Assets/Scripts/BuildSystem/PlacementSystem.cs`

**using 추가**:
```csharp
// 기존 코드
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
```
```csharp
// 변경 코드
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
```

**필드 추가**:
```csharp
// 기존 코드
    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹
```
```csharp
// 변경 코드
    [SerializeField] private GameObject baseStructurePrefab; // 건설 중 표시할 공용 건물 기반(BaseStructure) 프리팹

    [Header("시작 위치")]
    [Tooltip("게임 시작 시 메인기지(커맨드센터)를 그리드에 맞춰 즉시 생성할 위치. 빈 오브젝트를 씬에 배치해서 연결.")]
    [SerializeField] private GameObject startPoint;
```

**`Start()`에서 시작 메인기지 스폰 호출**:
```csharp
// 기존 코드
    void Start()
    {
        StopPlacement();
        StructureData = new();
        rtsController = FindFirstObjectByType<RTSUnitController>();
    }
```
```csharp
// 변경 코드
    void Start()
    {
        StopPlacement();
        StructureData = new();
        rtsController = FindFirstObjectByType<RTSUnitController>();

        SpawnStartingMainBase();
    }
```

**새 메서드 추가** (`StartPlacement` 위):
```csharp
    // 게임 시작 시 startPoint 위치에 메인기지(커맨드센터)를 건설 과정 없이 완성된 상태로 즉시 생성하고,
    // 다른 배치와 동일하게 그리드에 등록한다(리프트 이동을 위한 gridPosition도 함께 설정됨).
    private void SpawnStartingMainBase()
    {
        if (startPoint == null)
            return;

        int index = database.buildingData.FindIndex(d => d.ID == RTSUnitController.BuildingID.CommandCenter);
        if (index < 0)
        {
            Debug.LogWarning("BuildingDataSO에 메인기지(CommandCenter) 데이터가 없습니다.");
            return;
        }

        BuildingData data = database.buildingData[index];
        Vector3Int gridPos = grid.WorldToCell(startPoint.transform.position);

        if (!StructureData.CanPlaceObejctAt(gridPos, data.Size))
        {
            Debug.LogWarning("시작 위치(startPoint)에 메인기지를 배치할 수 없습니다 (그리드 겹침).");
            return;
        }

        Vector3 groundPos = GetGroundPosition(gridPos, data.Size);
        Vector3 spawnPos = groundPos + Vector3.up * GetGroundOffsetY(data.Prefab);

        GameObject obj = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
        if (obstacle != null)
            obstacle.enabled = true;

        if (obj.TryGetComponent<BuildingController>(out var controller))
            controller.SetGridInfo(gridPos); // 이후 리프트 이동 시 자기 자리를 해제할 수 있도록

        placedGameObject.Add(obj);
        int placedIndex = placedGameObject.Count - 1;
        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        rtsController?.AddMaxPopulation(data.maxpopulationamount); // 완공 건물과 동일하게 인구수 한도 반영
    }
```

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **자원/일꾼 소모 없음**: 게임 시작 시 세팅이므로 `TryConstructBuilding`(자원 차감)이나 일꾼 배정 없이 바로 완성된 건물로 생성합니다.
- **씬에 이미 다른 방식으로 배치해둔 시작 건물과의 관계**: 만약 씬에 메인기지를 이미 수동으로 배치해두셨다면, 이번 기능은 그와 별개로 `startPoint` 위치에 **추가로 하나 더** 생성합니다(중복 방지 로직은 넣지 않았습니다). 기존에 수동 배치된 메인기지가 있다면 삭제하고 `startPoint` 오브젝트로 대체하시는 걸 권장드립니다 — 그래야 그 건물도 그리드에 정상 등록되고 [[0054-building-lift-relocate|리프트 기능]]도 정상 동작합니다.
- **배치 실패 시(그리드 겹침 등)**: 조용히 무시하지 않고 `Debug.LogWarning`만 남기고 아무것도 생성하지 않습니다 (뭔가 잘못 배치했을 때 눈치챌 수 있도록).
- **`startPoint`를 비워두면**: 아무 일도 일어나지 않습니다(기존과 동일하게 동작) — 당장 연결하지 않아도 안전합니다.
- **인구수 한도**: 완공된 건물과 동일하게 시작하자마자 `maxpopulationamount`만큼 반영됩니다.

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).

적용 후 유니티 에디터에서 직접 해주셔야 하는 것:
- 씬에 빈 오브젝트(Empty GameObject)를 시작 위치에 배치.
- `Placement System` 컴포넌트의 `Start Point` 필드에 그 오브젝트를 드래그해서 연결.
- 씬에 이미 수동으로 배치해둔 메인기지가 있다면 제거(중복 방지 로직은 넣지 않았으므로 그대로 두면 2개가 생김).
