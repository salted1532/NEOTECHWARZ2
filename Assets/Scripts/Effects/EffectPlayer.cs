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
    // 루트뿐 아니라 모든 자식까지 뒤져서(GetComponentsInChildren) 그중 가장 긴 지속시간을 기준으로 삼는다 -
    // 에셋 프리팹마다 ParticleSystem이 루트에 바로 있기도 하고(예: MuzzleFlashEffect), 여러 자식 오브젝트로
    // 나뉘어 있기도 해서(예: BulletImpactFleshBigEffect의 BloodMist/BloodGlobs/BloodStreaks), 루트만 보면
    // 자식에만 있는 경우 자동 파괴가 아예 예약되지 않아 이펙트가 씬에 영원히 남는 문제가 있었다.
    public static GameObject Spawn(GameObject effectPrefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (effectPrefab == null)
            return null;

        GameObject instance = Object.Instantiate(effectPrefab, pos, rot, parent);

        ParticleSystem[] allSystems = instance.GetComponentsInChildren<ParticleSystem>();
        if (allSystems.Length > 0)
        {
            float lifetime = 0f;
            foreach (ParticleSystem ps in allSystems)
            {
                // 폭발 이펙트류(BigExplosionEffect 등)는 자식 파티클시스템들이 looping=true로 만들어져 있는
                // 경우가 흔하다 - 발사 후 잊기 용도로는 "한 번만 터지고 끝"이어야 하는데, loop가 켜져 있으면
                // 아래 Destroy 타이머가 끝날 때까지 duration이 짧은 시스템(불꽃 등)이 여러 번 재반복되면서
                // "같은 폭발이 여러 번 터지는" 것처럼 보인다. loop를 꺼서 지금 재생 중인 한 사이클만 마치고
                // 자연스럽게 멎게 한다(이미 나온 파티클은 startLifetime만큼 정상적으로 페이드아웃됨).
                var main = ps.main;
                if (main.loop)
                    main.loop = false;

                lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            }

            Object.Destroy(instance, lifetime);
        }

        return instance;
    }

    // 피격 이펙트를 "공격자 쪽을 향한 콜라이더 표면" 지점에서 재생한다. bodyCollider.ClosestPoint(attackerPosition)
    // 이 그 지점을 그대로 계산해준다(방향 벡터를 직접 구해 레이캐스트로 테두리를 찾을 필요가 없음). 피격 위치는
    // 맞을 때마다 방향이 달라지므로 다른 이펙트처럼 고정 리스트로 미리 지정할 수 없어 매번 동적으로 계산한다.
    // UnitEffects/BuildingEffects/ConstructionEffects가 공통으로 쓰는 로직이라 여기 하나로 모아뒀다.
    public static void PlayHit(Transform target, Collider bodyCollider, Vector3 attackerPosition, GameObject hitPrefab)
    {
        Vector3 hitPoint = bodyCollider != null
            ? bodyCollider.ClosestPoint(attackerPosition)
            : target.position; // 콜라이더가 없는 예외 상황 fallback

        Vector3 outward = hitPoint - target.position;
        Quaternion rot = outward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(outward.normalized) : Quaternion.identity;

        Spawn(hitPrefab, hitPoint, rot);
    }

    // 발사 후 잊기(fire-and-forget) 이펙트를 여러 지점에 동시에 스폰한다.
    // attachToPoint가 true(기본)면 스폰된 이펙트를 그 지점(또는 fallback)에 부모로 붙여서 이후 그 지점이
    // 움직이면 같이 따라가게 한다 - 안 붙이면 스폰된 자리에 그대로 고정돼서, 유닛이 이동하며 공격할 때
    // 총구 이펙트가 허공에 남는 문제가 생긴다. 다만 스폰 직후 그 오브젝트가 곧바로 Destroy될 예정이면
    // (사망 이펙트, 건설 완료 이펙트 등) false로 넘겨야 한다 - 안 그러면 부모가 파괴될 때 이펙트도
    // 재생을 채 끝내기 전에 같이 파괴돼버린다.
    // 스폰된 인스턴스 목록을 반환한다 - 호출자가 "공격이 취소되면 즉시 정지" 같은 조기 종료를 걸고 싶을 때
    // 자동 파괴 타이머를 기다리지 않고 직접 Destroy할 수 있도록(UnitEffects.StopAttackEffects 참고).
    public static List<GameObject> SpawnAtPoints(GameObject effectPrefab, IReadOnlyList<Transform> points, Transform fallback, bool attachToPoint = true)
    {
        List<GameObject> instances = new List<GameObject>();
        if (effectPrefab == null)
            return instances;

        if (points == null || points.Count == 0)
        {
            GameObject instance = Spawn(effectPrefab, fallback.position, fallback.rotation, attachToPoint ? fallback : null);
            if (instance != null)
                instances.Add(instance);
            return instances;
        }

        foreach (Transform point in points)
        {
            if (point == null)
                continue;

            GameObject instance = Spawn(effectPrefab, point.position, point.rotation, attachToPoint ? point : null);
            if (instance != null)
                instances.Add(instance);
        }

        return instances;
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
            instances.Add(ForceLooping(Object.Instantiate(effectPrefab, fallback.position, fallback.rotation, fallback)));
            return instances;
        }

        foreach (Transform point in points)
        {
            if (point == null)
                continue;

            instances.Add(ForceLooping(Object.Instantiate(effectPrefab, point.position, point.rotation, point)));
        }

        return instances;
    }

    // 지속형 이펙트는 "명시적으로 정리될 때까지 계속 반복 재생"이 기본 전제인데, 프리팹 자체가 한 번만 터지고
    // 끝나는(looping=false) 용도로 만들어져 있으면 오브젝트는 계속 살아서 따라다니는데 파티클 방출만 자기
    // duration만큼 하다 멈춰버린다(유닛은 계속 이동 중인데 트레일만 중간에 안 나오는 문제). 그래서 이 경로로
    // 스폰되는 파티클시스템은 전부 loop를 강제로 켠다.
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
}
