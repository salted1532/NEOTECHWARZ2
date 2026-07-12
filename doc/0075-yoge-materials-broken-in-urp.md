# 0075 — Yoge(Stylized Nature) 에셋 머티리얼 깨짐 수정

## 질문
"yoge에 materials 깨진것좀 고쳐줘"

## 조사 내용

`Assets/AssetFolder/Yoge/Textures/Stylized - Nature/Materials/`에 있는 머티리얼 30개를 전수 확인.
원인은 [[0071-canopus-materials-broken-in-urp]]와 완전히 동일한 패턴: 전부 **Built-in RP의 Standard 셰이더**
(`m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}`)를 참조하고 있고, 프로젝트는 URP라서
호환되지 않아 마젠타/핑크로 깨져 보임.

30개 전부 구조가 동일함 (텍스쳐 GUID만 다름):
- `_MainTex`(디퓨즈, 타일링 `{2,2}`)와 `_BumpMap`(노멀맵, 타일링 `{1,1}`)만 채워져 있고, 나머지 텍스쳐 슬롯
  (Emission/Metallic/Occlusion/Detail 등)은 전부 비어있음.
- `_Glossiness: 0`, `_Metallic: 0`, `_Color`/`_EmissionColor` 전부 기본값(흰색/검정).
- `m_ShaderKeywords: _NORMALMAP` (노멀맵 사용 키워드 활성화).
- `m_LightmapFlags: 4`, `m_CustomRenderQueue: -1` — 전부 동일.

대상 파일 (30개, `Texture_N_DiffuseM.mat` 패턴):
Texture_1~15 각각 Diffuse/Diffuse2/Diffuse3 중 실제 존재하는 조합만 (총 30개).

## 제안하는 수정

Canopus 때와 동일한 방식: 30개 파일 전부 `m_Shader`를 URP `Universal Render Pipeline/Lit`
(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체하고,
- `_MainTex` → `_BaseMap`에도 동일 GUID/타일링 매핑 (기존 `_MainTex`도 유지, 하위호환)
- `_BumpMap` → 그대로 유지 (URP Lit도 동일 프로퍼티명 사용), `m_ValidKeywords: [_NORMALMAP]`로 키워드 이전
- `_Glossiness(0)` → `_Smoothness(0)`, `_Metallic(0)` 그대로, `_Color`/`_EmissionColor` 그대로
- 나머지 URP Lit 표준 프로퍼티는 프로젝트 기존 URP 머티리얼(`Assets/Material/Red.mat`)과 동일한 기본값 사용

텍스쳐 GUID는 전부 원본 그대로 보존되므로 실제 이미지/노멀맵은 바뀌지 않고, 셰이더만 URP로 교체됨.

## 적용 완료

사용자 승인 후 변환 스크립트를 실행해서 30개 `.mat` 파일 전부를 위 방식대로 다시 씀. `Texture_1_Diffuse.mat`로
결과를 확인한 결과 `_BaseMap`/`_MainTex`(디퓨즈)와 `_BumpMap`(노멀)이 원본 텍스쳐 GUID를 그대로 유지한 채
URP Lit 셰이더로 정확히 변환됨을 확인.

## 확인 필요 사항
Unity 에디터에서 Yoge(Stylized Nature) 에셋이 쓰인 씬/프리팹을 열어서 지형/식생 등이 더 이상 핑크색으로
깨지지 않고 정상 텍스쳐+노멀맵으로 보이는지 확인 부탁.
