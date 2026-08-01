using UnityEngine;

// 완공된 건물(BuildingController)과 건설 중인 파운데이션(BaseStructure) 양쪽에 공용으로 부착하는 사운드
// 전담 컴포넌트. UnitAudio와 동일한 패턴(doc/0255) - BaseStructure는 Initialize()에서 뒤늦게 buildingID를
// 받으므로, 재생 시점마다 은행(soundBank)을 다시 조회한다.
public class BuildingAudio : MonoBehaviour
{
    private BuildingController buildingController;
    private BaseStructure baseStructure;
    private HealthManager healthManager;
    private RTSUnitController rtsController;

    // rtsController를 Start()가 아니라 Awake()에서 구하는 이유(doc/0267): PlacementSystem.StartConstruction()이
    // Instantiate(baseStructurePrefab) 직후 같은 프레임에 BaseStructure.Initialize() -> PlayConstructLoop()를
    // 바로 호출하는데, Start()는 다음 프레임 전까지 실행되지 않아 그 시점엔 rtsController가 아직 null이라
    // 건설 시작 소리가 조용히 씹혔다. Awake()는 Instantiate() 안에서 즉시(동기) 실행된다.
    private void Awake()
    {
        buildingController = GetComponent<BuildingController>();
        baseStructure = GetComponent<BaseStructure>();
        healthManager = GetComponent<HealthManager>();

        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    private void OnEnable()
    {
        if (healthManager != null)
        {
            healthManager.OnDeath += HandleDestroyed;
            healthManager.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (healthManager != null)
        {
            healthManager.OnDeath -= HandleDestroyed;
            healthManager.OnDamaged -= HandleDamaged;
        }
    }

    private BuildingSoundBankSO GetBank()
    {
        if (rtsController == null)
            return null;

        if (buildingController != null)
            return rtsController.GetBuildingData(buildingController.GetBuildingID())?.soundBank;

        if (baseStructure != null)
            return rtsController.GetBuildingData(baseStructure.GetBuildingID())?.soundBank;

        return null;
    }

    // BaseStructure.Initialize()에서 ConstructionEffects.StartLoop()와 나란히 호출된다.
    public void PlayConstructLoop()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.constructLoopSFX, transform.position);
    }

    // BaseStructure.CompleteConstruction()에서 ConstructionEffects.StopLoopAndPlayComplete()와 나란히 호출된다.
    public void PlayConstructComplete()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.constructCompleteSFX, transform.position);
    }

    // RTSUnitController.SelectBuilding()에서 호출된다 ("건물 음성").
    public void PlaySelectVoice()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlayVoice(bank.selectVoice);
    }

    // HealthManager.OnDeath는 전투로 체력이 0이 됐을 때만 발생한다(BuildingEffects.HandleDestroyed와 동일 조건).
    private void HandleDestroyed()
    {
        BuildingSoundBankSO bank = GetBank();
        if (bank != null)
            SoundManager.Instance?.PlaySFX(bank.destroySFX, transform.position);
    }

    // isEnemyAttacker가 false면(아군사격) 알리지 않는다 - "적에게 공격받았습니다"는 실제로 적에게
    // 공격받았을 때만 의미가 있다(doc/0292).
    private void HandleDamaged(int amount, Vector3 attackerPosition, AttackEffectType attackType, bool isEnemyAttacker)
    {
        if (isEnemyAttacker && !SoundManager.IsWorldPositionOnScreen(transform.position))
        {
            SoundManager.Instance?.PlayBuildingUnderAttackWarning();
            MinimapAlertController.Instance?.ShowAttackPing(transform); // doc/0349
        }
    }
}
