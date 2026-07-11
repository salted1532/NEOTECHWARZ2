# 0062. 버그수정: 배치 프리뷰/고스트에 체력바가 보임

**날짜:** 2026-07-12

## 요청 내용
> 건물 건설 프리뷰나 리프트 착륙 위치 선택 시 보이는 프리뷰/고스트에도 체력바가 나타난다. 프리뷰로 생성된 것들은 체력바도 안 보이도록 해달라.

## 원인 분석
건설 배치 프리뷰(마우스를 따라다니는 반투명 고스트)와 "일꾼 도착 전까지 남아있는 고정 고스트"(건설 확정 시), 그리고 [[0057-lift-freeflight-land-lock-shortcut|건물 리프트 착륙 위치 선택]] 시의 고스트까지, 전부 `PreviewSystem`이 실제 건물 프리팹을 그대로 `Instantiate`해서 만든다(`StartShowingPlacementPreview` / `SpawnConstructionGhost`). 두 경로 모두 `PreparePreview()`/직접 호출을 통해 `DisableGameplayComponents(GameObject obj)`를 호출해서 콜라이더/리지드바디/`NavMeshObstacle`을 꺼주고 있었지만, [[0060-healthbar-slider-and-billboard|0060]]에서 추가한 체력바(`HealthManager.healthSlider`)는 이 목록에 없었다.

프리팹을 그대로 `Instantiate`하기 때문에 프리뷰/고스트 오브젝트에도 실제 `HealthManager`가 그대로 붙어서 `Awake()`가 정상 실행되고(`currentHp = maxHealth`), `OnHealthChanged` 구독을 통해 체력바 슬라이더 값이 "가득 찬 상태"로 갱신되어 그대로 화면에 표시된다 — 그래서 프리뷰/고스트에도 (항상 풀피 상태인) 체력바가 보이는 것이다.

## 수정 내용

### 1. `Assets/Scripts/Unit/HealthManager.cs` — 체력바를 켜고 끌 수 있는 공개 메서드 추가
```csharp
// 추가 (UpdateHealthSlider 아래)
    // 체력바 UI 자체를 켜고 끈다 (건설 프리뷰/고스트처럼 체력 표시가 필요 없는 경우 PreviewSystem이 호출).
    public void SetHealthBarVisible(bool visible)
    {
        if (healthSlider != null)
            healthSlider.gameObject.SetActive(visible);
    }
```

### 2. `Assets/Scripts/BuildSystem/PreviewSystem.cs` — 프리뷰/고스트 생성 시 체력바 숨김
```csharp
// 기존 코드
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
```
```csharp
// 변경 코드
    // 콜라이더/리지드바디/NavMeshObstacle/체력바 등 실제 게임플레이에 영향을 주거나 오해를 일으키는 요소를 전부 비활성화한다.
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

## 이번 수정에서 결정한 세부 동작
- **적용 범위**: `DisableGameplayComponents()`를 거치는 모든 경로(마우스를 따라다니는 배치 프리뷰, 건설 확정 후 고정 고스트, 건물 리프트 착륙 위치 선택 시의 고스트)에 전부 동일하게 적용됩니다 - 하나의 공통 메서드만 고치면 되므로 별도 분기가 필요 없었습니다.
- **`HealthManager` 자체는 그대로 둠**: 컴포넌트를 비활성화(`enabled = false`)하지 않고 체력바 UI만 숨깁니다 - 프리뷰 오브젝트의 `HealthManager` 로직 자체는 어차피 실제 전투에 쓰이지 않아 무해하고, 굳이 비활성화할 필요가 없습니다.

## 변경 예정 파일
- `Assets/Scripts/Unit/HealthManager.cs`
- `Assets/Scripts/BuildSystem/PreviewSystem.cs`

## 상태
**적용 완료.**
