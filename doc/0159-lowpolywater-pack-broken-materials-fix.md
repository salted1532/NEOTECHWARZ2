# 0159. LowPolyWater_Pack 깨진 텍스쳐/머티리얼 수정

## 날짜
2026-07-17

## 요청
"LowPolyWater_Pack에서 깨진 텍스쳐들 고쳐줘"

## 조사
`Assets/AssetFolder/LowPolyWater_Pack`의 머티리얼 2개를 확인. 둘 다 [[0071-canopus-materials-broken-in-urp]]/[[0075-yoge-materials-broken-in-urp]]와 같은 "Built-in RP 전용 셰이더라 URP에서 핑크로 깨짐" 부류지만, 성격이 서로 달랐다.

1. **`LowPoly Island/Materials/IslandMat.mat`** — Built-in `Standard` 셰이더(`fileID: 46, guid: 0000...f000...`) 사용. Canopus/Yoge와 동일한 흔한 패턴 — 셰이더만 URP Lit으로 바꾸면 됨.
2. **`Materials/LowPolyWaterMaterial.mat`** — 에셋 자체 커스텀 셰이더 `Shaders/WaterShaded.shader` 사용. 단순 텍스쳐 셰이더가 아니라:
   - `GrabPass { "_RefractionTex" }`로 화면을 직접 캡처(Built-in 전용 기능, URP의 SRP Batcher와 호환 안 됨)
   - `sampler2D_float _CameraDepthTexture`를 Built-in 방식으로 직접 선언/샘플링해서 물-지형 경계를 부드럽게 페이드(shore/edge blend)
   - `_ShoreTex`를 두 번(다른 UV로) 샘플링해 곱해서 거품(foam) 패턴 생성, 시간에 따라 UV를 흘려 파도처럼 보이게 함
   - 직접 짠 Blinn-Phong 라이팅(ambient+diffuse+specular, `_LightColor0`/`_WorldSpaceLightPos0` 등 Built-in 전역 변수 사용)

   그냥 URP Lit으로 셰이더만 바꾸면 컴파일은 되지만 파도/거품/경계 페이드 등 이 에셋의 핵심 비주얼이 전부 사라지므로, URP용으로 새로 작성하는 방향으로 진행(사용자 확인받음).

   실제로 뜯어보니 `GrabPass`로 캡처한 `_RefractionTex`는 `frag()`에서 단 한 번도 샘플링되지 않는 죽은 코드였음(원래 더 고급 버전 셰이더의 흔적으로 추정 — 머티리얼에 저장된 `_GerstnerIntensity`/`_ReflectiveColorCube`/`_Caustics` 같은, 이 셰이더의 `Properties`에는 아예 없는 값들도 같이 남아있는 것과 같은 맥락). 즉 실제로 옮겨야 하는 건 **깊이 텍스쳐 기반 경계 페이드 + 거품 UV 애니메이션 + 직접 라이팅**뿐이고, 프로젝트 URP 파이프라인 에셋(`Assets/Settings/PC_RPAsset.asset`)에 이미 `m_RequireDepthTexture: 1`이 켜져 있어서 그대로 포팅 가능.

## 코드/에셋 변경

### 1) `IslandMat.mat` — URP Lit로 변환
`Assets/Material/Red.mat`(현재 프로젝트의 최신 URP Lit 머티리얼 포맷, `serializedVersion: 8`)을 템플릿으로 재작성:
- `m_Shader`를 `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체
- `_MainTex`(섬 텍스쳐, guid `c4b4ad6529883dc43874119c9045638a`) → `_BaseMap`+`_MainTex` 둘 다 매핑
- `_OcclusionMap`(guid `b9b487cdf5f0a1446986165bc650935d`) 그대로 유지
- `_Glossiness(0)` → `_Smoothness(0)`, `_Metallic(0)` 그대로, `_Color`(0.8,0.8,0.8,1) → `_BaseColor`+`_Color` 동일 매핑
- 나머지 표준 URP Lit 프로퍼티는 `Red.mat`과 동일한 기본값

### 2) `LowPolyWaterMaterial.mat` — 신규 URP 전용 셰이더로 전환
**신규 파일**: `Assets/AssetFolder/LowPolyWater_Pack/Shaders/WaterShaded_URP.shader`
- 원본 `WaterShaded.shader`(Built-in 전용)는 그대로 남겨둠(참고/향후 Built-in 복귀 대비), 새 파일로 분리
- `GrabPass` 제거(죽은 코드였으므로), `#include ".../DeclareDepthTexture.hlsl"`의 `SampleSceneDepth()`로 깊이 텍스쳐 기반 경계 페이드를 그대로 재현
- 라이팅은 `GetMainLight()`(URP) 기준으로 재작성 — 원본의 Point/Spot 라이트 분기는 이 프로젝트가 Directional Light 하나만 메인 라이트로 쓰므로 생략(단순화)
- `WATER_EDGEBLEND_ON/OFF` 멀티컴파일 분기는 제거하고 항상 켜진 상태로 고정(기존 머티리얼의 기본 활성 변형과 동일)
- `_ShoreTex` 이중 샘플링 기반 거품 생성, `_BumpTiling`/`_BumpDirection`으로 UV 애니메이션, `_InvFadeParemeter` 기반 경계 페이드, `_isInnerAlphaBlendOrColor` 토글 — 전부 원본 로직 그대로 유지
- `Blend SrcAlpha OneMinusSrcAlpha`, `ZWrite Off`, `Cull Off` 등 렌더 상태 동일 유지

`LowPolyWaterMaterial.mat`의 `m_Shader`를 이 새 셰이더로 교체. 기존에 저장돼 있던 `_BaseColor`/`_SpecColor`/`_Shininess`/`_ShoreTex`/`_InvFadeParemeter`/`_BumpTiling`/`_BumpDirection`/`_Foam`/`_isInnerAlphaBlendOrColor` 값은 전부 새 셰이더가 그대로 쓰는 프로퍼티라 원본 그대로 유지됨(원작자가 튜닝해둔 값 보존). 셰이더에 없는 나머지 저장값(Gerstner/Displacement/ReflectiveColorCube 등)은 그냥 무해하게 남아있음.

## 요약 / 영향받는 파일
- `Assets/AssetFolder/LowPolyWater_Pack/LowPoly Island/Materials/IslandMat.mat` — URP Lit로 변환
- `Assets/AssetFolder/LowPolyWater_Pack/Shaders/WaterShaded_URP.shader` (신규)
- `Assets/AssetFolder/LowPolyWater_Pack/Shaders/WaterShaded_URP.shader.meta` (신규)
- `Assets/AssetFolder/LowPolyWater_Pack/Materials/LowPolyWaterMaterial.mat` — 새 URP 셰이더로 교체

## 확인 필요 사항
셰이더 코드는 에디터에서 직접 컴파일 확인을 못 했음(텍스트 레벨 작성) — 반드시 Unity 에디터를 열어서:
1. 두 머티리얼이 핑크/에러 셰이더 없이 정상 표시되는지
2. 물 표면이 파도처럼 UV가 흐르고, 거품/해안선 경계가 원래처럼 부드럽게 페이드되는지
3. 콘솔에 셰이더 컴파일 에러가 없는지
확인 부탁. 문제가 있으면 구체적인 콘솔 에러 메시지를 알려주면 바로 고칠 수 있음.

## 비고
[[confirm_before_implementing]] — 워터 셰이더 처리 방식(URP용 새로 작성 vs 단순 교체 vs 보류)을 먼저 질문해서 확인받고 진행. IslandMat은 기존에 확립된 패턴이라 별도 확인 없이 바로 적용.
