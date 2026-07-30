# 0306. 차량/보병 idle 애니메이션 - 360도 랜덤 + 5~15초 스태거 (제안)

날짜: 2026-07-30

## 요청 내용

> 차량 idle 애니메이션에서 45도 말고 360도 랜덤하게 돌아가도록 하고 돌아간 상태에서도 한 5~10초 사이
> 랜덤하게 다시 돌아가도록 하고 돌아가는것도 너무 빠르게 돌리지 말고 처음 랜덤하게 돌아가는거 처럼
> 속도를 맞춰줘 그리고 모든 유닛이 다 같은 시간대에 회전하는게 부자연스러우니깐 5~15초까지 랜덤하게
> 주변을 둘러봤으면 좋겠어 이건 보병 애니메이션도 수정해고 차량형은 5~15초 랜덤하게 회전 -> 5~10초 동안
> 그 각도에서 가만히 -> 다시 원래대로 천천히 돌아오고 또 랜덤 5~15초 무한 반복 이런식으로 해주고 보병도
> 5~15초 주변둘러보기

정리하면 (`doc/0305`에서 만든 두 스크립트에 대한 수정):
- 차량 포탑: ±45° 상대 오프셋 → 360° 전체 범위 랜덤 절대 각도.
- 대기 시간(포탑을 다시 방황시키기 전): 고정 10초 → 매번 랜덤 5~15초 (유닛마다/사이클마다 달라서 한꺼번에
  안 돈다).
- 방황한 각도에서 대기: 고정 1~2초 → 랜덤 5~10초.
- 원위치 복귀: 기존엔 `TurretController`를 다시 켜서 그 자체 `RotateTowards`(초당 360도)에 맡겼는데, 이게
  360° 전체 회전 범위에서는 너무 빠르게 홱 돌아가 보일 수 있어서, 처음 돌아갈 때와 **같은 속도(같은
  duration)**로 천천히 복귀하는 DOTween을 추가.
- 보병: 고정 10초 대기 → 매번 랜덤 5~15초로 동일하게 스태거 적용. (회전 자체는 이미 360도 랜덤이라 변경 없음.)

## 코드 변경

### `Assets/Scripts/Animation/VehicleIdleAnimation.cs`

**기존 코드** (필드 + 방황 코루틴):
```csharp
    [Header("포탑 방황 (선택 - 포탑 없는 차량이면 비워도 자동으로 못 찾으면 스킵됨)")]
    [SerializeField] private TurretController turretController;
    [SerializeField] private float idleWaitTime = 10f;       // 이 시간 동안 가만히 있으면 포탑 방황 1회 재생
    [SerializeField] private float turretWanderAngle = 45f;  // 현재 각도 기준 ±각도
    [SerializeField] private float turretWanderDuration = 1f;
    [SerializeField] private float turretHoldMin = 1f;
    [SerializeField] private float turretHoldMax = 2f;
```
```csharp
    private IEnumerator IdleTurretWanderRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(idleWaitTime);

            if (HasTrackingTarget())
                continue; // 이미 조준 중인 대상이 있으면 방황시키지 않는다

            turretController.enabled = false;

            float randomYaw = Random.Range(-turretWanderAngle, turretWanderAngle);
            Vector3 targetEuler = turretController.transform.localEulerAngles + new Vector3(0f, randomYaw, 0f);
            bool rotateDone = false;
            wanderTween = turretController.transform.DOLocalRotate(targetEuler, turretWanderDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => rotateDone = true);

            while (!rotateDone)
            {
                if (HasTrackingTarget()) break;
                yield return null;
            }

            float hold = Random.Range(turretHoldMin, turretHoldMax);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                if (HasTrackingTarget()) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            wanderTween?.Kill();
            turretController.enabled = true; // 정면 복귀는 TurretController.Update()의 RotateTowards가 처리
        }
    }
```

