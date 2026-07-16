# 0135. 소유권(중립/아군/적) 별 외곽선 색상 자동 전환

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **수정안 제안**만 담고 있고
> `Assets/Scripts/CaptureSystem/TerritoryZone.cs`, `CaptureSystem.cs`는 아직 고치지 않았다. 확인해 주면 그대로 반영한다.

## 날짜
2026-07-16

## 요청
외곽선 머티리얼을 추가했는데(현재 White 머티리얼), 점령 전(중립)엔 흰색, 아군이 점령하면 초록색, 적이 점령하면 빨간색으로 **자동 전환**되게 해달라. 머티리얼을 색깔별로 여러 개 바꿔 끼우든, 새 머티리얼을 만들어 적용하고 그 색만 바꾸든 상관없음.

## 조사 내용
- `doc/0134`(머티리얼 미지정으로 인게임에 안 보이던 문제, 두께 실시간 반영 안 되던 문제, Play 모드 핀 중복 문제)가 **아직 실제 코드에 반영 안 된 상태**다 — 이번 요청이 머티리얼 색을 매 프레임 바꾸는 로직을 추가하는 것이라 0134의 수정과 겹치므로 이번에 한 번에 반영한다.
- **새로 발견한 문제**: 0134에서 제안했던 `outlineRenderer.material = outlineMaterial;`(그대로 대입)은, 인스펙터에 드래그한 머티리얼이 **씬에 있는 다른 오브젝트와 공유하는 에셋**이면 위험하다. `[ExecuteAlways]`인 이 스크립트가 매 프레임 `outlineMaterial.color = ...`로 색을 바꾸면, 그건 인스턴스가 아니라 **에셋 원본(.mat 파일)을 직접 수정**하는 것과 같다 — 플레이 모드가 아닌 에디터 편집 상태에서도 그대로 적용되므로, 저장하면 프로젝트의 White 머티리얼 자체가 실제로 초록/빨강으로 영구히 바뀌어 버리고, 그 머티리얼을 쓰는 다른 오브젝트도 같이 색이 변해버린다. → **인스펙터에 넣은 머티리얼은 "원본 참고용"으로만 쓰고, 실제로 색을 바꾸는 대상은 `Awake()`에서 복제한 전용 인스턴스**여야 한다.
- 소유권 값 자체(`CaptureOwner`)는 이미 `TerritoryZone.owner`에 있지만(0133), 지금은 `CaptureSystem`이 점령 완료(`CompleteCapture`) 시 이 값을 갱신해주는 연결이 없다 — "자동으로 바뀐다"는 요청을 만족하려면 `CaptureSystem`에서 `TerritoryZone.Owner`를 실제로 갱신해줘야 한다.

## 수정안

### 1. `TerritoryZone.cs` — 소유권별 색상 + 머티리얼 인스턴스화 + (0134 수정 포함)

**기존 코드**
```csharp
    [SerializeField] private CaptureOwner owner = CaptureOwner.Neutral;

    [Header("외곽선 표시")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 0.3f;

    private LineRenderer outlineRenderer;

    public CaptureOwner Owner { get => owner; set => owner = value; }

    private void Awake()
    {
        outlineRenderer = GetComponent<LineRenderer>();
        outlineRenderer.loop = true;
        outlineRenderer.useWorldSpace = true;
        outlineRenderer.startColor = outlineColor;
        outlineRenderer.endColor = outlineColor;
        outlineRenderer.startWidth = outlineWidth;
        outlineRenderer.endWidth = outlineWidth;
    }

    private void Update()
    {
        RefreshOutline();
    }
```

**변경 코드**
```csharp
    [SerializeField] private CaptureOwner owner = CaptureOwner.Neutral;

    [Header("외곽선 표시")]
    [SerializeField] private Material outlineMaterial; // 원본 참고용 — 실제로 색이 바뀌는 건 런타임 복제본
    [SerializeField] private float outlineWidth = 0.3f;

    [Header("소유권별 색상 (자동 전환)")]
    [SerializeField] private Color neutralColor = Color.white;
    [SerializeField] private Color allyColor = Color.green;
    [SerializeField] private Color enemyColor = Color.red;

    private LineRenderer outlineRenderer;
    private Material runtimeMaterial; // outlineMaterial(또는 기본 셰이더)을 복제한 전용 인스턴스 — 이것만 색을 바꾼다

    public CaptureOwner Owner { get => owner; set => owner = value; }

    private void Awake()
    {
        outlineRenderer = GetComponent<LineRenderer>();
        outlineRenderer.loop = true;
        outlineRenderer.useWorldSpace = true;

        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        outlineRenderer.material = runtimeMaterial;
    }

    private void Update()
    {
        ApplyOutlineStyle();
        RefreshOutline();
    }

    private Color CurrentOwnerColor()
    {
        switch (owner)
        {
            case CaptureOwner.Ally: return allyColor;
            case CaptureOwner.Enemy: return enemyColor;
            default: return neutralColor;
        }
    }

    private void ApplyOutlineStyle()
    {
        Color c = CurrentOwnerColor();
        outlineRenderer.startColor = c;
        outlineRenderer.endColor = c;
        outlineRenderer.startWidth = outlineWidth;
        outlineRenderer.endWidth = outlineWidth;
        if (runtimeMaterial != null) runtimeMaterial.color = c; // 복제본만 수정 — 원본 에셋(outlineMaterial)은 안 건드림
    }
```

