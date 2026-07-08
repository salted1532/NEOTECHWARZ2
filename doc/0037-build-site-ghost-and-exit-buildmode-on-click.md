# 0037. 건설 위치 클릭 시 건설모드 종료 + 정적 고스트 표시

**날짜:** 2026-07-09

## 요청 내용
> 잘 작동하고 이제 더 추가로 건설모드에서 건설할 건물을 정하고 건설할 위치에 클릭하면 건설모드가 끝나도록 해주고 그 자리에 건물 프리뷰가 남아있도록 해줘. 그리고 일꾼이 도착해서 건물을 지으면 그 프리뷰는 사라지도록

[[0036-worker-walks-to-build-site]]에서 구현한 "클릭 → 일꾼이 이동 후 완공" 흐름에 이어지는 추가 요청 2가지:
1. 건설 위치를 클릭하는 순간 건설모드가 종료된다 (지금은 계속 건설모드에 머물러 있어서 같은 건물을 연달아 여러 개 배치할 수 있음).
2. 클릭한 자리에 건물 프리뷰(고스트)가 그대로 남아있다가, 일꾼이 도착해서 실제로 건물이 완공되는 순간 사라진다.

## 조사 결과 (현재 코드 상태)
- `PlacementSystem.PlaceStructure()`는 클릭 후에도 `StopPlacement()`를 호출하지 않아 건설모드가 계속 유지됨(같은 건물을 계속 클릭해서 여러 개 배치 가능) — 이번 요청은 이 부분을 바꿔서 **클릭 한 번 = 건설모드 종료**로 변경.
- 건설모드 진입/종료는 `RTSUnitController.BuildModeOn()`/`ReturnState()` + `PlacementSystem.StartPlacement()`/`StopPlacement()` 조합으로 이뤄짐. 기존 "취소" 버튼이 정확히 이 조합(`PlacementSystem.StopPlacement(); ReturnState();`)을 쓰고 있으므로(`RTSUnitController.cs:800-807`), 이번에도 배치 성공 시 동일한 조합을 그대로 호출하면 됨.
- `PreviewSystem`은 현재 **마우스를 따라다니는 프리뷰 오브젝트 하나(`previewObject`)** 만 관리하고, 배치가 끝나면(`StopShowingPreview()`) 그 오브젝트를 파괴함. "클릭한 자리에 고정으로 남는 고스트"는 이 오브젝트와는 별개의 새 오브젝트가 필요함 — 안 그러면 건설모드 종료 시 `StopShowingPreview()`가 같이 파괴해버림.
- `PreviewSystem.PreparePreview()`가 프리뷰용 반투명 머티리얼 적용 + 콜라이더/리지드바디/NavMeshObstacle 비활성화를 담당하는데, 이때 쓰는 `previewMaterialInstance`는 마우스 프리뷰의 유효성(흰색/빨간색) 피드백에 따라 매 프레임 색이 바뀌는 **공유 인스턴스**임. 새로 만들 고정 고스트가 이 인스턴스를 그대로 재사용하면, 다음 건설모드에서 마우스를 움직일 때 이미 배치된(그리고 아직 일꾼이 도착 전인) 고스트의 색까지 같이 빨갛게/하얗게 바뀌는 부작용이 생김 → **고스트 전용 머티리얼 인스턴스를 새로 만들어 고정된 흰색으로 칠하고, 마우스 프리뷰와는 완전히 분리**해야 함.
- 고스트/실제 건물 모두 `data.Prefab`(건물 원본 프리팹)을 그대로 `Instantiate`해서 쓰는 기존 방식(마우스 프리뷰도 동일)을 그대로 따름 — `BuildingController` 등 실제 게임플레이 스크립트가 붙어있어도 콜라이더가 꺼져 있어 선택/클릭이 안 되고, 파괴되면 `RTSUnitController.Update()`의 `BuildingList.RemoveAll(b => b == null)`으로 자동 정리되므로 기존 마우스 프리뷰와 동일한 수준의 부작용만 있고 새로운 문제는 아님(기존 코드도 이미 이렇게 동작 중).

## 설계안

### 1. `PreviewSystem.cs` — 마우스 프리뷰와 분리된 "고정 건설 고스트" 추가

기존 `PreparePreview()`의 머티리얼 적용/게임플레이 컴포넌트 비활성화 로직을 재사용 가능하도록 두 개로 쪼갬:

```csharp
// 기존 코드
    // 생성된 프리뷰 오브젝트를 "허상"처럼 만든다: 모든 머티리얼을 반투명 프리뷰 머티리얼로 교체하고,
    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void PreparePreview(GameObject previewObject)
    {
        Renderer[] renderers = previewObject.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = previewMaterialInstance;
            }

            renderer.materials = materials;
        }

        // Collider OFF
        Collider[] colliders = previewObject.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        // Rigidbody OFF
        Rigidbody[] rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // ⭐ NavMeshObstacle OFF (핵심 추가)
        UnityEngine.AI.NavMeshObstacle[] obstacles =
            previewObject.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();

        foreach (var obs in obstacles)
        {
            obs.enabled = false;
        }
    }
```
```csharp
// 변경 코드
    // 생성된 프리뷰 오브젝트를 "허상"처럼 만든다: 지정한 머티리얼로 전부 교체하고,
    // 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화한다.
    private void PreparePreview(GameObject previewObject)
    {
        ApplyGhostMaterial(previewObject, previewMaterialInstance);
        DisableGameplayComponents(previewObject);
    }

    // 오브젝트의 모든 렌더러 머티리얼을 지정한 머티리얼 인스턴스로 교체한다.
    private void ApplyGhostMaterial(GameObject obj, Material material)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.materials;

            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }

            renderer.materials = materials;
        }
    }

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
    }

    // 배치가 확정된 위치에 "일꾼이 도착할 때까지 남아있는" 정적 건설 고스트를 생성한다.
    // 마우스를 따라다니는 previewObject와는 완전히 별개의 오브젝트/머티리얼 인스턴스를 사용하므로
    // 이후 다른 건물을 미리보기해도 서로 색이 간섭하지 않는다. 항상 고정된 흰색(배치 가능 색)으로 표시.
    public GameObject SpawnConstructionGhost(GameObject prefab, Vector3 position)
    {
        GameObject ghost = Instantiate(prefab, position, Quaternion.identity);

        Material ghostMaterial = new Material(previewMaterialPrefab);
        Color c = Color.white;
        c.a = 0.5f;
        ghostMaterial.color = c;

        ApplyGhostMaterial(ghost, ghostMaterial);
        DisableGameplayComponents(ghost);

        return ghost;
    }
```

