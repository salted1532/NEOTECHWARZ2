# 0177 - 영토 외곽선(LineRenderer)이 안개(어둠) 속에서는 안 보이도록

## 요청

"맞아 그리고 라인 렌더러로 만든 선이 어둠안에서도 안보였으면 좋겠어" — `TerritoryZone`이 그리는 영토
경계선이 지금은 안개(아직 정찰 안 한 어두운 지역)를 뚫고 항상 보이는데, 안개가 낀 곳에서는 안 보이게
해달라는 요청. 정찰 안 한 적/중립 영토의 경계선이 미리 보이면 안개가 가려야 할 지형 정보가 새는 셈이라
일리 있는 요청.

## 조사

- `Assets/Scripts/CaptureSystem/TerritoryZone.cs`의 `outlineRenderer`(LineRenderer)는 `Update()`에서
  매 프레임 `ApplyOutlineStyle()` + `RefreshOutline()`만 호출 — 안개 상태를 전혀 참조하지 않고 항상
  그려짐.
- 왜 안개를 뚫고 보이는지(셰이더/큐 순서 문제인지)는 `outlineMaterial`에 실제로 어떤 머티리얼이
  꽂혀있는지(프리팹 인스펙터 참조라 텍스트로 확정하기 어려움)에 따라 달라서 셰이더 렌더 큐만으로
  고치는 방법은 특정 머티리얼 설정에 의존하게 되어 불안정함.
- 대신 `csFogWar`는 이미 특정 월드 좌표가 "현재 보이는지"를 물어볼 수 있는 public API를 제공함:
  `CheckVisibility(Vector3 worldCoordinates, int additionalRadius)`. [[territory-fog-reveal-timing-fix]](0176)에서
  다룬 `TerritoryFogReveal`과 마찬가지로, 아군 소유 영토는 이미 강제로 `Revealed` 처리되므로 이 API로
  물어보면 "아군 땅이거나, 현재 유닛 시야 안에 들어와 있는 곳"은 true가 나옴.
- 따라서 셰이더/렌더 큐에 의존하지 않고, **코드에서 직접 안개 상태를 물어봐서 LineRenderer 자체를
  껐다 켰다** 하는 방식이 어떤 머티리얼을 쓰든 확실하게 동작함.

## 설계

`TerritoryZone`에 `csFogWar` 참조를 추가하고, 각 핀 포인트 위치가 하나라도 현재 보이는 상태
(`CheckVisibility(pinPos, 1)`, 여유를 위해 additionalRadius=1)면 외곽선을 그리고, 전부 안 보이면
`outlineRenderer.enabled = false`로 꺼서 감춘다.

- 씬에 `csFogWar`가 없거나(테스트 씬 등) 아직 못 찾은 경우엔 기존처럼 항상 보이게(안전한 기본값,
  안개 기능이 없는 씬에서 갑자기 선이 안 보이는 회귀를 막음).
- `[ExecuteAlways]`라 에디터(Edit Mode)에서도 `Update()`가 돌아가는데, 에디터에서는 `csFogWar`의
  `shadowcaster`가 초기화되어 있지 않아(`Initialize()`는 Play 모드 `Start()`에서만 호출됨)
  `CheckVisibility` 호출 시 내부 `FogField` 인덱서가 범위 밖 에러를 뱉을 위험이 있음 → **Play
  모드에서만(`Application.isPlaying == true`) 안개 검사를 적용**하고, 에디터 편집 중에는 항상 보이게
  둬서(디자이너가 씬 뷰에서 편집할 때 선이 사라지지 않게) 문제를 원천 차단.

## 계획된 코드 변경

`Assets/Scripts/CaptureSystem/TerritoryZone.cs`

Before:
```csharp
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

...

    private LineRenderer outlineRenderer;
    private Material runtimeMaterial; // outlineMaterial(또는 기본 셰이더)을 복제한 전용 인스턴스 — 이것만 색을 바꾼다

    public CaptureOwner Owner { get => owner; set => owner = value; }

    private void OnEnable() => TerritoryManager.Register(this);
    private void OnDisable() => TerritoryManager.Unregister(this);

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
```

