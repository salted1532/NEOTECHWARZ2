# 0113 - 버그수정: 이동 이펙트가 이동 중간에 끊기는 문제

## 1. 요청
"이동중인 유닛에서 이동 이펙트는 계속 나왔으면 좋겠어. 이펙트가 중간에 다 끝나서 안나오는게 아니라
계속 나왔으면 좋겠어."

## 2. 원인
[[0112-bugfix-looping-particle-systems-replay-multiple-times]]와 반대 방향의 같은 근본 원인.
`EffectPlayer.SpawnPersistentAtPoints()`(이동 트레일이 쓰는 스폰 함수)는 파티클시스템의 `loop` 설정을
전혀 건드리지 않고 프리팹에 저장된 값 그대로 재생했다. 이동 트레일용 프리팹이 원래 "한 번만 터지고 끝"
(looping=false, 짧은 duration)으로 만들어져 있으면, 오브젝트 자체는 파괴되지 않고 유닛을 계속 따라다니지만
파티클 방출은 자기 duration만큼만 하고 자연히 멈춘다 — 유닛은 계속 이동 중인데 트레일만 중간에 안 나오는
것으로 보인다.

## 3. 수정
`SpawnPersistentAtPoints()`로 스폰되는 모든 인스턴스에 `ForceLooping()`을 거쳐, 자식 파티클시스템들의
`main.loop`를 강제로 true로 켜도록 했다. 지속형 이펙트는 "멈춰야 할 때 명시적으로 Destroy될 때까지 계속
재생"이 기본 전제이므로, 프리팹 저작 시점의 looping 설정과 무관하게 항상 반복되도록 만드는 것이 맞다
(이동을 멈추면 `UnitEffects.SetMoveTrail()`이 이미 `Destroy(trail)`로 정리하고 있음).

```csharp
// Assets/Scripts/Effects/EffectPlayer.cs
private static GameObject ForceLooping(GameObject instance)
{
    foreach (ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>())
    {
        var main = ps.main;
        if (!main.loop)
            main.loop = true;
    }
    return instance;
}
```

`SpawnPersistentAtPoints()`의 두 스폰 지점(지점 있음/없음 fallback) 모두 `Object.Instantiate(...)` 결과를
이 `ForceLooping()`으로 감싸도록 수정했다.

## 4. 참고 — 반대 케이스(0112)와의 대비
| | `Spawn()` (발사 후 잊기: 공격/피격/사망/이착륙/건설완료) | `SpawnPersistentAtPoints()` (지속형: 이동 트레일/건설 중) |
|---|---|---|
| 의도 | 한 번만 재생하고 끝 | 멈출 때까지 계속 반복 |
| loop 처리 | 켜져 있으면 강제로 끔 (0112) | 꺼져 있으면 강제로 켬 (이번 수정) |