`OnValidate()`에도 0134에서 제안한 Play 모드 가드를 추가:
```csharp
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return; // Play 모드 중/전환 시점엔 씬 편집용 동기화를 돌리지 않는다

        EditorApplication.delayCall += SyncPinPoints;
    }
```

### 2. `CaptureSystem.cs` — 점령 완료 시 `TerritoryZone.Owner` 갱신

**기존 코드** (`Assets/Scripts/CaptureSystem/CaptureSystem.cs`)
```csharp
public class CaptureSystem : MonoBehaviour
{
    [SerializeField] private float captureDuration = 30f;

    [Header("상태별 이펙트 (흰색/초록/빨강)")]
    [SerializeField] private GameObject neutralEffect;
    [SerializeField] private GameObject allyEffect;
    [SerializeField] private GameObject enemyEffect;
    ...
    private void Awake()
    {
        ApplyEffect(CurrentOwner);
        ...
    }
    ...
    private void CompleteCapture(CaptureOwner newOwner)
    {
        CurrentOwner = newOwner;
        ApplyEffect(newOwner);
        SetCaptureBarVisible(false);

        Debug.Log("점령이 되었다");
    }
    ...
    private void ApplyEffect(CaptureOwner owner)
    {
        if (neutralEffect != null) neutralEffect.SetActive(owner == CaptureOwner.Neutral);
        if (allyEffect != null) allyEffect.SetActive(owner == CaptureOwner.Ally);
        if (enemyEffect != null) enemyEffect.SetActive(owner == CaptureOwner.Enemy);
    }
}
```

**변경 코드**
```csharp
public class CaptureSystem : MonoBehaviour
{
    [SerializeField] private float captureDuration = 30f;

    [Header("상태별 이펙트 (흰색/초록/빨강)")]
    [SerializeField] private GameObject neutralEffect;
    [SerializeField] private GameObject allyEffect;
    [SerializeField] private GameObject enemyEffect;

    [Header("이 거점이 관리하는 영토 (비워두면 같은 오브젝트에서 자동으로 찾음)")]
    [SerializeField] private TerritoryZone territoryZone;
    ...
    private void Awake()
    {
        if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();

        ApplyEffect(CurrentOwner);
        ...
    }
    ...
    private void CompleteCapture(CaptureOwner newOwner)
    {
        CurrentOwner = newOwner;
        ApplyEffect(newOwner);
        SetCaptureBarVisible(false);

        Debug.Log("점령이 되었다");
    }
    ...
    private void ApplyEffect(CaptureOwner owner)
    {
        if (neutralEffect != null) neutralEffect.SetActive(owner == CaptureOwner.Neutral);
        if (allyEffect != null) allyEffect.SetActive(owner == CaptureOwner.Ally);
        if (enemyEffect != null) enemyEffect.SetActive(owner == CaptureOwner.Enemy);

        if (territoryZone != null) territoryZone.Owner = owner;
    }
}
```
`ApplyEffect`가 이미 `Awake()`(중립 초기화)와 `CompleteCapture()`(점령 완료) 양쪽에서 불리는 지점이라, 여기 한 줄만 추가하면 기존 이펙트 전환과 영토 색 전환이 항상 같이 일어난다(둘이 어긋날 일이 없음).

## 요약
- `TerritoryZone`에 `neutralColor`(흰색)/`allyColor`(초록)/`enemyColor`(빨강) 3개 필드를 추가하고, 매 프레임 현재 `owner` 값에 맞는 색을 외곽선(`LineRenderer`)과 전용 머티리얼 인스턴스에 반영.
- 인스펙터에 넣은 머티리얼(`outlineMaterial`)은 색상 원본/참고용으로만 쓰고, 실제로 칠해지는 건 `Awake()`에서 복제한 `runtimeMaterial` — 프로젝트의 공유 White 머티리얼 에셋 자체는 건드리지 않음.
- `CaptureSystem`이 `TerritoryZone`을 참조해서, 점령 상태가 바뀔 때마다(`ApplyEffect` 호출 시점) `TerritoryZone.Owner`를 같이 갱신 — 완전히 자동으로 흰색→초록(→빨강, 적 점령 구현되면) 전환.
- `doc/0134`의 세 가지 수정(머티리얼 자동 생성, 두께/색 실시간 반영, Play 모드 핀 동기화 가드)도 이번에 같이 반영.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (수정 예정)
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정 예정)

## 결정이 필요한 부분
- `CaptureSystem`과 `TerritoryZone`을 같은 게임오브젝트에 두고 자동으로 찾게(`GetComponent`) 할지, 인스펙터에서 수동으로 연결할지 — 위 코드는 둘 다 되게(비어있으면 자동으로 찾고, 넣어뒀으면 그걸 우선 사용) 했는데 이 방식이 맞는지.
- 적 점령(`CaptureOwner.Enemy`)으로 전환되는 로직 자체는 아직 코드에 없음(`CompleteCapture(CaptureOwner.Ally)` 호출만 존재, 주석상 "추후"로 표시돼 있던 부분) — 이번엔 색상 매핑만 미리 준비해두고, 실제 적 점령 전환 로직 추가는 범위 밖으로 둘지.

## 다음 단계
이대로 반영해도 될지 확인 부탁.
