# 0112 - 버그수정: BigExplosionEffect가 한 번이 아니라 여러 번(3번) 터지는 문제

## 1. 요청
"내가 에셋들에서 쓸만한거만 모아둔 Effect 폴더 안에 있는 이펙트중에 BigExplosionEffect 이 이펙트를 사용하고
싶은데 폭발이 3번 발생하고 제거되는데 왜그럴까"

## 2. 원인
`Assets/prefabs/Effect/Explosion/BigExplosionEffect.prefab`을 열어보니, 자식 오브젝트 8개
(`Light`/`Embers`/`DebrisSmoke`/`Fire`/`Debris`/`DerisFire`/`ShockWave` + 루트 자신)에 각각 별도의
`ParticleSystem`이 붙어있고, **전부 `looping: 1`(반복 재생)** 로 설정돼 있다.

`EffectPlayer.Spawn()`(0110에서 자식까지 검색하도록 고친 버전)은 이 자식들 중 **가장 긴**
`duration + startLifetime`을 기준으로 딱 한 번 `Object.Destroy` 타이머를 건다. 그런데 각 파티클시스템
자체는 looping이 켜져 있어서, 그 최종 타이머가 끝날 때까지 자기 한 사이클(duration)을 계속 반복
재생한다 — duration이 짧은 시스템(Fire/Light/Embers 등, 폭발의 "터지는 순간"을 담당)은 duration이 긴
시스템(연기/충격파처럼 오래 잔류해야 하는 것)이 끝날 때까지 기다리는 동안 여러 번 다시 처음부터 재생돼서,
같은 폭발이 여러 번(대략 3번) 터지는 것처럼 보인다.

즉 코드가 이펙트를 여러 번 스폰하는 게 아니라, **하나의 인스턴스 안에서 개별 파티클시스템이 반복 재생되는
것**이 여러 번 터지는 것처럼 보이는 원인이다.

## 3. 수정
`EffectPlayer.Spawn()`에서 자식 파티클시스템들을 순회하며 지속시간을 계산할 때, `main.loop`가 켜져 있으면
그 자리에서 꺼버리도록 했다. Unity는 `main.loop`를 매 사이클이 끝나는 시점에 확인해서 반복 여부를 결정하므로,
이미 재생 중이던 사이클은 그대로 마치고(중간에 뚝 끊기지 않음) 그 다음부터 반복하지 않는다 — 이미 방출된
파티클은 각자의 `startLifetime`만큼 정상적으로 페이드아웃되고, 전체 오브젝트는 기존 로직대로 가장 긴
`duration + startLifetime` 시점에 파괴된다.

```csharp
// Assets/Scripts/Effects/EffectPlayer.cs, Spawn() 안
foreach (ParticleSystem ps in allSystems)
{
    var main = ps.main;
    if (main.loop)
        main.loop = false; // 추가 - 한 사이클만 재생하고 자연 종료

    lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
}
```

`SpawnAtPoints`/`SpawnPersistentAtPoints`도 전부 이 `Spawn()`을 거치므로, `BigExplosionEffect`뿐 아니라
looping이 켜진 채로 만들어진 다른 이펙트 프리팹에도 동일하게 적용된다.
