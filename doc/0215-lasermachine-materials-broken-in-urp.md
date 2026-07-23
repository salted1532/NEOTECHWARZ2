# 0215 — LaserMachine 에셋 머티리얼 깨짐 조사 및 수정 제안

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **조사 결과 + 제안 수정**만 담고
> 실제 `.mat` 파일은 아직 건드리지 않았다. 검토 후 적용 여부를 알려주면 그때 반영한다.

## 1. 요청
"LaserMachine이라는 에셋을 추가했는데 에셋폴더 안에 사용되는 메테리얼중에서 깨진것들을 확인해보고 고쳐줘"

## 2. 결론 먼저

`Assets/AssetFolder/LaserMachine/Demo/Materials/`의 머티리얼 **4개 전부**가
[[0071-canopus-materials-broken-in-urp]], [[0075-yoge-materials-broken-in-urp]], [[0107-effectexamples-broken-materials-investigation]]와
동일한 패턴으로 깨져 있다: 전부 **Built-in Render Pipeline 전용 셰이더**를 참조하는데 프로젝트는 **URP**라서 마젠타/핑크로 렌더링된다.

- `Laser_RED.mat`, `Laser_BLUE.mat`
- `Sparks_RED.mat`, `Sparks_BLUE.mat`

## 3. 조사 내용

### 3.1 공통 증상
4개 파일 전부 동일하게
`m_Shader: {fileID: 10755, guid: 0000000000000000f000000000000000, type: 0}`
를 참조한다. 이 guid(`...f000...`)는 Built-in RP 기본 제공 리소스(Editor 내장 셰이더 번들)를 가리키는 고정 guid이고,
프로젝트 전체 `.mat` 파일을 전수 검색(`m_Shader:` 전체 grep)해봐도 **fileID 10755를 쓰는 파일은 이 4개뿐**이었다.
반면 프로젝트 자체 머티리얼과 이미 URP로 정상 동작 중인 다른 에셋(AOSFogWar 등)은 전부
`guid: 933532a4fcc9baf4fa0491de14d08ed7` (Universal Render Pipeline/Lit) 같은 URP 셰이더 guid를 쓴다.

또한 같은 fileID 10755가 성격이 전혀 다른 두 종류의 머티리얼(불투명 PBR인 Laser vs 파티클 셰이더인 Sparks)에
동일하게 박혀 있다는 점도, 이 값이 애초에 에셋 제작 당시 export 과정에서 깨진(혹은 Built-in RP에서도 유효하지 않은) 값이라는 정황이다.

### 3.2 Laser_RED.mat / Laser_BLUE.mat — 원래 셰이더 = Standard
프로퍼티 구성(`_BumpMap`, `_MetallicGlossMap`, `_Glossiness`, `_Metallic`, `_Mode`, `_SrcBlend`/`_DstBlend`/`_ZWrite`, `_Color`/`_EmissionColor`, 키워드 `_EMISSION`)이
프로젝트 내 이미 정상 동작하는 `Assets/AssetFolder/Yuponic/YuME/PrototypeTiles/yuponicProtoTiles.mat`,
`Assets/AssetFolder/Matthew Guz/Select Character/Materials/Floor/Floor.mat` (둘 다 `fileID: 46` = Built-in **Standard** 셰이더)와
구조가 완전히 동일함. `_Mode: 0`(Opaque), `_SrcBlend:1/_DstBlend:0/_ZWrite:1` 값도 Opaque 셰이더 값과 일치.

- `Laser_RED`: `_Color {r:1, g:0.0735, b:0.0735, a:1}`, `_EmissionColor {r:1,g:0,b:0,a:1}`, `_Metallic:1`, `_Glossiness:0`
- `Laser_BLUE`: `_Color {r:0.0745, g:0.3593, b:1, a:1}`, `_EmissionColor {r:1,g:0,b:0,a:1}` (⚠️ 파란 레이저인데 EmissionColor가 빨강으로 박혀 있음 — 원본 에셋 데이터 자체의 값이고 셰이더 문제와는 무관. 셰이더만 고치면 이 값 그대로 유지됨. 원한다면 별도로 파란색 계열로 바꿔줄 수 있음), `_Metallic:1`, `_Glossiness:0`

### 3.3 Sparks_RED.mat / Sparks_BLUE.mat — 원래 셰이더 = Particles/Standard Surface
프로퍼티 구성에 Standard의 PBR 필드(`_Metallic`, `_Glossiness`, `_GlossyReflections`, `_SpecularHighlights` 등)와
파티클 전용 필드(`_ColorMode`, `_SoftParticlesEnabled`, `_CameraFadingEnabled`, `_DistortionEnabled`, `_FlipbookMode`, `_LightingEnabled`, `_EmissionEnabled`, `_BlendOp`)가
전부 섞여 있음 — 이건 Built-in RP의 레거시 **`Particles/Standard Surface`** 셰이더(Standard 서페이스 셰이더 + 파티클 인스펙터 확장) 특유의 프로퍼티 조합.

