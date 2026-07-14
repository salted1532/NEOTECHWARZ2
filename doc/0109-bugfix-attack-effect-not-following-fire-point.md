# 0109 - 버그수정: 이동하며 공격 시 공격 이펙트가 허공에 남는 문제

## 1. 요청
"공격 이펙트의 경우 공격 한번 할때 나타나고 Fire Points를 따라 갔으면 좋겠어. 현재 움직이면서 공격할때 그
이펙트가 공중에 그대로 남아있는 버그가 발견됐어."

## 2. 원인
`EffectPlayer.SpawnAtPoints()`(`Assets/Scripts/Effects/EffectPlayer.cs`)가 각 지점(`Transform`)의 위치/회전으로
이펙트를 스폰하기는 했지만, 스폰된 인스턴스를 그 지점에 **부모로 붙이지 않고** 있었다(`Instantiate(..., parent: null)`).
`UnitEffects.PlayAttack()`이 이 메서드를 호출할 때도 parent를 넘기지 않았기 때문에, 총구 이펙트가 스폰된 그
월드 좌표에 고정된 채로 남았다 — 유닛이 공격 직후 계속 이동하면 이펙트만 원래 총 쏜 자리에 남겨지고 유닛은
멀어지는 것처럼 보였다.

## 3. 수정

`EffectPlayer.SpawnAtPoints()`에 `parent`(안 쓰이던 파라미터) 대신 `bool attachToPoint = true`를 추가해서,
기본적으로 스폰된 이펙트를 그 지점(지점이 없으면 fallback)에 부모로 붙이도록 바꿨다 — 이제 총구 이펙트는
발사된 그 firePoint(또는 firePoints가 비어있으면 유닛 자신)를 따라간다.

다만 스폰 직후 그 부모가 **곧바로 `Destroy(gameObject)`될 예정인 경우**(사망 이펙트, 건설 완료 이펙트)는
부모를 붙이면 안 된다 — Unity는 부모가 파괴되면 자식도 함께 파괴하므로, 이펙트가 재생을 채 끝내기도 전에
같이 사라져버린다. 이 두 곳은 명시적으로 `attachToPoint: false`를 넘기도록 했다(전에는 몰랐지만 이번에
같이 확인하다 찾은, 같은 원인의 잠재 버그).

### `Assets/Scripts/Effects/EffectPlayer.cs`
```csharp
// 전
public static void SpawnAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback, Transform parent = null)
{
    ...
    Spawn(effectPrefab, point.position, point.rotation, parent); // parent는 항상 null로 호출되고 있었음
}

// 후
public static void SpawnAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback, bool attachToPoint = true)
{
    ...
    Spawn(effectPrefab, point.position, point.rotation, attachToPoint ? point : null);
}
```

### 호출부
| 파일 | 이펙트 | attachToPoint | 이유 |
|---|---|---|---|
| `UnitEffects.PlayAttack()` | 공격(총구) | 기본값 true | 이번 요청의 본 수정 대상 — firePoint를 따라가야 함 |
| `BuildingEffects.PlayTakeoff/PlayLanding()` | 이착륙 | 기본값 true | 건물이 상승/이동 중이어도 자연스럽게 따라감 |
| `UnitEffects.HandleDeath()` | 사망 | **false로 명시** | 유닛이 이 직후 Destroy됨 — 붙이면 이펙트도 같이 사라짐 |
| `ConstructionEffects.StopLoopAndPlayComplete()` | 건설 완료 | **false로 명시** | BaseStructure가 이 직후 Destroy됨 — 붙이면 이펙트도 같이 사라짐 |