After:
```csharp
using System.Collections.Generic;
using FischlWorks_FogWar;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

...

    private LineRenderer outlineRenderer;
    private Material runtimeMaterial; // outlineMaterial(또는 기본 셰이더)을 복제한 전용 인스턴스 — 이것만 색을 바꾼다
    private csFogWar fogWar; // 안개 속에서는 외곽선을 감추기 위한 참조 (못 찾으면 항상 보이게 유지)

    public CaptureOwner Owner { get => owner; set => owner = value; }

    private void OnEnable() => TerritoryManager.Register(this);
    private void OnDisable() => TerritoryManager.Unregister(this);

    private void Awake()
    {
        outlineRenderer = GetComponent<LineRenderer>();
        outlineRenderer.loop = true;
        outlineRenderer.useWorldSpace = true;

        Shader fallbackShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        runtimeMaterial = outlineMaterial != null ? new Material(outlineMaterial) : new Material(fallbackShader);
        outlineRenderer.material = runtimeMaterial;

        fogWar = FindFirstObjectByType<csFogWar>();
    }

    private void Update()
    {
        ApplyOutlineStyle();
        RefreshOutline();
        UpdateFogVisibility();
    }

    // 안개 속(아직 정찰 안 한 지역)에서는 경계선을 감춘다. Play 모드가 아니거나(에디터 편집 중)
    // 씬에 csFogWar가 없으면 항상 보이는 기존 동작을 유지한다.
    private void UpdateFogVisibility()
    {
        if (!Application.isPlaying || fogWar == null)
        {
            outlineRenderer.enabled = true;
            return;
        }

        outlineRenderer.enabled = IsAnyPinCurrentlyVisible();
    }

    private bool IsAnyPinCurrentlyVisible()
    {
        foreach (Transform pin in pinPoints)
        {
            if (pin != null && fogWar.CheckVisibility(pin.position, 1))
                return true;
        }
        return false;
    }
```

`ApplyOutlineStyle()` / `RefreshOutline()` / `Contains()` 등 나머지 로직은 그대로 유지.

## 영향

- 아군 소유 영토: [[territory-permanent-vision-design]](0175) + [[territory-fog-reveal-timing-fix]](0176)
  덕분에 이미 항상 `Revealed` 상태라 경계선도 계속 보임(변화 없음).
- 중립/적 영토: 유닛이 근처에서 실제로 정찰해 시야가 닿기 전까지는 경계선도 안 보임(이번 변경으로
  새로 생기는 동작) — 안개가 가리는 지형 정보와 일관성이 맞춰짐.
- 정찰했다가 다시 유닛이 빠져나가 안개가 재귀되면, `keepRevealedTiles = false`(현재 씬 설정)라
  경계선도 다시 사라짐 — 지형 자체의 안개 동작과 완전히 동일하게 맞춰짐.

## 확인 및 진행

사용자가 "이대로 수정해줘"로 승인 → 위 계획대로 `TerritoryZone.cs`에 `fogWar` 참조,
`UpdateFogVisibility()`, `IsAnyPinCurrentlyVisible()`를 추가 구현함.

## 되돌림

구현 직후 사용자가 "그런식으로 하면 안될거 같네 다시 되돌려줘"로 반려 — `TerritoryZone.cs`를 이
변경 이전 상태로 완전히 되돌림(`using FischlWorks_FogWar;`, `fogWar` 필드, `Awake()`의
`FindFirstObjectByType<csFogWar>()` 호출, `UpdateFogVisibility()`/`IsAnyPinCurrentlyVisible()`
메서드, `Update()`의 `UpdateFogVisibility()` 호출 전부 제거). 어떤 부분이 마음에 안 들었는지(핀
포인트 기준 판정이 부정확해서인지, 접근 방식 자체를 다르게 하고 싶은지 등) 아직 구체적으로 듣지
못해서, 다음 요청 시 원하는 방향을 다시 확인해야 함. [[territory-permanent-vision-design]](0175),
[[territory-fog-reveal-timing-fix]](0176)로 구현한 점령지 강제 시야 기능은 이번 되돌림과 무관하게
그대로 유지됨.
