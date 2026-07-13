# 0097 - 화면공간 실루엣 아웃라인 설계 (구현 없이 설계만)

**날짜:** 2026-07-13

## 요청 내용

> outline을 카메라 시점에서 봤을때 진짜 외곽선만 보이도록 할수 있나?

사용자가 "설계부터 doc에 정리"를 선택해서 이번엔 구현하지 않고 설계만 정리한다.

## 현재 방식(doc 0093/0095)의 한계

지금 `Outline.shader`는 인버티드 헐(inverted hull) 방식이다: 정점을 노멀 방향으로 부풀려서 뒷면만 그린다. 이 프로젝트 유닛/건물이 로우폴리(하드 엣지, 면마다 노멀이 뚝뚝 끊김) 모델이라 다음 문제가 생긴다.

- 각진 부분(하드 엣지)에서 정점 노멀 방향이 면마다 달라서 테두리 두께가 고르지 않거나 갈라져 보인다.
- 메시가 오목한 부분에서는 확장된 헐이 원래 메시와 겹쳐서 그 부분 테두리가 아예 안 보일 수 있다.
- 다른 오브젝트에 가려진 부분까지도 "그 오브젝트의 지오메트리 기준"으로 그려지기 때문에, 진짜 카메라에서 보이는 실루엣(가려진 부분은 안 보여야 함)과 다르게 나올 수 있다.

즉 지금 방식은 "메시 지오메트리를 부풀리는" 근사치이지, "카메라에서 실제로 보이는 경계선"을 그리는 게 아니다.

## 제안: 마스크 기반 화면공간 실루엣 아웃라인

### 원리

1. 선택된 오브젝트만 화면 해상도의 별도 렌더타겟(마스크, 예: R8 단일 채널)에 단색으로 렌더링한다.
2. 풀스크린 패스에서 마스크 텍셀을 이웃 텍셀(상하좌우 또는 8방향)과 비교한다 - 값이 다르면(안/밖 경계) 그 픽셀이 실루엣 경계이므로 `_OutlineColor`를 카메라 컬러 타겟에 합성한다.

이 방식은 인버티드 헐과 달리:
- **오클루전을 자동으로 반영한다** - 마스크 자체가 "카메라에서 실제로 보이는" 부분만 그려지므로, 다른 유닛/지형에 가려진 부분은 마스크에 없고, 따라서 그 부분엔 아웃라인이 안 생긴다. 이게 정확히 "카메라 시점에서 봤을 때 진짜 외곽선"이다.
- 메시 노멀 상태와 무관하다 - 로우폴리 하드 엣지든 뭐든 화면 픽셀 단위로 경계를 찾기 때문에 두께가 항상 균일하다.
- 여러 선택된 오브젝트가 화면상에서 겹치거나 인접해도, 겹친 안쪽 경계는 마스크상 둘 다 "선택됨"이라 안 그려지고 바깥 실루엣에만 선이 생긴다(자연스러운 그룹 아웃라인).

### 필요한 구성 요소

**1. "선택됨" 표시를 GameObject Layer가 아니라 Unity의 Rendering Layer(렌더링 레이어)로 한다.**

이 프로젝트는 이미 `layerUnit`/`layerBuilding`/`layerEnemy`/`layerGround`/`layerOre`/`layerGas` 같은 **GameObject Layer**를 우클릭/좌클릭 레이캐스트 판정에 쓰고 있다(`UserControl.cs`). 만약 선택 시 GameObject의 Layer 자체를 바꾸면 `Physics.Raycast(ray, out hit, dist, layerUnit)` 같은 기존 판정이 깨진다(선택된 유닛이 갑자기 layerUnit이 아니게 되어 우클릭 명령 대상에서 빠지는 등). 그래서 물리/레이캐스트에 영향 없는 별도의 **Rendering Layer**(`Renderer.renderingLayerMask`, Unity 6 URP가 라이트/렌더 필터링용으로 제공하는 별개의 비트마스크)에 "Selected" 비트를 하나 추가해서 그것만 켜고 끈다.

- `ProjectSettings/TagManager.asset`의 `m_RenderingLayers`에 `Selected` 항목 추가.
- `UnitController.SetOutline()`/`BuildingController.SetOutline()`을 지금처럼 `MaterialPropertyBlock._OutlineEnabled` 대신, 각 렌더러의 `renderingLayerMask`에 `Selected` 비트를 켜고 끄는 것으로 교체(호출부인 `SelectUnit()`/`DeselectUnit()`/`SelectBuilding()`/`DeselecBuilding()`는 그대로 유지, 내부 구현만 교체).

**2. 커스텀 `ScriptableRendererFeature` 2패스** (아래 "Unity 6 유의사항" 참고 - RenderGraph API로 작성)

