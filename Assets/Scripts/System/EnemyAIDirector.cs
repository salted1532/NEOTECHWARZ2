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

// spawnPoints/rallyPoint를 방향(레인) 단위로 묶은 세트. 웨이브가 waveIndex 순서대로 이 레인들을
// 라운드로빈으로 골라 그 레인 전용 스폰 지점에서 생산하고 그 레인 전용 rallyPoint에 집결한다(doc/0610).
[System.Serializable]
public class AttackLane
{
    public List<EnemySpawnPoint> spawnPoints; // 이 레인 전용 스폰 지점(+생산 건물) - 비워두면 이 레인은 생산 불가
    public Transform rallyPoint; // 이 레인 전용 집결지 - 비워두면 spawnPoints[0] 위치를 집결지로 사용

    [Header("<디버그> 이 레인의 현재 웨이브 병력")]
    public List<EnemyUnitController> garrison = new List<EnemyUnitController>();
}

// 미션 씬에 적 기지 하나당 하나씩 배치하는 "AI 관제소". 시간에 맞춰 공격 웨이브를 보내고, 점령지에
// 별동대를 보내고, 기지가 공격받으면 병력을 소집하고, 죽은 유닛을 보충 생산한다 (doc/0532 설계안).
public class EnemyAIDirector : MonoBehaviour
{
    [Header("진영 선택 - 아래 <OC>/<Spore Brood> 구역 중 어느 쪽을 쓸지 결정")]
    [SerializeField] private EnemyFaction faction;

    [Header("<공통> 스폰 (여러 곳 지정 가능, doc/0544)")]
    [SerializeField] private List<EnemySpawnPoint> spawnPoints;

    [Header("<공통> 기지 방어 - 모든 적 건물이 공격받으면 주변 유닛을 부른다. 이 리스트는 그중 defenseRadius의 2배 반경을 쓸 메인기지/미션 오브젝트만 지정(doc/0575)")]
    [SerializeField] private List<EnemyBuildingController> homeBuildings;
    [SerializeField] private float defenseRadius = 15f; // 공격받은 건물 기준, 방어하러 부를 유닛을 찾는 반경 - homeBuildings는 이 값의 2배(doc/0535, doc/0575)

    [Header("<디버그> 배치형 방어 유닛 현재 상태 (자동 감지 - Start() 시점에 씬에 이미 있던 모든 적 유닛을 방어 슬롯으로 등록. 죽으면 생산 큐에 주문해 완성되는 대로 원래 있던 위치로 보내 1회만 재생산, 그 대체 유닛까지 죽으면 더 생산 안 함. 건물 재건은 범위 밖, doc/0552, doc/0558, doc/0582)")]
    [SerializeField] private List<EnemyUnitController> defenseUnits = new List<EnemyUnitController>();

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

    [Header("<공통> 공격 방향(레인) - 2개 이상 두면 웨이브가 waveIndex 순서대로 이 레인들을 번갈아가며(라운드로빈) 사용해 그 레인 전용 스폰 지점에서 생산하고 그 레인 전용 rallyPoint에 집결한 뒤 출발한다. 비워두면 위 spawnPoints/rallyPoint/garrison으로 구성된 단일 레인 1개로 그대로 동작한다(doc/0610).")]
    [SerializeField] private List<AttackLane> attackLanes;

    [Header("<공통> 수비대 유지")]
    [SerializeField] private float reinforceCheckInterval = 20f;

    [Header("<공통> 점령지 탈환 타이밍")]
    [SerializeField] private List<CaptureSystem> raidTargets;
    [SerializeField] private float raidInterval = 45f;

    [Header("<공통> 웨이브 공격 후보에 포함할 아군 OC 메인기지 (doc/0579) - 지정 시 플레이어 MainBase와 함께 무작위로 목표가 됨")]
    [SerializeField] private List<EnemyBuildingController> allyMainBaseTargets;

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