URP 패키지 캐시(`Library/PackageCache/com.unity.render-pipelines.universal@.../Editor/Tools/MaterialUpgrader/MaterialUpgraderDefinitions.cs`)에서
Unity 공식 `ParticleUpgrader` 소스를 직접 확인:
- 이름에 "Unlit"이 없는 Particle 셰이더는 → `Universal Render Pipeline/Particles/Lit`로 변환하고 `_Glossiness`→`_Smoothness`도 리네임.
- `Universal Render Pipeline/Particles/Lit` 셰이더 소스(`Shaders/Particles/ParticlesLit.shader`)를 직접 열어 프로퍼티 목록을 대조: `_MetallicGlossMap`/`_Metallic`/`_Smoothness`/`_BumpMap`/`_ColorMode`/`_SoftParticlesEnabled`/`_CameraFadingEnabled`/`_DistortionEnabled`/`_FlipbookBlending` 전부 존재 — Sparks 머티리얼의 값들과 정확히 대응됨. guid는 `.shader.meta`에서 직접 확인: `b7839dad95683814aa64166edc107ae2`.
- `_Mode: 4`("add")는 `ParticleUpgrader.UpdateSurfaceBlendModes`의 `case 4: // add` 분기와 일치 → `_Surface = Transparent(1)`, `_Blend = Additive(2)`, `_SURFACE_TYPE_TRANSPARENT` 키워드 활성화.
- `BaseShaderGUI.SetupMaterialBlendModeInternal`의 Additive 분기 공식(`srcBlendRGB=SrcAlpha, dstBlendRGB=One`)을 대입해보면 원본에 이미 저장된 `_SrcBlend:5(SrcAlpha)/_DstBlend:1(One)`와 **정확히 일치** — 즉 이 값들은 그대로 재사용 가능 (변환 로직상 Particle 업그레이더는 SrcBlend/DstBlend를 새로 계산하지 않고 기존 값을 그대로 둠).
- `_ColorMode` 키워드(`_COLORCOLOR_ON` 등)와 `_FADING_ON`, `_EMISSION` 키워드는 URP 파티클 셰이더에도 **동일한 이름**으로 존재(`#pragma shader_feature_local_fragment _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON` 등, 셰이더 소스에서 직접 확인) → 그대로 유지 가능. 다만 레거시 전용 키워드인 `_ALPHABLEND_ON`은 URP 셰이더엔 없으므로 제거하고 `_SURFACE_TYPE_TRANSPARENT`로 대체해야 함.

- `Sparks_RED`: `_ColorMode:4`(→ `_COLORCOLOR_ON` 키워드), `_Color {r:0.981,g:0,b:0,a:1}`, `_EmissionColor {r:1,g:0,b:0,a:1}`
- `Sparks_BLUE`: `_ColorMode:0`(키워드 없음), `_Color {r:0,g:0.0727,b:0.783,a:1}`, `_EmissionColor {r:0,g:0.1223,b:1,a:1}`

## 4. 제안하는 수정

### Laser_RED.mat / Laser_BLUE.mat
- `m_Shader` → `{fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}` (`Universal Render Pipeline/Lit`)
- `_Mode`→`_Surface: 0`(Opaque), 키워드 변경 없음(투명 아님)
- `_MainTex`→`_BaseMap`(빈 텍스쳐 유지), `_Color`→`_BaseColor`(원본 색상 그대로)
- `_GlossyReflections`→`_EnvironmentReflections`(원본 값 1 그대로)
- `_Glossiness:0` → `_Smoothness:0`, `_Metallic:1` 그대로, `_EmissionColor` 원본 그대로, 키워드 `_EMISSION` 유지
- 나머지 URP Lit 표준 프로퍼티(`_Surface`, `_WorkflowMode:1`, `_SrcBlend:1`/`_DstBlend:0`/`_ZWrite:1`, `_Cull:2` 등)는
  프로젝트 기존 URP 머티리얼 `Assets/Material/Red.mat`과 동일한 기본값 사용 (0071 때와 동일 방식)
- `m_CustomRenderQueue: -1` 유지 (Opaque 기본 큐)

### Sparks_RED.mat / Sparks_BLUE.mat
- `m_Shader` → `{fileID: 4800000, guid: b7839dad95683814aa64166edc107ae2, type: 3}` (`Universal Render Pipeline/Particles/Lit`)
- `_Mode`→`_Surface: 1`(Transparent), `_Blend: 2`(Additive) 추가
- `_MainTex`→`_BaseMap`(빈 텍스쳐 유지), `_Color`→`_BaseColor`, `_FlipbookMode`→`_FlipbookBlending`(둘 다 값 0)
- `_Glossiness`→`_Smoothness`(둘 다 0.5 그대로)
- `_ColorMode`, `_SoftParticlesEnabled`, `_CameraFadingEnabled`, `_DistortionEnabled`, `_DistortionBlend`, `_DistortionStrength`, `_DistortionStrengthScaled`, `_CameraNear/FarFadeDistance`, `_SoftParticlesNear/FarFadeDistance`, `_Metallic`, `_BlendOp`, `_SrcBlend:5`/`_DstBlend:1`(원본 값 그대로 — 위 계산에서 이미 Additive 공식과 일치 확인됨), `_ZWrite:0`, `_Cull:2`, `_EmissionColor` 전부 원본 값 그대로 유지
- 셰이더 키워드: 기존 `_ALPHABLEND_ON` 제거, `_SURFACE_TYPE_TRANSPARENT` 추가. `_EMISSION`, `_FADING_ON`은 유지. `Sparks_RED`만 `_COLORCOLOR_ON` 유지(`Sparks_BLUE`는 원래도 없었음)
- URP Lit 셰이더에 없는 레거시 전용 프로퍼티(`_GlossyReflections`, `_SpecularHighlights`, `_OcclusionStrength`, `_Parallax`, `_DetailNormalMapScale`, `_SmoothnessTextureChannel`, `_UVSec`, `_LightingEnabled`)는 새 셰이더 프로퍼티 목록에 없으므로 정리(제거)
- `m_CustomRenderQueue: 3000`(Transparent) — 0107 때와 동일한 처리

