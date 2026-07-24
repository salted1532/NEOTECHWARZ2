# 0225 — BigExplosionEffect 중 일부 파티클(Embers/DerisFire/ShockWave)이 "누락된 것처럼" 보이는 원인

## 1. 요청
"현재 TestEnemy 프리팹에다가 살을 좀 더 붙여보고 있는데 UnitEffect를 넣었거든 아직 적 유닛은 공격이나 이동기능은
없으니깐 빼고 Hit관련 이펙트를 넣었는데 Explosive Hit 이펙트에 넣은 BigExplosionEffect가 왜 뭔가 누락된 거
처럼 보이지는건지 모르겠네"

## 2. 조사 내용

- `Assets/prefabs/Test/TestEnemy.prefab`의 `UnitEffects.hitEffects.explosiveHitPrefab`이
  `Assets/prefabs/Effect/Explosion/BigExplosionEffect.prefab` (guid `ee5b5d938b0f65247ac5a2b0fc8bac00`)을
  가리키는 것을 확인.
- 이 프리팹은 자식 오브젝트(Light/Embers/DebrisSmoke/Fire/Debris/DerisFire/ShockWave)마다 별도의
  ParticleSystem이 붙어있고, 전부 `looping: 1`로 만들어져 있음 — 이건 [[0112-bugfix-looping-particle-systems-replay-multiple-times]]에서
  이미 고친 "여러 번 터지는" 문제이고, `EffectPlayer.Spawn()`이 런타임에 loop를 꺼서 정상 동작 중이라 이번
  증상과는 무관.
- 파티클 6개(Light 제외)가 각각 참조하는 머티리얼과 셰이더를 전부 확인한 결과:

  | 자식 오브젝트 | 머티리얼 | 셰이더 | 상태 |
  |---|---|---|---|
  | 루트(BigExplosionEffect), DebrisSmoke | SmokeDarkParticle.mat | Universal Render Pipeline/Particles/Unlit | 정상 (URP) |
  | Fire | FlameRoundYellowParticle.mat | Universal Render Pipeline/Particles/Unlit | 정상 (URP) |
  | Debris | StoneSurface.mat | Universal Render Pipeline/Lit | 정상 (URP) |
  | **Embers** | EmbersParticle.mat | **Legacy Shaders/Particles/Additive** (fileID 200, guid `0000000000000000f000000000000000`) | **깨짐 — Built-in 전용, URP 미지원** |
  | **DerisFire** | FlameParticle.mat | **Legacy Shaders/Particles/Additive** | **깨짐** |
  | **ShockWave** | ShockWaveParticle.mat | **Legacy Shaders/Particles/Additive** | **깨짐** |

- 즉 6개 중 절반(Embers/DerisFire/ShockWave)만 [[0071-canopus-materials-broken-in-urp]] /
  [[0075-yoge-materials-broken-in-urp]]에서 겪었던 것과 똑같은 패턴 — Built-in Render Pipeline 전용 레거시
  셰이더를 URP 프로젝트에서 그대로 쓰고 있어서 제대로 렌더링되지 않음(핑크/깨짐 또는 사실상 안 보임).
  나머지 절반(Smoke/Fire/Debris)은 이미 URP 셰이더로 변환되어 있는 상태라 정상 재생됨.
- 그래서 폭발이 터질 때 연기·메인 화염·파편(돌덩이)은 눈에 보이는데, 불티(Embers)·추가 파편 화염(DerisFire)·
  충격파 링(ShockWave) 세 겹만 안 보이거나 깨진 채로 나와서 폭발 전체가 "뭔가 빠진 것처럼" 부실하게 보이는 것.
- 참고: `ShockWaveParticle.mat`은 `_MainTex`가 비어있는데, 원본 에셋
  (`Assets/AssetFolder/EffectExamples/FireExplosionEffects/Materials/ShockWaveParticle.mat`)부터 원래
  그렇게 비어있었음 — 이건 셰이더 문제와 별개이고 텍스쳐가 깨진 게 아니라 애초에 안 채워진 상태. URP 변환과는
  무관하므로 참고로만 남김.

## 3. 결론
`EmbersParticle.mat` / `FlameParticle.mat`(DerisFire가 씀) / `ShockWaveParticle.mat` 3개 머티리얼이 Built-in RP
전용 레거시 셰이더(`Legacy Shaders/Particles/Additive`)를 그대로 참조하고 있어서 URP 프로젝트에서 렌더링이
깨지는 것이 원인. 같은 프리팹 안의 나머지 3개 머티리얼은 이미 URP로 변환돼 있어서 정상 재생되기 때문에,
결과적으로 폭발의 절반만 보이는 것처럼 느껴짐.

## 4. 제안하는 수정 (아직 적용 안 함 — 승인 필요)
0071/0075와 동일한 방식으로 3개 `.mat` 파일을 URP 셰이더로 변환:

- 대상 (전부 `Assets/AssetFolder/EffectExamples/FireExplosionEffects/Materials/`):
  - `EmbersParticle.mat`
  - `FlameParticle.mat` (DerisFire가 참조)
  - `ShockWaveParticle.mat`
- 같은 에셋 팩 안에서 이미 정상 변환돼 있는 `SmokeDarkParticle.mat` / `FlameRoundYellowParticle.mat`과 동일하게
  `Universal Render Pipeline/Particles/Unlit` 셰이더(guid `0406db5a14f94604a8c57ccfbc9f3b46`)로 교체.
- `_MainTex` → `_BaseMap`에 기존 텍스쳐 GUID 그대로 매핑, `_TintColor`/`_Color` → `_BaseColor`, `_EMISSION`
  키워드 유지. 텍스쳐 GUID나 ParticleSystem 설정 자체는 건드리지 않고 셰이더/프로퍼티 매핑만 변경.
- (참고) `Assets/prefabs/Effect/Explosion/BigExplosionEffect 1.prefab`이라는 동일 guid를 참조하는 사본이
  하나 더 있음 — 지금 TestEnemy가 쓰는 건 " 1"이 안 붙은 원본 쪽이고, 머티리얼을 고치면 두 프리팹 모두
  같은 .mat 에셋을 참조하므로 자동으로 같이 고쳐짐 (사본 자체를 정리할지는 별개 문제이니 이번 수정 범위에서는 제외).

진행할지 확인 부탁.
