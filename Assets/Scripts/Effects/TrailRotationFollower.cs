using UnityEngine;

// 이동 트레일 등 지속형 이펙트가 부착 지점을 부모-자식으로 직접 붙잡는 대신, 위치는 매 프레임 그대로
// 따라가되(발밑에서 떨어지지 않도록) 회전만 Quaternion.Slerp로 서서히 뒤쫓게 한다. 부모-자식으로 붙이면
// 회전이 매 프레임 즉시 동기화되어 유닛이 급회전할 때 이펙트가 부자연스럽게 홱 도는 문제가 있었다(doc/0118).
// 추가로 지점의 각속도가 임계값을 넘는 급회전 중에는 파티클 크기/방출량을 줄였다가 회전이 끝나면 복구한다.
public class TrailRotationFollower : MonoBehaviour
{
    private Transform target;
    private float rotationFollowSpeed;
    private float fastRotationThreshold;
    private float shrinkScale;
    private float shrinkLerpSpeed;

    private Quaternion lastTargetRotation;
    private float currentShrink = 1f;

    private ParticleSystem[] particleSystems;
    private float[] baseStartSize;
    private float[] baseEmissionRate;

    public void Init(Transform target, float rotationFollowSpeed, float fastRotationThreshold, float shrinkScale, float shrinkLerpSpeed)
    {
        this.target = target;
        this.rotationFollowSpeed = rotationFollowSpeed;
        this.fastRotationThreshold = fastRotationThreshold;
        this.shrinkScale = shrinkScale;
        this.shrinkLerpSpeed = shrinkLerpSpeed;
        lastTargetRotation = target.rotation;

        transform.SetParent(null); // 위치/회전을 이 컴포넌트가 직접 계산하므로 더 이상 부모-자식으로 묶지 않는다

        particleSystems = GetComponentsInChildren<ParticleSystem>();
        baseStartSize = new float[particleSystems.Length];
        baseEmissionRate = new float[particleSystems.Length];
        for (int i = 0; i < particleSystems.Length; i++)
        {
            baseStartSize[i] = particleSystems[i].main.startSizeMultiplier;
            baseEmissionRate[i] = particleSystems[i].emission.rateOverTimeMultiplier;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Destroy(gameObject); // 부착 지점(유닛)이 먼저 파괴된 경우 - 정상적으로는 UnitEffects가 먼저 Destroy하지만 안전장치
            return;
        }

        float angularSpeed = Quaternion.Angle(lastTargetRotation, target.rotation) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastTargetRotation = target.rotation;

        float shrinkTarget = shrinkScale < 1f && angularSpeed > fastRotationThreshold ? shrinkScale : 1f;
        currentShrink = Mathf.Lerp(currentShrink, shrinkTarget, shrinkLerpSpeed * Time.deltaTime);
        ApplyShrink(currentShrink);

        transform.position = target.position;
        transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, rotationFollowSpeed * Time.deltaTime);
    }

    private void ApplyShrink(float factor)
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i] == null)
                continue;

            var main = particleSystems[i].main;
            main.startSizeMultiplier = baseStartSize[i] * factor;

            var emission = particleSystems[i].emission;
            emission.rateOverTimeMultiplier = baseEmissionRate[i] * factor;
        }
    }
}
