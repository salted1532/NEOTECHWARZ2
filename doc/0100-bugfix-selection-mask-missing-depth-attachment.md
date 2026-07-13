# 0100 - 버그수정(추정): 마스크 패스에 뎁스 어태치먼트가 없었음

**날짜:** 2026-07-13

## 요청 내용

> 건물이나 유닛 선택시 아웃라인이 나타나지 않아 오류도 없고 pc_renderer에도 체크 활성화 되어있어

doc 0099(ShaderTagId 수정)를 적용한 뒤에도 여전히 아웃라인이 안 보인다는 후속 리포트. 컴파일 에러 없음, Feature 활성화 확인됨 - 즉 스크립트 자체는 정상 작동 중이라는 뜻이라 다른 원인을 찾아야 했다.

## 원인 (추정)

`SelectionMaskPass`가 `SelectionMask.shader`의 패스 상태를 `ZWrite On`/`ZTest LEqual`로 뒀는데, RenderGraph 쪽에서는 이 마스크 패스에 **뎁스 어태치먼트를 아예 연결하지 않았다**(`builder.SetRenderAttachment(MaskTexture, ...)`만 있고 뎁스 관련 호출이 없었음). 뎁스 테스트/쓰기가 켜진 상태로 뎁스 어태치먼트가 없는 렌더 패스는 그리기 자체가 스킵되거나 정의되지 않은 방식으로 동작할 수 있어서, 마스크 텍스처가 계속 비어 있었을(=아무 것도 안 그려졌을) 가능성이 높다 - 그러면 컴포짓 패스가 경계를 하나도 못 찾아 아웃라인이 전혀 안 보이는 지금 증상과 정확히 일치한다.

(이 환경엔 Unity 에디터가 없어 Frame Debugger로 직접 확인은 못 했다 - 코드 리뷰 기반 추정이라는 점을 밝혀둔다.)

## 수정

**`Assets/Scripts/Rendering/SelectionOutlineFeature.cs`** - 마스크 패스에 씬의 기존 뎁스 텍스처를 "테스트 전용"으로 연결.

기존 코드:
```csharp
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
{
    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
    UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
    UniversalLightData lightData = frameData.Get<UniversalLightData>();
    ...
    builder.UseRendererList(passData.rendererList);
    builder.SetRenderAttachment(MaskTexture, 0, AccessFlags.Write);
    builder.AllowPassCulling(false);
```
변경 코드:
```csharp
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
{
    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
    UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
    UniversalLightData lightData = frameData.Get<UniversalLightData>();
    ...
    builder.UseRendererList(passData.rendererList);
    builder.SetRenderAttachment(MaskTexture, 0, AccessFlags.Write);
    // 씬의 기존 뎁스에 대고 테스트만 하고(쓰지는 않음) - 선택된 오브젝트가 다른(비선택) 오브젝트에
    // 가려져 있으면 그 부분은 마스크에 안 찍히게 해서 실제 카메라 시점 오클루전을 반영한다.
    builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
    builder.AllowPassCulling(false);
```

**`Assets/Shader/SelectionMask.shader`** - 씬의 실제 뎁스를 덮어쓰면 안 되므로 `ZWrite Off`로 변경(테스트만, 쓰기는 안 함).

기존 코드:
```hlsl
            Name "SelectionMask"
            Cull Back
            ZWrite On
            ZTest LEqual
```
변경 코드:
```hlsl
            Name "SelectionMask"
            Cull Back
            // 씬의 기존 뎁스(오퍼시크 패스가 이미 채워둔 것)에 대고 테스트만 한다 - 실제 뎁스를 덮어쓰면 안 되므로 ZWrite Off.
            ZWrite Off
            ZTest LEqual
```

## 확인 필요

- 여전히 Unity 에디터에서 직접 확인 못 함 - 재확인 부탁.
- 이번에도 안 보이면, 추측으로 계속 고치는 것보다 **Frame Debugger**(Window > Analysis > Frame Debugger)로 "Selection Mask"/"Outline Composite" 패스가 실제로 실행되는지, "Selection Mask" 패스의 드로우콜에 잡히는 오브젝트 수가 0인지 아닌지를 직접 확인해서 알려주면 훨씬 정확하게 원인을 좁힐 수 있다.
- 추가로 확인해볼 것: 유닛을 선택한 상태에서 그 유닛의 Renderer 컴포넌트(Inspector > Additional Settings > Rendering Layer Mask)에 "Selected"가 실제로 체크되는지 - 이게 안 되면 `UnitController.SetOutline()`/`BuildingController.SetOutline()` 쪽 문제로 원인이 옮겨간다.

## 변경된 파일

- `Assets/Scripts/Rendering/SelectionOutlineFeature.cs`
- `Assets/Shader/SelectionMask.shader`
