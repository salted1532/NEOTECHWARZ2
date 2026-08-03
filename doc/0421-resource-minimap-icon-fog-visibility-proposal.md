# 0421 - 광물/가스 미니맵 아이콘도 안개에 가려지면 숨기기 (제안)

**날짜:** 2026-08-04

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 34개(기존 33개 + `ResourceNode.cs`의
  신규 `FindFirstObjectByType` 호출 1개 - 다른 파일들과 동일한 기존 컨벤션, 새로운 종류의
  경고는 아님).
- `execute-dynamic-code`로 `Ore.prefab`/`Gas.prefab`의 `minimapIcon` 필드가 각 프리팹의
  `MiniMapIcon` 자식 SpriteRenderer를 가리키도록 연결되고 저장됐음을 `.prefab` 파일에서 직접
  확인(`fileID`가 0이 아님).

## 요청 내용

> Ore 와 Gas 에게 MiniMapIcon을 추가했는데 해당 스프라이트가 fog of war안에서 가려졌을때
> 안보였으면 좋겠어

## 조사 결과 - 이미 같은 목적의 공용 패턴이 있다

`EnemyUnitController.cs`(`14~27번째 줄`, `284~296번째 줄`)가 정확히 같은 문제를 이미 풀어놨다:

```csharp
// 미니맵에 표시하는 y40대 스프라이트 마커(자식 오브젝트, 인스펙터에서 연결). 이 프로젝트의 안개(csFogWar)는
// 실제 3D Plane(Y≈1)으로 구현돼 있어 이렇게 Y가 높은 오브젝트는 깊이 테스트로 가려지지 않는다 - 그래서
// Update()에서 안개 상태를 직접 조회해 이 렌더러를 켜고 끈다 (doc/0356).
[SerializeField] private SpriteRenderer minimapIcon;
[SerializeField] private int minimapFogVisibilityMargin = 1;
private csFogWar fogWar;
...
private void UpdateFogVisibility()
{
    bool revealed = FogVisibility.IsRevealed(fogWar, transform.position, minimapFogVisibilityMargin);
    if (minimapIcon != null)
        minimapIcon.enabled = revealed;
}
```

미니맵 마커가 안개 Plane의 깊이 테스트로는 안 가려지는 이유(Y좌표가 높아서)와 그 대응(안개
상태를 직접 조회해서 렌더러 자체를 껐다 켰다 하는 것)이 이미 [[0356]]에서 정리돼 있고,
`FogVisibility.IsRevealed()`(`FogOfWar/FogVisibility.cs`)가 이 조회를 공용 헬퍼로 제공한다.
`ResourceNode.cs`에는 지금 이 로직이 전혀 없다 - `Update()` 자체가 없다.

`Ore.prefab`을 확인해보니 이미 `MiniMapIcon`(SpriteRenderer 포함) 자식 오브젝트가 추가돼
있었다 - 다만 `ResourceNode` 스크립트에 이걸 받을 필드도, 안개 조회 로직도 아직 없어서 항상
켜진 채로 남아있다.

## 제안하는 수정

### `Assets/Scripts/Resource/ResourceNode.cs`

`EnemyUnitController`와 동일한 패턴을 그대로 적용한다.

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FischlWorks_FogWar;
```

필드 추가:
```csharp
    // 미니맵에 표시하는 스프라이트 마커(자식 오브젝트, 인스펙터에서 연결). 안개(csFogWar)가 실제
    // 3D Plane(Y≈1)으로 구현돼 있어 Y가 높은 오브젝트는 깊이 테스트로 가려지지 않는다 - 그래서
    // Update()에서 안개 상태를 직접 조회해 이 렌더러를 켜고 끈다 (EnemyUnitController와 동일한
    // 패턴, doc/0356/0421).
    [SerializeField] private SpriteRenderer minimapIcon;
    [SerializeField] private int minimapFogVisibilityMargin = 1; // UserControl.fogVisibilityMargin과 동일한 목적

    private csFogWar fogWar;
```

`Start()`에 추가:
```csharp
        fogWar = FindFirstObjectByType<csFogWar>(); // 안개가 없는 씬(테스트 씬 등)에서는 null - Update()에서 그 경우 마커를 항상 켜둠
```

`Update()` 신규 추가:
```csharp
    private void Update()
    {
        if (minimapIcon != null)
            minimapIcon.enabled = FogVisibility.IsRevealed(fogWar, transform.position, minimapFogVisibilityMargin);
    }
```

### 프리팹 필드 연결

`Ore.prefab`/`Gas.prefab`은 이미 `MiniMapIcon` 자식(SpriteRenderer)이 있으므로, 새로 추가한
`minimapIcon` 인스펙터 슬롯에 그 컴포넌트를 연결해야 실제로 동작한다. 두 프리팹 다
`execute-dynamic-code`로 `MiniMapIcon` 자식을 찾아 `SerializedProperty`로 연결하고 저장하는
방식으로 자동 처리하겠다(수동으로 에디터에서 드래그할 필요 없음).

## 영향받는 파일 (예정)

- `Assets/Scripts/Resource/ResourceNode.cs` (필드 추가, `Update()` 신규)
- `Assets/prefabs/Resource/Ore.prefab`, `Assets/prefabs/Resource/Gas.prefab`
  (`minimapIcon` 필드 연결)

## 요약

- `ResourceNode.cs`에 `EnemyUnitController`와 동일한 패턴으로 `minimapIcon`/
  `minimapFogVisibilityMargin`/`fogWar` 필드와 `Update()`를 추가 - 안개에 가려지면
  `SpriteRenderer.enabled`를 꺼서 미니맵 아이콘을 숨긴다.
- `Ore.prefab`/`Gas.prefab`의 `minimapIcon` 필드를 각 프리팹의 기존 `MiniMapIcon` 자식으로
  `execute-dynamic-code`(`SerializedObject`)를 이용해 자동 연결.
- 컴파일 확인 완료(에러 0, 경고 34 - 기존 컨벤션과 동일한 종류의 경고 1개만 추가).
