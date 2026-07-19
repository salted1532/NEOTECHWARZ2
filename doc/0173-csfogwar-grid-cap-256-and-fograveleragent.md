# 0173 - csFogWar 그리드 캡 256으로 확장 + 안전한 등록 어댑터(`FogRevealerAgent`)

## 요청

"그럼 최대 256으로 늘려주고 유닛,건물에 더 안전한 방법으로 fogwar에 자신을 등록하도록 해줘" —
[[csfogwar-usage-and-grid-cap-question]](0172) 상담에서 논의한 두 가지를 실제로 적용해달라는 지시.

## 변경 사항

### 1. `Assets/AssetFolder/AOSFogWar/csFogWar.cs` — 그리드 캡 128 → 256

Before:
```csharp
[SerializeField]
[Range(1, 128)]
[Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
private int levelDimensionX = 11;
[SerializeField]
[Range(1, 128)]
[Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
private int levelDimensionY = 11;
```

After:
```csharp
[SerializeField]
[Range(1, 256)]
[Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
private int levelDimensionX = 11;
[SerializeField]
[Range(1, 256)]
[Tooltip("If you need more than 128 units, consider using raycasting-based fog modules instead.")]
private int levelDimensionY = 11;
```

`[Range]`는 인스펙터 슬라이더 한도일 뿐이라 값만 바꿨다(툴팁 문구는 원본 그대로 유지 — 128이란
숫자는 저자의 권고 기준값 설명이라 그대로 둬도 무방). 0172에서 설명한 대로 `UpdateFogPlaneTextureBuffer()`가
매 프레임 텍스처 전체를 `GetPixels()`/`SetPixels()`하는 부분이 실제 비용이므로, 256까지 올렸다고
자동으로 그 크기를 다 쓰라는 뜻은 아니고 필요할 때 128~256 사이에서 조정 가능하다는 의미.

### 2. `Assets/Scripts/FogOfWar/FogRevealerAgent.cs` (신규)

Before: (파일 없음)

After: 유닛/건물에 붙이는 어댑터 컴포넌트.
- `Start()`에서 `GameObject.Find("FogWar")` 대신 `FindFirstObjectByType<csFogWar>()`로 조회
  (`BuildingController`/`EnemyController`가 이미 쓰는 프로젝트 관례와 동일).
- `AddFogRevealer(new csFogWar.FogRevealer(transform, sightRange, updateOnlyOnMove))`로 등록.
- **핵심 안전장치**: `AddFogRevealer`/`RemoveFogRevealer`는 리스트 인덱스 기반인데, 등록 시점에
  받은 인덱스를 그냥 캐싱해두면 다른 유닛이 먼저 죽어서 목록이 앞으로 당겨질 경우 인덱스가 어긋나
  엉뚱한 revealer를 지우게 되는 위험이 있다. 그래서 인덱스를 캐싱하지 않고, `OnDestroy()`에서
  제거 직전에 `fogWar._FogRevealers.IndexOf(fogRevealer)`로 **현재** 인덱스를 다시 조회해서 그 값으로
  제거하도록 했다 — [[csfogwar-usage-and-grid-cap-question]](0172)에서 예전 스니펫에 지적했던
  "등록만 하고 안전한 해제 로직이 없다"는 문제를 해결.
- `UnitController`/`BuildingController` 등 기존 스크립트는 전혀 수정하지 않음 — 프리팹에 이 컴포넌트만
  추가로 붙이면 됨.

## 남은 작업

- 씬에 `FogWar` 오브젝트 배치 + `csFogWar` 인스펙터 설정(`levelMidPoint`, `fogPlaneMaterial` =
  `FogPlane.mat`, `unitScale`/`levelDimensionX/Y` 확정)은 아직 안 함.
- 시야를 줄 유닛/건물 프리팹에 `FogRevealerAgent` 부착도 아직 안 함(에디터 수동 작업).
- 적 은폐(`csFogVisibilityAgent`류 로직을 `EnemyController`에 적용)는 이번 범위 밖 — 별도 요청 시 진행.
