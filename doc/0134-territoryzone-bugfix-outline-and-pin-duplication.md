# 0134. TerritoryZone 버그 수정 제안 (외곽선 미표시/두께 조절 불가/핀 중복 생성)

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **원인 조사 + 수정안 제안**만 담고 있고
> `Assets/Scripts/CaptureSystem/TerritoryZone.cs`는 아직 고치지 않았다. 확인해 주면 그대로 반영한다.

## 날짜
2026-07-16

## 요청
`doc/0133`에서 만든 `TerritoryZone`에서 3가지 문제 보고:
1. 외곽선이 인게임(Play)에서 안 보임.
2. 외곽선 두께(`outlineWidth`)를 인스펙터에서 바꿔도 반영이 안 됨.
3. Play 버튼을 눌렀다가 빠져나오면 핀이 다시 생성돼서 안 쓰는 핀이 쌓임.

## 조사 내용

### 1. 외곽선이 인게임에서 안 보이는 이유
`Awake()`에서 `GetComponent<LineRenderer>()`만 하고 **머티리얼을 한 번도 지정하지 않는다**(`TerritoryZone.cs:22-30`). `LineRenderer`는 머티리얼이 없으면 아무것도 그리지 않거나(빌트인 렌더 파이프라인이면 핑크로라도 보이지만) URP에서는 SRP 호환 셰이더가 없으면 그냥 안 그려지는 경우가 흔하다 — 이 프로젝트가 URP를 쓴다는 건 `doc/0071`(카노푸스 머티리얼 URP에서 깨짐), `doc/0075`(Yoge 머티리얼 URP에서 깨짐)에서 이미 확인된 사실과 같은 종류의 문제.

### 2. 외곽선 두께가 안 바뀌는 이유
```csharp
private void Awake()
{
    ...
    outlineRenderer.startWidth = outlineWidth;
    outlineRenderer.endWidth = outlineWidth;
}
```
`Awake()`는 오브젝트가 만들어질 때 **딱 한 번만** 실행된다. 인스펙터에서 `Outline Width` 슬라이더를 나중에 바꿔도 이미 지나간 `Awake()`가 다시 불리지 않으므로 `LineRenderer.startWidth/endWidth`에는 반영이 안 된다. `outlineColor`도 같은 자리에서만 적용되므로 동일한 문제를 안고 있다(색은 보고되진 않았지만 원인이 같아 같이 고치는 게 맞다고 판단).

### 3. Play 모드 껐다 켜면 핀이 쌓이는 이유
`OnValidate()`가 실제로는 **인스펙터에서 값이 바뀔 때뿐 아니라 Play 모드 진입/종료(도메인 리로드) 시점에도 호출**된다. 이때 `EditorApplication.delayCall += SyncPinPoints`가 걸리는데, 도메인 리로드 도중에는 `pinPoints` 리스트의 `Transform` 참조가 아직 완전히 복원되지 않은 순간이 있어 `SyncPinPoints()`가 이걸 "빈 슬롯"으로 오판 → 이미 있던 핀은 그대로 두고 새 핀을 또 만들어버릴 수 있다. 원래 핀은 리스트 참조가 새 핀으로 덮어써지면서 고아 오브젝트로 씬에 남는다(정리 로직도 같은 시점의 불완전한 상태를 보고 판단하므로 못 지운다). 즉 **`OnValidate`/`SyncPinPoints`는 Play 모드와 관련된 시점에는 아예 실행되면 안 되는데 그 가드가 없었다.**

## 수정안

### 기존 코드 (`Assets/Scripts/CaptureSystem/TerritoryZone.cs`)
```csharp
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
```csharp
#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate 안에서 바로 씬을 건드리면 경고/에러가 날 수 있어 다음 에디터 틱으로 미룬다.
        EditorApplication.delayCall += SyncPinPoints;
    }
```

### 변경 코드
```csharp
    [SerializeField] private Material outlineMaterial; // 비워두면 URP Unlit 머티리얼을 자동 생성해서 사용

    private void Awake()
    {
        outlineRenderer = GetComponent<LineRenderer>();
        outlineRenderer.loop = true;
        outlineRenderer.useWorldSpace = true;

        if (outlineMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            outlineMaterial = new Material(shader);
        }
        outlineRenderer.material = outlineMaterial;
    }

    private void Update()
    {
        ApplyOutlineStyle(); // 색/두께를 매 프레임 반영해서 인스펙터에서 바로바로 조절 가능하게 함
        RefreshOutline();
    }

    private void ApplyOutlineStyle()
    {
        outlineRenderer.startColor = outlineColor;
        outlineRenderer.endColor = outlineColor;
        outlineRenderer.startWidth = outlineWidth;
        outlineRenderer.endWidth = outlineWidth;
        if (outlineMaterial != null) outlineMaterial.color = outlineColor;
    }
```
```csharp
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return; // Play 모드 중/전환 시점엔 씬 편집용 동기화를 돌리지 않는다

        EditorApplication.delayCall += SyncPinPoints;
    }
```

## 요약
- `Awake()`에서 머티리얼이 없으면 URP Unlit(또는 Sprites/Default) 머티리얼을 자동 생성해서 `LineRenderer.material`에 지정 — 인게임에서도 보이게 됨. 원하는 머티리얼을 직접 넣고 싶으면 `Outline Material` 필드에 드래그해서 덮어쓸 수 있음.
- 색/두께 적용을 `Awake()` 1회성에서 `Update()`마다 도는 `ApplyOutlineStyle()`로 옮겨 인스펙터에서 값 조절 시 실시간 반영.
- `OnValidate()`에 `Application.isPlaying` 가드를 추가해 Play 모드 진입/종료 시점에 핀 동기화 로직이 도는 것 자체를 막음 — 핀 생성/삭제는 순수 편집 모드에서만 일어나게 제한.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (수정 예정, 아직 미반영)

## 다음 단계
이대로 반영해도 될지 확인 부탁 — 특히 `outlineMaterial`을 자동 생성(URP Unlit) 대신 직접 만든 머티리얼 에셋을 쓰고 싶다면 알려주면 그 방식으로 바꾼다.
