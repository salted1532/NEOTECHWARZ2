# 0107 - AssetFolder/EffectExamples 이펙트 머티리얼 텍스쳐(핑크) 깨짐 조사

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **조사 결과 + 제안 수정**만 담고
> 실제 `.mat` 파일은 아직 건드리지 않았다. 아래 내용을 검토한 뒤 어느 범위까지 적용할지 알려주면 그때 반영한다.

## 1. 요청
"AssetFolder 안에 있는 이펙트 부분에서 텍스쳐가 깨진 부분들을 확인해보고 고쳐줘"

## 2. 결론 먼저

`Assets/AssetFolder/EffectExamples`(War FX류 파티클 이펙트 에셋 — Blood/FireExplosionEffects/WaterEffects/WeaponEffects/Misc Effects 폴더 구성)의 **머티리얼 54개 전부가 [[0071-canopus-materials-broken-in-urp]], [[0075-yoge-materials-broken-in-urp]]와 동일한 원인**으로 깨져 있다: 전부 **Built-in Render Pipeline 전용 셰이더**를 참조하는데 프로젝트는 **URP**를 쓰고 있어서 마젠타/핑크로 렌더링된다.

다만 이번 건은 이전 두 건과 달리 셰이더 종류가 하나가 아니라 **최소 8종류 이상 섞여 있어서**, 신뢰도에 따라 3그룹으로 나눠서 대응을 다르게 제안한다.

## 3. 조사 방법

- 프로젝트 자체에는 원본 Built-in 셰이더 리소스가 없어서(fileID만으로는 이름을 알 수 없음), `Library/PackageCache/com.unity.render-pipelines.universal@.../Editor/Tools/MaterialUpgrader/MaterialUpgraderDefinitions.cs`에서 Unity가 실제로 "Convert Selected Built-in Materials to URP" 실행 시 쓰는 **공식 변환 로직(ParticleUpgrader, StandardUpgrader)의 소스 코드**를 직접 확인해서 blend mode/속성 매핑 공식을 얻었다.
- URP 쪽 대상 셰이더(`Universal Render Pipeline/Particles/Unlit` 등)의 정확한 GUID도 같은 패키지 캐시 안의 `.shader.meta` 파일에서 직접 확인했다(추측 아님).
- 각 `.mat` 파일에 이미 저장된 `_Mode`/`_SrcBlend`/`_DstBlend` 등의 값을 위 공식 로직에 대입해서, 저장된 값이 "진짜 의도된 값"인지 "예전에 다른 셰이더를 쓰다 남은 찌꺼기 값"인지 교차검증했다.

## 4. 3개 그룹

### 그룹 A — 확신도 높음, 바로 적용 제안 (2개 파일)
`FlameRoundYellowParticle.mat`, `SmokeTrail.mat` (둘 다 `fileID: 211`).
이 둘은 저장된 `_Mode`/`_SrcBlend`/`_DstBlend`/`_ZWrite`/`_Cull` 값이 `ParticleUpgrader`의 계산 공식과 **정확히 일치**한다 — 즉 이미 "URP 변환 후"에 해당하는 값이 들어있고, 딱 `m_Shader` 참조 자체와 프로퍼티 이름(`_MainTex`→`_BaseMap`, `_Color`→`_BaseColor`, `_FlipbookMode`→`_FlipbookBlending`)만 안 바뀐 상태다. 대상 셰이더는 `Universal Render Pipeline/Particles/Unlit`(guid `0406db5a14f94604a8c57ccfbc9f3b46`, 패키지 캐시에서 직접 확인).

- `Assets/AssetFolder/EffectExamples/FireExplosionEffects/Materials/FlameRoundYellowParticle.mat` (`_Mode: 4` = Additive)
- `Assets/AssetFolder/EffectExamples/FireExplosionEffects/Materials/SmokeTrail.mat` (`_Mode: 2` = Alpha)

### 그룹 B — 확신도 높음, 바로 적용 제안 (4개 파일, 커스텀 셰이더)
`Assets/AssetFolder/EffectExamples/Shared/Shaders/SurfaceShader_VC.shader`(에셋 자체 포함 커스텀 셰이더, `#pragma surface`)를 참조하는 4개. 이건 **Unity의 자동 변환 도구(Convert Selected Built-in Materials to URP)가 아예 손대지 못하는 케이스**다 — Built-in 전용 커스텀 Surface Shader라서 URP에서 컴파일 자체가 안 되기 때문. 셰이더 소스를 직접 읽어 확인한 결과 로직은 단순함(정점색 × `_MainTex` × `_Color`, `Blend One OneMinusSrcAlpha` 고정, 노멀맵 입력 있음) → `Universal Render Pipeline/Particles/Unlit`로 대체 제안.

