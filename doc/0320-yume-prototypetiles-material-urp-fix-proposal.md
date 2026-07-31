# 0320. YuME PrototypeTiles 메테리얼 깨짐 수정

**날짜:** 2026-07-31

## 요청 내용

> yuponic폴더에 yume라는 에셋 폴더안에 PrototypeTiles의 메테리얼들이 깨져 보이거든 이것좀 수정해줘

## 조사 내용

- 대상 파일: `Assets/AssetFolder/Yuponic/YuME/PrototypeTiles/yuponicProtoTiles.mat` (PrototypeTiles 폴더 내 유일한 `.mat`. `CustomBrushes/`의 브러시 프리팹·서브메시들이 전부 이 하나의 메테리얼을 참조함)
- 원인: 이 메테리얼이 **Built-in Render Pipeline의 Standard 셰이더**를 참조 중 (`m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}`)
- 프로젝트 확인 결과 실제 사용 파이프라인은 **URP**임:
  - `ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline`이 URP 에셋을 가리킴
  - `Packages/manifest.json`: `com.unity.render-pipelines.universal: 17.4.0`
- URP 프로젝트에서 Built-in Standard 셰이더는 지원되지 않아 **분홍색(깨짐) 머티리얼**로 표시됨. "깨져 보인다"는 증상과 일치.
- 텍스처 참조(`_MainTex`, guid `5a6b6c2feaed1d74d857879e8881c4f3`) 자체는 정상 — 셰이더 파이프라인 불일치만 문제.

## 적용한 수정

사용자 확인 후, Unity Editor를 dynamic-code로 원격 조작해 Unity가 제공하는 표준 업그레이드 기능(`Edit/Rendering/Materials/Convert Selected Built-In Materials to Current SRP`, 내부적으로 `UnityEditor.Rendering.MaterialUpgraderEditMenus.UpgradeMaterialsSelection`)을 이 메테리얼 하나에 대해 실행함. 직접 프로퍼티를 손으로 매핑하는 대신 Unity 자체 컨버터가 기존 값을 그대로 옮기고 URP Lit이 기대하는 직렬화 포맷(`AssetVersion` 서브에셋 포함)까지 맞춰줌 — 프로젝트 내 다른 URP 머티리얼(`Assets/Material/White.mat` 등)과 동일한 결과.

(참고: 처음 시도한 메뉴 경로 `Edit/Rendering/Materials/Convert Selected Built-in Materials to URP`는 이 Unity/URP 버전에 존재하지 않아 실패 로그만 남기고 아무 변화 없음. 정확한 메뉴 경로로 재시도해 성공.)

### 기존 코드
```yaml
m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}
...
m_TexEnvs:
- _MainTex:
    m_Texture: {fileID: 2800000, guid: 5a6b6c2feaed1d74d857879e8881c4f3, type: 3}
...
m_Floats:
- _Glossiness: 0.398
- _Metallic: 0.073
...
m_Colors:
- _Color: {r: 1, g: 1, b: 1, a: 1}
```

### 변경 코드
```yaml
m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
...
m_TexEnvs:
- _BaseMap:
    m_Texture: {fileID: 2800000, guid: 5a6b6c2feaed1d74d857879e8881c4f3, type: 3}
...
m_Floats:
- _Glossiness: 0.398   # 레거시 키 값도 그대로 보존
- _Smoothness: 0.398   # URP Lit이 실제로 읽는 키
- _Metallic: 0.073
...
m_Colors:
- _BaseColor: {r: 1, g: 1, b: 1, a: 1}   # URP Lit이 실제로 읽는 키
- _Color: {r: 1, g: 1, b: 1, a: 1}       # 레거시 키 값도 그대로 보존
--- !u!114 &5369286361742272196
MonoBehaviour:
  ...
  m_EditorClassIdentifier: Unity.RenderPipelines.Universal.Editor::UnityEditor.Rendering.Universal.AssetVersion
  version: 10
```
(`_MainTex` 텍스처 참조는 `_BaseMap`으로 그대로 이전됨. 다른 값들은 레거시/URP 키가 함께 저장되어 그대로 보존됨)

## 검증

- `git diff --stat`: `yuponicProtoTiles.mat` 한 파일만 변경 (67줄 추가, 8줄 삭제)
- Unity Console 로그: `Upgrade to SRP Material / Upgrading material: yuponicProtoTiles using shader: Standard` 확인, 에러 없음
- Unity Editor는 변환 후에도 정상 응답 (스크린샷/로그 조회 정상)

## 영향받는 파일

- `Assets/AssetFolder/Yuponic/YuME/PrototypeTiles/yuponicProtoTiles.mat` (수정 완료)
- 이 머티리얼을 참조하는 프리팹/서브메시는 guid 참조라 별도 수정 불필요
