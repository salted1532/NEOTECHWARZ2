# 0586. 립팽(Ripfang)이 건물을 공격 못하는 이유 - 원인 및 수정 제안

- 날짜: 2026-08-16

## 요청 내용

- "립팽의 경우 건물을 공격을 못하는데 왜그럴까 확인좀"

## 조사 내용

전투 사거리 판정(`EnemyAttackRange.Update()`)은 유닛/건물 종류에 상관없이 항상 **중심점 대 중심점**
거리로 계산한다:

```csharp
float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;
if (sqrDistance <= UnitRange * UnitRange)
    enemyUnit.Attack(target.transform.position, target);
```

플레이어 유닛은 콜라이더가 몸체와 거의 붙어있어 중심 간 거리 ≈ 실제 붙어있는 거리라 문제가 없지만,
건물(예: MainBase)은 덩치가 커서 콜라이더 절반 크기만 3유닛 이상이다(`MainBase.prefab`의
BoxCollider `m_Size {1, 0.3, 1}` × 부모 `m_LocalScale {6, 4, 6}` ≈ 가로/세로 6유닛, 중심에서
벽까지 절반인 약 3유닛). 즉 유닛이 건물 벽에 완전히 붙어 서 있어도 "중심 대 중심" 거리는 최소
3유닛은 된다.

`Ripfang.prefab`의 `EnemyAttackRange.UnitRange` 값을 확인하니 **2**였다 - 건물 벽에 완전히 붙어도
닿을 수 있는 최소 거리(약 3)보다 작다. 그래서 아무리 가까이 다가가도 `sqrDistance <= UnitRange^2`
조건을 절대 만족하지 못해 `Attack()`이 호출되지 않는다 - 반면 플레이어 유닛처럼 작은 대상은 중심
거리가 2 이내로 들어올 수 있어 정상적으로 공격된다. "유닛은 때리는데 건물만 못 때린다"는 증상과
정확히 일치.

다른 유닛들의 `UnitRange`와 비교해보면 립팽만 확연한 이상치다:

| 유닛 | UnitRange |
|---|---|
| Ripfang (Spore Brood) | **2** |
| Spitter (Spore Brood) | 13 |
| Skitterwing (Spore Brood) | 11 |
| Cyborg Soldier (OC) | 12 |
| Striker (OC) | 14 |
| Brute Mech (OC, 근접 컨셉) | 6 |

(사용자 확인: 근접 컨셉은 Striker가 아니라 Brute Mech - 실제 값은 6으로 확인됨, 사용자가 기억한
4와는 다름. 사용자는 이 사실을 확인한 뒤에도 립팽은 4로 지정해달라고 명시적으로 요청함.)

## 수정 제안

`Ripfang.prefab`의 `EnemyAttackRange.UnitRange`를 2 → **4**로 변경(사용자 지정값). 건물 중심까지의
최소 거리(약 3)보다는 커서 건물 공격 자체는 가능해지나, Brute Mech(6)보다 더 짧은 근접 사거리로
설정하는 것 - 밸런스 의도로 확인됨.

## 변경 예정 파일

- Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab (`EnemyAttackRange.UnitRange`: 2 → 4)

## 결과
- 사용자 확인 후 위 값(4)으로 적용함.
