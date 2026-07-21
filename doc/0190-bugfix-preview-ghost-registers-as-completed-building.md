# 0190 - 버그: 건설 프리뷰/고스트가 완공된 건물로 잘못 등록되어 테크 트리가 조기 해제됨

## 요청

"병영을 선택하면 프리뷰가 생성되는데 그때 공장 건설 버튼이 활성화 되버리네 건물이 지어지고 나서
공장이 열려야하는데 해당 내용 확인하고 해결해줘" — 병영(Barracks)을 아직 짓지도 않았는데, 건설
패널에서 병영 버튼을 눌러 배치 프리뷰(마우스를 따라다니는 반투명 고스트)만 띄운 시점에 벌써 공장
(Factory) 버튼이 잠금 해제되는 버그. [[0189-building-tech-tree-prerequisite]]에서 추가한 테크
트리 조건과 관련된 버그.

## 원인

- `Assets/Scripts/System/RTSUnitController.cs`의 `HasCompletedBuilding(int buildingID)`는
  `BuildingList.Exists(b => b.GetBuildingID() == buildingID)`로 판단한다. `BuildingList`에는
  `BuildingController.Start()`(84번 줄)가 `rtsController.BuildingList.Add(this)`로 자기 자신을
  등록하는데, 이 로직은 "이 컴포넌트가 붙은 오브젝트가 씬에서 활성화되어 Start가 호출됐다"는 것만
  보고 있어서, 그 오브젝트가 실제로 완공된 건물인지 단순 프리뷰용 겉모습인지 구분하지 못한다.
- `Assets/Scripts/BuildSystem/PreviewSystem.cs`의 `StartShowingPlacementPreview(prefab, size)`
  (50번 줄)가 `previewObject = Instantiate(prefab);`로 **완공된 건물의 진짜 프리팹**(`BuildingData.Prefab`,
  즉 `BuildingController`가 붙어 있는 그 프리팹)을 그대로 통째로 Instantiate한다. `PreparePreview()`가
  머티리얼 교체/레이어 변경/`DisableGameplayComponents()`(Collider, Rigidbody, NavMeshObstacle,
  HealthManager, FogRevealerAgent)로 겉모습과 게임플레이 영향만 제거할 뿐, `BuildingController` 자체는
  꺼주지 않는다. 그 결과 `Instantiate` 직후 프리뷰 오브젝트의 `BuildingController.Start()`가 정상적으로
  호출되어 `BuildingList.Add(this)`가 실행된다 - 마우스로 위치만 고르고 있는 "환영" 오브젝트가 실제
  완공된 병영처럼 리스트에 들어가 버린다.
- 같은 이유로 `SpawnConstructionGhost(prefab, position)`(148번 줄, 클릭으로 배치를 확정한 뒤 일꾼이
  도착할 때까지 자리에 남아있는 고정 고스트)도 똑같이 `DisableGameplayComponents()`를 거치므로 동일한
  문제를 갖고 있다 - 일꾼이 도착하기 전까지(걸어가는 시간 내내) 이 고스트도 `BuildingList`에 가짜로
  들어가 있게 된다.
- 즉 실제로는 "프리뷰 표시" 또는 "일꾼 도착 대기" 단계에서부터 이미 `HasCompletedBuilding(Barracks)`가
  `true`가 되어, `IsBuildingPrerequisiteMet(Factory)`도 곧바로 `true`로 잘못 판정된다.
- 반대로, 실제 완공 시점(`BaseStructure.CompleteConstruction()`이 `Instantiate(data.Prefab, ...)`을
  직접 호출하는 경로, 194번 줄)과 게임 시작 시 메인기지 스폰(`PlacementSystem.SpawnStartingMainBase()`)은
  `PreviewSystem`을 거치지 않는 순수 Instantiate라서 원래도 정상 동작한다 - 이번 버그는 프리뷰/고스트
  경로에서만 발생한다.

## 계획된 코드 변경

### `Assets/Scripts/BuildSystem/PreviewSystem.cs`