    // 지금 웨이브가 목표를 고를 후보 풀(플레이어 MainBase + allyMainBaseTargets) - PickAttackTarget()과
    // 계산을 공유하며 Update()에서 매 프레임 갱신해 인스펙터에서 실시간으로 확인할 수 있다(doc/0579).
    [Header("<디버그> 현재 웨이브 공격 후보 (플레이어 MainBase + allyMainBaseTargets, 실시간 갱신)")]
    [SerializeField] private List<Component> attackTargetCandidates = new List<Component>();

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
    // 만들어 구독하고 여기 보관해뒀다가 해지할 때 정확히 그 델리게이트로 뗀다(doc/0535). homeBuildings뿐
    // 아니라 씬의 모든 적 건물(EnemyBuildingController.ActiveBuildings)을 대상으로 하며, 건물이 나중에
    // 생기거나 파괴돼도 OnActiveBuildingsChanged로 계속 맞춰준다(doc/0575).
    private readonly Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>> baseDefenseHandlers
        = new Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>>();

    // 건물별로 지원 소집을 이미 한 번 내렸는지 - 같은 건물이 계속 공격받아도 소집은 최초 1회만
    // 내린다(doc/0576). 건물이 파괴되면 SyncBuildingDefenseHandlers에서 함께 제거한다.
    private readonly HashSet<EnemyBuildingController> defenseRequested = new HashSet<EnemyBuildingController>();

    // 생산 대기열 항목 - 완성되면 destinationPool(garrison 또는 raidGarrison)에 추가된다(doc/0544).
    private class EnemyProductionOrder
    {
        public int unitID;
        public float remainTime;
        public float totalTime;
        public List<EnemyUnitController> destinationPool; // 웨이브/별동대용 - 완성되면 이 풀에 추가되고 집결지로 이동
        public DefenseSlot targetSlot; // 방어 유닛 대체 생산용(doc/0582) - 완성되면 이 슬롯에 등록되고 원래 위치로 이동. 둘 중 하나만 채워짐
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

    // 레인(AttackLane) 하나의 런타임 상태 - 그 레인 소속 스폰 지점들의 생산 대기열만 담는다. 웨이브
    // 병력 풀(garrison)은 AttackLane 자신에 있는 직렬화 필드를 그대로 쓴다(인스펙터에서 레인별로
    // 바로 확인 가능, doc/0610).
    private class LaneRuntime
    {
        public AttackLane config;
        public readonly List<SpawnQueue> spawnQueues = new List<SpawnQueue>();
    }

    private readonly List<LaneRuntime> lanes = new List<LaneRuntime>();

    // 배치형 방어 유닛 슬롯 - Start() 시점의 위치/방향/종류를 기억해뒀다가, 그 유닛이 죽으면(current ==
    // null) 같은 자리에 같은 종류로 다시 세운다(doc/0552) - 단, 대체 생산은 슬롯당 1회뿐이라 그 대체
    // 유닛까지 죽으면 더 이상 채우지 않는다(doc/0558). 생산 대기열(EnqueueProduction)은 spawnPoint
    // 기준이라 임의의 방어 위치엔 안 맞으므로, 이 슬롯은 즉시 Instantiate하는 별도의 단순한 경로를 쓴다.
    private class DefenseSlot
    {
        public int unitID;
        public Vector3 position;
        public Quaternion rotation;
        public EnemyUnitController current;
        public bool respawned; // 원본이 죽어 이미 한 번 대체 생산됐는지 - true면 그 대체 유닛이 죽어도 더 생산하지 않음(doc/0558)
        public bool pendingProduction; // 지금 생산 대기열에 이미 이 슬롯 몫 주문이 들어가 있는지(doc/0582) - 중복 주문 방지
    }

    private readonly List<DefenseSlot> defenseSlots = new List<DefenseSlot>();

    private void OnEnable()
    {
        EnemyBuildingController.OnActiveBuildingsChanged += SyncBuildingDefenseHandlers;
        SyncBuildingDefenseHandlers();
    }

    private void OnDisable()
    {
        EnemyBuildingController.OnActiveBuildingsChanged -= SyncBuildingDefenseHandlers;

        foreach (var pair in baseDefenseHandlers)
            if (pair.Key != null && pair.Key.GetHealthManager() != null)
                pair.Key.GetHealthManager().OnDamaged -= pair.Value;

        baseDefenseHandlers.Clear();
    }