- **Mask Pass**: `renderingLayerMask`가 "Selected"인 오브젝트만 골라, 간단한 단색(흰색) 언리트 쉐이더로 오프스크린 R8 텍스처에 렌더링(클리어 색은 검정).
- **Outline Composite Pass**: 풀스크린 블릿. 마스크 텍스처를 이웃 텍셀과 비교(Sobel/Roberts cross 등 간단한 엣지 검출 커널)해서 경계로 판정된 픽셀에 `_OutlineColor`를 카메라 컬러 타겟에 합성.

**3. `PC_Renderer.asset`(및 `Mobile_Renderer.asset`)에 이 Feature 등록.**

### Unity 6 / URP 17 유의사항 (이 프로젝트 확인됨)

- `ProjectSettings/ProjectVersion.txt` 확인 결과 Unity `6000.4.8f1`, `Packages/manifest.json` 확인 결과 URP `17.4.0` - Unity 6세대 URP는 기본적으로 **Render Graph API**를 쓴다. 커스텀 `ScriptableRendererFeature`/`ScriptableRenderPass`는 레거시 `Execute(context, ref renderingData)`(CommandBuffer 직접 조작) 방식이 아니라 **`RecordRenderGraph()`** API로 작성해야 한다(레거시 방식은 Compatibility Mode를 켜야만 동작하고, 이 프로젝트는 명시적으로 그 모드를 켜둔 흔적이 없음).
- `Assets/Settings/PC_Renderer.asset`을 보면 이미 `ScreenSpaceAmbientOcclusion` Renderer Feature가 활성화돼 있고 `Source: 1`(DepthNormals)로 설정돼 있다 - 즉 Depth+Normals prepass 인프라 자체는 이미 이 프로젝트에 존재한다. (다만 이번 마스크 방식은 깊이/노멀 텍스처가 아니라 별도의 단색 마스크 렌더타겟을 새로 만드는 것이라 SSAO의 것과는 별개 리소스이고, 직접 재사용하진 않는다.)
- `m_RenderingMode: 2`(Forward+)로 설정돼 있음 - Forward+에서도 이 마스크 방식은 문제없이 동작한다(추가 오프스크린 패스라 렌더링 모드에 의존하지 않음).

### 대안으로 검토했지만 기각한 방식: 씬 전체 Depth+Normal 엣지 디텍션

SSAO처럼 이미 있는 `_CameraNormalsTexture`/`_CameraDepthTexture`를 이용해 씬 전체에서 깊이/노멀 불연속을 찾아 외곽선을 그리는 방법도 있다(카툰 렌더링에서 흔한 "씬 전체 윤곽선" 효과). 하지만:
- 이 방식은 "선택된 오브젝트만" 강조하는 이번 목적과 안 맞는다 - 결국 선택된 오브젝트만 걸러내려면 스텐실 마스크가 또 필요해서, 위 마스크 방식보다 복잡도만 늘어난다.
- 씬의 모든 지오메트리 경계(지형 능선, 건물 모서리 등)에도 선이 그어져서 노이즈가 많다.

그래서 "선택 시 강조 표시"라는 이번 목적에는 **마스크 기반 실루엣 방식**이 더 적합하다고 판단.

## 예상 작업 범위 (구현 승인 시)

신규:
- `SelectionOutlineFeature.cs` (`ScriptableRendererFeature`, RenderGraph 기반)
- Mask Pass / Outline Composite Pass용 `ScriptableRenderPass` 스크립트
- 마스크용 단색 언리트 쉐이더, 풀스크린 아웃라인 컴포짓 쉐이더

수정:
- `ProjectSettings/TagManager.asset` (`Selected` Rendering Layer 추가)
- `Assets/Settings/PC_Renderer.asset`, `Mobile_Renderer.asset` (Feature 등록)
- `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/Building/BuildingController.cs`의 `SetOutline()` (renderingLayerMask 토글로 교체)

정리 필요(결정 대기):
- `Assets/Shader/Outline.shader`의 인버티드 헐 Outline 패스는 새 방식으로 대체되면 더 이상 안 쓰이게 됨 - 지울지, 남겨둘지(예: 저사양 폴백용) 결정 필요.
- `Assets/Shader/testShader.mat`(doc 0093, 인버티드 헐 실험용)은 이번 변경과 무관하게 유지할지 여부 별도 결정.

## 남은 작업

이번엔 설계만. 위 방향으로 구현을 진행할지, 아니면 다른 대안(예: 인버티드 헐을 유지하되 스무드 노멀만 보정)으로 갈지 정하면 별도 `doc/NNNN-*.md` 구현 제안을 작성 후 승인받아 진행.
