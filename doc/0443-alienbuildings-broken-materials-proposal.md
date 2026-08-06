# 0443. AlienBuildings 깨진 머티리얼 수정

**날짜:** 2026-08-06
**상태: 적용됨** — 표준 12개 머티리얼은 URP Lit로 변환. `Custom_Spores.mat`/`Spores.shader`는
사용자 선택("그대로 둔다")에 따라 미변경.

## 요청 내용
> AlienBuildings 에셋 추가했는데 머티리얼 깨진것좀 고쳐줘

## 조사

`Assets/AssetFolder/AlienBuildings`의 머티리얼을 전부 확인. [[0071-canopus-materials-broken-in-urp]] /
[[0075-yoge-materials-broken-in-urp]] / [[0159-lowpolywater-pack-broken-materials-fix]] /
[[0434-rpg-crystals-broken-materials-investigation]]와 같은 계열의 문제 — 프로젝트는 URP인데
에셋은 Built-in RP 전용 셰이더로 제작됨.

- **`Materials/*.mat` 12개**(Birther, Brain Detail, BrainBody, Claw Body, Claw Detail,
  CreepLike, Crystal, Stomach Detail, StomachBody, Terraformer, Undercrystal,
  terraformer_detail) — 전부 Built-in `Standard` 셰이더(`fileID: 46, guid: 0000...f000...`)
  사용, 텍스쳐 슬롯(`_MainTex`/`_BumpMap`/`_MetallicGlossMap`/`_OcclusionMap`/`_EmissionMap`/
  `_ParallaxMap`)은 전부 정상 연결돼 있음(파일 자체는 안 깨짐, 셰이더 호환성 문제만).
  `Prefabs/birther.prefab`, `brain.prefab`, `claw.prefab`, `Crystals.prefab`, `stomach.prefab`,
  `terraformer.prefab`가 이 12개를 실제로 참조 — **씬에 배치하면 바로 핑크로 보임.**
- **`Shaders/Custom_Spores.mat` + `Shaders/Spores.shader`** — 에셋 제작자가 만든 커스텀
  Surface Shader(`#pragma surface surf Standard`, Built-in 전용 CGPROGRAM). 원형이 퍼져나가는
  느낌의 전파(propagation) 이펙트용(`_CirclePos`/`_CircleSize`/`_CircleBlur`로 원 모양 알파
  마스크를 그려서 크립처럼 바닥에 번지는 효과). **이 팩의 6개 프리팹 중 어느 것도 이 머티리얼을
  참조하지 않음** — 지금 당장 씬에 영향 없는 미사용 자원.

## 제안하는 변경