    // ActiveBuildings(씬 전체 적 건물)와 baseDefenseHandlers를 맞춘다 - 파괴된 건물의 핸들러는 떼어내고,
    // 아직 구독하지 않은 새 건물엔 핸들러를 붙인다(doc/0575). ActiveBuildings는 각 건물이 자기 Start()
    // 끝에서 등록하므로(healthManager 캐싱 이후), 여기서 걸리는 건물은 항상 GetHealthManager()가 준비돼
    // 있다.
    private void SyncBuildingDefenseHandlers()
    {
        List<EnemyBuildingController> stale = new List<EnemyBuildingController>();
        foreach (var pair in baseDefenseHandlers)
            if (pair.Key == null || !EnemyBuildingController.ActiveBuildings.Contains(pair.Key))
                stale.Add(pair.Key);

        foreach (EnemyBuildingController building in stale)
        {
            if (building != null && building.GetHealthManager() != null)
                building.GetHealthManager().OnDamaged -= baseDefenseHandlers[building];
            baseDefenseHandlers.Remove(building);
            defenseRequested.Remove(building);
        }

        foreach (EnemyBuildingController building in EnemyBuildingController.ActiveBuildings)
        {
            if (building == null || baseDefenseHandlers.ContainsKey(building) || building.GetHealthManager() == null)
                continue;

            EnemyBuildingController capturedBuilding = building;
            System.Action<int, Vector3, AttackEffectType, bool> handler =
                (damage, attackerPosition, type, isEnemyAttacker) => HandleBaseAttacked(capturedBuilding, attackerPosition, isEnemyAttacker);

            baseDefenseHandlers[building] = handler;
            building.GetHealthManager().OnDamaged += handler;
        }
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();

        // attackLanes를 안 쓰면(기본값) 기존 spawnPoints/rallyPoint/garrison 3개를 그대로 묶어 레인
        // 1개로 취급한다 - 이 프로젝트의 다른 모든 미션(레인 미설정)이 지금과 100% 동일하게 동작하도록
        // 하는 하위 호환 경로(doc/0610).
        List<AttackLane> configuredLanes = attackLanes.Count > 0
            ? attackLanes
            : new List<AttackLane> { new AttackLane { spawnPoints = spawnPoints, rallyPoint = rallyPoint, garrison = garrison } };

        foreach (AttackLane lane in configuredLanes)
        {
            LaneRuntime rt = new LaneRuntime { config = lane };

            foreach (EnemySpawnPoint sp in lane.spawnPoints)
            {
                if (sp == null || sp.point == null)
                    continue;

                rt.spawnQueues.Add(new SpawnQueue { spawnPoint = sp, hasProductionBuilding = sp.productionBuilding != null });
            }

            lanes.Add(rt);
        }

        // Start() 시점(이 director가 아직 아무것도 생산하기 전)에 씬에 이미 존재하는 적 유닛은 전부
        // 레벨 디자이너가 미리 세워둔 고정 수비 유닛으로 간주한다(doc/0582) - 더 이상 인스펙터에
        // 수동으로 드래그해 넣을 필요 없음.
        // ponytail: 기지가 여러 개(EnemyAIDirector가 씬에 여럿)인 미션에서는 같은 유닛을 두 director가
        // 동시에 방어 슬롯으로 잡을 수 있음 - 지금은 미션당 기지 하나뿐이라 무시, 다중 기지 미션이
        // 생기면 유닛에 소속 기지 마커를 달아 구분할 것.
        foreach (EnemyUnitController unit in FindObjectsByType<EnemyUnitController>(FindObjectsInactive.Exclude))
        {
            defenseSlots.Add(new DefenseSlot
            {
                unitID = unit.GetEnemyUnitID(),
                position = unit.transform.position,
                rotation = unit.transform.rotation,
                current = unit,
            });
        }
        RefreshDefenseUnitsDebugList();

        LaneRuntime firstLane = CurrentLane();
        if (firstLane != null)
            FillPool(firstLane.config.garrison, CurrentWaveComposition(), firstLane.spawnQueues);
        FillPool(raidGarrison, RaidSquadComposition, AllSpawnQueues());

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
        attackTargetCandidates.Clear();
        attackTargetCandidates.AddRange(GetMainBaseCandidates());

        foreach (LaneRuntime rt in lanes)
        {
            foreach (SpawnQueue sq in rt.spawnQueues)
            {
                if (!sq.IsAvailable)
                {
                    // 생산 건물이 파괴돼 취소되는 주문 중 방어 슬롯용이 있으면 pendingProduction을 풀어줘야
                    // ReplenishDeadDefenseSlots가 다음 주기에 다른 스폰 지점으로 다시 주문한다(doc/0582).
                    foreach (EnemyProductionOrder order in sq.orders)
                        if (order.targetSlot != null)
                            order.targetSlot.pendingProduction = false;

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
                    if (front.targetSlot != null) // 방어 슬롯용 - 원래 있던 위치로 이동(doc/0582)
                    {
                        front.targetSlot.current = unit;
                        front.targetSlot.respawned = true;
                        front.targetSlot.pendingProduction = false;
                        unit.MoveTo(front.targetSlot.position);
                    }
                    else
                    {
                        front.destinationPool.Add(unit);
                        unit.MoveTo(LaneRallyPosition(rt)); // 생산되자마자 그 레인의 집결지로 - 웨이브/별동대 공통(doc/0545, doc/0610)
                    }
                }
            }
        }
    }

