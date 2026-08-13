using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 어떤 진영(OC/Spore Brood)의 웨이브/별동대 구성인지 구분하는 용도 - 실제 동작 차이는 이 값으로
// 분기하지 않고 attackWaves/raidSquadComposition에 이미 어떤 유닛 ID를 쓸지 명시되어 있으므로,
// 여기선 인스펙터에서 구분하기 위한 라벨일 뿐(doc/0532 "진영별 차이", doc/0539).
public enum EnemyFaction { OC, SporeBrood }

// 유닛 종류(ID) + 마릿수 하나의 묶음. 웨이브/별동대 구성의 최소 단위(doc/0539).
[System.Serializable]
public class UnitGroup
{
    public int unitID;
    public int count;
}

// 웨이브 하나의 고정 구성(예: "Cyborg Soldier 8 + Railgunner 3") - 스타크래프트1 Custom AI처럼
// 웨이브 번호별로 미리 정해진 편성을 쓴다(doc/0539).
[System.Serializable]
public class WaveComposition
{
    public List<UnitGroup> units;
}

// 웨이브 하나(예: "3차")에 대해 미리 준비해둔 구성 패턴 여러 개 - 그 웨이브 차례가 되면 이 중 하나를
// 무작위로 골라 쓴다(doc/0551) - 매번 같은 조합만 나오지 않도록.
[System.Serializable]
public class WaveVariants
{
    public List<WaveComposition> variants;
}

// 스폰 지점 하나 - productionBuilding을 넣어두면 그 건물이 파괴됐을 때 이 지점의 생산이 멈춘다.
// 비워두면(마커만 있으면) 항상 쓸 수 있는 고정 스폰 지점으로 취급한다(doc/0544).
[System.Serializable]
public class EnemySpawnPoint
{
    public Transform point;
    public EnemyBuildingController productionBuilding;
}

// 미션 씬에 적 기지 하나당 하나씩 배치하는 "AI 관제소". 시간에 맞춰 공격 웨이브를 보내고, 점령지에
// 별동대를 보내고, 기지가 공격받으면 병력을 소집하고, 죽은 유닛을 보충 생산한다 (doc/0532 설계안).
public class EnemyAIDirector : MonoBehaviour
{
    [Header("진영 선택 - 아래 <OC>/<Spore Brood> 구역 중 어느 쪽을 쓸지 결정")]
    [SerializeField] private EnemyFaction faction;

    [Header("<공통> 스폰 (여러 곳 지정 가능, doc/0544)")]
    [SerializeField] private List<EnemySpawnPoint> spawnPoints;

    [Header("<공통> 기지 방어")]
    [SerializeField] private List<EnemyBuildingController> homeBuildings; // 메인기지/미션 오브젝트 등 방어 트리거로 삼을 건물들(doc/0535)
    [SerializeField] private float defenseRadius = 15f; // 공격받은 건물 기준, 방어하러 부를 유닛을 찾는 반경(doc/0535)

    [Header("<공통> 배치형 방어 유닛 (씬에 미리 세워둔 고정 수비 유닛 - 죽으면 같은 자리에 같은 종류로 즉시 재생산. 건물 재건은 범위 밖, doc/0552)")]
    [SerializeField] private List<EnemyUnitController> defenseUnits;

