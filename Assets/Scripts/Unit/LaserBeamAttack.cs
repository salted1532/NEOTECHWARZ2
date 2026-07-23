using System.Collections;
using UnityEngine;

// 레이저 공격 유닛에 붙이는 옵셔널 컴포넌트(UnitEffects와 동일하게 없으면 그냥 무시됨).
// UnitController.Attack()이 데미지를 적용한 직후 Fire(target)를 호출해서 firePoint와 대상을 잇는 빔을 0.2초간 재생한다.
//
// Attack_Laser_Blue_3D의 LaserMachine 컴포넌트는 SetActive(false)->true로 재활성화할 때마다 LineRenderer
// 자식을 새로 쌓기만 하고(OnEnable이 elementsList를 비우지 않음), 방향도 raycast+transform.forward 기반이라
// 공격자가 회전하면 조준 방향도 같이 돌아가 버려서(doc/0218) 이 용도(정확히 두 지점을 잇는 재사용 빔)에 안 맞는다.
// 그래서 그 프리팹에 직접 붙여둔 LineRenderer를 이 스크립트가 매 프레임 월드 좌표로만 갱신한다 - 로컬 회전을
// 전혀 참조하지 않으므로 공격자가 회전 중이어도 빔은 항상 firePoint와 대상의 실제 위치를 그대로 연결한다.
public class LaserBeamAttack : MonoBehaviour
{
    [SerializeField] private GameObject laserBeamPrefab; // Attack_Laser_Blue_3D
    [SerializeField] private Transform firePoint;
    [SerializeField] private float beamDuration = 0.2f;

    private LineRenderer beamLine;
    private GameObject beamInstance;
    private Coroutine activeBeam;

    // 풀링: 공격마다 Instantiate/Destroy 하지 않고 시작할 때 한 번만 만들어 firePoint 밑에 붙여두고 꺼둔다.
    private void Awake()
    {
        if (laserBeamPrefab == null || firePoint == null)
            return;

        beamInstance = Instantiate(laserBeamPrefab, firePoint.position, firePoint.rotation, firePoint);
        beamLine = beamInstance.GetComponentInChildren<LineRenderer>();
        beamInstance.SetActive(false);
    }

    // UnitController.Attack()에서 데미지 적용 직후 호출된다 (UnitEffects.PlayAttack()과 같은 훅 지점).
    public void Fire(Transform target)
    {
        if (beamInstance == null || beamLine == null || target == null)
            return;

        if (activeBeam != null)
            StopCoroutine(activeBeam);

        activeBeam = StartCoroutine(BeamRoutine(target));
    }

    private IEnumerator BeamRoutine(Transform target)
    {
        // 콜라이더 표면(가장 가까운 점)에 닿는 느낌을 주기 위해 EffectPlayer.PlayHit과 동일하게 ClosestPoint를 쓴다.
        // 콜라이더가 없으면 대상 Transform 위치 그대로 사용.
        Collider targetCollider = target.GetComponent<Collider>();

        beamInstance.SetActive(true);

        float elapsed = 0f;
        while (elapsed < beamDuration && target != null)
        {
            Vector3 endPoint = targetCollider != null ? targetCollider.ClosestPoint(firePoint.position) : target.position;
            beamLine.SetPosition(0, firePoint.position);
            beamLine.SetPosition(1, endPoint);

            elapsed += Time.deltaTime;
            yield return null;
        }

        beamInstance.SetActive(false);
        activeBeam = null;
    }
}