텍스쳐는 4개 파일 전부 원래도 비어 있었으므로(스프라이트 미할당, 색+발광만 사용하는 순수 컬러 파티클/빔) 이번 수정으로 새로 깨지거나 바뀌는 텍스쳐는 없음.

## 5. 적용 완료

사용자 승인 후 4절 내용대로 4개 `.mat` 파일 전부를 직접 재작성함:

- `Laser_RED.mat`, `Laser_BLUE.mat` → `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`), Opaque. `_BaseColor`/`_EmissionColor`는 원본 값 그대로, `_Smoothness:0`/`_Metallic:1`/`_EnvironmentReflections:1`로 매핑, `m_ValidKeywords: [_EMISSION]`.
- `Sparks_RED.mat`, `Sparks_BLUE.mat` → `Universal Render Pipeline/Particles/Lit`(guid `b7839dad95683814aa64166edc107ae2`), Transparent/Additive(`_Surface:1`,`_Blend:2`). `_SrcBlend:5`/`_DstBlend:1`/`_ZWrite:0` 등 기존 파티클 관련 값 전부 유지, `_Glossiness`→`_Smoothness`/`_FlipbookMode`→`_FlipbookBlending`/`_Color`→`_BaseColor` 매핑, `m_ValidKeywords`에 `_EMISSION`/`_FADING_ON`/`_SOFTPARTICLES_ON`/`_SURFACE_TYPE_TRANSPARENT`(+RED만 `_COLORCOLOR_ON`) 설정, `m_CustomRenderQueue: 3000`.
- 새 URP 셰이더 포맷(serializedVersion 8, `m_ValidKeywords`/`m_InvalidKeywords` 배열, AssetVersion MonoBehaviour 블록 포함)은 이 저장소에서 이미 실제 변환된 `mtrl_canopus-iii_set01-red.mat`(Lit)와 `FlameRoundYellowParticle.mat`(Particles)의 실제 구조를 그대로 템플릿으로 재사용함.
- 레거시 전용 프로퍼티(`_GlossyReflections`, `_SpecularHighlights`, `_OcclusionStrength`, `_Parallax` 등)는 URP 셰이더 프로퍼티 목록엔 없지만, 기존 변환 사례(`FlameRoundYellowParticle.mat`)와 동일하게 고아(orphan) 값으로 그대로 남겨둠 — Unity가 알아서 무시하며 실제 변환 결과와 동일한 패턴.
- `Laser_BLUE.mat`의 `EmissionColor`가 빨간색으로 박혀있는 원본 데이터 값은 이번 수정 범위 밖이라 그대로 둠(원하면 별도로 파란 계열로 바꿔줄 수 있음).

## 6. 확인 필요 사항
Unity 에디터에서 `DemoScene2D`/`DemoScene3D`를 열어 레이저 빔과 스파크 파티클이 핑크색 없이 의도된 색(빨강/파랑)으로,
스파크는 Additive 발광 파티클로 정상 렌더링되는지 확인 부탁. 텍스트 레벨에서 Unity 공식 변환 로직을 그대로 재현했지만
최종 검증은 에디터에서 직접 보는 것이 확실함.

## 7. 되돌림 (2026-07-23)

사용자가 에디터에서 확인한 결과, 이 수정과는 **별개의 문제**가 있었음이 드러남
(레이저 빔 실린더 자체가 애초에 머티리얼이 할당 안 되어 있었고, `Laser_BLUE.mat`의 `EmissionColor`가 원래도 빨간색으로 박혀 있던 문제 — 자세한 내용은 [[0216-lasermachine-cylinder-missing-material-and-emission-color]] 참고).
혼란을 피하기 위해 사용자 요청으로 4개 `.mat` 파일을 이 문서 4절 수정 전 원본 상태(Built-in `fileID: 10755` 셰이더 참조, 핑크로 깨지는 상태)로 완전히 되돌림.
이 문서의 4절 수정안 자체(셰이더 매핑 로직)는 여전히 유효하지만, 실제 적용은 0216에서 새로 파악된 문제까지 함께 정리한 뒤 다시 진행하기로 함.
