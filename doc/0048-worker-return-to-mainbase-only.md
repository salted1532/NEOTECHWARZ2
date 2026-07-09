# 0048 — 일꾼 자원 반납 대상을 "가까운 건물" → "가까운 메인기지"로 제한

## Q&A

**Q (사용자):** 현재 일꾼이 자원채취후 가까운 건물로 가져가는데 가까운 아무건물이 아니라 가까운 + 메인기지에만 자원재취후 리턴했으면 좋겠어.

**A:** 원인 파악 완료. 아래 변경안 제시, 실제 코드 수정 전 확인 요청.

## 현재 동작

`Assets\Scripts\Unit\UnitController.cs`의 `FindNearestDepositBuilding()` (라인 1097~1115)이
`rtsController.BuildingList` 전체(정유소/배럭/팩토리 등 모든 아군 건물 포함)를 대상으로
가장 가까운 건물을 찾아 그곳으로 자원을 반납한다.

```csharp
private Transform FindNearestDepositBuilding()
{
    BuildingController nearest = null;
    float nearestSqrDist = float.MaxValue;

    foreach (BuildingController building in rtsController.BuildingList)
    {
        if (building == null) continue;

        float sqrDist = (building.transform.position - transform.position).sqrMagnitude;
        if (sqrDist < nearestSqrDist)
        {
            nearestSqrDist = sqrDist;
            nearest = building;
        }
    }

    return nearest != null ? nearest.transform : null;
}
```

건물 종류는 Unity 태그로 구분되며(`RTSUnitController.cs` 389~409줄), 메인기지는 태그가
`"MainBase"`로 고정되어 있다.

## 제안 변경

`FindNearestDepositBuilding()`의 순회 조건에 `building.CompareTag("MainBase")` 필터를 추가해서
메인기지만 후보로 삼는다.

```csharp
private Transform FindNearestDepositBuilding()
{
    BuildingController nearest = null;
    float nearestSqrDist = float.MaxValue;

    foreach (BuildingController building in rtsController.BuildingList)
    {
        if (building == null) continue;
        if (!building.CompareTag("MainBase")) continue; // 메인기지에만 반납

        float sqrDist = (building.transform.position - transform.position).sqrMagnitude;
        if (sqrDist < nearestSqrDist)
        {
            nearestSqrDist = sqrDist;
            nearest = building;
        }
    }

    return nearest != null ? nearest.transform : null;
}
```

### 영향 범위
- `FindNearestDepositBuilding()`을 호출하는 세 지점(837~849줄 재채취 명령 처리, 907~917줄 수동
  복귀 명령, 1028~1036줄 자원 채취 완료 후 자동 복귀) 모두 동일하게 적용됨 — 이 함수만 고치면
  전부 반영된다.
- 메인기지가 하나도 없으면(파괴됨 등) `FindNearestDepositBuilding()`이 `null`을 반환하고,
  호출부는 이미 null 체크 후 대기 상태로 전환하는 로직이 있어 별도 처리 불필요.
- 정유소(가스 채취 시 반납 대상)도 메인기지가 아니면 후보에서 제외된다. 가스 자원도 메인기지로
  반납하는 것이 의도한 동작인지 확인 필요 (일반적인 RTS는 가스 정제소 자체에 반납하는 경우가
  많음).

## 확인 필요 사항
1. 가스(Gas)도 메인기지로만 반납해도 되는지, 아니면 가스는 기존처럼 가까운 건물(정유소)에
   반납해야 하는지?
2. 위 변경안대로 진행해도 되는지?

## 사용자 답변

광물+가스 모두 메인기지로만 반납. (별도 분기 로직 불필요)

## 적용된 변경 (Before → After)

`Assets\Scripts\Unit\UnitController.cs` — `FindNearestDepositBuilding()`

```diff
         foreach (BuildingController building in rtsController.BuildingList)
         {
             if (building == null) continue;
+            if (!building.CompareTag("MainBase")) continue; // 메인기지에만 반납

             float sqrDist = (building.transform.position - transform.position).sqrMagnitude;
```

제안안 그대로 적용됨. 호출부 3곳(837, 907, 1028줄) 모두 이 함수를 통해 자동으로 메인기지만
대상으로 삼게 됨.
