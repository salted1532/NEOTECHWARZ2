# 0268 - 버그: 건설 도착 시 장애물 검사에 담당 일꾼 본인이 걸림

**날짜:** 2026-07-28

## 요청 내용

> 일꾼이 건물을 지으러 갔을때 장애물이 있는것에 본인은 포함되면 안돼는데 이것좀 수정해줘

doc/0266에서 추가한 "도착 시 장애물 재검사"가 담당 일꾼 자신의 콜라이더까지 장애물로 잡아버리는
버그. 일꾼은 건설 위치에 도착해서 그 자리에 서 있는 상태이므로, `blockingLayers`에 유닛 레이어가
포함돼 있으면 `Physics.OverlapBox` 검사에 자기 자신이 항상 걸려서 매번 건설이 실패 처리됐다.

## 코드 변경

### `Assets/Scripts/BuildSystem/PlacementSystem.cs`

`IsBlocked`에 "이 오브젝트(및 자식)는 장애물로 치지 않음" 옵션(`ignoreObject`, 기본값 `null` - 기존
호출부는 그대로 동작)을 추가하고, 검사 대상에서 제외하도록 수정.

Before:
```csharp
    private bool IsBlocked(Vector3 worldPos, Vector2Int size)
    {
        ...
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, blockingLayers);

        foreach (Collider hit in hits)
        {
            Debug.Log($"Blocked by : {hit.name} | Layer : {LayerMask.LayerToName(hit.gameObject.layer)}");
        }

        return hits.Length > 0;
    }
```

After:
```csharp
    private bool IsBlocked(Vector3 worldPos, Vector2Int size, GameObject ignoreObject = null)
    {
        ...
        Collider[] hits = Physics.OverlapBox(center, halfExtents, Quaternion.identity, blockingLayers);

        bool blocked = false;

        foreach (Collider hit in hits)
        {
            if (ignoreObject != null && hit.transform.IsChildOf(ignoreObject.transform))
                continue; // 담당 일꾼 자신은 장애물로 치지 않음

            Debug.Log($"Blocked by : {hit.name} | Layer : {LayerMask.LayerToName(hit.gameObject.layer)}");
            blocked = true;
        }

        return blocked;
    }
```

`StartConstruction()`의 도착 시점 호출만 담당 일꾼을 넘겨서 제외시킨다(클릭 시점 검사/리프트 착륙
위치 검사 등 기존 호출부는 그대로 `ignoreObject` 없이 동작).

Before:
```csharp
        if (IsBlocked(groundPos, data.Size))
```

After:
```csharp
        if (IsBlocked(groundPos, data.Size, worker.gameObject))
```

## 요약/영향받는 파일

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`: `IsBlocked`에 `ignoreObject` 파라미터 추가,
  `StartConstruction()`의 도착 시점 검사에 담당 일꾼(`worker.gameObject`)을 넘겨 자기 자신은 장애물
  판정에서 제외.
- 다른 유닛/건물/지형지물이 그 자리를 막고 있는 진짜 장애물 상황은 doc/0266대로 그대로 감지된다 -
  이번 수정은 "일꾼 본인만 오탐 제외"하는 것.
