# 0148. TZ_Futuristic Panel Textures Lite 머티리얼 깨짐 수정

## 날짜
2026-07-16

## 요청
"TZ_Futuristic Panel Textures Lite 폴더 안에있는 material 파일들 텍스쳐 깨진거좀 고쳐줘"

## 조사 내용
`Assets/AssetFolder/TZ_Futuristic Panel Textures Lite/Materials/`의 머티리얼 15개를 전수 확인. 원인은 [[0071-canopus-materials-broken-in-urp]], [[0075-yoge-materials-broken-in-urp]]와 완전히 동일한 패턴: 전부 **Built-in RP의 Standard 셰이더**(`m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}`)를 참조하고 있고, 프로젝트는 URP라서 호환되지 않아 마젠타/핑크로 깨져 보임.

15개 전부 구조가 동일함 (이름과 텍스쳐 GUID만 다름):
- `_MainTex`(디퓨즈)와 `_BumpMap`(노멀맵, `Texture Normals Maps` 폴더)만 채워져 있고 나머지 텍스쳐 슬롯은 비어있음.
- `_Glossiness: 0.5`, `_Metallic: 0`, `_Color`/`_EmissionColor` 전부 기본값(흰색/검정).
- `m_ValidKeywords: [_EMISSION, _NORMALMAP]`, `m_LightmapFlags: 1`, `m_CustomRenderQueue: -1` — 전부 동일.

대상 파일 (15개):
`1panel_braced_B`, `1panel_braced_C`, `1panel_braced_D`, `1panel_braced_F`, `1panel_braced_F_dark2`, `1panel_braced_J`, `1panel_C`, `1panel_E`, `1panel_G`, `2panel_C`, `2panel_caution`, `2panel_vent`, `braced_D`, `screw_A`, `vent_circular_A`

## 제안하는 수정
`doc/0071`/`doc/0075`와 동일한 방식: 15개 파일 전부 `m_Shader`를 URP `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체하고,
- `_MainTex` → `_BaseMap`에도 동일 GUID 매핑 (기존 `_MainTex`도 유지, 하위호환)
- `_BumpMap` → 그대로 유지 (URP Lit도 동일 프로퍼티명 사용), 키워드도 그대로 이전
- `_Glossiness(0.5)` → `_Smoothness(0.5)`로 매핑(기존 `_Glossiness` 필드도 남겨둠 — 프로젝트 기존 URP 머티리얼(`Assets/Material/Red.mat`)도 이 두 필드를 같이 갖고 있어 실제 Unity 변환기와 동일한 결과)
- `_Metallic(0)`, `_Color`/`_EmissionColor`는 그대로 유지
- 나머지 URP Lit 표준 프로퍼티는 프로젝트 기존 URP 머티리얼(`Assets/Material/Red.mat`)과 동일한 기본값 사용

텍스쳐 GUID는 전부 원본 그대로 보존되므로 실제 이미지/노멀맵은 바뀌지 않고 셰이더만 URP로 교체됨.

## 영향받는 파일
`Assets/AssetFolder/TZ_Futuristic Panel Textures Lite/Materials/*.mat` 15개 전부 (수정 예정)

## 다음 단계
이대로 15개 파일에 적용해도 될지 확인 부탁 (이전 두 번과 동일한 방식이라 별다른 결정 사항은 없음).

## 적용 완료
사용자 승인 후 15개 `.mat` 파일 전부를 위 방식대로 다시 씀 — `m_Shader`를 URP Lit(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체, `_MainTex`/`_BumpMap` 텍스쳐 GUID는 원본 그대로 유지하며 `_BaseMap`에도 동일 매핑, `_Glossiness(0.5)`→`_Smoothness(0.5)` 등 나머지 프로퍼티는 `Assets/Material/Red.mat` 기준값으로 채움.

## 확인 필요 사항
유니티 에디터에서 이 에셋이 쓰인 씬/프리팹을 열어서 패널/포탑 등이 더 이상 핑크색으로 깨지지 않고 정상 텍스쳐+노멀맵으로 보이는지 확인 부탁.
