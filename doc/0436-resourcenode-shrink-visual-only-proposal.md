# 0436. ResourceNode 축소를 그래픽 자식 오브젝트에만 적용 (제안)

**날짜:** 2026-08-05

## 요청 내용
> ResourceNode에 경우 1/4구간마다 크기가 줄어드는데 이게 안에있는 그래픽적 GameObject만
> 줄어들게 해줄수 있어 인스펙터는 내가 연결할게

## 현재 구조

`Assets/Scripts/Resource/ResourceNode.cs`의 `ShrinkByRemainingRatio()`가 **루트 오브젝트의
`transform.localScale`**을 직접 줄이고 `transform.position`도 같이 내림. 루트에 `CapsuleCollider`가
있어서, 루트가 줄어들면 콜라이더도 같이 줄어드는 걸 막기 위해 `ApplyColliderSizeCompensation()`이
콜라이더 radius/height/center를 스케일 역수로 보정해서 원래 월드 크기를 유지시키는 중.

`Assets/prefabs/Resource/Ore.prefab` 구조를 보면 루트 `Ore` 아래 `Ore_prefab`(그래픽 메시),
`MiniMapIcon`, `Marker`, `Point Light`가 자식으로 있음. 즉 지금은 이 자식들 전부(마커, 미니맵 아이콘,
포인트 라이트까지) 루트와 함께 눈에 보이지 않게 스케일이 줄었다가 콜라이더만 보정되는 구조.

## 제안하는 변경

`Assets/Scripts/Resource/ResourceNode.cs`:
- `[SerializeField] private Transform visualRoot;` 추가 — 그래픽 전용 자식(예: `Ore_prefab`)을
  인스펙터에서 직접 연결하실 필드.
- `ShrinkByRemainingRatio()`가 `transform.localScale`/`transform.position` 대신
  **`visualRoot.localScale`/`visualRoot.localPosition`**을 줄이도록 변경. `visualRoot`가 아직
  연결 안 됐으면(null) 시각적 변경 없이 `consumedQuarters`만 갱신(추후 연결 시 어색하게 건너뛰지 않도록).
- 루트가 더 이상 스케일되지 않으므로 콜라이더도 원래 크기 그대로 유지됨 →
  `nodeCollider`/`colliderBaseRadius`/`colliderBaseHeight`/`colliderBaseCenter` 필드와
  `ApplyColliderSizeCompensation()` 전체 삭제(더 이상 필요 없는 보정 코드).

버튼/오브젝트 연결은 요청대로 직접 하실 것이므로 프리팹 파일은 건드리지 않음 — 스크립트만 변경.

## 구현 (승인 후 적용됨)

**Before:**
```csharp
[SerializeField] private SpriteRenderer minimapIcon;
[SerializeField] private int minimapFogVisibilityMargin = 1;

private csFogWar fogWar;

private const float ShrinkStepPerQuarter = 0.2f;
private const float MinScale = 0.1f;

private int initialAmount;
private int consumedQuarters;

private CapsuleCollider nodeCollider;
private float colliderBaseRadius;
private float colliderBaseHeight;
private Vector3 colliderBaseCenter;
...
private void Awake()
{
    initialAmount = remainingAmount;

    nodeCollider = GetComponent<CapsuleCollider>();
    if (nodeCollider != null)
    {
        colliderBaseRadius = nodeCollider.radius;
        colliderBaseHeight = nodeCollider.height;
        colliderBaseCenter = nodeCollider.center;
    }
}
...
private void ShrinkByRemainingRatio()
{
    if (initialAmount <= 0)
        return;

    float quarterAmount = initialAmount / 4f;
    int targetQuarters = Mathf.Min(4, Mathf.FloorToInt((initialAmount - remainingAmount) / quarterAmount));

    while (consumedQuarters < targetQuarters)
    {
        consumedQuarters++;

        Vector3 scale = transform.localScale;
        float newY = Mathf.Max(scale.y - ShrinkStepPerQuarter, MinScale);
        float appliedYShrink = scale.y - newY;

        transform.localScale = new Vector3(
            Mathf.Max(scale.x - ShrinkStepPerQuarter, MinScale),
            newY,
            Mathf.Max(scale.z - ShrinkStepPerQuarter, MinScale));

        transform.position -= new Vector3(0f, appliedYShrink, 0f);
    }

    ApplyColliderSizeCompensation();
}

private void ApplyColliderSizeCompensation()
{
    if (nodeCollider == null)
        return;

    Vector3 scale = transform.localScale;
    float radialScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z), 0.0001f);
    float verticalScale = Mathf.Max(Mathf.Abs(scale.y), 0.0001f);

    nodeCollider.radius = colliderBaseRadius / radialScale;
    nodeCollider.height = colliderBaseHeight / verticalScale;
    nodeCollider.center = new Vector3(
        colliderBaseCenter.x / radialScale,
        colliderBaseCenter.y / verticalScale,
        colliderBaseCenter.z / radialScale);
}
```

**After:**
```csharp
[SerializeField] private SpriteRenderer minimapIcon;
[SerializeField] private int minimapFogVisibilityMargin = 1;

[SerializeField] private Transform visualRoot; // 축소 애니메이션을 적용할 그래픽 전용 자식 (직접 연결)

private csFogWar fogWar;

private const float ShrinkStepPerQuarter = 0.2f;
private const float MinScale = 0.1f;

private int initialAmount;
private int consumedQuarters;
...
private void Awake()
{
    initialAmount = remainingAmount;
}
...
private void ShrinkByRemainingRatio()
{
    if (initialAmount <= 0)
        return;

    float quarterAmount = initialAmount / 4f;
    int targetQuarters = Mathf.Min(4, Mathf.FloorToInt((initialAmount - remainingAmount) / quarterAmount));

    while (consumedQuarters < targetQuarters)
    {
        consumedQuarters++;

        if (visualRoot == null)
            continue;

        Vector3 scale = visualRoot.localScale;
        float newY = Mathf.Max(scale.y - ShrinkStepPerQuarter, MinScale);
        float appliedYShrink = scale.y - newY;

        visualRoot.localScale = new Vector3(
            Mathf.Max(scale.x - ShrinkStepPerQuarter, MinScale),
            newY,
            Mathf.Max(scale.z - ShrinkStepPerQuarter, MinScale));

        visualRoot.localPosition -= new Vector3(0f, appliedYShrink, 0f);
    }
}
```

- 루트(콜라이더가 있는 오브젝트)는 더 이상 스케일되지 않으므로 콜라이더 보정 코드가 통째로 필요 없어짐.
- `visualRoot`를 아직 연결하지 않은 상태에서도(= null) 채취 로직/`consumedQuarters` 갱신은 정상 동작 —
  나중에 인스펙터에서 연결하면 그 시점부터 시각적으로 반영됨 (과거 구간을 소급 적용하진 않음, 다음
  구간부터 적용).

## 검증

- `npx uloop-cli compile`: `Success: true`, `ErrorCount: 0`, `WarningCount: 34`(전부 기존에도 있던
  무관한 경고, 새로 추가된 경고 없음).
- `visualRoot` 연결은 요청대로 직접 하실 것이므로 프리팹은 변경하지 않음.

## 영향받는 파일

- `Assets/Scripts/Resource/ResourceNode.cs`
