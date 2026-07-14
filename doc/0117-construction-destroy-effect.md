# 0117 - Construction(BaseStructure) 파괴 이펙트 추가

## 1. 요청
"construction도 destory이펙트 추가해줘" — [[0116-building-destroy-effect]]에서 완공된 `BuildingController`에만
파괴 이펙트를 넣고 `BaseStructure`(건설중 파운데이션)는 "취소와 헷갈릴 수 있다"며 범위에서 뺐던 것을,
`BaseStructure`도 포함해달라는 후속 요청.

## 2. "취소" vs "전투로 파괴" 구분
0116에서 제기했던 우려(취소 버튼과 파괴가 같은 `CancelConstruction()` 경로를 공유해서, 잘못 걸면 취소했을
뿐인데 폭발 이펙트가 나감)는 `HealthManager.OnDeath`를 트리거로 쓰면 자연히 해결된다:

- `BaseStructure.Die()`(→ `CancelConstruction()` 호출)는 **오직 `HealthManager.Die()`를 통해서만** 호출된다
  (프로젝트 전체에서 `.Die()` 호출부는 `HealthManager.cs` 한 곳뿐 — 재확인함).
- 플레이어가 "건설 취소" 버튼을 눌러 `CancelConstruction()`을 **직접** 호출하는 경로는 `HealthManager`를
  거치지 않는다 — `GetDamage()`도 안 부르고 `OnDeath`도 안 나간다.
- 즉 `HealthManager.OnDeath`는 "체력이 0이 돼서 죽었다"는 신호로만 나가고, 취소 버튼 경로에서는 절대
  발생하지 않는다. `UnitEffects`/`BuildingEffects`와 동일한 이벤트를 그대로 구독하면 된다.

## 3. 변경 내용

### `Assets/Scripts/Effects/ConstructionEffects.cs`
```csharp
[Header("파괴 (비워두면 건물 중심 1곳) - 전투로 파괴됐을 때만, 건설 취소 버튼으로는 재생 안 됨")]
[SerializeField] private GameObject destroyPrefab;
[SerializeField] private List<Transform> destroyPoints = new();

private void OnEnable()
{
    if (healthManager != null)
    {
        healthManager.OnDamaged += HandleDamaged;
        healthManager.OnDeath += HandleDestroyed; // 추가
    }
}

private void OnDisable()
{
    if (healthManager != null)
    {
        healthManager.OnDamaged -= HandleDamaged;
        healthManager.OnDeath -= HandleDestroyed; // 추가
    }
}

// attachToPoint: false - 곧바로 Destroy(gameObject)될 예정이라 부모로 붙이면 이펙트도 같이 사라진다.
private void HandleDestroyed()
{
    EffectPlayer.SpawnAtPoints(destroyPrefab, destroyPoints, transform, attachToPoint: false);
}
```

`StartLoop()`/`StopLoopAndPlayComplete()`(건설 중 지속 이펙트)는 건드리지 않았다 — `CancelConstruction()`
(취소든 파괴든)이 결국 `Destroy(gameObject)`를 호출하면 `activeLoops`에 들어있는 인스턴스들은 전부
`BaseStructure` 계층의 자식으로 붙어있어서 Unity가 알아서 같이 정리한다(0105 3.8절에서 이미 확인한 동작).
