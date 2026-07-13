# 0098 - 화면공간 실루엣 아웃라인 구현 (doc 0097 설계 실행)

**날짜:** 2026-07-13

## 요청 내용

> 이대로 그럼 진ㄴ행시켜줘 (doc 0097의 마스크 기반 화면공간 실루엣 아웃라인 설계를 그대로 구현해달라는 승인)

## 구현 내용

doc 0097에서 설계한 대로, 유닛/건물 인버티드 헐 아웃라인(doc 0093/0095, `Assets/Shader/Outline.shader`)을 화면공간 마스크 기반 실루엣 아웃라인으로 교체했다. `Outline.shader`/`testShader.mat` 자체는 건드리지 않고 그대로 남겨뒀다(둘 다 이번 변경과 무관하게 독립적으로 존재).

### 1. `ProjectSettings/TagManager.asset` - "Selected" 렌더링 레이어 추가

기존 코드:
```yaml
  m_RenderingLayers:
  - Default
```
변경 코드:
```yaml
  m_RenderingLayers:
  - Default
  - Selected
```
Rendering Layer는 GameObject Layer(레이캐스트/물리에 쓰이는 `layerUnit`/`layerBuilding` 등)와 완전히 별개의 비트마스크라서, 선택 시 이 값을 바꿔도 `UserControl.cs`의 `Physics.Raycast(..., layerUnit)` 같은 기존 판정에는 전혀 영향이 없다. Default=인덱스0(값 1), Selected=인덱스1(값 2).

### 2. 새 쉐이더/머티리얼

- `Assets/Shader/SelectionMask.shader` (`Shader "Hidden/SelectionMask"`) - 마스크 패스의 오버라이드 머티리얼용. 입력 메시가 뭐든 상관없이 흰색(1,1,1,1)만 출력하는 최소 언리트 쉐이더.
- `Assets/Shader/OutlineComposite.shader` (`Shader "Hidden/OutlineComposite"`) - 풀스크린 컴포짓 쉐이더. `_BlitTexture`(마스크 텍스처)를 상하좌우 이웃과 비교해서, 마스크 안(1)인데 이웃 중 하나라도 마스크 밖(0)이면 그 픽셀을 실루엣 경계로 보고 `_OutlineColor`를 알파 블렌드로 합성한다. `_OutlineThickness`(텍셀 단위, 기본 2)로 두께 조절 가능.
- `Assets/Shader/SelectionMask.mat`, `Assets/Shader/OutlineComposite.mat` - 각 쉐이더를 쓰는 머티리얼. `OutlineComposite.mat`의 기본 `_OutlineColor`는 시안색(0,1,1,1) - 유닛 팀 색(흰/초록/파랑)과 잘 구분되도록.

### 3. `Assets/Scripts/Rendering/SelectionOutlineFeature.cs` (신규)

URP `ScriptableRendererFeature` (Unity 6/URP 17.4.0의 RenderGraph API로 작성). 두 패스:
- **SelectionMaskPass**: `settings.selectedRenderingLayerMask`(기본값 2 = "Selected") 렌더링 레이어의 오브젝트만 `FilteringSettings`로 걸러서, `SelectionMask` 머티리얼로 오버라이드해 R8 오프스크린 텍스처(`_SelectionMaskTexture`)에 그린다.
- **OutlineCompositePass**: 마스크 텍스처를 `Blitter.BlitTexture`로 `OutlineComposite` 머티리얼에 넘겨 카메라 컬러 타겟(`resourceData.activeColorTexture`)에 합성.

`Settings` 클래스에 `selectionMaskMaterial`/`outlineCompositeMaterial`/`layerMask`/`selectedRenderingLayerMask` 노출.

### 4. `Assets/Settings/PC_Renderer.asset` - Feature 등록

이 프로젝트의 기본 렌더 파이프라인(`PC_RPAsset.asset`)이 쓰는 렌더러가 `PC_Renderer.asset`(인덱스 0)이라 여기에만 등록했다(`Mobile_Renderer.asset`은 현재 사용되지 않는 것으로 확인돼 건드리지 않음).

