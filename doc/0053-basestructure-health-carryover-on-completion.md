# 0053. 건설 중 피해 입은 체력을 완공된 건물에 그대로 반영

**날짜:** 2026-07-10

## 요청 내용
> 현재 basestructrue이 피해를 입거나 아군유닛에게 강제공격을 당해서 피해를 입은 상태로 건설이 완료되면 완료된건물의 체력에 반영되도록 basestructrue의 최종 체력이 완성된 건물의 체력으로 반영되도록 수정해줘

## 현재 동작 확인
`BaseStructure`는 건설 중에도 이미 `HealthManager`를 갖고 있고, `Initialize()`에서 이 `HealthManager`의 **최대 체력을 완공될 건물의 최대체력(`finalMaxHealth`)과 동일하게** 맞춰둔다:
```csharp
healthPerSecond = buildTime > 0f ? finalMaxHealth / buildTime : finalMaxHealth;

if (healthManager != null)
{
    healthManager.SetMaxHealth(finalMaxHealth);
    healthManager.SetHealth(0); // 건설 시작 시점엔 0에서부터 진행률만큼 차오르게 함
}
```
즉 건설 중 체력은 "완공될 건물과 같은 척도(0~finalMaxHealth)"로 관리되고 있고, `Update()`에서 매 초 `healthPerSecond`만큼 차오르며, 전투(공격/강제공격)로 `GetDamage()`를 맞으면 그만큼 줄어든다.

문제는 `CompleteConstruction()`에서 완공된 건물을 생성할 때, 이 누적된 체력값을 전혀 넘겨주지 않는다는 점:
```csharp
private void CompleteConstruction()
{
    ...
    if (data != null && data.Prefab != null)
    {
        Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

        GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);
        ...
    }
    ...
}
```
`Instantiate(data.Prefab, ...)`로 생성된 새 오브젝트의 `HealthManager`는 자기 `Awake()`에서 `currentHp = maxHealth`로 초기화되므로, `BaseStructure`가 아무리 두들겨 맞았어도 완공된 건물은 **항상 풀피로 스폰**된다. 이 부분을 고쳐서, `BaseStructure`가 갖고 있던 최종 체력값을 완공된 건물의 `HealthManager`에 그대로 넘겨준다.

## 설계안

**`BaseStructure.cs`** — `CompleteConstruction()`에서 완공된 건물의 `HealthManager`를 찾아 현재 체력을 이어받게 함:
```csharp
// 기존 코드
        if (data != null && data.Prefab != null)
        {
            Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

            GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;

            rtsController?.AddMaxPopulation(data.maxpopulationamount); // 건설 완료 시점에만 인구수 한도 반영 (건설 중엔 미반영)
        }
```
```csharp
// 변경 코드
        if (data != null && data.Prefab != null)
        {
            Vector3 spawnPos = groundPosition + Vector3.up * PlacementSystem.GetGroundOffsetY(data.Prefab);

            GameObject obj = Instantiate(data.Prefab, spawnPos, transform.rotation);

            // ⭐ 건설 중 입은 피해가 완공된 건물에도 그대로 이어지도록, BaseStructure의 최종 체력을 넘겨준다.
            // (BaseStructure의 HealthManager는 Initialize()에서 이미 완공될 건물과 같은 최대체력 척도로 맞춰져 있음)
            if (healthManager != null && obj.TryGetComponent<HealthManager>(out var finishedHealthManager))
                finishedHealthManager.SetHealth(healthManager.GetHealth());

            NavMeshObstacle obstacle = obj.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
                obstacle.enabled = true;

            rtsController?.AddMaxPopulation(data.maxpopulationamount); // 건설 완료 시점에만 인구수 한도 반영 (건설 중엔 미반영)
        }
```

## 참고
- 최대체력은 넘길 필요 없음 — `data.Prefab`의 `HealthManager`에 이미 완공된 건물 기준 `maxHealth`가 그대로 직렬화돼 있고(`Initialize()`가 애초에 이 값을 읽어서 `finalMaxHealth`를 계산했던 것과 같은 값), `BaseStructure` 쪽도 같은 값으로 맞춰뒀으므로 두 척도가 동일함. `SetHealth()`만 호출하면 정확히 이어짐.
- 건설 중 체력이 0까지 떨어지면 `HealthManager.GetDamage()`가 자체적으로 `Die()`(→ `BaseStructure.Die()` → `CancelConstruction()`)를 호출해 애초에 `CompleteConstruction()`까지 도달하지 못하므로, 여기서 넘겨줄 체력은 항상 1 이상이다.
- 극히 드문 경우(막판에 데미지를 입어 `healthPerSecond` 회복이 다 못 따라잡은 채로 `remainingBuildTime`이 0이 되는 경우), 완공된 건물이 최대체력보다 살짝 낮은 상태로 스폰될 수 있음 — 이번 요청의 취지("피해 입은 상태가 반영되어야 함")와 정확히 일치하는 의도된 동작.

## 변경 예정 파일
- `Assets/Scripts/Building/BaseStructure.cs`

## 상태
**적용 완료** — 설계안 그대로 `BaseStructure.cs`에 반영함.