    // ======================
    // 1. 시간에 맞춰 공격 웨이브
    // ======================
    private IEnumerator AttackWaveRoutine()
    {
        while (true)
        {
            yield return CountdownSeconds(WaveIntervalFor(waveIndex), v => nextWaveCountdown = v);

            // 카운트다운이 끝난 뒤에 확인한다 - Start() 직후(0프레임째)엔 플레이어 건물이 아직 자기
            // Start()에서 BuildingList에 등록되기 전이라(스크립트 실행 순서 미보장) 여기서 즉시 확인하면
            // 항상 "전멸"로 오판해서 웨이브 타이머 자체가 한 번도 안 도는 버그가 있었다(doc/0548).
            if (IsPlayerDefeated())
                yield break; // 더 공격할 대상이 없음 - 웨이브 스케줄 보류(doc/0547)

            // waveIndex % 레인 개수로 이번 웨이브가 쓸 레인을 라운드로빈으로 고른다(doc/0610) - 레인이
            // 1개(기존 미션)면 항상 그 레인 하나만 고르므로 지금과 동일하게 동작한다.
            LaneRuntime lane = CurrentLane();
            if (lane == null)
                yield break; // 레인 구성이 하나도 없음(스폰 지점 미설정) - 더 진행 불가

            yield return WaitUntilReady(lane.config.garrison, CurrentWaveComposition());
            yield return LaunchWave(lane); // doc/0560: 별동대가 전멸할 때까지 여기서 대기 (구 doc/0534 결정 번복)
        }
    }

    // 이번 웨이브가 쓸 레인 - waveIndex 순서대로 lanes를 라운드로빈으로 순회한다(doc/0610).
    private LaneRuntime CurrentLane() => lanes.Count > 0 ? lanes[waveIndex % lanes.Count] : null;

    // waveTimes[index] 기준 이번 사이클의 대기 시간 - 리스트를 넘어서면 마지막 두 항목 간격을 반복한다
    // (doc/0532 결정 사항 #2와 동일한 간격 규칙). doc/0560부터는 이 간격을 미션 시작 시각이 아니라
    // "이전 웨이브가 전멸한 시점" 기준으로 다시 잰다.
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

