using UnityEngine;

// 유닛 프리팹에 UnitController(아군) 또는 EnemyUnitController(적)/HealthManager와 같이 부착하는 사운드
// 전담 컴포넌트. UnitEffects(Assets/Scripts/Effects/UnitEffects.cs)와 완전히 동일한 자리, 동일한 패턴
// (doc/0255) - 재생할 클립은 전부 RTSUnitController를 거쳐 조회한 UnitData.soundBank(UnitSoundBankSO)에서
// 가져온다. 아군/적 컨트롤러 둘 다 null 체크해서 프리팹에 공용으로 하나만 붙이면 된다.
public class UnitAudio : MonoBehaviour
{
    private UnitController unitController;
    private EnemyUnitController enemyUnitController;
    private AllyController allyController; // 아군 OC (doc/0469)
    private HealthManager healthManager;
    private RTSUnitController rtsController;
    private UnitSoundBankSO bank;

    // bank 조회를 Start()가 아니라 Awake()에서 하는 이유(doc/0267): UnitSpawner.Spawn()이
    // Instantiate() 직후 같은 프레임에 곧바로 PlaySpawnSound()를 호출하는데, Start()는 다음 프레임
    // 전까지 실행되지 않아 그 시점엔 bank가 아직 null이라 스폰 소리가 조용히 씹혔다. Awake()는
    // Instantiate() 안에서 즉시(동기) 실행되므로 이 시점엔 bank가 이미 준비돼 있다.
    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        enemyUnitController = GetComponent<EnemyUnitController>();
        allyController = GetComponent<AllyController>();
        healthManager = GetComponent<HealthManager>();

        rtsController = FindFirstObjectByType<RTSUnitController>();
        if (rtsController == null)
            return;

        // 아군 OC(AllyController)도 적 OC와 같은 EnemyUnitDataSO 로스터를 그대로 재사용하므로(doc/0447/0448),
        // enemyUnitController와 동일하게 GetEnemyUnitData로 조회한다 - 이게 빠져있어서 Ally OC 유닛은
        // bank가 계속 null로 남아 공격음/스폰음/선택대사 등 모든 소리가 조용히 씹히고 있었다(doc/0469).
        UnitData data = unitController != null
            ? rtsController.GetUnitData(unitController.GetUnitID())
            : enemyUnitController != null ? rtsController.GetEnemyUnitData(enemyUnitController.GetEnemyUnitID())
            : allyController != null ? rtsController.GetEnemyUnitData(allyController.GetAllyUnitID())
            : null;

        // "구조 가능한 OC 유닛"(enemyDataUnitID로 스탯을 가져오는 UnitController, doc/0458)은 unitID가
        // 0이라 위 조회가 항상 실패한다. OC 로스터는 NTA와 완전히 동일한 번호로 재스킨된 구조라서(doc/0441),
        // OC 쪽에 사운드뱅크가 없으면 enemyDataUnitID를 그대로 NTA unitID로 다시 조회해서 사운드뱅크만
        // 대신 가져온다(스탯은 이미 ApplyUnitData에서 OC 값으로 적용됐으므로 안 건드림).
        if (data?.soundBank == null && unitController != null && unitController.GetEnemyDataUnitID() > 0)
            data = rtsController.GetUnitData(unitController.GetEnemyDataUnitID());