`DisableGameplayComponents()`가 `BuildingController`도 비활성화하도록 추가한다. `PreparePreview()`
(프리뷰)와 `SpawnConstructionGhost()`(건설 대기 고스트) 양쪽에서 공용으로 쓰는 메서드라 한 곳만
고치면 두 경로 모두 고쳐진다. (`enabled = false`로 끄면 `Start()` 자체가 호출되지 않아
`BuildingList.Add()`가 아예 실행되지 않는다 - 바로 아래 `FogRevealerAgent` 처리와 동일한 방식.)

Before:
```csharp
        // 프리뷰/고스트는 실제 체력이 없으므로(항상 풀피로 초기화됨) 체력바를 표시하면 오해를 일으킨다 - 숨긴다.
        HealthManager[] healthManagers = obj.GetComponentsInChildren<HealthManager>();
        foreach (HealthManager hm in healthManagers)
        {
            hm.SetHealthBarVisible(false);
        }

        // 프리뷰/고스트는 실제 건물이 아니므로 안개를 걷으면 안 된다. FogRevealerAgent는 Start()에서
        // csFogWar에 자신을 등록하는데, 비활성화된 컴포넌트는 Unity가 Start()를 호출하지 않으므로
        // 여기서 꺼두면 애초에 시야 소스로 등록되지 않는다.
        FogRevealerAgent[] fogRevealers = obj.GetComponentsInChildren<FogRevealerAgent>();
        foreach (FogRevealerAgent fra in fogRevealers)
        {
            fra.enabled = false;
        }
    }
```

After:
```csharp
        // 프리뷰/고스트는 실제 체력이 없으므로(항상 풀피로 초기화됨) 체력바를 표시하면 오해를 일으킨다 - 숨긴다.
        HealthManager[] healthManagers = obj.GetComponentsInChildren<HealthManager>();
        foreach (HealthManager hm in healthManagers)
        {
            hm.SetHealthBarVisible(false);
        }

        // 프리뷰/고스트는 실제 건물이 아니므로 안개를 걷으면 안 된다. FogRevealerAgent는 Start()에서
        // csFogWar에 자신을 등록하는데, 비활성화된 컴포넌트는 Unity가 Start()를 호출하지 않으므로
        // 여기서 꺼두면 애초에 시야 소스로 등록되지 않는다.
        FogRevealerAgent[] fogRevealers = obj.GetComponentsInChildren<FogRevealerAgent>();
        foreach (FogRevealerAgent fra in fogRevealers)
        {
            fra.enabled = false;
        }

        // 프리뷰/고스트는 실제로 지어진 건물이 아니므로 RTSUnitController.BuildingList(테크 트리 선행
        // 조건 판정용)에 등록되면 안 된다. BuildingController.Start()가 아예 호출되지 않도록 미리
        // 비활성화한다(FogRevealerAgent와 동일한 이유 - doc/0190).
        BuildingController[] buildingControllers = obj.GetComponentsInChildren<BuildingController>();
        foreach (BuildingController bc in buildingControllers)
        {
            bc.enabled = false;
        }
    }
```

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/BuildSystem/PreviewSystem.cs` (`DisableGameplayComponents`에
  `BuildingController` 비활성화 추가).
- 동작 변화:
  - 건설 패널에서 건물 버튼을 눌러 배치 프리뷰(마우스 추적 고스트)를 띄우는 것만으로는 더 이상
    `BuildingList`에 등록되지 않는다 - 테크 트리 선행 조건이 프리뷰만으로 조기 충족되지 않는다.
  - 배치를 확정한 뒤 일꾼이 도착할 때까지 남아있는 고정 고스트(`SpawnConstructionGhost`)도 동일하게
    등록되지 않는다.
  - 테크 트리 조건은 이제 오직 `BaseStructure.CompleteConstruction()`이 실제 완공 건물 프리팹을
    Instantiate하는 시점(건설시간이 다 차서 진짜 건물이 생성되는 순간)에만 충족된다 - 요청대로
    "건물이 실제로 지어지고 나서" 다음 티어가 열린다.
  - 프리뷰/고스트 오브젝트는 원래도 `BuildingController`의 기능(마커, 유닛 생산, 리프트 등)을 전혀
    쓰지 않으므로 비활성화에 따른 다른 부작용은 없다.

## 확인 필요

이대로 진행해도 될까요?
