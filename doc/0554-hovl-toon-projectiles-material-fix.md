# 0554 - HOVL Toon Projectiles(Magic Pig Games/Infinity PBR) 머티리얼 깨짐 수정

## 날짜
2026-08-13

## 요청 내용
"Magic Pig Games (Infinity PBR)에서 머티리얼 깨진것좀 고쳐줘"

## 조사 내용
`Assets/AssetFolder/Magic Pig Games (Infinity PBR)/HOVL - Toon Projectiles/Materials/`에 머티리얼
3개가 있음:

| 파일 | 원래 셰이더 | 상태 |
|---|---|---|
| `Wall Mod 3.mat` | Built-in `Standard`(fileID 46) | doc/0071·0075와 동일한 "URP 미지원 Built-in 셰이더" 패턴 |
| `Wall Mod 4.mat` | Built-in `Standard`(fileID 46) | 위와 동일 |
| `Waves22cg Mod 2.mat` | 커스텀 셰이더(guid `bd6af5d5...`) | **셰이더 에셋 자체가 프로젝트에 없음**(아래 참고) |

### 셰이더 문제 - Wall Mod 3/4
doc/0071(Canopus)·doc/0075(Yoge)와 동일한 원인: Built-in RP 전용 `Standard` 셰이더를 URP 프로젝트에서
쓰고 있어서 마젠타/핑크로 깨짐.

### 부가로 발견한 더 큰 문제 - 텍스쳐/셰이더 에셋 자체가 프로젝트에 없음
셰이더를 고치기 전에 각 머티리얼이 참조하는 텍스쳐 GUID를 프로젝트 전체에서 찾아봤는데, **4개 전부
(`_BumpMap`, `_MainTex`, `_MetallicGlossMap`, `_ParallaxMap`) 어떤 `.meta` 파일에도 존재하지 않음** -
텍스쳐 이미지 파일 자체가 임포트가 안 돼 있음. `Waves22cg Mod 2.mat`이 쓰는 커스텀 셰이더도 프로젝트
어디에도 없음(`Assets`에서 `.shader` 파일 검색 결과 이 패키지엔 아예 `Shaders` 폴더가 없음).

`HOVL - Toon Projectiles` 폴더 구조를 확인해보니 `Materials`/`Particles`/`Prefabs`/`Projectiles`/
`Data Objects`/`Scene`만 있고 **`Textures`/`Shaders` 폴더가 통째로 없음** - 원본 에셋 패키지를 가져올 때
이 두 폴더가 빠진 채로 임포트된 것으로 보임. 즉 이번 문제는 "셰이더 호환성" 문제(Canopus/Yoge 케이스)와
"텍스쳐/셰이더 에셋 자체가 프로젝트에 없음" 문제가 겹쳐 있음.

## 적용한 수정 - Wall Mod 3/4 셰이더 변환
Unity의 실제 URP 머티리얼 업그레이더 API(`UnityEditor.Rendering.MaterialUpgrader.Upgrade()` -
`Edit > Rendering > Materials > Convert Selected Built-in Materials to URP` 메뉴가 내부적으로 쓰는
바로 그 API)를 `uloop-cli execute-dynamic-code`로 직접 호출해서 변환 - 텍스트 레벨에서 YAML을 손으로
재현하는 대신 Unity가 실제로 쓰는 변환 로직을 그대로 실행함(속성 매핑/키워드 처리 모두 Unity가 보장).

`m_Shader`가 `Universal Render Pipeline/Lit`로 정확히 바뀌었고, `_MainTex`(있었다면)→`_BaseMap`,
`_Color`→`_BaseColor`, `_Glossiness`→`_Smoothness` 등 표준 매핑이 적용됨. **단, 위에서 확인했듯 원래
`_MainTex` 자체가 깨진 참조(존재하지 않는 텍스쳐)였어서, 변환 후에도 `_BaseMap`은 빈 채로 남음** -
셰이더는 이제 정상(더 이상 핑크 아님)이지만, 텍스쳐가 없어서 그냥 `_BaseColor` 단색으로만 보임.
`_BumpMap`/`_MetallicGlossMap`/`_ParallaxMap`은 (원래도 깨진 참조였지만) 속성명이 Built-in Standard와
URP Lit에서 동일해 그대로 유지됨 - 여전히 깨진 참조 상태.

