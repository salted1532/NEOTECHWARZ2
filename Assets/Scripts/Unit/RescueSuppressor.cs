using UnityEngine;

// 겉모습은 OC 유닛 프리팹 그대로지만 실제로는 플레이어 유닛(UnitController, doc/0458의
// enemyDataUnitID로 OC 스탯을 그대로 사용)인 "구조 대상" 유닛에 붙인다. 구조되기 전까지
// UnitController를 꺼둬서(Start()가 안 돌아 UnitList 미등록/이동·전투 로직 정지) 일반 유닛
// 클릭/드래그 선택에서 제외하고, Layer/Tag도 임시 상태로 둬서 플레이어의 다른 유닛이 자동공격하지
// 않게 한다. Stage3Objectives가 "생존자 구조" 서브목표를 완료 처리하는 순간 Rescue()를 호출해서
// 정상적인 조종 가능한 아군 전투유닛으로 되돌린다.
[RequireComponent(typeof(UnitController))]
public class RescueSuppressor : MonoBehaviour
{
    [SerializeField] private int normalUnitLayer; // 구조 후 되돌릴 Layer("Unit") - 직접 지정
    private const string RescuedTag = "AttackUnit"; // 구조 후 적용할 Tag - 일반 NTA 전투유닛과 동일

    private UnitController unitController;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        unitController.enabled = false;
    }

    public void Rescue()
    {
        if (unitController.enabled)
            return; // 이미 구조됨 - 중복 호출 방지

        unitController.enabled = true;
        gameObject.layer = normalUnitLayer;
        gameObject.tag = RescuedTag;
        Destroy(this);
    }
}