`m_RendererFeatures` 목록에 새 fileID 추가, 그리고 새 `MonoBehaviour` 블록으로 `SelectionOutlineFeature` 인스턴스를 추가하면서 `selectionMaskMaterial`/`outlineCompositeMaterial`을 방금 만든 두 머티리얼 guid로 미리 연결해뒀다(에디터에서 수동으로 드래그해 넣을 필요 없음).

### 5. `UnitController.cs`, `BuildingController.cs` - 아웃라인 토글 방식 교체

doc 0095에서 만든 `SetOutline()`(당시엔 `MaterialPropertyBlock`으로 `_OutlineEnabled` 토글)을 렌더러의 `renderingLayerMask` 비트 토글로 교체했다. `SelectUnit()`/`DeselectUnit()`/`SelectBuilding()`/`DeselecBuilding()`에서의 호출부(`SetOutline(true/false)`)는 그대로.

기존 코드 (양쪽 파일 공통 패턴):
```csharp
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private Renderer[] outlineRenderers;
    private MaterialPropertyBlock outlinePropertyBlock;
    ...
    private void SetOutline(bool enabled)
    {
        foreach (Renderer renderer in outlineRenderers)
        {
            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
```
변경 코드:
```csharp
    private const uint SelectedRenderingLayerBit = 1u << 1;
    private Renderer[] outlineRenderers;
    ...
    private void SetOutline(bool enabled)
    {
        foreach (Renderer renderer in outlineRenderers)
        {
            renderer.renderingLayerMask = enabled
                ? renderer.renderingLayerMask | SelectedRenderingLayerBit
                : renderer.renderingLayerMask & ~SelectedRenderingLayerBit;
        }
    }
```

## 변경된 파일

- `ProjectSettings/TagManager.asset`
- `Assets/Shader/SelectionMask.shader` (+ `.meta`)
- `Assets/Shader/OutlineComposite.shader` (+ `.meta`)
- `Assets/Shader/SelectionMask.mat` (+ `.meta`)
- `Assets/Shader/OutlineComposite.mat` (+ `.meta`)
- `Assets/Scripts/Rendering/SelectionOutlineFeature.cs` (+ `.meta`, 신규 폴더)
- `Assets/Settings/PC_Renderer.asset`
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Building/BuildingController.cs`

## 검증 필요 (중요 - 이 환경에서 컴파일/실행 불가)

이 세션에는 Unity 에디터가 없어서 `SelectionOutlineFeature.cs`를 직접 컴파일/실행해보지 못했다. Unity 6(6000.4.8f1) + URP 17.4.0의 RenderGraph API(`RecordRenderGraph`, `UniversalResourceData`/`UniversalCameraData`/`UniversalRenderingData`/`UniversalLightData`, `RenderingUtils.CreateDrawingSettings`, `FilteringSettings(RenderQueueRange, LayerMask, uint)`, `TextureDesc`, `RendererListParams`, `Blitter.BlitTexture`)는 마이너 버전마다 시그니처가 미세하게 바뀔 수 있는 영역이라, **에디터에서 열어서 컴파일 에러가 없는지 반드시 확인 필요**. 에러가 나면 Console에 뜨는 정확한 시그니처 불일치 지점만 고치면 되는 구조로 작성해뒀다(패스 2개짜리 단순한 구조).

에디터에서 확인할 것:
1. 컴파일 에러 여부 (`SelectionOutlineFeature.cs`).
2. `PC_Renderer.asset`을 선택했을 때 Inspector에 "Selection Outline Feature"가 정상적으로 나타나는지, `Selection Mask Material`/`Outline Composite Material`이 제대로 연결돼 있는지.
3. Play 모드에서 유닛/건물 선택 시 시안색 아웃라인이 보이는지, 다른 오브젝트에 가려진 부분엔 선이 안 생기는지(오클루전 확인).
4. 우클릭 이동/공격 명령이 기존처럼 정상 동작하는지(Rendering Layer 변경이 GameObject Layer 기반 레이캐스트에 영향 없어야 함 - 영향 있으면 버그).

## 남은 작업 / 결정 보류 항목

- `Assets/Shader/Outline.shader`(인버티드 헐)와 `testShader.mat`(doc 0093)은 이번에 안 건드림 - 필요 없어졌다고 판단되면 별도 요청으로 정리.
- `Assets/Settings/Mobile_Renderer.asset`에는 이 기능을 추가하지 않음(현재 파이프라인이 PC_Renderer만 사용).