## 추가 수정 (2026-08-13) - Waves22cg Mod 2.mat도 요청받아 처리
"아직도 깨지네"라는 후속 요청으로, 커스텀 셰이더를 복원할 수는 없지만(원본 파일 자체가 프로젝트에 없음)
**최소한 깨진 채(마젠타)로 두지 않도록** 대체 처리함. 실제로 `mat.shader.name`을 확인해보니 원래 값은
`Hidden/InternalErrorShader`였음 - 이게 바로 "깨짐" 현상의 정체(참조하던 셰이더 guid가 프로젝트에 없어서
Unity가 내부 에러 셰이더로 강제 대체한 상태).

프로젝트에 이미 있는 URP 코어 셰이더 `Universal Render Pipeline/Unlit`로 교체 - Flow/Distortion/Depth
등 원본 전용 프로퍼티는 되살릴 수 없지만, 원래 `_Color`(파란색)를 그대로 살리고 반투명 Additive
블렌드(`_Surface: Transparent`, `_Blend: Additive`, `_ZWrite: 0`, `_Cull: Off`)로 설정해서 "에너지
웨이브" 느낌의 반투명 파란 발광 이펙트처럼 보이게 함 - Unity 실제 API(`BaseShaderGUI.SetupMaterialBlendMode`)로
블렌드/큐/키워드까지 정확히 세팅해서 더 이상 깨진 셰이더가 아님.

**한계**: 원본의 Flow맵 기반 왜곡, 깊이 기반 소프트 파티클, 노이즈 흔들림 등 "웨이브" 고유의 움직이는
효과는 재현 안 됨 - 그냥 고정된 반투명 파란 판으로 보임. 원본 셰이더/텍스쳐를 구해서 다시 넣으면 그 때
원래 효과로 되돌릴 수 있음(현재 구조는 그대로 둔 채 셰이더만 교체한 것이라, 나중에 원본 셰이더 에셋만
다시 임포트하고 이 머티리얼의 셰이더를 다시 그걸로 지정하면 `_Flow`/`_Mask` 등 기존 텍스쳐 참조는 이미
GUID로 남아있어 자동으로 복원됨).

## 확인이 필요한 부분 (더 진행하려면 필요)
1. **`Wall Mod 3/4`의 텍스쳐(디퓨즈/노멀/메탈릭글로스/패럴랙스)와 `Waves22cg Mod 2`의 커스텀 셰이더는
   제가 만들어낼 수 없음** - 원본 "HOVL - Toon Projectiles" 에셋 패키지(Unity Asset Store/Package
   Manager)에 있는 `Textures`/`Shaders` 폴더를 다시 가져와야 함. 원본 패키지 파일을 갖고 계시면 그
   폴더들만 다시 import해주시면 지금 연결해둔 셰이더/구조 그대로 텍스쳐가 살아남.
2. 원본 패키지를 구할 수 없다면: `Wall Mod 3/4`는 지금처럼 단색으로 쓰거나 프로젝트에 이미 있는 다른
   텍스쳐로 대체할 수 있음 - 원하시면 알려주세요.

## 컴파일 확인
C# 스크립트 변경 없음(머티리얼 에셋만 수정) - `npx uloop-cli compile`은 해당 없음. Unity 에디터에서
`Wall Mod 3/4.mat` 인스펙터를 열어 셰이더가 `Universal Render Pipeline/Lit`로 정상 표시되는지, 텍스쳐
슬롯이 비어있는지(예상된 상태) 확인 가능.

## 영향받는 파일
- 변경: `Assets/AssetFolder/Magic Pig Games (Infinity PBR)/HOVL - Toon Projectiles/Materials/Wall Mod 3.mat`
- 변경: `Assets/AssetFolder/Magic Pig Games (Infinity PBR)/HOVL - Toon Projectiles/Materials/Wall Mod 4.mat`
- 변경 없음: `Waves22cg Mod 2.mat` (셰이더 자체가 없어 보류)