        bank = data?.soundBank;
    }

    private void OnEnable()
    {
        if (healthManager != null)
        {
            healthManager.OnDeath += HandleDeath;
            healthManager.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnDeath -= HandleDeath;
            healthManager.OnDamaged -= HandleDamaged;
        }
    }

    // UnitController.Attack()에서 데미지 적용 직후 직접 호출된다 (UnitEffects.PlayAttack()과 나란히).
    public void PlayAttackSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.attackSFX, transform.position);
    }

    // UnitSpawner.Spawn()에서 Instantiate 직후 호출된다. spawnSFX=이륙음/엔진음 등(음성 제외), spawnVoice=생성 음성.
    public void PlaySpawnSound()
    {
        if (bank == null)
            return;

        SoundManager.Instance?.PlaySFX(bank.spawnSFX, transform.position);
        SoundManager.Instance?.PlayVoice(bank.spawnVoice);
    }

    // UnitController.GatherTick()이 채취를 시작하는 순간 호출한다 (워커 전용, 다른 유닛은 bank.gatherSFX가 비어있어 무음).
    public void PlayGatherSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.gatherSFX, transform.position);
    }

    // 고급유닛 액티브 스킬 사용 시 호출 (doc/0228 IUnitSkill 구현체 쪽에서 호출).
    public void PlaySkillSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.skillSFX, transform.position);
    }

    // UnitController.HealTick()이 회복 틱마다 호출한다 (BuildingAudio.PlayRepairTick()과 동일 패턴, doc/0661).
    public void PlayHealTick()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.healTickSFX, transform.position);
    }

    // 아래 3개는 RTSUnitController의 선택/이동/공격명령 진입점에서, "이번 명령에 영향받는 대표 유닛 1마리"에
    // 대해서만 호출된다 - 다수 선택 시 대사가 겹쳐 시끄러워지지 않도록. SoundManager.PlayOrderVoice를 통해
    // 재생되며, bank(이 유닛 "종류"의 SoundBank 에셋)를 식별자로 넘긴다 - 같은 종류의 유닛끼리는 단일
    // 선택/드래그 선택을 섞어도 재생 중인 대사가 끊기지 않는다 (doc/0262~0264).
    public void PlaySelectVoice()
    {
        if (bank != null)
            SoundManager.Instance?.PlayOrderVoice(bank.selectVoice, bank, "select");
    }

    // 이동/순찰 명령 시 재생 (구 PlayMoveVoice, doc/0289 - 순찰 명령까지 범위 확대).
    public void PlayOrderVoice()
    {
        if (bank != null)
            SoundManager.Instance?.PlayOrderVoice(bank.orderVoice, bank, "order");
    }

    // 선택 대사와 별개로 같이 나는 효과음(삑 소리 등, doc/0269) - 전용 단일 채널로 재생되어 재생 중이면
    // 새 요청을 무시하고 끝난 뒤부터 다시 재생한다 (doc/0285).
    // 대사(Voice)처럼 2D로 재생한다 - 확인음은 카메라가 유닛에서 멀리 있어도(거리 감쇠 없이)
    // 항상 또렷하게 들려야 한다 (doc/0278). 공격/사망/채취 등 전투·근접 SFX는 3D 그대로 유지.
    public void PlaySelectSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlaySelectSFX(bank.selectSFX);
    }

    // 이동/공격/순찰/정지/홀드/따라가기/채취/자원반환/건물이동 등 선택된 유닛에게 명령을 내리는
    // 모든 진입점에서 대표 유닛 1마리에 대해 호출된다 (doc/0279 - 이동 전용이던 moveSFX를 명령
    // 전반으로 확대). RTSUnitController의 각 명령 메서드가 대사(Voice) 재생과 나란히 호출한다.
    // 전용 단일 채널로 재생되어 재생 중이면 새 요청을 무시하고 끝난 뒤부터 다시 재생한다 (doc/0285).
    public void PlayOrderSFX()
    {
        if (bank != null)
            SoundManager.Instance?.PlayOrderSFX(bank.orderSFX);
    }

    public void PlayAttackOrderVoice()
    {
        if (bank != null)
            SoundManager.Instance?.PlayOrderVoice(bank.attackOrderVoice, bank, "attack");
    }

    // 워커 전용 - PlacementSystem(건설 실패)/BaseStructure(건설 완료)가 담당 일꾼을 통해 호출한다.
    public void PlayBuildCompleteVoice()
    {
        if (bank != null)
            SoundManager.Instance?.PlayVoice(bank.buildCompleteVoice);
    }

    public void PlayBuildFailVoice()
    {
        if (bank != null)
            SoundManager.Instance?.PlayVoice(bank.buildFailVoice);
    }

    private void HandleDeath()
    {
        if (bank == null)
            return;

        SoundManager.Instance?.PlaySFX(bank.deathSFX, transform.position);
        SoundManager.Instance?.PlayVoice(bank.deathVoice);
    }

    // attackerPosition/attackType은 HealthManager.OnDamaged 시그니처를 맞추기 위해 받지만, 여기선 화면
    // 밖에서 공격받았는지만 확인해 전역 나레이션(경고음)/미니맵 핑으로 알린다 - PlayGlobalVoice의 쿨다운
    // 덕분에 여러 유닛이 동시에 맞아도 경고음이 스팸되지 않는다.
    // isEnemyAttacker가 false면(아군사격) 알리지 않는다 - "적에게 공격받았습니다"는 실제로 적에게
    // 공격받았을 때만 의미가 있다(doc/0292).
    private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
    {
        if (isEnemyAttacker && !SoundManager.IsWorldPositionOnScreen(transform.position))
        {
            // 쿨다운에 안 걸리고 실제로 경고음이 새로 재생된 순간에만 미니맵 마커도 같이 띄운다 -
            // 계속 얻어맞아도 경고음처럼 10초 간격으로만 나오게 (doc/0362).
            if (SoundManager.Instance != null && SoundManager.Instance.PlayUnitUnderAttackWarning())
                MinimapAlertController.Instance?.SpawnAttackedPointer(transform.position);
        }
    }
}
