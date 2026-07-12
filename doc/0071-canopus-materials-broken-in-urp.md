# 0071 — Canopus-III 에셋 머티리얼이 텍스쳐 깨짐(핑크/보라)으로 보이는 원인

## 질문
Canopus-III Low Poly Sci-fi Desert Units Set을 import했는데 모든 머티리얼의 텍스쳐가 깨져서(핑크/보라색 또는 회색) 나온다. 원인이 뭔가?

## 조사 내용

1. 프로젝트는 **Universal Render Pipeline(URP)** 을 사용 중임을 확인.
   - `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline` 이 URP 에셋을 가리킴.
   - `Packages/manifest.json`: `"com.unity.render-pipelines.universal": "17.4.0"` 등록됨.
   - 프로젝트 자체 머티리얼(`Assets/Material/*.mat`, `Assets/Shader/*.mat`)은 전부
     `m_Shader: {..., guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}` — 이 GUID가 URP의
     `Universal Render Pipeline/Lit` 셰이더.

2. Canopus-III 세트의 머티리얼 10개를 전부 확인한 결과, 전부 **Built-in Render Pipeline의 레거시 셰이더**를 참조하고 있음.
   - `Assets/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_{1,2,3}/materials/mtrl_canopus-iii_set01-{red,green,blue}.mat`
     → `m_Shader: {fileID: 10, guid: 0000000000000000f000000000000000, type: 0}` (Legacy Shaders/Diffuse)
   - `Assets/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_1/materials/mtrl_canopus-iii_desert_cliffs_a.mat`
     → `m_Shader: {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}` (또 다른 Legacy 셰이더 계열)

3. 텍스쳐 자체는 정상. `mtrl_canopus-iii_set01-blue.mat`의 `_MainTex` GUID(`14b91beb45ed8504b89f82b2291c9af3`)가
   `txtr_canopus-iii_set01-blue_diff.png.meta`의 GUID와 정확히 일치함. 즉 텍스쳐 파일이 누락되거나 GUID가 깨진 게 아니라,
   **셰이더 자체가 현재 렌더 파이프라인(URP)에서 지원되지 않아서** Unity가 fallback으로 마젠타/핑크 "에러 셰이더"를 렌더링하는 것.

## 결론 (원인)

Canopus-III 에셋 패키지는 **Built-in Render Pipeline 전용 셰이더**로 제작된 오래된(2017년 무렵) 에셋이고,
현재 프로젝트는 **URP**를 쓰고 있어서 발생하는 전형적인 "머티리얼 깨짐(pink/magenta shader)" 현상. 이것은 흔한 증상으로,
텍스쳐 파일이나 import 자체가 잘못된 게 아니라 **셰이더 호환성 문제**임.

## 해결 옵션 (아직 적용 안 함 — 승인 필요)

1. **(권장) Unity 내장 머티리얼 업그레이더 사용**
   Project 창에서 아래 10개 머티리얼을 전부 선택 후:
   `Edit > Rendering > Materials > Convert Selected Built-in Materials to URP`
   - Unity가 자동으로 Built-in 프로퍼티(`_MainTex`, `_Color` 등)를 URP Lit 셰이더의 대응 프로퍼티로 매핑해서 변환.
   - 원본 텍스쳐 참조는 그대로 유지됨 (재할당 불필요).
   - 대상 파일 10개:
     - `Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_1/materials/mtrl_canopus-iii_desert_cliffs_a.mat`
     - `Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_{1,2,3}/materials/mtrl_canopus-iii_set01-red.mat`
     - `Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_{1,2,3}/materials/mtrl_canopus-iii_set01-green.mat`
     - `Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_{1,2,3}/materials/mtrl_canopus-iii_set01-blue.mat`

2. **수동으로 셰이더 재지정**
   각 `.mat`에서 셰이더를 `Universal Render Pipeline/Lit`(또는 `Simple Lit`)로 바꾸고 `_MainTex` → `_BaseMap` 등
   텍스쳐 슬롯을 직접 재연결. 10개라 번거롭고 옵션 1보다 실수 여지가 큼.

3. **프로젝트를 Built-in RP로 되돌린다** — 비권장. 프로젝트 전체가 URP 기준으로 세팅되어 있어 파급 범위가 너무 큼.

옵션 1(Unity 자동 변환)을 `.mat` 파일에 직접 적용해서 진행할지 확인 부탁.

## 적용한 변경 (승인 후 실행)

사용자가 "자동 변환 적용"을 선택하여 10개 `.mat` 파일을 직접 URP 포맷으로 재작성함
(Unity 에디터의 `Convert Selected Built-in Materials to URP`와 동일한 결과를 텍스트 레벨에서 재현).

- `m_Shader`를 `Universal Render Pipeline/Lit` (guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체.
- 기존 `_MainTex` 텍스쳐 GUID를 그대로 유지하면서 `_BaseMap`에도 동일하게 매핑 (URP Lit은 `_BaseMap`을 실제로 사용, `_MainTex`은 하위호환용으로 같이 채움).
- 기존 `_Illum`(레거시 Self-Illumin 마스크, 셰이더가 Diffuse였을 때는 사실상 미사용) 텍스쳐가 있는 8개 파일은 `_EmissionMap`에 매핑하되 `_EmissionColor`는 `{0,0,0,1}`(검정)로 두어 기존 렌더링 결과(발광 없음)를 그대로 유지. 발광 효과를 켜고 싶다면 나중에 머티리얼 인스펙터에서 `Emission` 체크 후 `_EmissionColor`를 밝게 조정하면 됨.
- `desert_cliffs_a.mat`은 `_Illum` 프로퍼티가 원래 없었으므로 `_EmissionMap`은 비워둠.
- `_BaseColor`/`_Color`는 원본과 동일하게 흰색(`1,1,1,1`) 유지 — 색조는 애초에 텍스쳐 자체에서 나옴.
- 나머지 URP Lit 표준 프로퍼티(`_Smoothness`, `_Metallic`, `_Surface`, `_WorkflowMode` 등)는 프로젝트 내 기존 URP 머티리얼(`Assets/Material/Red.mat` 등)과 동일한 기본값으로 채움.

변경된 파일 (10개):
- `Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_1/materials/mtrl_canopus-iii_desert_cliffs_a.mat`
- `Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_{1,2,3}/materials/mtrl_canopus-iii_set01-{red,green,blue}.mat`

### 확인 필요 사항
Unity 에디터를 열어서 씬/프리팹에서 해당 머티리얼들이 정상적으로(핑크색 없이) 렌더링되는지 눈으로 확인 부탁. 텍스트 레벨에서 프로젝트 내 기존 URP 머티리얼 포맷을 그대로 복제했지만, 최종 검증은 Unity 에디터에서 열어보는 것이 확실함.