### 1) 표준 12개 머티리얼 → URP Lit 변환 (이 프로젝트에서 이미 3번 이상 승인받아 적용한 방식)
`Assets/Material/Red.mat`(현재 URP Lit 표준 포맷)을 템플릿으로, 각 파일의 `m_Shader`를
`Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체하고 프로퍼티를
1:1로 재배치. 텍스쳐 참조(guid)는 전부 그대로 유지 — 재할당 없음.

프로퍼티 매핑(12개 파일 공통, 텍스쳐 guid만 파일마다 다름):
- `_MainTex` → `_BaseMap` + `_MainTex` 둘 다 채움 (URP Lit이 실제로 읽는 건 `_BaseMap`)
- `_BumpMap`/`_MetallicGlossMap`/`_OcclusionMap`/`_EmissionMap`/`_ParallaxMap` → 슬롯 이름 동일,
  텍스쳐/스케일/오프셋 그대로 복사 (CreepLike는 `_MainTex`/`_EmissionMap` 타일링 18×18도 유지)
- `_Color` → `_BaseColor` + `_Color` 동일 매핑, `_EmissionColor` 그대로
- `_Glossiness` → `_Smoothness`로 값 이전(원본 0 또는 0.5), `_Metallic` 그대로(전부 0)
- `_Mode(0)` → `_Surface(0)`(Opaque), `_Cutoff`/`_BumpScale`/`_OcclusionStrength`/`_Parallax`/
  `_SmoothnessTextureChannel`/`_SrcBlend`/`_DstBlend`/`_ZWrite` 값 그대로 이전
- 나머지 URP Lit 전용 필드(`_WorkflowMode`, `_EnvironmentReflections`, `_ReceiveShadows`,
  `_Cull`, `_QueueOffset`, `_SrcBlendAlpha`/`_DstBlendAlpha` 등)는 `Red.mat` 기본값 그대로 채움

대상 파일:
- `Materials/Birther.mat`, `Brain Detail.mat`, `BrainBody.mat`, `Claw Body.mat`,
  `Claw Detail.mat`, `CreepLike.mat`, `Crystal.mat`, `Stomach Detail.mat`, `StomachBody.mat`,
  `Terraformer.mat`, `Undercrystal.mat`, `terraformer_detail.mat` (총 12개)

### 2) `Custom_Spores.mat` / `Spores.shader` — 확인 필요

이건 앞의 12개와 성격이 달라서 방식을 먼저 정해야 함([[0159-lowpolywater-pack-broken-materials-fix]]의
워터 셰이더 때와 동일한 판단 포인트 — 단순 셰이더 교체로는 핵심 이펙트가 사라짐):

- **그대로 둔다(권장)** — 현재 이 팩의 어느 프리팹도 참조하지 않는 미사용 자원이라 씬에는 영향
  없음. 프로젝트 뷰에서만 핑크로 보임.
- **URP Lit로 단순 교체** — 컴파일은 통과하지만 원형 전파(circle propagation) 이펙트 로직 전체가
  사라지고 그냥 평범한 텍스쳐 머티리얼이 됨(사실상 이 머티리얼을 쓰는 의미가 없어짐).
- **URP용 커스텀 셰이더로 새로 작성** — 원형 전파 이펙트(`_CirclePos`/`_CircleSize`/`_CircleBlur`
  알파 마스크 로직)를 유지한 채 URP Shader Graph 또는 HLSL로 이식. 지금 쓰지도 않는 자원에 들이는
  공수치고 크므로, 실제로 이 크립/전파 이펙트를 맵에 쓸 계획이 있을 때만 추천.

## 영향받는 파일
- `Assets/AssetFolder/AlienBuildings/Materials/Birther.mat` ~ `terraformer_detail.mat` (12개,
  URP Lit로 변환)
- `Assets/AssetFolder/AlienBuildings/Shaders/Custom_Spores.mat` — 사용자 선택에 따라 변경 또는
  보류

## 확인 필요 사항 → 답변
- 12개 진행: 승인.
- `Custom_Spores.mat`: "그대로 둔다"(미사용 자원이라 영향 없음) 선택 → 미변경.

## 실제 변경 (적용됨)

12개 파일 전부 동일한 규칙으로 재작성(텍스쳐 guid는 파일마다 원본 그대로 유지, 재할당 없음).
`Birther.mat` 기준 예시:

**Before:**
```yaml
Material:
  serializedVersion: 6
  m_Name: Birther
  m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
  m_ShaderKeywords: _METALLICGLOSSMAP _NORMALMAP
  m_LightmapFlags: 4
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    m_TexEnvs:
    - _BumpMap: {m_Texture: {guid: 6520205fc976f5f4397462f317b8ff29}, ...}
    - _MainTex: {m_Texture: {guid: 2b97846f20c5c5d47beaa566314056f3}, ...}
    - _MetallicGlossMap: {m_Texture: {guid: f09b5748805031e4cb8d8687129ed42a}, ...}
    m_Floats: [..., _Glossiness: 0, _Mode: 0, ...]
    m_Colors: [_Color: {1,1,1,1}, _EmissionColor: {0,0,0,1}]
