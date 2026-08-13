using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 유닛 종류(ID) + 마릿수 하나의 묶음 (EnemyAIDirector.UnitGroup과 동일한 패턴, doc/0543).
[System.Serializable]
public class AllyUnitGroup
{
    public int unitID;
    public int count;
}

// 웨이브 하나의 고정 구성 - 웨이브 번호가 오를수록 더 강한 조합을 쓰도록 미리 정해둔다.
[System.Serializable]
public class AllyWaveComposition
{
    public List<AllyUnitGroup> units;
}

// 미션 씬에 아군 OC 생산 건물 하나당 하나씩 배치하는 "AI 관제소". EnemyAIDirector(doc/0532)의 아군판 -
// 시간에 맞춰 점점 강해지는 공격 웨이브를 적대 세력(외계종족/적대 OC) 쪽으로 보내고, 죽은 유닛을 보충
// 생산한다. 점령지 탈환 별동대/기지 피격 시 병력 소집은 이번 요청 범위 밖이라 뺐다(doc/0543 확인 필요
// 사항 1번 결정 - 필요해지면 EnemyAIDirector의 해당 로직을 그대로 옮겨오면 됨). 아군 OC는 현재 하나의
// 진영만 존재하므로 EnemyAIDirector의 EnemyFaction 분기도 두지 않는다.
public class AllyAIDirector : MonoBehaviour
{
    [Header("스폰 (여러 곳 지정 가능 - 생산 대기열이 가장 덜 찬 곳에 자동 분산, doc/0570)")]
    [SerializeField] private List<Transform> spawnPoints;

    [Header("배치형 방어 유닛 (씬에 미리 세워둔 고정 수비 유닛 - 죽으면 같은 자리에 같은 종류로 1회 재생산, 그 대체 유닛까지 죽으면 더 생산 안 함. 건물 재건은 범위 밖, doc/0552, doc/0558)")]
    [SerializeField] private List<AllyController> defenseUnits;

