using UnityEngine;
using DG.Tweening;

// 보병 유닛 루트(UnitController/EnemyUnitController가 붙은 오브젝트)에 직접 부착한다 - 이동 중에는
// NavMeshAgent가, 공격 중에는 UnitController의 회전 로직이 이미 이 트랜스폼의 회전을 담당하므로
// 그 두 경우를 제외한 "가만히 있는" 상태에서만 개입한다.
// 랜덤 idleWaitMin~idleWaitMax초(기본 5~15초, 매번 다시 뽑아서 유닛마다 한꺼번에 안 돈다)째 계속 가만히
// 있으면 랜덤한 Y축 방향(0~360도)으로 몸을 돌려 주변을 경계하는 느낌을 준다. 다시 이동/공격이 시작되면
// 즉시 트윈을 끊고 기존 로직에 회전을 넘겨준다.
public class InfantryIdleLookAround : MonoBehaviour
{
    [SerializeField] private float idleWaitMin = 5f;  // 다음 방향 전환까지 대기하는 최소 시간
    [SerializeField] private float idleWaitMax = 15f; // 다음 방향 전환까지 대기하는 최대 시간 - 매번 다시 뽑아서 한꺼번에 안 돈다
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private Ease turnEase = Ease.InOutSine;

    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private AllyController allyController; // 아군 OC 보병 - EnemyController와 동일 판정 (doc/0468)
    private float idleTimer;
    private float nextLookWait;
    private Tween turnTween;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
        allyController = GetComponent<AllyController>();
        nextLookWait = Random.Range(idleWaitMin, idleWaitMax);
    }

    private void Update()
    {
        if (!IsIdle())
        {
            turnTween?.Kill();
            idleTimer = 0f;
            return;
        }

        idleTimer += Time.deltaTime;
        if (idleTimer < nextLookWait)
            return;

        idleTimer = 0f;
        nextLookWait = Random.Range(idleWaitMin, idleWaitMax);
        turnTween?.Kill();
        float randomYaw = Random.Range(0f, 360f);
        turnTween = transform.DORotate(new Vector3(0f, randomYaw, 0f), turnDuration).SetEase(turnEase);
    }

    private bool IsIdle()
    {
        if (unitController != null)
            return !unitController.IsCurrentlyMoving() && !unitController.IsAttack() && !unitController.IsHealing();
        if (enemyUnitController != null)
            return !enemyUnitController.IsCurrentlyMoving() && !enemyUnitController.IsAttack();
        if (allyController != null)
            return !allyController.IsCurrentlyMoving() && !allyController.IsAttack();
        return false;
    }

    private void OnDestroy() => turnTween?.Kill();
}
