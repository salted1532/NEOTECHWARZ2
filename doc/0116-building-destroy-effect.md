# 0116 - 건물 파괴 이펙트 추가

## 1. 요청
"추가로 건물들도 파괴시 이펙트 부분도 추가해줘"

## 2. 설계
`UnitEffects.HandleDeath()`와 완전히 동일한 패턴. `BuildingController.Die()`(전투로 체력이 0이 됐을 때만
`HealthManager.Die()` → `IDestructible.Die()` 경로로 호출됨 — 프로젝트 전체에서 `Die()` 호출부는
`HealthManager.cs` 한 곳뿐임을 재확인)가 `Destroy(gameObject)`하기 직전에 `HealthManager.OnDeath`가
발생하므로, `BuildingEffects`가 이걸 구독해서 파괴 이펙트를 재생한다.

`BaseStructure`(건설중 파운데이션)는 이번 범위에서 제외했다 — `BaseStructure.Die()`는 내부적으로
`CancelConstruction()`을 호출하는데, 이 메서드는 플레이어가 건설을 "취소" 버튼으로 직접 취소할 때도 똑같이
호출된다. 그 경로에 파괴 이펙트(폭발 등)를 걸면 플레이어가 그냥 건설을 취소했을 뿐인데 폭발 이펙트가 나가는
어색한 상황이 생긴다. 완공된 `BuildingController`만 "체력 0 → 파괴"가 명확히 전투로 인한 것이라 이번 요청
("건물들도 파괴시")의 범위로 적절하다.

## 3. 변경 내용

### `Assets/Scripts/Effects/BuildingEffects.cs`
```csharp
[Header("파괴 (비워두면 건물 자신의 위치에서 재생)")]
[SerializeField] private GameObject destroyPrefab;
[SerializeField] private List<Transform> destroyPoints = new(); // 큰 건물의 다중 폭발 지점(선택)

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

// attachToPoint: false - 이 건물은 곧바로 Destroy(gameObject)될 예정이라, 부모로 붙이면 이펙트가
// 재생을 채 끝내기도 전에 같이 파괴돼버린다(UnitEffects.HandleDeath와 동일한 이유, doc/0109 참고).
private void HandleDestroyed()
{
    EffectPlayer.SpawnAtPoints(destroyPrefab, destroyPoints, transform, attachToPoint: false);
}
```

`deathPoints`처럼 여러 지점(큰 건물의 다중 폭발 위치 등) 지정을 지원하도록 `List<Transform>`으로 뒀다
(doc/0105의 다중 위치 리스트 규칙과 동일).