### 2. `PlacementSystem.cs` — 배치 확정 시 고스트 생성 + 건설모드 즉시 종료

```csharp
// 기존 코드
        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        Vector3 spawnPos = GetPlacementWorldPosition(gridPos, data.Size);

        // 그리드는 클릭 즉시 예약(다른 곳에 겹쳐 짓지 못하게) - 실제 오브젝트는 일꾼 도착 후 생성
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;

        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        worker.GoBuild(
            spawnPos,
            onArrived: () => CompleteConstruction(data, spawnPos, placedIndex),
            onCancelled: () => CancelReservedConstruction(gridPos));

        preview.UpdatePosition(spawnPos, false);
    }

    // 일꾼이 건설 위치에 도착했을 때(GoBuild 콜백) 실제 건물을 생성한다.
    private void CompleteConstruction(BuildingData data, Vector3 spawnPos, int placedIndex)
    {
        GameObject obj = Instantiate(data.Prefab);

        var obstacle = obj.GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.enabled = true;
        }
        obj.transform.position = spawnPos;

        placedGameObject[placedIndex] = obj;
    }

    // 일꾼이 도착하기 전에 다른 명령으로 건설 이동이 취소됐을 때(GoBuild 콜백) 예약해둔 그리드 셀을 비워준다.
    private void CancelReservedConstruction(Vector3Int gridPos)
    {
        StructureData.RemoveObjectAt(gridPos);
    }
```
```csharp
// 변경 코드
        UnitController worker = rtsController != null ? rtsController.GetSelectedWorker() : null;
        if (worker == null)
            return; // 건설을 맡을 일꾼이 없으면 배치하지 않음

        Vector3 spawnPos = GetPlacementWorldPosition(gridPos, data.Size);

        // 그리드는 클릭 즉시 예약(다른 곳에 겹쳐 짓지 못하게) - 실제 오브젝트는 일꾼 도착 후 생성
        placedGameObject.Add(null);
        int placedIndex = placedGameObject.Count - 1;

        StructureData.AddObjectAt(gridPos, data.Size, data.ID, placedIndex);

        // 클릭한 자리에 일꾼이 도착할 때까지 남아있을 고정 고스트를 생성
        GameObject ghost = preview.SpawnConstructionGhost(data.Prefab, spawnPos);

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

    // 일꾼이 도착하기 전에 다른 명령으로 건설 이동이 취소됐을 때(GoBuild 콜백) 고스트를 지우고 예약해둔 그리드 셀을 비워준다.
    private void CancelReservedConstruction(Vector3Int gridPos, GameObject ghost)
    {
        if (ghost != null)
            Destroy(ghost);

        StructureData.RemoveObjectAt(gridPos);
    }
```

(`preview.UpdatePosition(spawnPos, false)` 호출은 삭제 — 어차피 `StopPlacement()`가 마우스 프리뷰/셀 커서를 곧바로 숨기므로 더 이상 의미가 없음.)

## 이번 설계에서 결정한 세부 동작 (이견 있으면 알려주세요)
- **건설모드 종료 방식**: 기존 "취소" 버튼과 동일하게 `PlacementSystem.StopPlacement()` + `RTSUnitController.ReturnState()`를 호출 — 건물 목록 패널이 사라지고 원래 일꾼의 Move/Attack/.../Build 패널로 돌아감.
- **고스트 색상**: 배치 유효성 검사를 이미 통과한 자리이므로 항상 고정된 흰색(반투명)으로 표시. 마우스 프리뷰처럼 색이 바뀌지 않음(더 이상 유효성 판정 대상이 아니기 때문).
- **고스트가 남는 동안 다른 건물을 새로 짓기 시작해도**: 마우스를 따라다니는 새 프리뷰와는 완전히 다른 머티리얼 인스턴스를 쓰므로 서로 색이 간섭하지 않음.
- **일꾼의 건설 이동이 도착 전에 취소된 경우**: [[0036-worker-walks-to-build-site]]에서 이미 그리드 예약을 해제하도록 만들어뒀는데, 이번에 고스트도 함께 남아있게 되므로 그 시점에 고스트도 같이 파괴하도록 함(요청엔 없었지만, 안 지우면 취소돼도 고스트가 영원히 남는 명백한 버그라 같이 처리).

## 변경 예정 파일
- `Assets/Scripts/BuildSystem/PreviewSystem.cs`
- `Assets/Scripts/BuildSystem/PlacementSystem.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 실제 코드에 반영함 (설계와 구현 간 차이 없음).