    [Header("<공통> 공격 웨이브 타이밍")]
    [SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900. 실제 출발은 구성이 준비된 이후(doc/0544)

    [Header("<OC> 공격 웨이브 구성 - 웨이브별 3가지 패턴 중 무작위(doc/0551, doc/0539/0550 물량 기준)")]
    [SerializeField] private List<WaveVariants> attackWavesOC = new List<WaveVariants>
    {
        new WaveVariants { variants = new List<WaveComposition> // 1차 (기준 5명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 5 } } }, // 보병 러시
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 3 }, new UnitGroup { unitID = 3, count = 2 } } }, // 경장갑 스카웃 믹스
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 2 }, new UnitGroup { unitID = 4, count = 2 }, new UnitGroup { unitID = 3, count = 1 } } }, // 저격수 위주
        }},
        new WaveVariants { variants = new List<WaveComposition> // 2차 (기준 6명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 4 }, new UnitGroup { unitID = 4, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 3 }, new UnitGroup { unitID = 3, count = 3 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 2 }, new UnitGroup { unitID = 4, count = 1 }, new UnitGroup { unitID = 5, count = 1 }, new UnitGroup { unitID = 3, count = 2 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 3차 (기준 7명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 4 }, new UnitGroup { unitID = 3, count = 2 }, new UnitGroup { unitID = 5, count = 1 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 3 }, new UnitGroup { unitID = 4, count = 2 }, new UnitGroup { unitID = 5, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 3, count = 3 }, new UnitGroup { unitID = 5, count = 2 }, new UnitGroup { unitID = 4, count = 2 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 4차 (기준 6명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 3 }, new UnitGroup { unitID = 6, count = 2 }, new UnitGroup { unitID = 7, count = 1 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 5, count = 2 }, new UnitGroup { unitID = 6, count = 2 }, new UnitGroup { unitID = 4, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 2, count = 2 }, new UnitGroup { unitID = 7, count = 2 }, new UnitGroup { unitID = 6, count = 1 }, new UnitGroup { unitID = 3, count = 1 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 5차(이후 반복) (기준 4명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 6, count = 2 }, new UnitGroup { unitID = 8, count = 1 }, new UnitGroup { unitID = 9, count = 1 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 6, count = 1 }, new UnitGroup { unitID = 8, count = 2 }, new UnitGroup { unitID = 7, count = 1 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 9, count = 1 }, new UnitGroup { unitID = 6, count = 1 }, new UnitGroup { unitID = 5, count = 2 } } },
        }},
    };

    [Header("<Spore Brood> 공격 웨이브 구성 - 웨이브별 3가지 패턴 중 무작위(doc/0551, doc/0539/0550 물량 기준)")]
    [SerializeField] private List<WaveVariants> attackWavesSporeBrood = new List<WaveVariants>
    {
        new WaveVariants { variants = new List<WaveComposition> // 1차 (기준 7명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 7 } } }, // 근접 러시
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 5 }, new UnitGroup { unitID = 11, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 4 }, new UnitGroup { unitID = 11, count = 2 }, new UnitGroup { unitID = 12, count = 1 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 2차 (기준 8명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 5 }, new UnitGroup { unitID = 11, count = 3 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 6 }, new UnitGroup { unitID = 12, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 11, count = 4 }, new UnitGroup { unitID = 12, count = 2 }, new UnitGroup { unitID = 10, count = 2 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 3차 (기준 6명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 11, count = 4 }, new UnitGroup { unitID = 12, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 4 }, new UnitGroup { unitID = 11, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 2 }, new UnitGroup { unitID = 11, count = 2 }, new UnitGroup { unitID = 12, count = 2 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 4차 (기준 10명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 6 }, new UnitGroup { unitID = 11, count = 4 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 5 }, new UnitGroup { unitID = 12, count = 3 }, new UnitGroup { unitID = 11, count = 2 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 11, count = 5 }, new UnitGroup { unitID = 12, count = 3 }, new UnitGroup { unitID = 10, count = 2 } } },
        }},
        new WaveVariants { variants = new List<WaveComposition> // 5차(이후 반복) (기준 12명)
        {
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 5 }, new UnitGroup { unitID = 11, count = 4 }, new UnitGroup { unitID = 12, count = 3 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 10, count = 6 }, new UnitGroup { unitID = 11, count = 3 }, new UnitGroup { unitID = 12, count = 3 } } },
            new WaveComposition { units = new List<UnitGroup> { new UnitGroup { unitID = 11, count = 5 }, new UnitGroup { unitID = 12, count = 4 }, new UnitGroup { unitID = 10, count = 3 } } },
        }},
    };

    [Header("<공통> 집결지 (AssembleBeforeAttack일 때만 사용)")]
    [SerializeField] private Transform rallyPoint; // 비워두면 spawnPoints[0] 위치를 집결지로 사용(doc/0544)
    [SerializeField] private float rallyRadius = 3f;
    [SerializeField] private float rallyTimeout = 15f;

    [Header("<공통> 수비대 유지")]
    [SerializeField] private float reinforceCheckInterval = 20f;

    [Header("<공통> 점령지 탈환 타이밍")]
    [SerializeField] private List<CaptureSystem> raidTargets;
    [SerializeField] private float raidInterval = 45f;

    [Header("<OC> 별동대 구성 (doc/0539 콘텐츠 초안)")]
    [SerializeField] private List<UnitGroup> raidSquadCompositionOC = new List<UnitGroup>
    {
        new UnitGroup { unitID = 2, count = 2 }, // Cyborg Soldier x2
        new UnitGroup { unitID = 3, count = 1 }, // Striker x1
    };

    [Header("<Spore Brood> 별동대 구성 (doc/0539 콘텐츠 초안)")]
    [SerializeField] private List<UnitGroup> raidSquadCompositionSporeBrood = new List<UnitGroup>
    {
        new UnitGroup { unitID = 10, count = 2 }, // Ripfang x2
        new UnitGroup { unitID = 11, count = 1 }, // Spitter x1
    };

    [Header("<OC> 웨이브 집결 여부")]
    [SerializeField] private bool assembleBeforeAttackOC = true; // 웨이브 인원이 rallyPoint에 다 모일 때까지 대기 후 한꺼번에 출발(doc/0532)

    [Header("<Spore Brood> 웨이브 집결 여부")]
    [SerializeField] private bool assembleBeforeAttackSporeBrood = false; // 집결 없이 스폰되는 즉시 개별 출발(doc/0532)

    // faction에 따라 위 <OC>/<Spore Brood> 필드 중 하나를 골라주는 프로퍼티 - 이 셋을 거치면 나머지
    // 로직은 진영을 몰라도 된다(doc/0540).
    private List<WaveVariants> AttackWaves => faction == EnemyFaction.OC ? attackWavesOC : attackWavesSporeBrood;
    private List<UnitGroup> RaidSquadComposition => faction == EnemyFaction.OC ? raidSquadCompositionOC : raidSquadCompositionSporeBrood;
    private bool AssembleBeforeAttack => faction == EnemyFaction.OC ? assembleBeforeAttackOC : assembleBeforeAttackSporeBrood;

    private RTSUnitController rtsController;

    // 이 director가 스폰한 유닛 전체(원정 나간 유닛도 죽기 전까진 계속 포함 - ReinforceRoutine이 다음
    // 웨이브 구성에 맞춰 유지하는 기준). null(죽은 유닛)은 각 루틴에서 그때그때 정리한다. 공격 웨이브
    // 전용 풀 - 점령지 탈환 별동대는 raidGarrison이라는 별도 풀을 쓴다(doc/0538, 두 시스템이 같은
    // 인원을 놓고 경쟁하지 않도록). [SerializeField]로 노출해 인스펙터에서 바로 확인 가능(doc/0546).
    [Header("<디버그> 공격 별동대(웨이브) 현재 병력")]
    [SerializeField] private List<EnemyUnitController> garrison = new List<EnemyUnitController>();

    // 점령지 탈환 별동대 전용 병력 풀(doc/0538) - garrison과 완전히 분리, raidSquadComposition으로 유지.
    [Header("<디버그> 점령 별동대 현재 병력")]
    [SerializeField] private List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();

    // 다음 공격 웨이브/점령 별동대까지 남은 시간(초) - AttackWaveRoutine/RaidRoutine의 대기 코루틴이
    // 매 프레임 갱신한다. 0이면 예정 시각은 지났고 구성이 갖춰지길(WaitUntilReady) 기다리는 중(doc/0546).
    [Header("<디버그> 다음 웨이브/별동대까지 남은 시간(초)")]
    [SerializeField] private float nextWaveCountdown;
    [SerializeField] private float nextRaidCountdown;

    // 이 director의 내부 풀(garrison/raidGarrison)이 아니라, 현재 씬에 존재하는 적 유닛/건물 전체를
    // 그대로 보여주는 디버그 전용 리스트(doc/0542) - ReinforceRoutine 주기로 갱신. 유닛/건물엔 소속
    // 진영을 구분하는 필드가 없어 director가 여러 개면 전부 동일한 "씬 전체" 목록이 뜬다(doc/0542 참고).
    [Header("<디버그> 현재 씬에 존재하는 적 전체 (런타임 전용, 주기적으로 갱신)")]
    [SerializeField] private List<EnemyUnitController> allEnemyUnits = new List<EnemyUnitController>();
    [SerializeField] private List<EnemyBuildingController> allEnemyBuildings = new List<EnemyBuildingController>();

    // 웨이브/별동대로 이미 내보낸 유닛 - "보내고 끝"(doc/0532 결정 사항 #4)이라 돌아오지 않으므로,
    // 다음 웨이브/별동대 차출이나 기지 방어 소집 대상에서 제외해 같은 유닛을 두 번 부리지 않는다.
    private readonly HashSet<EnemyUnitController> deployed = new HashSet<EnemyUnitController>();

    // 현재 파견된(또는 다음에 파견할) 점령 별동대 - 전멸하기 전까진 새로 편성하지 않고 이 부대를 그대로
    // 재사용해서 다음 점령지로 보낸다(doc/0549). 웨이브(garrison)와 달리 별동대는 매번 새 부대가 아니라
    // 하나의 부대가 여러 점령지를 순회하는 구조.
    private readonly List<EnemyUnitController> currentRaidSquad = new List<EnemyUnitController>();

    // 몇 번째 웨이브를 보냈는지(0부터) - attackWaves의 인덱스로 그대로 쓰인다. waveTimes 리스트를 다
    // 돌고 반복 구간에 들어가도 리셋하지 않고 계속 이어서 증가하며, attackWaves보다 커지면 마지막
    // 구성을 계속 반복한다(doc/0539).
    private int waveIndex;

    // OnDamaged 이벤트엔 "어느 건물이 맞았는지"가 안 실려오므로, 건물별로 그 건물을 캡처한 델리게이트를
    // 만들어 구독하고 여기 보관해뒀다가 OnDisable에서 정확히 그 델리게이트로 해지한다(doc/0535).
    private readonly Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>> baseDefenseHandlers
        = new Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>>();

    // 생산 대기열 항목 - 완성되면 destinationPool(garrison 또는 raidGarrison)에 추가된다(doc/0544).
    private class EnemyProductionOrder
    {
        public int unitID;
        public float remainTime;
        public float totalTime;
        public List<EnemyUnitController> destinationPool;
    }

    // 스폰 지점 하나의 런타임 생산 대기열. hasProductionBuilding은 Start() 시점에 캐싱해둔다 - 건물이
    // 나중에 파괴돼도(참조가 null이 됨) "원래 건물이 있었는지"를 구분할 수 있어야 IsAvailable이
    // 정확해진다(doc/0544).
    private class SpawnQueue
    {
        public EnemySpawnPoint spawnPoint;
        public bool hasProductionBuilding;
        public readonly List<EnemyProductionOrder> orders = new List<EnemyProductionOrder>();

        public bool IsAvailable => !hasProductionBuilding || spawnPoint.productionBuilding != null;
    }

    private readonly List<SpawnQueue> spawnQueues = new List<SpawnQueue>();

    // 배치형 방어 유닛 슬롯 - Start() 시점의 위치/방향/종류를 기억해뒀다가, 그 유닛이 죽으면(current ==
    // null) 같은 자리에 같은 종류로 다시 세운다(doc/0552). 생산 대기열(EnqueueProduction)은 spawnPoint
    // 기준이라 임의의 방어 위치엔 안 맞으므로, 이 슬롯은 즉시 Instantiate하는 별도의 단순한 경로를 쓴다.
    private class DefenseSlot
    {
        public int unitID;
        public Vector3 position;
        public Quaternion rotation;
        public EnemyUnitController current;
    }

    private readonly List<DefenseSlot> defenseSlots = new List<DefenseSlot>();

    private void OnEnable()
    {
        foreach (EnemyBuildingController building in homeBuildings)
        {
            if (building == null || building.GetHealthManager() == null)
                continue;

            EnemyBuildingController capturedBuilding = building;
            System.Action<int, Vector3, AttackEffectType, bool> handler =
                (damage, attackerPosition, type, isEnemyAttacker) => HandleBaseAttacked(capturedBuilding, attackerPosition, isEnemyAttacker);

            baseDefenseHandlers[building] = handler;
            building.GetHealthManager().OnDamaged += handler;
        }
    }

    private void OnDisable()
    {
        foreach (var pair in baseDefenseHandlers)
            if (pair.Key != null && pair.Key.GetHealthManager() != null)
                pair.Key.GetHealthManager().OnDamaged -= pair.Value;

        baseDefenseHandlers.Clear();
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();

        foreach (EnemySpawnPoint sp in spawnPoints)
        {
            if (sp == null || sp.point == null)
                continue;

            spawnQueues.Add(new SpawnQueue { spawnPoint = sp, hasProductionBuilding = sp.productionBuilding != null });
        }

        foreach (EnemyUnitController unit in defenseUnits)
        {
            if (unit == null)
                continue;

            defenseSlots.Add(new DefenseSlot
            {
                unitID = unit.GetEnemyUnitID(),
                position = unit.transform.position,
                rotation = unit.transform.rotation,
                current = unit,
            });
        }

        FillPool(garrison, CurrentWaveComposition());
        FillPool(raidGarrison, RaidSquadComposition);

        if (waveTimes.Count > 0)
            StartCoroutine(AttackWaveRoutine());
        if (raidTargets.Count > 0)
            StartCoroutine(RaidRoutine());
        StartCoroutine(ReinforceRoutine());
    }

    // 스폰 지점마다 독립된 생산 대기열을 매 프레임 진행한다(UnitSpawner.Produce()와 동일한 FIFO 방식,
    // doc/0544). 생산 건물이 파괴된(IsAvailable == false) 큐는 미완성 주문을 통째로 버린다 - 다음
    // FillPool 체크 때 살아있는 다른 스폰 지점에서 다시 주문되므로 별도 재배치 로직이 필요 없다.
    private void Update()
    {
        foreach (SpawnQueue sq in spawnQueues)
        {
            if (!sq.IsAvailable)
            {
                sq.orders.Clear();
                continue;
            }

            if (sq.orders.Count == 0)
                continue;

            EnemyProductionOrder front = sq.orders[0];
            front.remainTime -= Time.deltaTime;
            if (front.remainTime > 0f)
                continue;

            sq.orders.RemoveAt(0);

            UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(front.unitID) : null;
            if (data == null || data.Prefab == null)
                continue;

            GameObject spawned = Instantiate(data.Prefab, sq.spawnPoint.point.position, sq.spawnPoint.point.rotation);
            if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
            {
                front.destinationPool.Add(unit);
                unit.MoveTo(DefaultRallyPosition()); // 생산되자마자 집결지로 - 웨이브/별동대 공통(doc/0545)
            }
        }
    }

    // ======================
    // 1. 시간에 맞춰 공격 웨이브
    // ======================
    private IEnumerator AttackWaveRoutine()
    {
        for (int i = 0; i < waveTimes.Count; i++)
        {
            float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
            yield return CountdownSeconds(wait, v => nextWaveCountdown = v);

            // 카운트다운이 끝난 뒤에 확인한다 - Start() 직후(0프레임째)엔 플레이어 건물이 아직 자기
            // Start()에서 BuildingList에 등록되기 전이라(스크립트 실행 순서 미보장) 여기서 즉시 확인하면
            // 항상 "전멸"로 오판해서 웨이브 타이머 자체가 한 번도 안 도는 버그가 있었다(doc/0548).
            if (IsPlayerDefeated())
                yield break; // 더 공격할 대상이 없음 - 웨이브 스케줄 보류(doc/0547)

            yield return WaitUntilReady(garrison, CurrentWaveComposition());
            yield return LaunchWave();
        }

        // 리스트를 다 쓰면 끝내지 않고 마지막 두 항목의 간격으로 계속 반복한다(doc/0532 결정 사항 #2).
        float repeatInterval = Mathf.Max(1f, waveTimes.Count >= 2
            ? waveTimes[^1] - waveTimes[^2]
            : waveTimes[0]);

        while (true)
        {
            yield return CountdownSeconds(repeatInterval, v => nextWaveCountdown = v);

            if (IsPlayerDefeated())
                yield break;

            yield return WaitUntilReady(garrison, CurrentWaveComposition());
            yield return LaunchWave();
        }
    }

    // 플레이어 건물이 하나도 안 남았는지 - 더 공격/점령할 대상 자체가 없다는 신호로 쓴다(doc/0547).
    private bool IsPlayerDefeated() =>
        rtsController == null || rtsController.BuildingList.FindAll(b => b != null).Count == 0;

    // seconds초를 매 프레임 카운트다운하며 setRemaining으로 남은 시간을 계속 알려준다 - 인스펙터
    // 디버그 필드(nextWaveCountdown/nextRaidCountdown)를 실시간으로 갱신하기 위한 WaitForSeconds 대체
    // (doc/0546).
    private IEnumerator CountdownSeconds(float seconds, System.Action<float> setRemaining)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            setRemaining(remaining);
            yield return null;
            remaining -= Time.deltaTime;
        }
        setRemaining(0f);
    }

    // composition이 요구하는 유닛이 pool에 전부(원정 안 나간 채로) 갖춰질 때까지 대기한다(doc/0544) -
    // "예정 시각이 됐어도 준비가 안 됐으면 공격/별동대 출발 안 함" 요청 반영. 생산 건물이 전부 파괴돼
    // 영영 못 채우면 이 대기도 무기한 계속된다(의도된 동작, doc/0544 결정 사항 #3) - ReinforceRoutine이
    // 계속 채우려고 시도하므로 여기선 폴링만 한다.
    private IEnumerator WaitUntilReady(List<EnemyUnitController> pool, List<UnitGroup> composition)
    {
        while (!IsComposeReady(pool, composition))
            yield return new WaitForSeconds(1f);
    }

    private bool IsComposeReady(List<EnemyUnitController> pool, List<UnitGroup> composition)
    {
        pool.RemoveAll(u => u == null);

        foreach (UnitGroup group in composition)
            if (AvailableCount(pool, group.unitID) < group.count)
                return false;

        return true;
    }

    private IEnumerator LaunchWave()
    {
        List<UnitGroup> composition = CurrentWaveComposition();
        waveIndex++;

        List<EnemyUnitController> squad = TakeSquad(garrison, composition);
        if (squad.Count == 0)
            yield break;

        if (AssembleBeforeAttack)
            yield return AssembleAtRally(squad);

        // 목표 파괴 시 재조준/전멸 시 종료 감시는 별도 코루틴 - 여기서 기다리면 이 부대가 다 죽을 때까지
        // AttackWaveRoutine의 다음 웨이브 스케줄이 막혀버린다(doc/0534).
        StartCoroutine(RunWaveSquad(squad));
    }

    // waveIndex별로 무작위로 고른 패턴을 캐싱해둔다 - CurrentWaveComposition()이 한 웨이브 주기 동안
    // (ReinforceRoutine/WaitUntilReady/LaunchWave에서) 여러 번 불려도 매번 다른 패턴이 나오면 생산 중이던
    // 조합과 실제로 발사하는 조합이 어긋나므로, waveIndex가 바뀔 때만 다시 뽑는다(doc/0551).
    private int cachedWaveIndex = -1;
    private List<UnitGroup> cachedWaveComposition;

    // 이번 웨이브에 보낼 구성 - AttackWaves[waveIndex](faction에 맞는 쪽, doc/0540)의 3가지 패턴 중
    // 무작위 하나(doc/0551), 리스트를 넘어서면 마지막 웨이브의 패턴들에서 계속 무작위로 반복한다
    // (doc/0539, waveTimes 반복과 동일한 패턴).
    private List<UnitGroup> CurrentWaveComposition()
    {
        if (cachedWaveIndex == waveIndex && cachedWaveComposition != null)
            return cachedWaveComposition;

        List<WaveVariants> waves = AttackWaves;
        List<UnitGroup> result = new List<UnitGroup>();

        if (waves.Count > 0)
        {
            int index = Mathf.Min(waveIndex, waves.Count - 1);
            List<WaveComposition> variants = waves[index].variants;
            if (variants != null && variants.Count > 0)
                result = variants[Random.Range(0, variants.Count)].units;
        }

        cachedWaveIndex = waveIndex;
        cachedWaveComposition = result;
        return result;
    }

    // 부대가 전멸할 때까지: 목표가 없으면(최초, 또는 방금 파괴됨) PickAttackTarget()으로 다시 뽑아
    // 전원에게 재발령한다(doc/0534). "즉시 재조준"이 요청 사항이라 매 프레임 확인한다 - 동시에 활성화된
    // 웨이브 부대 수가 적어 비용은 무시할 만함.
    private IEnumerator RunWaveSquad(List<EnemyUnitController> squad)
    {
        BuildingController target = null;

        while (true)
        {
            squad.RemoveAll(u => u == null);
            if (squad.Count == 0)
                yield break; // 전멸 - 이 웨이브 종료

            if (target == null)
            {
                target = PickAttackTarget();
                if (target == null)
                    yield break; // 플레이어 건물이 하나도 안 남음 - 더 공격할 곳이 없음

                foreach (EnemyUnitController unit in squad)
                    if (unit != null)
                        unit.AttackMoveTo(target.transform.position);
            }

            yield return null;
        }
    }

    // 플레이어 MainBase 중 무작위 하나, MainBase가 하나도 없으면 플레이어 건물 아무거나(doc/0534).
    private BuildingController PickAttackTarget()
    {
        if (rtsController == null)
            return null;

        List<BuildingController> mainBases = rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase"));
        if (mainBases.Count > 0)
            return mainBases[Random.Range(0, mainBases.Count)];

        List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
        return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
    }

    // 인스펙터에 rallyPoint를 안 넣었을 때의 기본 집결지 - 첫 스폰 지점, 그마저 없으면 이 오브젝트의
    // 위치(doc/0544, spawnPoint가 리스트로 바뀌며 갱신).
    private Vector3 DefaultRallyPosition()
    {
        if (rallyPoint != null)
            return rallyPoint.position;
        if (spawnPoints.Count > 0 && spawnPoints[0].point != null)
            return spawnPoints[0].point.position;
        return transform.position;
    }

    private IEnumerator AssembleAtRally(List<EnemyUnitController> squad)
    {
        Vector3 rally = DefaultRallyPosition();

        foreach (EnemyUnitController unit in squad)
            if (unit != null)
                unit.MoveTo(rally);

        float elapsed = 0f;
        while (elapsed < rallyTimeout)
        {
            bool allArrived = true;
            foreach (EnemyUnitController unit in squad)
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

    // ======================
    // 2. 점령지 탈환 별동대
    // ======================
    private IEnumerator RaidRoutine()
    {
        while (true)
        {
            yield return CountdownSeconds(raidInterval, v => nextRaidCountdown = v);

            if (IsPlayerDefeated())
                yield break; // 더 점령할 대상이 없음 - 별동대 스케줄 보류(doc/0547)

            // 살아있는 별동대가 있으면 그 부대를 그대로 재사용 - 전멸했을 때만 새로 편성한다(doc/0549).
            currentRaidSquad.RemoveAll(u => u == null);
            if (currentRaidSquad.Count == 0)
            {
                yield return WaitUntilReady(raidGarrison, RaidSquadComposition); // doc/0544 결정 사항 #2 - 완성 대기
                currentRaidSquad.AddRange(TakeSquad(raidGarrison, RaidSquadComposition));

                if (currentRaidSquad.Count == 0)
                    continue; // 뽑을 병력이 없음 - 다음 주기에 다시 시도
            }

            CaptureSystem target = PickRaidTarget();
            if (target == null)
                continue;

            foreach (EnemyUnitController unit in currentRaidSquad)
                if (unit != null)
                    unit.AttackMoveTo(target.transform.position);
        }
    }

    // Ally가 뺏어간 곳을 Neutral보다 우선(doc/0532).
    private CaptureSystem PickRaidTarget()
    {
        CaptureSystem allyOwned = raidTargets.Find(t => t != null && t.CurrentOwner == CaptureOwner.Ally);
        if (allyOwned != null)
            return allyOwned;

        return raidTargets.Find(t => t != null && t.CurrentOwner == CaptureOwner.Neutral);
    }

    // ======================
    // 3. 공격받으면 주변 병력 소집
    // ======================
    // 탐색 중심은 "공격받은 건물의 위치"(this director가 스폰했는지와 무관하게 미션 씬에 미리 배치해둔
    // 유닛도 포함), 실제로 보내는 목적지는 "공격이 들어온 위치"(attackerPosition) - 건물 앞에 서 있지
    // 않고 공격자 쪽으로 달려가 반격한다(doc/0535).
    private void HandleBaseAttacked(EnemyBuildingController building, Vector3 attackerPosition, bool isEnemyAttacker)
    {
        if (isEnemyAttacker)
            return; // 플레이어에게 맞았을 때만 반응

        foreach (EnemyUnitController unit in FindNearbyEnemyUnits(building.transform.position))
            if (!deployed.Contains(unit) && unit.IsIdle())
                unit.AttackMoveTo(attackerPosition);
    }

    // center 반경 defenseRadius 안의 살아있는 적 유닛을 전부 찾는다. EnemyUnitController엔 전역
    // 레지스트리가 없지만, 전투 유닛은 이미 콜라이더를 갖고 있어(선택/사거리 판정용) 물리 쿼리로 충분하다
    // - 이 director가 스폰했는지 여부와 무관하게 미션 씬에 프리팹으로 미리 배치해둔 유닛도 잡힌다(doc/0535).
    private List<EnemyUnitController> FindNearbyEnemyUnits(Vector3 center)
    {
        List<EnemyUnitController> found = new List<EnemyUnitController>();

        foreach (Collider hit in Physics.OverlapSphere(center, defenseRadius))
            if (hit.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit) && !found.Contains(unit))
                found.Add(unit);

        return found;
    }

    // ======================
    // 4. 죽은 유닛 보충 생산
    // ======================
    private IEnumerator ReinforceRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(reinforceCheckInterval);
        while (true)
        {
            yield return wait;

            garrison.RemoveAll(u => u == null);
            raidGarrison.RemoveAll(u => u == null);
            currentRaidSquad.RemoveAll(u => u == null);
            deployed.RemoveWhere(u => u == null);

            // 다음에 나갈 웨이브(아직 발사 안 한 waveIndex)의 구성을 미리 갖춰둔다 - 웨이브가 실제로
            // 발사되는 순간 그제서야 생산을 시작하면 시간이 안 맞으므로 항상 선제적으로 채워둔다.
            FillPool(garrison, CurrentWaveComposition());

            // 별동대는 전멸했을 때만 다음 편성을 미리 생산한다(doc/0549) - 살아있는 동안은 생산하지
            // 않음(스폰 지점 생산 대기열을 garrison과 공유하므로, 안 쓰는 예비 별동대에 낭비하지 않음).
            if (currentRaidSquad.Count == 0)
                FillPool(raidGarrison, RaidSquadComposition);

            RespawnDeadDefenseUnits();

            // 디버그용 "씬 전체 적" 스냅샷 갱신(doc/0542) - 이 director의 내부 풀과 무관하게 항상 최신
            // 상태를 인스펙터에서 볼 수 있도록.
            allEnemyUnits.Clear();
            allEnemyUnits.AddRange(FindObjectsByType<EnemyUnitController>(FindObjectsInactive.Exclude));
            allEnemyBuildings.Clear();
            allEnemyBuildings.AddRange(FindObjectsByType<EnemyBuildingController>(FindObjectsInactive.Exclude));
        }
    }

    // ======================
    // 5. 배치형 방어 유닛 재생산 (건물 재건은 범위 밖, doc/0552)
    // ======================
    // defenseSlots 중 유닛이 죽어서 비어있는(current == null) 자리마다 같은 종류를 같은 위치/방향으로
    // 즉시 다시 세운다 - garrison/raidGarrison과 달리 별도 큐 없이 바로 Instantiate한다(자리를 지키는
    // 게 목적이라 집결/이동이 필요 없음).
    private void RespawnDeadDefenseUnits()
    {
        foreach (DefenseSlot slot in defenseSlots)
        {
            if (slot.current != null)
                continue;

            UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(slot.unitID) : null;
            if (data == null || data.Prefab == null)
                continue;

            GameObject spawned = Instantiate(data.Prefab, slot.position, slot.rotation);
            if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
                slot.current = unit;
        }
    }

    // composition이 요구하는 유닛 종류별 개수(보유 + 이미 생산 대기열에 들어간 수)가 못 미치면 부족한
    // 만큼 생산을 주문한다(doc/0544) - 예전처럼 즉시 Instantiate하지 않고, 스폰 지점 중 대기열이 가장
    // 덜 찬 곳에 자동 분산된다(LeastLoadedQueue).
    private void FillPool(List<EnemyUnitController> pool, List<UnitGroup> composition)
    {
        pool.RemoveAll(u => u == null);

        foreach (UnitGroup group in composition)
        {
            int have = AvailableCount(pool, group.unitID) + PendingCount(group.unitID, pool);

            for (int i = have; i < group.count; i++)
                EnqueueProduction(group.unitID, pool);
        }
    }

    // pool 중 아직 원정 안 나간(deployed에 없는) unitID 유닛 수. TakeSquad가 실제로 뽑아갈 수 있는
    // 유닛만 "보유"로 쳐야 한다 - 이미 다른 웨이브로 나간 유닛까지 세면 다음 웨이브 생산량이 과소
    // 계산되는 버그가 있었다(doc/0544에서 발견 및 수정).
    private int AvailableCount(List<EnemyUnitController> pool, int unitID)
    {
        int count = 0;
        foreach (EnemyUnitController unit in pool)
            if (unit != null && !deployed.Contains(unit) && unit.GetEnemyUnitID() == unitID)
                count++;
        return count;
    }

    // 이미 생산 대기열에 들어가 있어 완성을 기다리는 중인, 같은 목적지(pool) 행 unitID 개수 - 이걸
    // 안 세면 ReinforceRoutine이 돌 때마다 같은 부족분을 중복 주문하게 된다.
    private int PendingCount(int unitID, List<EnemyUnitController> destinationPool)
    {
        int count = 0;
        foreach (SpawnQueue sq in spawnQueues)
            foreach (EnemyProductionOrder order in sq.orders)
                if (order.unitID == unitID && order.destinationPool == destinationPool)
                    count++;
        return count;
    }

    private void EnqueueProduction(int unitID, List<EnemyUnitController> destinationPool)
    {
        if (rtsController == null)
            return;

        UnitData data = rtsController.GetEnemyUnitData(unitID);
        if (data == null || data.Prefab == null)
            return;

        SpawnQueue sq = LeastLoadedQueue();
        if (sq == null)
            return; // 쓸 수 있는 스폰 지점이 하나도 없음(전부 파괴됐거나 애초에 없음)

        sq.orders.Add(new EnemyProductionOrder
        {
            unitID = unitID,
            remainTime = data.productionTime,
            totalTime = data.productionTime,
            destinationPool = destinationPool,
        });
    }

    // 사용 가능한(IsAvailable) 스폰 지점 중 남은 생산 시간의 합이 가장 적은 곳을 고른다(doc/0544) -
    // 유닛마다 생산 시간이 달라서 개수가 아니라 시간 합으로 비교해야 실제로 고르게 분산된다.
    private SpawnQueue LeastLoadedQueue()
    {
        SpawnQueue best = null;
        float bestLoad = float.MaxValue;

        foreach (SpawnQueue sq in spawnQueues)
        {
            if (!sq.IsAvailable)
                continue;

            float load = QueueLoad(sq);
            if (load < bestLoad)
            {
                bestLoad = load;
                best = sq;
            }
        }

        return best;
    }

    private float QueueLoad(SpawnQueue sq)
    {
        float total = 0f;
        for (int i = 0; i < sq.orders.Count; i++)
            total += i == 0 ? sq.orders[i].remainTime : sq.orders[i].totalTime;
        return total;
    }

    // ======================
    // 공용
    // ======================

    // pool에서 composition이 요구하는 유닛 종류별 개수만큼(아직 원정 안 나간 것만) 뽑아 deployed에
    // 등록한다 - 뽑힌 유닛은 이후 재사용(다른 웨이브/별동대/기지 방어 소집)되지 않는다. 특정 종류가
    // 부족하면 그만큼만 못 채우고 반환한다(WaitUntilReady가 먼저 완성을 보장하므로 평소엔 안 부족함).
    // 웨이브는 garrison, 별동대는 raidGarrison을 넘겨 서로 다른 풀에서 뽑는다(doc/0538).
    private List<EnemyUnitController> TakeSquad(List<EnemyUnitController> pool, List<UnitGroup> composition)
    {
        pool.RemoveAll(u => u == null);

        List<EnemyUnitController> squad = new List<EnemyUnitController>();
        foreach (UnitGroup group in composition)
        {
            int taken = 0;
            foreach (EnemyUnitController unit in pool)
            {
                if (taken >= group.count)
                    break;
                if (deployed.Contains(unit) || unit.GetEnemyUnitID() != group.unitID)
                    continue;

                squad.Add(unit);
                deployed.Add(unit);
                taken++;
            }
        }

        return squad;
    }
}
