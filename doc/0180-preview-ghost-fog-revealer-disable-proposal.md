# 0180 - 건물 배치/이전(착륙) 프리뷰가 시야를 가지는 문제 - 수정 제안

## 요청

"지금 forofwar랑 건물 건설이나 착륙시 생기는 프리뷰가 시야를 가지는 문제가 있어. 건물 프리뷰가
시야를 가지지 못하도록 forrevealerAgent를 꺼지도록 해줘" — 건물을 지을 때 마우스를 따라다니는
반투명 프리뷰(고스트)와, 건물 리프트(착륙) 재배치 시 나오는 프리뷰가 실제 건물처럼 안개를 걷어내는
문제. `FogRevealerAgent`를 꺼서 프리뷰가 시야를 제공하지 않도록 해달라는 요청.

## 조사 내용

- `Assets/Scripts/FogOfWar/FogRevealerAgent.cs` — 유닛/건물 프리팹에 직접 붙어 있는 컴포넌트.
  `Start()`에서 `csFogWar.AddFogRevealer(...)`로 자신을 시야 소스로 등록한다. 비활성화된 컴포넌트는
  Unity가 `Start()`를 호출하지 않으므로, `enabled = false`를 `Start()` 실행 전에 걸면 애초에 등록
  자체가 안 된다.
- `FogRevealerAgent`는 건물 프리팹(`SupplyDepot`, `Tier1/2/3`, `BaseStructure`, `Lab`, `MainBase`)에
  직접 붙어 있다 — 별도 프리뷰 전용 프리팹이 없고, 배치 시스템이 실제 건물 프리팹을 그대로
  `Instantiate`해서 프리뷰로 쓴다.
- `Assets/Scripts/BuildSystem/PreviewSystem.cs`:
  - `StartShowingPlacementPreview()` (마우스를 따라다니는 건설 프리뷰) → `PreparePreview()` →
    `DisableGameplayComponents()` 호출.
  - `SpawnConstructionGhost()` (배치 확정 후 일꾼이 도착할 때까지 남는 정적 건설 고스트) → 역시
    `DisableGameplayComponents()` 호출.
  - `DisableGameplayComponents()`는 이미 프리뷰/고스트에서 실제 게임플레이에 영향을 주면 안 되는
    컴포넌트들(Collider, Rigidbody, NavMeshObstacle)을 비활성화하고, `HealthManager`의 체력바도
    숨기고 있다 — 같은 패턴으로 `FogRevealerAgent`도 여기서 꺼야 한다.
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`의 건물 리프트(착륙) 재배치 로직
  (`LiftOff` 이후 재배치 시점, 224~254번 줄 부근)도 동일한 `preview.StartShowingPlacementPreview(...)`를
  호출한다 — 즉 "착륙 시 생기는 프리뷰"도 건설 프리뷰와 동일한 `PreviewSystem.PreparePreview()` 경로를
  타므로, `DisableGameplayComponents()` 한 곳만 고치면 건설 프리뷰와 착륙 프리뷰 양쪽 다 해결된다.
- 실제로 배치가 완료되어 진짜 건물이 되는 시점(리프트 착지 완료, 건설 완료)에는 이 프리뷰
  오브젝트가 아니라 별개의 정식 건물 오브젝트/컴포넌트가 활성 상태로 동작하므로, 프리뷰에서
  `FogRevealerAgent`를 꺼도 실제 건물의 시야 제공에는 영향이 없다.

## 계획된 코드 변경

### `Assets/Scripts/BuildSystem/PreviewSystem.cs`

Before:
```csharp
    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void DisableGameplayComponents(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        UnityEngine.AI.NavMeshObstacle[] obstacles =
            obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();

        foreach (var obs in obstacles)
        {
            obs.enabled = false;
        }

        // 프리뷰/고스트는 실제 체력이 없으므로(항상 풀피로 초기화됨) 체력바를 표시하면 오해를 일으킨다 - 숨긴다.
        HealthManager[] healthManagers = obj.GetComponentsInChildren<HealthManager>();
        foreach (HealthManager hm in healthManagers)
        {
            hm.SetHealthBarVisible(false);
        }
    }
```

After:
```csharp
    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void DisableGameplayComponents(GameObject obj)
    {
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies = obj.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        UnityEngine.AI.NavMeshObstacle[] obstacles =
            obj.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();

        foreach (var obs in obstacles)
        {
            obs.enabled = false;
        }

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

## 요약/영향받는 파일

- 수정 파일: `Assets/Scripts/BuildSystem/PreviewSystem.cs` (한 곳, `DisableGameplayComponents` 메서드에
  블록 추가).
- `PreparePreview()`(건설 중 마우스 추적 프리뷰)와 `SpawnConstructionGhost()`(배치 확정 후 대기 고스트)
  둘 다 `DisableGameplayComponents()`를 거치므로 동일한 수정으로 함께 해결됨.
- `PlacementSystem.cs`의 건물 리프트(착륙) 재배치도 같은 `PreviewSystem.StartShowingPlacementPreview()`
  경로를 타므로 별도 수정 없이 함께 해결됨.
- 실제 완성된 건물(정식 배치 완료/착륙 완료 후)에는 영향 없음 — 그 시점엔 프리뷰 오브젝트가 아닌
  별개의 정식 건물 오브젝트가 사용됨.

## 확인 필요

이대로 진행해도 될까요?
