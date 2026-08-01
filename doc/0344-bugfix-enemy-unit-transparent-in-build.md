# 0344 — 버그수정(조사): 적 유닛(헤비탱크/브루트 메크)이 빌드에서만 투명하게 보임

**날짜:** 2026-07-31

## 요청 내용

"Samplescene에서 빌드하면 적유닛, 헤비탱크랑 브루트 메치가 투명하게 보이는데 왜그런건지 확인하고 고쳐줘"

## 조사 내용

### 1. 정적 파일(YAML) 분석

- 대상 유닛 확인: `Assets/prefabs/OC/Unit/Tier2/Heavy Assault Tank.prefab`, `Brute Mech.prefab` — 둘 다 루트에 `EnemyUnitController`가 붙어있는 실제 적 유닛 프리팹.
- 두 프리팹 모두 실제 3D 모델은 **중첩 프리팹**으로 들어있음:
  - Heavy Assault Tank → `Assets/prefabs/Asset/OC/unit_Tank_Heavy_B_yup.prefab`
  - Brute Mech → `Assets/prefabs/Asset/OC/unit_Quad_B_yup.prefab`
  - 둘 다 머티리얼로 `Assets/prefabs/Asset/OC/mtrl_canopus-iii_set01-red.mat` (guid `332ceb5b538887a4c85dac53a7f405a6`)를 씀.
- 이 머티리얼을 열어본 결과: `m_Shader`가 URP `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`)로 **이미 정상 설정**되어 있음. `_Surface: 0`(Opaque), `_AlphaClip: 0`, `_ZWrite: 1`, `_BaseColor.a: 1` — [[0071-canopus-materials-broken-in-urp]]에서 발견됐던 "Built-in RP 셰이더가 URP에서 깨짐" 패턴이 **아님**.
- 비교군으로 같은 Tier2의 정상 유닛 **Ironhawk**를 확인: 실제 모델은 `Assets/prefabs/Asset/OC/unit_Tank_Missile_B_yup red.prefab`, 머티리얼은 `Assets/AssetFolder/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_3/materials/mtrl_canopus-iii_set01-red.mat`(guid `e289395f8d7855f4b930b8a1716ec9cb`) — 이건 [[0071-canopus-materials-broken-in-urp]]에서 **이미 수정 완료한 파일 그 자체**.
- 두 머티리얼 파일(Heavy Tank/Brute Mech가 쓰는 것 vs Ironhawk가 쓰는 것)을 바이트 단위로 비교 — **완전히 동일한 내용**(셰이더, 텍스쳐 GUID, 키워드(`_EMISSION`), 색상 전부 일치). 서로 다른 GUID를 가진 별개의 에셋 파일이지만 내용은 복제본.

### 2. Unity 에디터에서 실제 런타임 오브젝트 동적 조사

SampleScene을 열어 씬에 배치된 Heavy Assault Tank/Brute Mech/Ironhawk 인스턴스를 직접 코드로 순회하며 확인:
- 세 유닛 모두 루트 컴포넌트 구성 동일 (`EnemyUnitController`, `HealthManager`, `UnitEffects`, `UnitAudio` 등) — 프리뷰/고스트/알파 페이드 관련 컴포넌트는 없음.
- 실제 3D 모델의 Renderer 전부 `enabled = true`, `shader.isSupported = true`, `renderQueue = 2000`(Opaque), `_Surface = 0`, `_BaseColor.a = 1` — **세 유닛 사이에 차이 없음.**

### 3. 결론

정적 분석과 에디터 동적 조사 양쪽 다 "씬/프리팹/머티리얼 설정 자체는 문제없다"로 수렴했다. 즉 **에디터에서는 재현되지 않고, 빌드에서만 재현되는 유형의 버그**로 판단된다. 이 패턴(에디터에서 보이는 모든 값이 정상인데 빌드에서만 특정 오브젝트가 안 보이거나 비정상적으로 렌더링됨)의 가장 흔한 원인은 **URP의 셰이더 변형(variant) 스트리핑**이다.

- `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`에서 `m_StripUnusedVariants: 1`이 켜져 있음을 확인 — 빌드 시 "실제로 쓰이는 것으로 감지된" 셰이더 키워드 조합만 남기고 나머지는 제거하는 기본 설정.
- Heavy Tank/Brute Mech가 쓰는 `Assets/prefabs/Asset/OC/mtrl_canopus-iii_set01-red.mat`은 (Ironhawk가 쓰는, 이미 검증된 원본과) **완전히 동일한 내용이지만 별도 GUID를 가진 복제 파일**이다. 이 파일이 [[0071-canopus-materials-broken-in-urp]] 방식대로 텍스트 레벨에서 직접 작성된 것으로 보이는데(에디터의 "Convert to URP" 메뉴가 아니라 파일을 직접 재구성), Unity 에디터의 실제 임포트 파이프라인을 거치지 않고 만들어진 자산은 에디터 상에서 보이는 값은 정상이어도, 빌드용 셰이더 스트리핑이 이 자산의 키워드 사용 여부를 다른 경로로(에셋 자체가 아니라 어떤 씬/컴포넌트에서 실제로 그 키워드 조합을 "사용 중"이라고 스캔했는지 여부로) 판단하는 과정에서 누락됐을 가능성이 있다.

에디터 내부 조사만으로는 이 이상 확정할 수 없다(실제 빌드를 만들어 셰이더 스트리핑 로그를 봐야 100% 확증 가능) — 아래 수정안은 **원인 메커니즘에 대한 근거 있는 가설에 기반한, 부작용 없는 표준 대응**이다.

## 수정안 (제안 — 아직 미적용)

### 방법: Graphics Settings의 "Always Included Shaders"에 URP Lit 셰이더 명시적으로 추가