    [Header("공격 웨이브 타이밍 (점점 강해지는 패턴)")]
    [SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
    [SerializeField]
    private List<AllyWaveComposition> attackWaves = new List<AllyWaveComposition>
    {
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 10 } } }, // 1차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 8 }, new AllyUnitGroup { unitID = 4, count = 3 } } }, // 2차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 8 }, new AllyUnitGroup { unitID = 3, count = 3 }, new AllyUnitGroup { unitID = 5, count = 2 } } }, // 3차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 6 }, new AllyUnitGroup { unitID = 6, count = 3 }, new AllyUnitGroup { unitID = 7, count = 2 } } }, // 4차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 6, count = 3 }, new AllyUnitGroup { unitID = 8, count = 2 }, new AllyUnitGroup { unitID = 9, count = 1 } } }, // 5차(이후 반복)
    };

    [Header("집결지 (assembleBeforeAttack일 때만 사용)")]
    [SerializeField] private Transform rallyPoint; // 비워두면 spawnPoints 중 첫 유효한 위치를 집결지로 사용
    [SerializeField] private float rallyRadius = 3f;
    [SerializeField] private float rallyTimeout = 15f;
    [SerializeField] private bool assembleBeforeAttack = true; // 웨이브 인원이 rallyPoint에 다 모일 때까지 대기 후 한꺼번에 출발

    [Header("수비대 유지")]
    [SerializeField] private float reinforceCheckInterval = 20f;

    // 이 director가 스폰한 유닛 전체(원정 나간 유닛도 죽기 전까진 계속 포함 - ReinforceRoutine이 다음
    // 웨이브 구성에 맞춰 유지하는 기준). [SerializeField]로 노출해 Play 모드 인스펙터에서 현재 보유
    // 유닛을 바로 확인할 수 있게 함(EnemyAIDirector와 동일한 패턴, doc/0541).
    [Header("<디버그> 현재 보유 병력 (런타임 전용 - Play 모드에서만 채워짐)")]
    [SerializeField] private List<AllyController> garrison = new List<AllyController>();

    // 웨이브로 나가서 아직 살아있는 유닛(=현재 파견된 별동대) - deployed(HashSet)를 그대로 인스펙터에
    // 노출할 순 없어서 ReinforceRoutine 주기로 이 리스트에 복사해둔다(doc/0543).
    [Header("<디버그> 현재 파견된 공격 별동대")]
    [SerializeField] private List<AllyController> currentSquad = new List<AllyController>();

    // 이 director의 내부 풀(garrison)이 아니라, 현재 씬에 존재하는 아군 OC 유닛/건물 전체를 그대로
    // 보여주는 디버그 전용 리스트(EnemyAIDirector.allEnemyUnits/allEnemyBuildings와 동일한 패턴,
    // doc/0542/0553) - ReinforceRoutine 주기로 갱신. AllyController/AllyBuildingController는 적대
    // 컨트롤러와 독립된 타입이라(doc/0452) 따로 필터링 없이 타입 그대로 검색하면 아군만 잡힌다.
    [Header("<디버그> 현재 씬에 존재하는 아군 OC 전체 (런타임 전용, 주기적으로 갱신)")]
    [SerializeField] private List<AllyController> allAllyUnits = new List<AllyController>();
    [SerializeField] private List<AllyBuildingController> allAllyBuildings = new List<AllyBuildingController>();

    // 다음 공격 웨이브까지 남은 시간(초) - AttackWaveRoutine의 대기 코루틴이 매 프레임 갱신한다
    // (EnemyAIDirector.nextWaveCountdown과 동일한 패턴, doc/0546/0543).
    [Header("<디버그> 다음 웨이브까지 남은 시간(초)")]
    [SerializeField] private float nextWaveCountdown;

    // 웨이브로 이미 내보낸 유닛 - "보내고 끝"이라 돌아오지 않으므로 다음 웨이브 차출 대상에서 제외해
    // 같은 유닛을 두 번 부리지 않는다(EnemyAIDirector.deployed와 동일한 이유, doc/0532 결정 사항 #4).
    private readonly HashSet<AllyController> deployed = new HashSet<AllyController>();

    // 몇 번째 웨이브를 보냈는지(0부터) - attackWaves의 인덱스로 그대로 쓰인다. waveTimes 리스트를 다
    // 돌고 반복 구간에 들어가도 리셋하지 않고 계속 이어서 증가하며, attackWaves보다 커지면 마지막
    // 구성을 계속 반복한다(EnemyAIDirector.waveIndex와 동일한 패턴).
    private int waveIndex;

    private RTSUnitController rtsController;

    // 배치형 방어 유닛 슬롯 - Start() 시점의 위치/방향/종류를 기억해뒀다가, 그 유닛이 죽으면(current ==
    // null) 같은 자리에 같은 종류로 다시 세운다(doc/0552, EnemyAIDirector.DefenseSlot과 동일한 패턴) -
    // 단, 대체 생산은 슬롯당 1회뿐이라 그 대체 유닛까지 죽으면 더 이상 채우지 않는다(doc/0558).
    private class DefenseSlot
    {
        public int unitID;
        public Vector3 position;
        public Quaternion rotation;
        public AllyController current;
        public bool respawned; // 원본이 죽어 이미 한 번 대체 생산됐는지 - true면 그 대체 유닛이 죽어도 더 생산하지 않음(doc/0558)
    }

    private readonly List<DefenseSlot> defenseSlots = new List<DefenseSlot>();

    // 생산 대기열 항목 - 완성되면 garrison에 추가된다(EnemyAIDirector.EnemyProductionOrder와 동일한
    // 패턴, doc/0570 - 유닛별 생산시간을 지키도록).
    private class AllyProductionOrder
    {
        public int unitID;
        public float remainTime;
        public float totalTime;
    }

    // 스폰 지점 하나의 런타임 생산 대기열(EnemyAIDirector.SpawnQueue와 동일한 패턴 - 아군 OC 스폰
    // 지점엔 생산 건물 파괴 개념이 없어 그 부분만 뺌, doc/0570).
    private class AllySpawnQueue
    {
        public Transform spawnPoint;
        public readonly List<AllyProductionOrder> orders = new List<AllyProductionOrder>();
    }

    private readonly List<AllySpawnQueue> spawnQueues = new List<AllySpawnQueue>();

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();

        foreach (Transform sp in spawnPoints)
            if (sp != null)
                spawnQueues.Add(new AllySpawnQueue { spawnPoint = sp });

        foreach (AllyController unit in defenseUnits)
        {
            if (unit == null)
                continue;

            defenseSlots.Add(new DefenseSlot
            {
                unitID = unit.GetAllyUnitID(),
                position = unit.transform.position,
                rotation = unit.transform.rotation,
                current = unit,
            });
        }

        FillPool(CurrentWaveComposition());

        if (waveTimes.Count > 0)
            StartCoroutine(AttackWaveRoutine());
        StartCoroutine(ReinforceRoutine());
    }

    // 스폰 지점마다 독립된 생산 대기열을 매 프레임 진행한다(EnemyAIDirector.Update()와 동일한 패턴,
    // doc/0570).
    private void Update()
    {
        foreach (AllySpawnQueue sq in spawnQueues)
        {
            if (sq.orders.Count == 0)
                continue;

            AllyProductionOrder front = sq.orders[0];
            front.remainTime -= Time.deltaTime;
            if (front.remainTime > 0f)
                continue;

            sq.orders.RemoveAt(0);

            AllyController unit = SpawnUnit(front.unitID, sq.spawnPoint);
            if (unit != null)
                garrison.Add(unit);
        }
    }

    // ======================
    // 1. 시간에 맞춰 점점 강해지는 공격 웨이브
    // ======================
    private IEnumerator AttackWaveRoutine()
    {
        while (true)
        {
            yield return CountdownSeconds(WaveIntervalFor(waveIndex));

            yield return WaitUntilTargetExists(); // doc/0565 - 공격할 건물이 없으면 보류(생산은 계속)
            yield return WaitUntilReady(CurrentWaveComposition());
            yield return LaunchWave(); // doc/0560: 별동대가 전멸할 때까지 여기서 대기
        }
    }

    // 공격할 적대 건물이 하나도 없으면 계속 폴링 대기한다(그동안도 ReinforceRoutine은 별개 코루틴이라
    // 계속 생산됨) - 대상이 생기는 즉시(플레이어가 새로 짓거나 파괴됐던 건물이 다시 있는 등) 재개된다.
    // TakeSquad()는 LaunchWave() 안에서 이 대기가 끝난 뒤에야 불리므로, 보류 중엔 병력이 garrison에
    // 그대로 남아 낭비되지 않는다(doc/0565).
    private IEnumerator WaitUntilTargetExists()
    {
        while (PickAttackTarget() == null)
            yield return new WaitForSeconds(1f);
    }

    // waveTimes[index] 기준 이번 사이클의 대기 시간 - 리스트를 넘어서면 마지막 두 항목 간격을 반복한다
    // (EnemyAIDirector.WaveIntervalFor와 동일한 패턴, doc/0560). 이 간격은 미션 시작 시각이 아니라
    // "이전 웨이브가 전멸한 시점" 기준으로 잰다.
    private float WaveIntervalFor(int index)
    {
        if (waveTimes.Count == 0)
            return 0f;
        if (index == 0)
            return waveTimes[0];
        if (index < waveTimes.Count)
            return waveTimes[index] - waveTimes[index - 1];

        return Mathf.Max(1f, waveTimes.Count >= 2 ? waveTimes[^1] - waveTimes[^2] : waveTimes[0]);
    }

    // 시각이 지나도 구성이 안 갖춰지면 계속 폴링 대기한다(그동안 ReinforceRoutine이 계속 채움) -
    // EnemyAIDirector.WaitUntilReady/IsComposeReady와 동일한 패턴(doc/0560 신규 추가).
    private IEnumerator WaitUntilReady(List<AllyUnitGroup> composition)
    {
        while (!IsComposeReady(composition))
            yield return new WaitForSeconds(1f);
    }

    private bool IsComposeReady(List<AllyUnitGroup> composition)
    {
        garrison.RemoveAll(u => u == null);

        foreach (AllyUnitGroup group in composition)
        {
            int available = 0;
            foreach (AllyController unit in garrison)
                if (unit != null && !deployed.Contains(unit) && unit.GetAllyUnitID() == group.unitID)
                    available++;

            if (available < group.count)
                return false;
        }

        return true;
    }

    // seconds초를 매 프레임 카운트다운하며 nextWaveCountdown을 계속 갱신한다 - 인스펙터에서 다음 웨이브
    // 출발까지 남은 시간을 실시간으로 볼 수 있다(EnemyAIDirector.CountdownSeconds와 동일한 패턴,
    // doc/0546/0543).
    private IEnumerator CountdownSeconds(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            nextWaveCountdown = remaining;
            yield return null;
            remaining -= Time.deltaTime;
        }
        nextWaveCountdown = 0f;
    }

    private IEnumerator LaunchWave()
    {
        List<AllyUnitGroup> composition = CurrentWaveComposition();
        waveIndex++;

        List<AllyController> squad = TakeSquad(composition);
        if (squad.Count == 0)
            yield break;

        if (assembleBeforeAttack)
            yield return AssembleAtRally(squad);

        // doc/0560: fire-and-forget이던 것을 직접 대기로 바꿔 AttackWaveRoutine이 전멸까지 기다리게 한다
        // (EnemyAIDirector와 동일한 이유로 구 결정 번복).
        yield return RunWaveSquad(squad);
    }

    // 이번 웨이브에 보낼 구성 - attackWaves[waveIndex], 리스트를 넘어서면 마지막 구성을 계속 반복한다.
    private List<AllyUnitGroup> CurrentWaveComposition()
    {
        if (attackWaves.Count == 0)
            return new List<AllyUnitGroup>();

        int index = Mathf.Min(waveIndex, attackWaves.Count - 1);
        return attackWaves[index].units;
    }

    // 부대가 전멸할 때까지: 목표가 없으면(최초, 또는 방금 파괴됨) PickAttackTarget()으로 다시 뽑아
    // 전원에게 재발령한다(EnemyAIDirector.RunWaveSquad와 동일한 패턴).
    private IEnumerator RunWaveSquad(List<AllyController> squad)
    {
        EnemyBuildingController target = null;

        while (true)
        {
            squad.RemoveAll(u => u == null);
            if (squad.Count == 0)
                yield break; // 전멸 - 이 웨이브 종료

            if (target == null)
            {
                target = PickAttackTarget();
                if (target != null)
                {
                    foreach (AllyController unit in squad)
                        if (unit != null)
                            unit.AttackMoveTo(target.transform.position);
                }
                // doc/0565: 목표가 없어도 포기(yield break)하지 않는다 - 다음 프레임에 다시 찾는다.
                // 부대가 전멸하기 전까진 이 코루틴이 끝나지 않아야 "전멸해야 다음 웨이브"(doc/0560)
                // 규칙이 "목표를 다 잃으면"에서 새지 않는다.
            }

            yield return null;
        }
    }

    // Spore Brood Building Data SO(doc/0543)에서 Hive Core에 부여된 ID - 살아있는 동안은 항상 최우선
    // 공격 목표로 삼는다.
    private const int HiveCoreBuildingID = 7;

    // 진짜 적대 세력(적 OC/외계종족) 건물 중 목표 하나를 고른다: Hive Core가 살아있으면 그것부터, 없으면
    // (파괴됐거나 애초에 이 미션에 없음) EnemyBuildingController.ActiveBuildings 등록 순서대로 차례차례
    // (doc/0543 - "Hive Core 먼저, 그 다음은 순서대로"). 이 리스트는 등록(Start)/해제(파괴) 시에만
    // 바뀌므로 매 웨이브 같은 순서를 유지하다가 앞에서부터 하나씩 사라진다. 아군 OC 건물
    // (AllyBuildingController)도 같이 등록돼 있으므로(같은 컴포넌트를 상속) 타입으로 걸러낸다 - 새 태그
    // 체계를 만들지 않고 기존 전역 레지스트리를 재사용한다(doc/0543).
    private EnemyBuildingController PickAttackTarget()
    {
        List<EnemyBuildingController> hostileBuildings =
            EnemyBuildingController.ActiveBuildings.FindAll(b => b != null && !(b is AllyBuildingController));

        if (hostileBuildings.Count == 0)
            return null;

        EnemyBuildingController hiveCore = hostileBuildings.Find(b => b.GetBuildingID() == HiveCoreBuildingID);
        return hiveCore != null ? hiveCore : hostileBuildings[0];
    }

    private IEnumerator AssembleAtRally(List<AllyController> squad)
    {
        Vector3 rally = rallyPoint != null ? rallyPoint.position : DefaultRallyPosition();

        foreach (AllyController unit in squad)
            if (unit != null)
                unit.MoveTo(rally);

        float elapsed = 0f;
        while (elapsed < rallyTimeout)
        {
            bool allArrived = true;
            foreach (AllyController unit in squad)
            {
                if (unit == null)
                    continue;
                if (Vector3.Distance(unit.transform.position, rally) > rallyRadius)
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // rallyPoint를 인스펙터에 안 넣었을 때의 기본 집결지 - spawnPoints 중 첫 유효한 위치, 그마저 없으면
    // 이 오브젝트의 위치(EnemyAIDirector.DefaultRallyPosition과 동일한 패턴, doc/0544).
    private Vector3 DefaultRallyPosition()
    {
        foreach (Transform sp in spawnPoints)
            if (sp != null)
                return sp.position;

        return transform.position;
    }

    // ======================
    // 2. 죽은 유닛 보충 생산
    // ======================
    private IEnumerator ReinforceRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(reinforceCheckInterval);
        while (true)
        {
            yield return wait;

            garrison.RemoveAll(u => u == null);
            deployed.RemoveWhere(u => u == null);

            // 다음에 나갈 웨이브(아직 발사 안 한 waveIndex)의 구성을 미리 갖춰둔다 - 웨이브가 실제로
            // 발사되는 순간 그제서야 스폰하면 도착까지 시간이 안 맞으므로 항상 선제적으로 채워둔다.
            FillPool(CurrentWaveComposition());

            RespawnDeadDefenseUnits();

            // 디버그용 리스트 갱신(doc/0543) - 현재 파견된 별동대(deployed) + 씬 전체 아군 OC 스냅샷.
            currentSquad.Clear();
            currentSquad.AddRange(deployed);

            allAllyUnits.Clear();
            allAllyUnits.AddRange(FindObjectsByType<AllyController>(FindObjectsInactive.Exclude));
            allAllyBuildings.Clear();
            allAllyBuildings.AddRange(FindObjectsByType<AllyBuildingController>(FindObjectsInactive.Exclude));
        }
    }

    // defenseSlots 중 유닛이 죽어서 비어있고(current == null) 아직 대체 생산을 한 번도 안 한(!respawned)
    // 자리마다 같은 종류를 같은 위치/방향으로 즉시 다시 세운다(doc/0552, EnemyAIDirector와 동일한
    // 패턴) - garrison과 달리 별도 웨이브 큐 없이 바로 Instantiate한다(자리를 지키는 게 목적이라
    // 집결/이동이 필요 없음). 대체 생산은 슬롯당 딱 1번뿐이라, 그 대체 유닛까지 죽으면
    // (respawned == true) 더 이상 채우지 않는다(doc/0558).
    private void RespawnDeadDefenseUnits()
    {
        foreach (DefenseSlot slot in defenseSlots)
        {
            if (slot.current != null || slot.respawned)
                continue;

            UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(slot.unitID) : null;
            if (data == null || data.AllyPrefab == null)
                continue;

            GameObject spawned = Instantiate(data.AllyPrefab, slot.position, slot.rotation);
            if (spawned.TryGetComponent<AllyController>(out AllyController unit))
                slot.current = unit;
            slot.respawned = true;
        }
    }

    // composition이 요구하는 유닛 종류별 개수(보유 + 이미 생산 대기열에 들어간 수)가 못 미치면 부족한
    // 만큼 생산을 주문한다(EnemyAIDirector.FillPool과 동일한 패턴, doc/0570) - 즉시 Instantiate하지
    // 않고, 스폰 지점 중 대기열이 가장 덜 찬 곳에 자동 분산된다(LeastLoadedQueue).
    private void FillPool(List<AllyUnitGroup> composition)
    {
        garrison.RemoveAll(u => u == null);

        foreach (AllyUnitGroup group in composition)
        {
            int have = garrison.FindAll(u => u != null && u.GetAllyUnitID() == group.unitID).Count
                + PendingCount(group.unitID);

            for (int i = have; i < group.count; i++)
                EnqueueProduction(group.unitID);
        }
    }

    // 이미 생산 대기열에 들어가 있어 완성을 기다리는 중인 unitID 개수(EnemyAIDirector.PendingCount와
    // 동일한 패턴) - 안 세면 ReinforceRoutine이 돌 때마다 같은 부족분을 중복 주문하게 된다.
    private int PendingCount(int unitID)
    {
        int count = 0;
        foreach (AllySpawnQueue sq in spawnQueues)
            foreach (AllyProductionOrder order in sq.orders)
                if (order.unitID == unitID)
                    count++;
        return count;
    }

    private void EnqueueProduction(int unitID)
    {
        if (rtsController == null)
            return;

        UnitData data = rtsController.GetEnemyUnitData(unitID);
        if (data == null || data.AllyPrefab == null)
            return;

        AllySpawnQueue sq = LeastLoadedQueue();
        if (sq == null)
            return; // 스폰 지점이 하나도 없음

        sq.orders.Add(new AllyProductionOrder
        {
            unitID = unitID,
            remainTime = data.productionTime,
            totalTime = data.productionTime,
        });
    }

    // 남은 생산 시간의 합이 가장 적은 스폰 지점을 고른다(EnemyAIDirector.LeastLoadedQueue와 동일한
    // 패턴, doc/0570) - 유닛마다 생산 시간이 달라서 개수가 아니라 시간 합으로 비교해야 실제로 고르게
    // 분산된다.
    private AllySpawnQueue LeastLoadedQueue()
    {
        AllySpawnQueue best = null;
        float bestLoad = float.MaxValue;

        foreach (AllySpawnQueue sq in spawnQueues)
        {
            float load = QueueLoad(sq);
            if (load < bestLoad)
            {
                bestLoad = load;
                best = sq;
            }
        }

        return best;
    }

    private float QueueLoad(AllySpawnQueue sq)
    {
        float total = 0f;
        for (int i = 0; i < sq.orders.Count; i++)
            total += i == 0 ? sq.orders[i].remainTime : sq.orders[i].totalTime;
        return total;
    }

    // unitID로 OC 로스터 DB(OC Unit Data SO)를 조회해 아군 전용 프리팹(UnitData.AllyPrefab)을 자동으로
    // 얻는다 - EnemyAIDirector가 GetEnemyUnitData(id).Prefab으로 적대 프리팹을 얻는 것과 동일한 패턴
    // (doc/0543). 미션 제작자가 unitID별로 프리팹을 director 인스펙터에 직접 연결할 필요가 없다 - 웨이브
    // 구성표(attackWaves)에 unitID만 적어두면 이 조회로 자동 스폰된다.
    private AllyController SpawnUnit(int unitID, Transform spawnPoint)
    {
        UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(unitID) : null;
        if (data == null || data.AllyPrefab == null || spawnPoint == null)
            return null;

        GameObject spawned = Instantiate(data.AllyPrefab, spawnPoint.position, spawnPoint.rotation);
        if (!spawned.TryGetComponent<AllyController>(out AllyController unit))
            return null;

        unit.MoveTo(rallyPoint != null ? rallyPoint.position : DefaultRallyPosition()); // 생산되자마자 집결지로 (EnemyAIDirector와 동일한 패턴, doc/0545, doc/0564)
        return unit;
    }

    // garrison에서 composition이 요구하는 유닛 종류별 개수만큼(아직 원정 안 나간 것만) 뽑아 deployed에
    // 등록한다 - 뽑힌 유닛은 이후 재사용(다음 웨이브)되지 않는다. 특정 종류가 부족하면 그만큼만 못
    // 채우고 반환한다(ReinforceRoutine이 미리 채워두므로 평소엔 안 부족함).
    private List<AllyController> TakeSquad(List<AllyUnitGroup> composition)
    {
        garrison.RemoveAll(u => u == null);

        List<AllyController> squad = new List<AllyController>();
        foreach (AllyUnitGroup group in composition)
        {
            int taken = 0;
            foreach (AllyController unit in garrison)
            {
                if (taken >= group.count)
                    break;
                if (deployed.Contains(unit) || unit.GetAllyUnitID() != group.unitID)
                    continue;

                squad.Add(unit);
                deployed.Add(unit);
                taken++;
            }
        }

        return squad;
    }
}
