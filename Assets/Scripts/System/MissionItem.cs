using System.Collections.Generic;
using UnityEngine;

// 유물/데이터베이스 등 "줍기/반납"은 개별 스테이지 스크립트(Stage2Objectives 등)가 직접 처리하고,
// 이 컴포넌트는 "좌클릭으로 선택했을 때 Info Panel에 무엇을 보여줄지"(체력/전투 스탯 없음 -
// ResourceNode의 선택 관련 부분만 떼어낸 축소판, doc/0455)와 "지금 어떤 트리거 콜라이더에 실제로
// 닿아 있는지"(비콘 반납 판정용, doc/0456)를 담당한다.
public class MissionItem : MonoBehaviour
{
    [SerializeField] private string itemID; // 로컬라이제이션 키 구분용 (예: "artifact", "researchdata")
    [SerializeField] private string itemName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject selectionMarker; // 선택 시 표시할 마커 (없으면 그냥 표시 없이 선택만 됨)

    // 비콘 등 트리거 콜라이더에 실제로 겹쳐 있는지 판정한다 - 겹친 트리거를 전부 추적해서, 특정
    // 콜라이더(비콘)에 지금 닿아 있는지 IsTouching()으로 물어볼 수 있게 한다. 스크립트로 매 프레임
    // transform.position을 직접 옮기는 아이템이라 물리 트리거가 안정적으로 발동하려면 이 오브젝트에
    // Kinematic Rigidbody가 있어야 한다(doc/0456).
    private readonly HashSet<Collider> overlappingTriggers = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other) => overlappingTriggers.Add(other);
    private void OnTriggerExit(Collider other) => overlappingTriggers.Remove(other);

    public bool IsTouching(Collider other) => other != null && overlappingTriggers.Contains(other);

    // 선택 전까지는 꺼둔다 (ResourceNode.resourceMarker/EnemyUnitController.enemyMarker와 동일한 패턴, doc/0457).
    private void Start()
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
    }

    public void SelectItem()
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(true);
    }

    public void DeselectItem()
    {
        if (selectionMarker != null)
            selectionMarker.SetActive(false);
    }

    public Sprite GetIcon() => icon;

    public string GetItemName() =>
        LocalizationManager.GetTextOrFallback($"missionitem.{itemID}.name", itemName);

    public string GetDescription() =>
        LocalizationManager.GetTextOrFallback($"missionitem.{itemID}.desc", description);
}
