# 0434. RPG CRYSTALS 깨진 머티리얼 수정

## 날짜
2026-08-05

## 요청
"RPG CRYSTALS폴더 안에 메테리얼이 깨진것들좀 고쳐줘"

## 조사
`Assets/AssetFolder/RPG CRYSTALS`는 에셋 스토어 팩 특성상 렌더 파이프라인별로 폴더가 3개 나뉘어 있음: `BUILT_IN/`, `URP/`, `HDRP/`. 프로젝트는 `Packages/manifest.json` 기준 URP만 설치되어 있음(HDRP 패키지 없음).

각 폴더의 `m_Shader` 참조를 확인:
- **`URP/Materials URP/Crystal 1~4.mat`** — `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`) 정상 사용. 텍스쳐 참조(`_BaseMap`, `_BumpMap`, `_MetallicGlossMap`, `_OcclusionMap`, `_EmissionMap`)도 전부 `SHARED TEXTURES/` 안의 실제 파일 guid와 일치. **문제 없음.**
- **`URP/Prefab URP/Crystal_1~50.prefab`** — 전부 위 URP 머티리얼 4개 중 하나를 정상 참조. **문제 없음.**
- **`BUILT_IN/Materials Built_in/Crystal 1~4.mat`** — Built-in RP 전용 `Standard` 셰이더(`fileID: 46, guid: 0000...f000...`) 사용 → URP 프로젝트에서는 항상 핑크로 표시됨. [[0071-canopus-materials-broken-in-urp]] / [[0075-yoge-materials-broken-in-urp]] / [[0159-lowpolywater-pack-broken-materials-fix]]와 같은 패턴.
- **`HDRP/Materials HD/Crystal 1~4.mat`**, **`HDRP/HDRP DEMO SCENE/*.mat`**(Black/White/Gold/Ground/DefaultHDMaterial) — HDRP `Lit` 셰이더(guid `6e4ae4064600d784cac1e41a9e6f2e59`) 사용 → 이 프로젝트엔 HDRP 패키지 자체가 없어서 항상 핑크.
- 어느 프리팹에도 머티리얼 슬롯이 `None`(fileID: 0)으로 빠진 진짜 "누락" 케이스는 없음. 즉 전부 "다른 파이프라인용 셰이더라 핑크로 보이는" 케이스이지, 참조가 깨져서 에러 나는 케이스가 아님.
- 이 팩의 프리팹/머티리얼은 현재 어떤 씬(`SampleScene`, `Missions/*`)이나 `Assets/prefabs`에도 아직 배치되어 있지 않음(참조 없음) — 순수하게 에셋 폴더 안에서만 존재.

## 판단 포인트
이전 사례들(Canopus/Yoge/LowPolyWater 등)과 달리, 이 팩은 **이미 정상 동작하는 URP 전용 머티리얼(`URP/Materials URP/`)이 갖춰져 있음**. `BUILT_IN`/`HDRP` 폴더는 "깨진 것"이 아니라 "이 프로젝트가 안 쓰는 파이프라인용으로 에셋 제작자가 같이 넣어준 여분 변형"이라, URP Lit로 바꿔봐야 이미 있는 `URP/` 폴더 내용을 그대로 복제하는 것밖에 안 됨.

선택지:
1. **그대로 둔다** — 프로젝트가 URP만 쓰므로 `BUILT_IN`/`HDRP` 폴더는 애초에 참조/사용할 일이 없음. 프로젝트 뷰에서 핑크로 보여도 실제 씬에 아무 영향 없음.
2. **`BUILT_IN`/`HDRP` 폴더 자체를 삭제** — 안 쓸 파이프라인 변형이므로 용량/혼동 제거. (BUILT_IN DEMO SCENE.unity, HDRP DEMO SCENE.unity, Textures HD 등 관련 파일 전부 포함해서 삭제)
3. **`BUILT_IN`/`HDRP` 머티리얼도 URP Lit로 변환** — 프로젝트 뷰에서 미리보기까지 깨끗하게 만들고 싶다면. 다만 결과물은 이미 있는 `URP/Materials URP/Crystal 1~4.mat`과 사실상 동일해짐(중복).

사용자 확인 결과 **3번(URP Lit로 변환)**으로 진행.

## 실제 변경
`HDRP/Materials HD/Crystal N.mat`는 `_BaseMap`/`_BumpMap`/`_MetallicGlossMap`/`_OcclusionMap`/`_EmissionMap` 같은 URP Lit용 텍스쳐 슬롯 자체가 저장돼 있지 않음(HDRP 전용 `_BaseColorMap`/`_MaskMap` 등만 있음). 반면 `BUILT_IN/Materials Built_in/Crystal N.mat`는 (Standard→URP Lit 자동 업그레이드 흔적으로) 이미 URP Lit용 텍스쳐 슬롯이 `URP/Materials URP/Crystal N.mat`와 동일한 guid로 다 채워져 있었고, 셰이더/키워드만 Standard로 되어 있었음.

두 경우 다 채널 리매핑을 직접 하는 대신, **이미 정상 동작 확인된 `URP/Materials URP/Crystal N.mat` 파일 내용을 그대로 복사**해서 덮어씀 (guid는 `.meta`에 있으므로 그대로 유지 → 기존에 이 파일들을 참조하던 `BUILT_IN`/`HDRP` 프리팹들의 참조는 안 깨짐):

```
cp "URP/Materials URP/Crystal N.mat" "BUILT_IN/Materials Built_in/Crystal N.mat"
cp "URP/Materials URP/Crystal N.mat" "HDRP/Materials HD/Crystal N.mat"
```
(N = 1, 2, 3, 4)

결과: `m_Shader`가 `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 바뀌고, 텍스쳐/키워드/stringTagMap이 URP 버전과 완전히 동일해짐.

**범위에서 제외한 것**: `HDRP/HDRP DEMO SCENE/*.mat`(Black/White/Gold/Ground/DefaultHDMaterial)는 그대로 둠. Crystal 텍스쳐를 안 쓰는 데모 씬 전용 머티리얼이라 복사할 URP 대응본이 없고, 어차피 `HDRP DEMO SCENE.unity` 자체가 이 프로젝트(URP, HDRP 패키지 없음)에서 정상 동작할 일이 없어서 머티리얼만 바꿔봐야 실효성이 없음.

## 요약 / 영향받는 파일
- `Assets/AssetFolder/RPG CRYSTALS/BUILT_IN/Materials Built_in/Crystal 1~4.mat` — URP Lit로 변환 (URP 버전 내용 복사)
- `Assets/AssetFolder/RPG CRYSTALS/HDRP/Materials HD/Crystal 1~4.mat` — URP Lit로 변환 (URP 버전 내용 복사)
- `HDRP DEMO SCENE` 안의 머티리얼 5개는 미변경 (스코프 제외, 위 사유 참고)

## 확인 필요 사항
Unity 에디터에서 직접 컴파일/렌더링 확인은 못 했음 — 프로젝트 열어서 `BUILT_IN`/`HDRP` 폴더의 Crystal 1~4 프리팹이 더 이상 핑크가 아니고 `URP/Prefab URP`의 동일 번호 크리스탈과 똑같이 보이는지 확인 부탁.