```

**After:**
```yaml
Material:
  serializedVersion: 8
  m_Name: Birther
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}  # URP Lit
  m_ValidKeywords:
  - _METALLICSPECGLOSSMAP
  - _NORMALMAP
  m_LightmapFlags: 4
  stringTagMap: {RenderType: Opaque}
  disabledShaderPasses: [MOTIONVECTORS]
  m_SavedProperties:
    m_TexEnvs:
    - _BaseMap: {m_Texture: {guid: 2b97846f20c5c5d47beaa566314056f3}, ...}   # _MainTex와 동일 guid 이중 매핑
    - _BumpMap: {m_Texture: {guid: 6520205fc976f5f4397462f317b8ff29}, ...}  # 동일 유지
    - _MainTex: {m_Texture: {guid: 2b97846f20c5c5d47beaa566314056f3}, ...}
    - _MetallicGlossMap: {m_Texture: {guid: f09b5748805031e4cb8d8687129ed42a}, ...}  # 동일 유지
    m_Floats: [..., _Glossiness: 0, _Smoothness: 0, _Surface: 0, _WorkflowMode: 1, ...]  # URP Lit 표준 필드 추가
    m_Colors: [_BaseColor: {1,1,1,1}, _Color: {1,1,1,1}, _EmissionColor: {0,0,0,1}, _SpecColor: {0.2,0.2,0.2,1}]
```

나머지 11개도 동일 규칙, 파일별로 다른 부분만:

| 파일 | 특이사항 |
|---|---|
| Brain Detail / BrainBody / Claw Body / Claw Detail / Stomach Detail / StomachBody / Terraformer / terraformer_detail | 표준(BaseMap+BumpMap+MetallicGlossMap, `_Smoothness: 0`) — Birther와 동일 패턴, guid만 다름 |
| CreepLike.mat | `_MainTex`/`_EmissionMap` 타일링 18×18 유지, `_ParallaxMap` 연결 유지(`_PARALLAXMAP` 키워드 추가), `_Smoothness: 0.5` |
| Crystal.mat | `_BumpMap` 없음(원본에 없었음) → `_NORMALMAP` 키워드 제외, `_EmissionMap` 연결 + `_EmissionColor: {1.3041189, 1.3041189, 1.3041189, 1}` 유지, `_EMISSION` 키워드 추가, `m_LightmapFlags: 2` 원본값 유지(다른 11개는 4) |
| Undercrystal.mat | `_Smoothness: 0.5`(원본 `_Glossiness: 0.5`) |

URP Lit 셰이더가 실제로 읽는 키워드(`_METALLICSPECGLOSSMAP`/`_NORMALMAP`/`_EMISSION`/`_PARALLAXMAP`)는
`Assets/AssetFolder/RPG CRYSTALS/URP/Materials URP/Crystal 1.mat`(에셋 제작자가 이미 만들어둔
정상 동작 URP Lit 머티리얼)에서 실제 사용 중인 키워드 표기를 확인해서 그대로 맞춤 — Unity 에디터
없이 텍스트로만 작성했기 때문에, 키워드 이름을 추측하지 않고 프로젝트 내 이미 검증된 URP Lit
머티리얼 사례에서 가져옴.

## 영향받는 파일 (최종)
- `Assets/AssetFolder/AlienBuildings/Materials/Birther.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Brain Detail.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/BrainBody.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Claw Body.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Claw Detail.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/CreepLike.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Crystal.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Stomach Detail.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/StomachBody.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Terraformer.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/Undercrystal.mat`
- `Assets/AssetFolder/AlienBuildings/Materials/terraformer_detail.mat`
- (미변경) `Assets/AssetFolder/AlienBuildings/Shaders/Custom_Spores.mat`, `Spores.shader`

## 확인 필요 사항
Unity 에디터가 꺼져 있어 `npx uloop-cli compile`/렌더링 확인을 못 함. 에디터를 열어서
`Prefabs/birther.prefab`, `brain.prefab`, `claw.prefab`, `Crystals.prefab`, `stomach.prefab`,
`terraformer.prefab`가 핑크 없이 정상 텍스쳐로 보이는지, 콘솔에 셰이더 관련 에러/경고가 없는지
확인 부탁. 만약 일부가 여전히 흐릿하게(키워드 미적용) 보이면, 머티리얼을 한 번 클릭해서
인스펙터를 열었다가 닫으면(Unity의 ShaderGUI가 키워드를 재검증) 즉시 해결됨.
