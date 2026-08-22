using UnityEngine;

// 치유 유닛에 붙이는 옵셔널 컴포넌트 (LaserBeamAttack.cs와 동일 골격, doc/0661).
// LaserBeamAttack.Fire()는 beamDuration(0.2초) 뒤 자동으로 꺼지는 "버스트"라 회복처럼 지속되는 동안
// 계속 이어져야 하는 용도에는 안 맞는다 - 그래서 시간 기반 종료 대신 StartBeam/StopBeam으로 직접
// 켜고 끄는 버전으로 새로 둔다. 색상은 코드에 고정하지 않고 beamColor로 인스펙터에서 바로 조정한다.
public class HealBeam : MonoBehaviour
{
    [SerializeField] private GameObject laserBeamPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Color beamColor = new Color(0.098f, 1f, 0.6f, 1f); // 사용자가 인스펙터에서 직접 조정

    private LineRenderer beamLine;
    private GameObject beamInstance;
    private Transform activeTarget;
    private Collider activeTargetCollider;

    private void Awake()
    {
        if (laserBeamPrefab == null || firePoint == null)
            return;

        beamInstance = Instantiate(laserBeamPrefab, firePoint.position, firePoint.rotation, firePoint);
        beamLine = beamInstance.GetComponentInChildren<LineRenderer>();
        if (beamLine != null)
        {
            beamLine.startColor = beamColor;
            beamLine.endColor = beamColor;
        }
        beamInstance.SetActive(false);
    }

    // UnitController.BeginHeal()이 새 대상을 잡을 때 호출한다.
    public void StartBeam(Transform target)
    {
        if (beamInstance == null || beamLine == null || target == null)
            return;

        activeTarget = target;
        activeTargetCollider = target.GetComponent<Collider>();
        beamInstance.SetActive(true);
    }

    // UnitController.StopHeal()이 회복이 끝날 때(대상 상실/만피/사망/사거리 이탈) 호출한다.
    public void StopBeam()
    {
        activeTarget = null;
        activeTargetCollider = null;
        if (beamInstance != null)
            beamInstance.SetActive(false);
    }

    // 대상이 파괴되면 activeTarget이 Unity의 fake-null이 되므로 매 프레임 직접 확인한다.
    private void Update()
    {
        if (activeTarget == null)
        {
            if (beamInstance != null && beamInstance.activeSelf)
                beamInstance.SetActive(false);
            return;
        }

        Vector3 endPoint = activeTargetCollider != null ? activeTargetCollider.ClosestPoint(firePoint.position) : activeTarget.position;
        beamLine.SetPosition(0, firePoint.position);
        beamLine.SetPosition(1, endPoint);
    }
}