    private IEnumerator LaunchWave(LaneRuntime lane)
    {
        List<UnitGroup> composition = CurrentWaveComposition();
        waveIndex++;

        List<EnemyUnitController> squad = TakeSquad(lane.config.garrison, composition);
        if (squad.Count == 0)
            yield break;

        if (AssembleBeforeAttack)
            yield return AssembleAtRally(squad, LaneRallyPosition(lane));

        // doc/0560: fire-and-forget이던 것을 직접 대기로 바꿔 AttackWaveRoutine이 전멸까지 기다리게 한다
        // (구 doc/0534 결정 번복 - "전멸해야 다음 웨이브 타이머가 돈다" 요청 반영).
        yield return RunWaveSquad(squad);
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
        Component target = null;

        while (true)
        {
            squad.RemoveAll(u => u == null);
            if (squad.Count == 0)
                yield break; // 전멸 - 이 웨이브 종료

            if (target == null)
            {
                target = PickAttackTarget();
                if (target == null)
                    yield break; // 공격할 곳이 하나도 안 남음

                foreach (EnemyUnitController unit in squad)
                    if (unit != null)
                        unit.AttackMoveTo(target.transform.position);
            }

            yield return null;
        }
    }

    // 플레이어 MainBase + allyMainBaseTargets(지정돼 있으면 아군 OC 메인기지, doc/0579) 중 무작위 하나,
    // 후보가 하나도 없으면 플레이어 건물 아무거나(doc/0534).
    private Component PickAttackTarget()
    {
        if (rtsController == null)
            return null;

        List<Component> candidates = GetMainBaseCandidates();
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
        return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
    }