**변경 코드**:
```csharp
    [Header("포탑 방황 (선택 - 포탑 없는 차량이면 비워도 자동으로 못 찾으면 스킵됨)")]
    [SerializeField] private TurretController turretController;
    [SerializeField] private float idleWaitMin = 5f;   // 다음 방황까지 대기하는 최소 시간
    [SerializeField] private float idleWaitMax = 15f;  // 다음 방황까지 대기하는 최대 시간 - 유닛마다 매번 다시 뽑아서 한꺼번에 안 돈다
    [SerializeField] private float turretWanderDuration = 1f; // 방황/복귀 둘 다 이 시간으로 돈다 - 같은 속도로 돌아오게
    [SerializeField] private float turretHoldMin = 5f;
    [SerializeField] private float turretHoldMax = 10f;
```
```csharp
    private IEnumerator IdleTurretWanderRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(idleWaitMin, idleWaitMax));

            if (HasTrackingTarget())
                continue; // 이미 조준 중인 대상이 있으면 방황시키지 않는다

            turretController.enabled = false;

            Vector3 originalEuler = turretController.transform.localEulerAngles;
            Vector3 wanderEuler = new Vector3(originalEuler.x, Random.Range(0f, 360f), originalEuler.z);

            bool rotateDone = false;
            wanderTween = turretController.transform.DOLocalRotate(wanderEuler, turretWanderDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => rotateDone = true);

            while (!rotateDone)
            {
                if (HasTrackingTarget()) break;
                yield return null;
            }

            float hold = Random.Range(turretHoldMin, turretHoldMax);
            float elapsed = 0f;
            while (elapsed < hold)
            {
                if (HasTrackingTarget()) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            wanderTween?.Kill();

            if (HasTrackingTarget())
            {
                turretController.enabled = true; // 대상이 잡혔으면 복귀 트윈 없이 바로 조준 재개
                continue;
            }

            // 처음 돌아갈 때와 같은 duration으로 천천히 복귀 - TurretController의 기본 회전속도(초당 360도)에
            // 맡기면 360도 범위에서는 너무 빠르게 홱 돌아가 보이기 때문에 직접 트윈으로 속도를 맞춘다.
            bool returnDone = false;
            wanderTween = turretController.transform.DOLocalRotate(originalEuler, turretWanderDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => returnDone = true);

            while (!returnDone)
            {
                if (HasTrackingTarget()) break;
                yield return null;
            }

            wanderTween?.Kill();
            turretController.enabled = true;
        }
    }
```

### `Assets/Scripts/Animation/InfantryIdleLookAround.cs`

**기존 코드**:
```csharp
    [SerializeField] private float idleWaitTime = 10f; // 이 시간 동안 가만히 있으면 방향 전환 1회
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private Ease turnEase = Ease.InOutSine;

    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private float idleTimer;
    private Tween turnTween;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
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
        if (idleTimer < idleWaitTime)
            return;

        idleTimer = 0f;
        turnTween?.Kill();
        float randomYaw = Random.Range(0f, 360f);
        turnTween = transform.DORotate(new Vector3(0f, randomYaw, 0f), turnDuration).SetEase(turnEase);
    }
```

**변경 코드**:
```csharp
    [SerializeField] private float idleWaitMin = 5f;  // 다음 방향 전환까지 대기하는 최소 시간
    [SerializeField] private float idleWaitMax = 15f; // 다음 방향 전환까지 대기하는 최대 시간 - 유닛마다 매번 다시 뽑아서 한꺼번에 안 돈다
    [SerializeField] private float turnDuration = 1f;
    [SerializeField] private Ease turnEase = Ease.InOutSine;

    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private float idleTimer;
    private float nextLookWait;
    private Tween turnTween;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
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
```

## 요약

- 차량: `idleWaitTime`(고정 10초) → `idleWaitMin/Max`(5~15초 랜덤), `turretWanderAngle`(±45°) 제거하고
  0~360° 절대 각도로 전환, `turretHoldMin/Max`를 1~2초 → 5~10초로 확장, 복귀도 방황과 같은
  `turretWanderDuration`으로 트윈.
- 보병: `idleWaitTime`(고정 10초) → `idleWaitMin/Max`(5~15초 랜덤), 회전 로직 자체는 변경 없음.
- 인스펙터에 이미 값이 세팅된 기존 프리팹이 있다면, 필드 이름이 바뀌므로(`idleWaitTime` →
  `idleWaitMin/idleWaitMax`, 차량 쪽 `turretWanderAngle` 삭제) 재직렬화되며 새 기본값으로 리셋된다.
  필요하면 인스펙터에서 다시 조정.

## 영향받는 파일

- `Assets/Scripts/Animation/VehicleIdleAnimation.cs` (수정)
- `Assets/Scripts/Animation/InfantryIdleLookAround.cs` (수정)

## 다음 단계

위 내용대로 두 스크립트를 수정해도 될지 확인 부탁드립니다.