`ProjectSettings/GraphicsSettings.asset`의 `m_AlwaysIncludedShaders` 목록에 `Universal Render Pipeline/Lit`(guid `933532a4fcc9baf4fa0491de14d08ed7`) 항목을 추가한다.

기존 코드:
```yaml
  m_AlwaysIncludedShaders:
  - {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15104, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15105, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15106, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10753, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10770, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10783, guid: 0000000000000000f000000000000000, type: 0}
```

변경 코드:
```yaml
  m_AlwaysIncludedShaders:
  - {fileID: 7, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15104, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15105, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 15106, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10753, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10770, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 10783, guid: 0000000000000000f000000000000000, type: 0}
  - {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
```

**왜 이게 안전한가:** 이 목록은 순수하게 "빌드에 무조건 포함시킬 셰이더" 지정이라 게임 로직/에디터 동작에는 영향이 없다 — 에디터에서는 이미 모든 셰이더가 로드 가능하므로 아무 차이도 없고, 빌드 결과물의 셰이더 데이터가 조금 더 포함되는 것뿐이다(빌드 용량이 아주 약간 늘 수 있음). 프로젝트 전체에서 이미 가장 많이 쓰이는 셰이더(대부분의 유닛/건물이 URP Lit 계열)라 이걸 강제 포함해도 기능적으로 달라지는 것은 없다.

### 확인이 필요한 부분

이 수정은 **가설에 근거한 조치**임을 분명히 밝힌다 — 에디터 안에서는 문제를 재현할 방법이 없어서(빌드 전용 버그), 100% 확증은 실제로 빌드해서 확인하는 것뿐이다. 적용 후 빌드해서 Heavy Assault Tank/Brute Mech가 정상으로 보이는지 확인 부탁드리며, 만약 이 조치로도 안 고쳐지면 다음 후보로:
1. `Assets/prefabs/Asset/OC/mtrl_canopus-iii_set01-red.mat`을 에디터에서 직접 열어 한 번 저장(재직렬화)해서 임포트 파이프라인을 강제로 다시 태우는 방법
2. 프로젝트 세팅의 `m_StripUnusedVariants`를 임시로 꺼서 스트리핑 자체를 비활성화하고 빌드해 재현 여부를 비교하는 방법
을 시도해볼 수 있다.

## 요약 / 영향받는 파일

- `ProjectSettings/GraphicsSettings.asset` (1개 파일, `m_AlwaysIncludedShaders` 목록에 항목 1개 추가)
- 게임플레이 스크립트/프리팹/씬은 전혀 건드리지 않음.

## 추가: 진단 로그 (2026-08-01)

Always Included Shaders 수정이 실제로 효과가 있었는지, 아니면 다른 원인(유닛이 땅속에 박힘/메쉬 미로드 등)인지
디밸로퍼 빌드에서 직접 확인할 수 있도록 `EnemyUnitController`에 스폰 시점 진단 로그를 추가했다 (원인이
확정되면 삭제해도 되는 임시 코드).

- **`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`**: `Start()` 끝에서 `LogSpawnDiagnostics()` 호출 추가.
  - **위치 진단**: 유닛 위치에서 아래로 레이캐스트해서 바닥까지의 실제 거리(`deltaY`)를 로그로 남김 —
    "땅속에 박힘" 가설을 곧바로 확인 가능.
  - **렌더러/메쉬/셰이더 진단**: 자식의 모든 `Renderer`를 순회하며 `enabled`, 바운드, 메쉬(`SkinnedMeshRenderer`/
    `MeshFilter`의 `sharedMesh`가 null인지·정점 수), 각 머티리얼의 셰이더 이름/`isSupported`/`renderQueue`를
    로그로 남김 — "메쉬 미로드"·"셰이더 스트리핑" 두 가설 모두 이 로그 한 번으로 구분 가능.
  - Heavy Assault Tank/Brute Mech뿐 아니라 모든 적 유닛에 동일하게 적용되므로, 정상으로 보이는 Ironhawk 등과
    로그를 나란히 비교해서 차이를 바로 확인할 수 있다.
  - **빌드에서 로그 확인 위치**: `Debug.Log`는 `Player.log`에 쌓인다 — Windows 기본 경로는
    `%USERPROFILE%\AppData\LocalLow\<회사명>\<제품명>\Player.log`. 테스트 후 이 파일에서 `[UnitDiag]`로
    검색하면 됨.

## 최종 결론: 진짜 원인은 셰이더 스트리핑이 아니었음 (2026-08-01, 사용자 직접 발견)

사용자가 SampleScene을 직접 조사해서 진짜 원인을 찾았다: mission 맵 지형 아래에 **testmap이 겹쳐서 남아있었고**,
NavMesh가 두 지형에 대해 이중으로 구워져 있었다. 헤비탱크/브루트메크 등 일부 유닛이 NavMesh 상에서 자기 위치를
잡을 때 위쪽(mission맵)이 아니라 **아래쪽 testmap의 NavMesh로 스냅**되면서 지면 아래에 위치하게 됐던 것 —
이게 "투명해 보인다"/"땅속에 박혀 보인다" 두 증상 모두의 진짜 원인이었다. testmap을 제거하자 정상으로 돌아옴을
확인함(자세한 경위는 [[0345-bugfix-testing-feedback-batch]] 참고).

이 문서의 원래 가설(URP 셰이더 변형 스트리핑)과 그에 따른 `ProjectSettings/GraphicsSettings.asset` 수정
(`m_AlwaysIncludedShaders`에 URP Lit 추가)은 **진짜 원인이 아니었던 것으로 결론** — 다만 그 수정 자체는
부작용이 없는 안전한 변경이라 되돌릴 필요는 없음(그대로 둬도 무방).