    // PickAttackTarget()과 attackTargetCandidates(디버그 실시간 표시)가 공유하는 후보 풀 계산(doc/0579).
    private List<Component> GetMainBaseCandidates()
    {
        List<Component> candidates = new List<Component>();
        if (rtsController != null)
            candidates.AddRange(rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase")));
        candidates.AddRange(allyMainBaseTargets.FindAll(b => b != null));
        return candidates;
    }

    // 그 레인의 집결지 - 레인에 rallyPoint를 안 넣었을 때의 기본값은 그 레인의 첫 스폰 지점, 그마저
    // 없으면 이 오브젝트의 위치(doc/0544의 규칙을 레인 단위로 그대로 적용, doc/0610).
    private Vector3 LaneRallyPosition(LaneRuntime lane)
    {
        if (lane.config.rallyPoint != null)
            return lane.config.rallyPoint.position;
        if (lane.config.spawnPoints.Count > 0 && lane.config.spawnPoints[0].point != null)
            return lane.config.spawnPoints[0].point.position;
        return transform.position;
    }

    private IEnumerator AssembleAtRally(List<EnemyUnitController> squad, Vector3 rally)
    {
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

    // 점령지 후보(자기 진영 소유가 아닌 곳) 중 우선순위 없이 무작위로 하나(doc/0561 - 기존 Ally 우선 →
    // Neutral 순 우선순위를 없앰). 이미 자기 진영(Enemy) 소유인 곳은 보내도 효과가 없어 계속 제외.
    private CaptureSystem PickRaidTarget()
    {
        List<CaptureSystem> candidates = raidTargets.FindAll(t => t != null && t.CurrentOwner != CaptureOwner.Enemy);
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : null;
    }

    // ======================
    // 3. 공격받으면 주변 병력 소집
    // ======================
    // 탐색 중심은 "공격받은 건물의 위치"(this director가 스폰했는지와 무관하게 미션 씬에 미리 배치해둔
    // 유닛도 포함), 실제로 보내는 목적지는 "공격이 들어온 위치"(attackerPosition) - 건물 앞에 서 있지
    // 않고 공격자 쪽으로 달려가 반격한다(doc/0535). homeBuildings로 지정한 건물은 defenseRadius의 2배
    // 반경까지 소집하고, 그 외 모든 적 건물은 defenseRadius 그대로 쓴다(doc/0575). 건물 하나당 소집은
    // 최초 1회만 - 그 뒤로 계속 공격받아도 다시 명령을 내리지 않는다(doc/0576).
    private void HandleBaseAttacked(EnemyBuildingController building, Vector3 attackerPosition, bool isEnemyAttacker)
    {
        if (isEnemyAttacker)
            return; // 플레이어에게 맞았을 때만 반응

        if (!defenseRequested.Add(building))
            return; // 이 건물은 이미 한 번 소집했음

        float radius = homeBuildings.Contains(building) ? defenseRadius * 2f : defenseRadius;

        foreach (EnemyUnitController unit in FindNearbyEnemyUnits(building.transform.position, radius))
            if (!deployed.Contains(unit) && unit.IsIdle())
                unit.AttackMoveTo(attackerPosition);
    }

    // center 반경 radius 안의 살아있는 적 유닛을 전부 찾는다. EnemyUnitController엔 전역 레지스트리가
    // 없지만, 전투 유닛은 이미 콜라이더를 갖고 있어(선택/사거리 판정용) 물리 쿼리로 충분하다 - 이
    // director가 스폰했는지 여부와 무관하게 미션 씬에 프리팹으로 미리 배치해둔 유닛도 잡힌다(doc/0535).
    private List<EnemyUnitController> FindNearbyEnemyUnits(Vector3 center, float radius)
    {
        List<EnemyUnitController> found = new List<EnemyUnitController>();

        foreach (Collider hit in Physics.OverlapSphere(center, radius))
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

            foreach (LaneRuntime rt in lanes)
                rt.config.garrison.RemoveAll(u => u == null);
            raidGarrison.RemoveAll(u => u == null);
            currentRaidSquad.RemoveAll(u => u == null);
            deployed.RemoveWhere(u => u == null);

            // 다음에 나갈 웨이브(아직 발사 안 한 waveIndex)가 쓸 레인만 미리 채워둔다 - 웨이브가 실제로
            // 발사되는 순간 그제서야 생산을 시작하면 시간이 안 맞으므로 항상 선제적으로 채워둔다. 아직
            // 차례가 안 된 다른 레인은 건드리지 않는다(doc/0610).
            LaneRuntime nextLane = CurrentLane();
            if (nextLane != null)
                FillPool(nextLane.config.garrison, CurrentWaveComposition(), nextLane.spawnQueues);

            // 별동대는 전멸했을 때만 다음 편성을 미리 생산한다(doc/0549) - 살아있는 동안은 생산하지
            // 않음(스폰 지점 생산 대기열을 garrison과 공유하므로, 안 쓰는 예비 별동대에 낭비하지 않음).
            // 레인 개념과 무관하게 모든 레인의 스폰 지점을 합친 전체 풀에서 생산한다(doc/0610).
            if (currentRaidSquad.Count == 0)
                FillPool(raidGarrison, RaidSquadComposition, AllSpawnQueues());

            ReplenishDeadDefenseSlots();

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
    // defenseSlots 중 유닛이 죽어서 비어있고(current == null) 아직 대체 생산을 한 번도 안 한(!respawned)
    // 자리마다 같은 종류를 같은 위치/방향으로 즉시 다시 세운다 - garrison/raidGarrison과 달리 별도 큐
    // 없이 바로 Instantiate한다(자리를 지키는 게 목적이라 집결/이동이 필요 없음). 대체 생산은 슬롯당
    // 딱 1번뿐이라, 그 대체 유닛까지 죽으면(respawned == true) 더 이상 채우지 않는다(doc/0558).
    // 빈 슬롯(죽었고 아직 대체 생산 안 한)마다 생산 대기열에 주문을 넣는다 - 완성되면 Update()가
    // targetSlot을 보고 원래 위치로 보낸다(doc/0582). respawned는 주문 시점이 아니라 실제로 유닛이
    // 배치된 뒤(Update() 완성 처리)에 true로 바뀐다 - 그 전까지는 pendingProduction으로 중복 주문만 막는다.
    private void ReplenishDeadDefenseSlots()
    {
        foreach (DefenseSlot slot in defenseSlots)
        {
            if (slot.current != null || slot.respawned || slot.pendingProduction)
                continue;

            if (EnqueueDefenseProduction(slot))
                slot.pendingProduction = true;
        }

        RefreshDefenseUnitsDebugList();
    }

    // defenseSlots(내부 상태)를 인스펙터에서 실시간으로 볼 수 있는 defenseUnits 디버그 리스트에
    // 반영한다 - allEnemyUnits 등과 동일한 패턴(doc/0582). 죽어서 current가 null이 된 슬롯은 자동으로
    // 목록에서 빠진다.
    private void RefreshDefenseUnitsDebugList()
    {
        defenseUnits.Clear();
        foreach (DefenseSlot slot in defenseSlots)
            if (slot.current != null)
                defenseUnits.Add(slot.current);
    }

    // composition이 요구하는 유닛 종류별 개수(보유 + 이미 생산 대기열에 들어간 수)가 못 미치면 부족한
    // 만큼 생산을 주문한다(doc/0544) - 예전처럼 즉시 Instantiate하지 않고, queues 중 대기열이 가장
    // 덜 찬 곳에 자동 분산된다(LeastLoadedQueue). queues는 웨이브 풀이면 그 레인 소속만, 별동대/방어
    // 슬롯 풀이면 전체 레인을 합친 것을 넘긴다(doc/0610).
    private void FillPool(List<EnemyUnitController> pool, List<UnitGroup> composition, List<SpawnQueue> queues)
    {
        pool.RemoveAll(u => u == null);

        foreach (UnitGroup group in composition)
        {
            int have = AvailableCount(pool, group.unitID) + PendingCount(group.unitID, pool, queues);

            for (int i = have; i < group.count; i++)
                EnqueueProduction(group.unitID, pool, queues);
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
    private int PendingCount(int unitID, List<EnemyUnitController> destinationPool, List<SpawnQueue> queues)
    {
        int count = 0;
        foreach (SpawnQueue sq in queues)
            foreach (EnemyProductionOrder order in sq.orders)
                if (order.unitID == unitID && order.destinationPool == destinationPool)
                    count++;
        return count;
    }

    private void EnqueueProduction(int unitID, List<EnemyUnitController> destinationPool, List<SpawnQueue> queues) =>
        EnqueueOrder(unitID, destinationPool, null, queues);

    // 방어 슬롯 대체 생산 주문(doc/0582) - 쓸 수 있는 스폰 지점이 없으면 주문을 못 넣고 false를
    // 반환하므로, 호출부(ReplenishDeadDefenseSlots)가 pendingProduction을 세우지 않고 다음 주기에
    // 다시 시도한다. 레인과 무관하게 전체 스폰 지점을 대상으로 한다(doc/0610).
    private bool EnqueueDefenseProduction(DefenseSlot slot) =>
        EnqueueOrder(slot.unitID, null, slot, AllSpawnQueues());

    private bool EnqueueOrder(int unitID, List<EnemyUnitController> destinationPool, DefenseSlot targetSlot, List<SpawnQueue> queues)
    {
        if (rtsController == null)
            return false;

        UnitData data = rtsController.GetEnemyUnitData(unitID);
        if (data == null || data.Prefab == null)
            return false;

        SpawnQueue sq = LeastLoadedQueue(queues);
        if (sq == null)
            return false; // 쓸 수 있는 스폰 지점이 하나도 없음(전부 파괴됐거나 애초에 없음)

        sq.orders.Add(new EnemyProductionOrder
        {
            unitID = unitID,
            remainTime = data.productionTime,
            totalTime = data.productionTime,
            destinationPool = destinationPool,
            targetSlot = targetSlot,
        });
        return true;
    }

    // 모든 레인의 스폰 지점 생산 대기열을 합친 목록 - 레인 개념과 무관한 별동대/방어 슬롯 생산에
    // 쓴다(doc/0610). 레인이 1개(기존 미션)면 기존 spawnQueues와 동일한 목록이 된다.
    private List<SpawnQueue> AllSpawnQueues()
    {
        List<SpawnQueue> all = new List<SpawnQueue>();
        foreach (LaneRuntime rt in lanes)
            all.AddRange(rt.spawnQueues);
        return all;
    }

    // 사용 가능한(IsAvailable) 스폰 지점 중 남은 생산 시간의 합이 가장 적은 곳을 고른다(doc/0544) -
    // 유닛마다 생산 시간이 달라서 개수가 아니라 시간 합으로 비교해야 실제로 고르게 분산된다.
    private SpawnQueue LeastLoadedQueue(List<SpawnQueue> queues)
    {
        SpawnQueue best = null;
        float bestLoad = float.MaxValue;

        foreach (SpawnQueue sq in queues)
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