- `Assets/AssetFolder/EffectExamples/FireExplosionEffects/Materials/SmokeLightParticle.mat`
- `Assets/AssetFolder/EffectExamples/FireExplosionEffects/Materials/SmokeDarkParticle.mat`
- `Assets/AssetFolder/EffectExamples/WeaponEffects/Materials/RocketTrailParticle.mat`
- `Assets/AssetFolder/EffectExamples/WaterEffects/Materials/StormCloudParticle.mat`

### 그룹 C — 확신도 낮음, 유니티 에디터 작업 필요 (나머지 48개 파일)
나머지 48개는 `fileID: 45/46/106/200/203/208` 조합으로, 프로퍼티 목록이 전부 "Standard 셰이더가 남긴 찌꺼기 값 템플릿"(`_MetallicGlossMap`, `_Glossiness`, `_Parallax` 등)만 있고 그룹 A처럼 "진짜 의도된 값"이라고 교차검증할 근거가 없다. **이 fileID들이 정확히 어느 Built-in 셰이더 이름인지는 Unity 에디터 내부의 BuiltinExtra 리소스 번들을 열어야만 확실히 알 수 있고, 이 저장소 안의 파일만으로는 신뢰성 있게 역산할 수 없었다.** (0071/0075는 셰이더가 하나였어서 손으로 재현 가능했지만, 이번엔 최소 8종류가 섞여 있어 잘못 추측하면 블렌드 모드가 틀려서 "안 깨졌지만 이상하게" 보일 위험이 있다.)

**대신 정확한 해결법**: 유니티 에디터에서
1. Project 창에서 `Assets/AssetFolder/EffectExamples` 폴더를 선택(하위 전체 포함)하거나, 그룹 A/B에 해당하는 6개를 제외하고 나머지를 다중 선택
2. 메뉴 `Edit > Rendering > Materials > Convert Selected Built-in Materials to URP` 실행

이 메뉴가 정확히 방금 분석에 쓴 `ParticleUpgrader`/`StandardUpgrader` 코드를 그대로 실행하는 것이므로, 에디터가 각 머티리얼의 실제 셰이더 이름을 내부적으로 정확히 읽어서 안전하게 변환해준다. 대상 파일 목록(48개):

<details>
<summary>펼치기</summary>

```
Blood/Materials/BloodPoolParticle.mat
Blood/Materials/BloodSplashParticle.mat
Blood/Materials/BloodSplatParticle.mat
Blood/Materials/BloodStreamParticle.mat
FireExplosionEffects/Materials/DebrisParticle.mat
FireExplosionEffects/Materials/EmbersParticle.mat
FireExplosionEffects/Materials/FlameParticle.mat
FireExplosionEffects/Materials/IgnitionFlameParticle.mat
FireExplosionEffects/Materials/LightningParticle.mat
FireExplosionEffects/Materials/PlasmaExplosionParticle.mat
FireExplosionEffects/Materials/PlasmaFireParticle.mat
FireExplosionEffects/Materials/ShockWaveParticle.mat
Misc Effects/Materials/DustMoteParticle.mat
Misc Effects/Materials/SandSwirlsParticle.mat
Misc Effects/Materials/Spark2Particle.mat
Misc Effects/Materials/SparkParticle.mat
Shared/Materials/Background.mat
Shared/Materials/DarkDefault.mat
Shared/Materials/FleshSurface.mat
Shared/Materials/MetalSurface.mat
Shared/Materials/SandSurface.mat
Shared/Materials/Skybox.mat
Shared/Materials/StoneSurface.mat
Shared/Materials/WoodSurface.mat
Shared/Models/Materials/MazeLowMan.mat
Shared/Models/Materials/No Name.mat
Shared/Models/Materials/ShooterFPSWeapon.mat
WaterEffects/Materials/ImpactSplashParticle.mat
WaterEffects/Materials/WaterDropParticle.mat
WaterEffects/Materials/WaterMistParticle.mat
WaterEffects/Materials/WaterRipplesParticle.mat
WaterEffects/Materials/WaterTrailParticle.mat
WeaponEffects/Materials/BulletDecalMetal.mat
WeaponEffects/Materials/BulletDecalStone.mat
WeaponEffects/Materials/BulletDecalWood.mat
WeaponEffects/Materials/BulletFleshDecal.mat
WeaponEffects/Materials/DustPuffParticle.mat
WeaponEffects/Materials/MuzzleFlashParticle.mat
WeaponEffects/Materials/Projectile.mat
WeaponEffects/Materials/ShallCasing.mat
WeaponEffects/Materials/TinyStonesParticle.mat
WeaponEffects/Materials/WaterStreamParticle.mat
WeaponEffects/Materials/WoodSplintersParticle.mat
WeaponEffects/Models/Materials/No Name.mat
WeaponEffects/Textures/BloodMist.mat
WeaponEffects/Textures/BloodSplat2.mat
WeaponEffects/Textures/BloodStreak.mat
WeaponEffects/Textures/WaterSplashParticle.mat
```
(전부 `Assets/AssetFolder/EffectExamples/` 밑)
</details>

