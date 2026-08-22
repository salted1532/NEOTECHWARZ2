# 0658 - LowPoly Environment Pack 머티리얼 깨짐(마젠타) 수정

## 요청
"LowPoly Environment Pack 폴더에 머티리얼이 깨진것좀 고쳐줘"

## 원인
- 프로젝트는 URP(Universal Render Pipeline 17.4.0, `ProjectSettings/GraphicsSettings.asset`의 `m_CustomRenderPipeline`으로 확인)를 사용 중.
- `Assets/AssetFolder/LowPoly Environment Pack/FBX/Materials/` 안의 머티리얼 39개(Brown/Gray/Green/Orange/Pink/Purple/Yellow 시리즈)가 전부 Built-in "Standard" 셰이더(`m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000}`)를 참조하고 있었음. URP는 Built-in Standard 셰이더를 렌더링하지 못해 에디터/게임 화면에서 마젠타(핑크)로 깨져 보이는 전형적인 케이스.
- 39개 전부 텍스처 없이 `_Color`(단색) + `_Metallic`/`_Glossiness`만 쓰는 플랫 컬러 머티리얼이었고(`_MainTex` 등 모든 텍스처 슬롯이 비어있음, `_Mode: 0` = Opaque, `_EmissionColor`도 전부 검정) - 셰이더만 갈아끼우면 되는 단순 케이스.
- 같은 폴더의 `Demo/Skybox.mat`(Skybox/Procedural, fileID 106)은 Built-in 셰이더지만 URP에서도 정상 렌더링되는 종류라 대상에서 제외.

## 수정
`uloop execute-dynamic-code`로 에디터에서 직접 39개 머티리얼을 순회하며 셰이더 교체 + 프로퍼티 재매핑:
```csharp
Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
mat.shader = urpLit;
mat.SetColor("_BaseColor", 기존 _Color 값);
mat.SetFloat("_Metallic", 기존 _Metallic 값);
mat.SetFloat("_Smoothness", 기존 _Glossiness 값);
mat.SetFloat("_Surface", 0); // Opaque
mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Back);
```
(URP 공식 "Convert Selected Built-in Materials to URP" 메뉴 항목을 먼저 시도했으나 이 Unity/URP 버전에는 해당 메뉴 경로가 없어 실행되지 않았음 - 텍스처가 전혀 없는 단순 케이스라 직접 프로퍼티 매핑으로 대체.)

## 결과
- `Assets/AssetFolder/LowPoly Environment Pack/FBX/Materials/*.mat` 39개 전부 `m_Shader`가 URP Lit(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 교체됨, `_BaseColor`에 기존 색상 값 보존 확인(예: Green.1 → `{r: 0.126, g: 0.468, b: 0.037, a: 1}`).
- 폴더 내 Built-in Standard 셰이더 참조 0개로 확인.
- `uloop compile` 결과 에러 0, 경고 0.

## 범위 밖
- `Demo/Skybox.mat` - 정상 렌더링되는 것으로 판단해 건드리지 않음.
- 프로젝트 내 다른 에셋 폴더(다른 3rd-party 패키지 등)의 유사한 Standard 셰이더 머티리얼 - 이번 요청 범위(LowPoly Environment Pack)만 처리.
