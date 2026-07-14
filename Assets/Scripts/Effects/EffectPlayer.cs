using System.Collections.Generic;
using UnityEngine;

// 이펙트 프리팹(파티클/사운드)을 스폰하고 정리하는 공용 정적 헬퍼.
// 단일 지점(Spawn), 다중 지점 발사 후 잊기(SpawnAtPoints), 다중 지점 지속형(SpawnPersistentAtPoints)을 제공한다.
// 다중 지점 API는 points가 비어있으면 fallback(보통 호출자 자신의 transform) 하나에만 재생한다 - 이펙트
// 지점을 아직 안 채운 프리팹도 기존과 동일하게 동작하고, 나중에 인스펙터에서 지점을 늘리기만 하면 된다.
public static class EffectPlayer
{
    // 이펙트 프리팹을 pos/rot에 스폰하고, ParticleSystem의 재생시간(main.duration + startLifetime.constantMax)
    // 을 기준으로 자동 파괴한다. AudioSource가 붙어있으면 같이 재생된다(프리팹에 미리 세팅).
    public static GameObject Spawn(GameObject effectPrefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (effectPrefab == null)
            return null;

        GameObject instance = Object.Instantiate(effectPrefab, pos, rot, parent);

        if (instance.TryGetComponent<ParticleSystem>(out var ps))
        {
            float lifetime = ps.main.duration + ps.main.startLifetime.constantMax;
            Object.Destroy(instance, lifetime);
        }

        return instance;
    }

    // 발사 후 잊기(fire-and-forget) 이펙트를 여러 지점에 동시에 스폰한다.
    public static void SpawnAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback, Transform parent = null)
    {
        if (effectPrefab == null)
            return;

        if (points == null || points.Count == 0)
        {
            Spawn(effectPrefab, fallback.position, fallback.rotation, parent);
            return;
        }

        foreach (Transform point in points)
        {
            if (point == null)
                continue;

            Spawn(effectPrefab, point.position, point.rotation, parent);
        }
    }

    // 지속형 이펙트(이동 트레일, 건설 중 파티클처럼 켜져 있는 동안 계속 유지되는 것)를 각 지점에 하나씩
    // 스폰해 그 지점에 붙여두고(parent = 그 지점 자신, 계속 따라다니게), 나중에 꺼야 할 때 정리할 수
    // 있도록 인스턴스 목록을 반환한다. 지속형이라 Spawn()과 달리 자동 파괴를 걸지 않는다 - 호출자가
    // 명시적으로 Destroy한다.
    public static List<GameObject> SpawnPersistentAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback)
    {
        List<GameObject> instances = new List<GameObject>();
        if (effectPrefab == null)
            return instances;

        if (points == null || points.Count == 0)
        {
            instances.Add(Object.Instantiate(effectPrefab, fallback.position, fallback.rotation, fallback));
            return instances;
        }

        foreach (Transform point in points)
        {
            if (point == null)
                continue;

            instances.Add(Object.Instantiate(effectPrefab, point.position, point.rotation, point));
        }

        return instances;
    }
}