메뉴 실행 후 Console에 경고/에러가 남으면 캡처해서 알려주면, 남은 것들도 이어서 봐주겠다.

## 5. 제안 수정 내용 (그룹 A + B, 6개 파일)

공통: `m_Shader`를 `{fileID: 4800000, guid: 0406db5a14f94604a8c57ccfbc9f3b46, type: 3}` (`Universal Render Pipeline/Particles/Unlit`)로 교체.

| 파일 | `_Surface`/`_Blend` | 비고 |
|---|---|---|
| FlameRoundYellowParticle.mat | Transparent / Additive | 기존 `_SrcBlend`/`_DstBlend`가 이미 정답과 일치 (5/1) |
| SmokeTrail.mat | Transparent / Alpha | 기존 `_SrcBlend`/`_DstBlend`가 이미 정답과 일치 (5/10). 텍스쳐 자체가 비어있음(원본부터 미할당) — 이번 수정 범위 밖 |
| SmokeLightParticle.mat / SmokeDarkParticle.mat / RocketTrailParticle.mat / StormCloudParticle.mat | Transparent / Premultiply | 원본 커스텀 셰이더가 `Blend One OneMinusSrcAlpha`로 고정돼 있던 것과 동일 |

각 파일 공통 프로퍼티 리네임: `_MainTex`→`_BaseMap`, `_Color`→`_BaseColor`, (그룹 A만) `_FlipbookMode`→`_FlipbookBlending`. 노멀맵 텍스쳐가 있는 파일(SmokeLight/SmokeDark/RocketTrail/StormCloud)은 `_NORMALMAP` 키워드를 켠다.

## 6. 적용 결과

사용자 확인 후 그룹 A(2개) + 그룹 B(4개) 총 6개 `.mat` 파일을 5절 표대로 직접 수정 완료:

- `FireExplosionEffects/Materials/FlameRoundYellowParticle.mat`
- `FireExplosionEffects/Materials/SmokeTrail.mat`
- `FireExplosionEffects/Materials/SmokeLightParticle.mat`
- `FireExplosionEffects/Materials/SmokeDarkParticle.mat`
- `WeaponEffects/Materials/RocketTrailParticle.mat`
- `WaterEffects/Materials/StormCloudParticle.mat`

공통 작업: `m_Shader`를 `Universal Render Pipeline/Particles/Unlit`(guid `0406db5a14f94604a8c57ccfbc9f3b46`)로 교체, `_MainTex`→`_BaseMap`/`_Color`→`_BaseColor`/(그룹 A만)`_FlipbookMode`→`_FlipbookBlending` 리네임, `_Surface`/`_Blend`(+ Premultiply 대상은 `_SrcBlend`/`_DstBlend`/`_SrcBlendAlpha`/`_DstBlendAlpha`를 One/OneMinusSrcAlpha로) 추가·수정, 셰이더 키워드를 `_SURFACE_TYPE_TRANSPARENT` 등 URP 쪽 키워드로 교체, `m_CustomRenderQueue`를 3000(Transparent)으로 설정.

그룹 C(48개)는 사용자가 유니티 에디터에서 `Edit > Rendering > Materials > Convert Selected Built-in Materials to URP`로 직접 처리하기로 함(4절 파일 목록 참고).

## 7. 확인 필요 사항

텍스트 레벨에서 URP `ParticleUpgrader`/`SetupMaterialBlendMode`의 공식 로직을 그대로 재현했지만, 최종 확인은 유니티 에디터에서 해당 이펙트 프리팹/파티클을 재생해 눈으로 보는 것이 확실하다. 특히 그룹 B(커스텀 셰이더 대체)는 원래 노멀맵의 라이팅 반응이 사라지고(Unlit으로 교체) 정점색 곱 방식이 살짝 달라질 수 있어, 연기/로켓트레일/폭풍구름 이펙트가 기대한 톤으로 보이는지 확인 부탁.
