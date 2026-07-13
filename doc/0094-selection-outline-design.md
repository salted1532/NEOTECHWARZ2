# 0094 - 선택 시에만 아웃라인 표시 (설계)

**날짜:** 2026-07-13

## 요청 내용

> 현재 testshader를 유닛이나 건물이 선택 되었을때만 아웃라인이 보이도록 하는게 가능한가?

가능한지 여부를 묻는 질문이라 이번엔 구현하지 않고 설계만 정리한다 (사용자가 "일단 구현 없이 설계만 doc에 정리"를 선택함).

## 결론

가능하다. 핵심은 `Outline.shader`(doc 0093에서 추가한 커스텀 쉐이더)의 아웃라인 패스에 on/off 토글용 프로퍼티(`_OutlineEnabled`)를 추가하고, 유닛/건물의 선택 진입점(`UnitController.SelectUnit()`/`DeselectUnit()`, 건물 쪽 대응 메서드)에서 `Renderer.SetPropertyBlock()`(`MaterialPropertyBlock`)으로 그 값을 1/0으로 바꿔주면 된다. `MaterialPropertyBlock`을 쓰면 머티리얼을 인스턴스화(런타임 복제)하지 않고도 유닛/건물마다 독립적으로 켜고 끌 수 있다.

### 쉐이더 쪽 변경 (둘 중 어느 적용 방식을 택하든 공통)

`Outline.shader`의 `Properties`에 토글 추가:
```hlsl
[Toggle] _OutlineEnabled ("Outline Enabled", Float) = 0
```

"Outline" 패스의 프래그먼트에서 꺼져 있으면 그 픽셀을 아예 그리지 않도록 `clip()`으로 버림:
```hlsl
CBUFFER_START(UnityPerMaterial)
    float4 _OutlineColor;
    float  _OutlineWidth;
    float  _OutlineEnabled;
CBUFFER_END
...
half4 frag(Varyings IN) : SV_Target
{
    clip(_OutlineEnabled - 0.5);
    return _OutlineColor;
}
```
`clip()`으로 버려진 프래그먼트는 컬러/뎁스 기록을 아예 안 하므로, `_OutlineWidth`를 0으로 낮추는 방식(같은 위치에 겹쳐 그려서 z-fighting 위험이 있는 방식)보다 안전하다.

스크립트에서는:
```csharp
private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
private MaterialPropertyBlock outlineBlock;

public void SelectUnit()
{
    unitMarker.SetActive(true);
    SetOutline(true);
}

public void DeselectUnit()
{
    unitMarker.SetActive(false);
    SetOutline(false);
}

private void SetOutline(bool enabled)
{
    outlineBlock ??= new MaterialPropertyBlock();
    meshRenderer.GetPropertyBlock(outlineBlock);
    outlineBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
    meshRenderer.SetPropertyBlock(outlineBlock);
}
```
(건물 쪽도 선택/해제 진입점에 동일한 패턴을 넣으면 됨.)

## 적용 범위 - 두 가지 방식과 트레이드오프

현재 `testShader.mat`은 어떤 유닛/건물 프리팹에도 실제로 할당돼 있지 않다 (테스트용으로만 존재). 실제 유닛/건물에 적용하려면 아래 두 방식 중 하나가 필요하다.

### 방식 A: 기존 유닛/건물 머티리얼 자체를 Outline 쉐이더로 교체
- 각 유닛/건물이 쓰는 머티리얼(예: Canopus, Yoge 세트의 각 머티리얼)의 쉐이더를 `Custom/Outline`으로 바꾸고, `_BaseMap`/`_EmissionMap` 등 기존 텍스처 슬롯을 그대로 물려준다.
- 장점: 렌더러/오브젝트 구조를 안 건드림, 머티리얼 파일만 교체.
- 단점: 머티리얼 개수만큼 전부 교체해야 하고, 원래 쉐이더가 URP Lit의 노멀맵/메탈릭/스무스니스 등 PBR 기능을 쓰고 있었다면 그 기능들을 Outline.shader에도 이식해야 시각적 손실이 없다(현재 Outline.shader는 베이스컬러+이미션만 지원하는 간소화 버전). 유닛 종류가 많으면 손이 많이 감.

### 방식 B: 아웃라인 전용 자식 렌더러 추가
- 각 유닛/건물 프리팹에 같은 메시를 참조하는 자식 오브젝트를 하나 추가하고, 거기에 "Outline 패스만 있는" 별도 머티리얼(베이스 컬러 패스 없이 Cull Front 확장 패스만)을 입힌다.
- 장점: 기존 머티리얼/쉐이더는 전혀 안 건드림 - PBR 기능 손실 없음. 아웃라인 on/off도 이 자식 오브젝트의 `GameObject.SetActive()`만으로 가능해서 `_OutlineEnabled` 프로퍼티조차 필요 없어짐(더 단순).
- 단점: 드로우콜이 유닛당 1개 늘어남(자식 렌더러), 프리팹마다 자식 오브젝트를 추가하는 작업이 필요(유닛/건물 프리팹 개수만큼).

## 권장

프로젝트에 유닛/건물 종류가 여럿이고 각각 서로 다른 PBR 머티리얼(노멀맵 등)을 쓰고 있다면 **방식 B(전용 자식 렌더러)**가 기존 비주얼을 안 깨서 더 안전하다. 다만 프리팹 편집(자식 오브젝트 추가)이 필요해서 스크립트만으로 끝나지 않고 Unity 에디터 작업이 같이 들어간다.

## 남은 작업

- 아직 아무 것도 구현하지 않음(설계만). 방식 A/B 중 하나를 정하고, 실제 적용 대상(전체 유닛/건물 vs 테스트용 일부)을 정하면 그에 맞춰 별도 `doc/NNNN-*.md` 구현 제안을 다시 작성 후 승인받아 진행.
