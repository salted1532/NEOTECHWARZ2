using System.Collections;
using UnityEngine;

// 적 유닛에 부착되는 컨트롤러. 선택 표시(마커)와 Info_panel에 필요한 아이콘/이름 조회, 사망 처리를 담당한다.
// (UnitController/BuildingController와 동일한 선택 마커 패턴)
public class EnemyController : MonoBehaviour, IDestructible
{
    [SerializeField]
    private GameObject enemyMarker;

    [SerializeField]
    private Sprite icon; // Info_panel에 표시할 아이콘

    [SerializeField]
    private string enemyName; // Info_panel에 표시할 이름

    // ===== 전투 스탯 (공격력/방어력) =====
    // UnitController와 동일한 패턴: Info_panel에서 UnitDamage/UnitArmor 아이콘 호버 시 표시할 값.
    [SerializeField] private int attackDamage;
    [SerializeField] private int armor;

    [SerializeField] private float flashInterval = 0.3f; // 공격 명령(우클릭/A) 피드백 깜빡임 간격
    [SerializeField] private int flashCount = 3;          // 깜빡이는 횟수

    private Coroutine flashRoutine;
    private RTSUnitController rtsController;

    void Start()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(false);

        rtsController = FindFirstObjectByType<RTSUnitController>();
    }

    // 공격 명령(우클릭/A 모드)을 받았을 때 "어느 적이 대상인지" 피드백으로 마커를 짧게 깜빡인다.
    // 좌클릭 선택 마커와 같은 오브젝트를 사용하므로, 끝나면 실제 선택 상태에 맞춰 복원한다.
    public void FlashMarker()
    {
        if (enemyMarker == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashMarkerRoutine());
    }

    private IEnumerator FlashMarkerRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(flashInterval);

        for (int i = 0; i < flashCount; i++)
        {
            enemyMarker.SetActive(true);
            yield return wait;
            enemyMarker.SetActive(false);
            yield return wait;
        }

        // 깜빡이는 도중 좌클릭으로 선택된 상태였다면(드문 경우) 꺼진 채로 두지 않고 선택 마커 상태로 복원
        bool isSelected = rtsController != null && rtsController.selectedEnemyList.Contains(this);
        enemyMarker.SetActive(isSelected);

        flashRoutine = null;
    }

    // 적 선택 시 마커(테두리 등 표시)를 활성화한다.
    public void SelectEnemy()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(true);
    }

    // 적 선택 해제 시 마커를 비활성화한다.
    public void DeselectEnemy()
    {
        if (enemyMarker != null)
            enemyMarker.SetActive(false);
    }

    public Sprite GetIcon() => icon;
    public string GetEnemyName() => enemyName;
    public int GetAttackDamage() => attackDamage;
    public int GetArmor() => armor;

    // 사망 처리: 선택 목록에서 제거하고 게임오브젝트를 파괴한다 (HealthManager의 IDestructible 구현체로 호출됨).
    public void Die()
    {
        rtsController?.selectedEnemyList.Remove(this); // 선택된 채로 죽었을 때 UI가 유령 참조를 들고 있지 않도록

        Destroy(gameObject);
    }
}
