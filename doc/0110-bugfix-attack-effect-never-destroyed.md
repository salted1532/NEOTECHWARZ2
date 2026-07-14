# 0110 - 버그수정: 공격 이펙트 하나가 파괴되지 않고 씬에 계속 남는 문제

## 1. 요청
"유니티에서 확인해보니깐 공격 이펙트가 1개는 그대로 있고 나머지 하나가 나타났다가 마는데 왜그런거야?"

## 2. 원인
`EffectPlayer.Spawn()`(`Assets/Scripts/Effects/EffectPlayer.cs`)이 자동 파괴 여부를 `instance.TryGetComponent<ParticleSystem>()`로
**루트 오브젝트에만** ParticleSystem이 붙어있는지 검사하고 있었다.

`Assets/AssetFolder/EffectExamples`의 이펙트 프리팹들을 직접 열어보니 구조가 프리팹마다 다르다:
- `MuzzleFlashEffect.prefab`: 루트 오브젝트에 `ParticleSystem`이 바로 붙어있음 → 루트 검사로 정상 감지됨.
- `BulletImpactFleshBigEffect.prefab`: 루트 밑에 `BloodMist`/`BloodGlobs`/`BloodStreaks`/`Decal` 등 **자식
  오브젝트**로 나뉘어 각각 `ParticleSystem`을 들고 있음. `CartridgeEjectEffect.prefab`도 루트+자식(`Puff`)
  구조.

루트에 `ParticleSystem`이 없는(전부 자식에만 있는) 프리팹은 `TryGetComponent`가 실패해서 `Object.Destroy`
예약 자체가 안 걸리고, 그 이펙트 오브젝트가 씬에 영원히 남는다 — "1개는 그대로 있고"가 이 경우다. 반대로
루트에 바로 붙은 프리팹은 정상 감지돼서 재생 후 스스로 사라진다 — "나머지 하나가 나타났다가 마는" (정상 동작).

## 3. 수정
`Assets/Scripts/Effects/EffectPlayer.cs`의 `Spawn()`에서 `TryGetComponent<ParticleSystem>()`(루트만) 대신
`GetComponentsInChildren<ParticleSystem>()`(자식 포함 전체)을 쓰고, 발견된 모든 ParticleSystem 중 **가장 긴
지속시간**을 기준으로 파괴 타이머를 걸도록 바꿨다. 프리팹 구조가 루트 단일이든 다중 자식이든 관계없이 항상
올바르게 자동 파괴되도록 함.

```csharp
// 전
if (instance.TryGetComponent<ParticleSystem>(out var ps))
{
    float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
    Object.Destroy(instance, lifetime);
}

// 후
ParticleSystem[] allSystems = instance.GetComponentsInChildren<ParticleSystem>();
if (allSystems.Length > 0)
{
    float lifetime = 0f;
    foreach (ParticleSystem ps in allSystems)
        lifetime = Mathf.Max(lifetime, ps.main.duration + ps.main.startLifetime.constantMax);

    Object.Destroy(instance, lifetime);
}
```

`SpawnAtPoints`/`SpawnPersistentAtPoints`는 전부 이 `Spawn()`을 통해서만 인스턴스화하므로 이 수정 하나로
공격/이동/이착륙/건설/사망/피격 이펙트 전체에 동일하게 적용된다.
