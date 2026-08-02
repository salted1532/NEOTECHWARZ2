using UnityEngine;

// 아군 유닛/건물이 적에게 공격받으면 공격받은 위치에 3D 마커(Attacked_MiniMapPointer)를 잠깐 띄운다
// (doc/0362, doc/0349의 UI 핑 방식은 제거하고 이 방식으로 대체). UnitAudio/BuildingAudio의 기존
// HealthManager.OnDamaged 구독 지점에서 호출된다 - 새 구독 시스템을 따로 만들지 않고 이미 있는 연결
// 지점에 얹는다.
public class MinimapAlertController : MonoBehaviour
{
    public static MinimapAlertController Instance { get; private set; }

    [Header("공격받은 위치 3D 마커 (미니맵 카메라 전용)")]
    [SerializeField] private GameObject attackedPointerPrefab;
    [SerializeField] private float attackedPointerHeight = 40f;
    [SerializeField] private float attackedPointerLifetime = 3f;

    private void Awake()
    {
        Instance = this;
    }

    // 공격받은 위치(대상 자신의 위치) Y=attackedPointerHeight에 3D 마커를 스폰하고 일정 시간 뒤 자동 파괴한다.
    public void SpawnAttackedPointer(Vector3 attackedPosition)
    {
        if (attackedPointerPrefab == null)
            return;

        Vector3 pos = new Vector3(attackedPosition.x, attackedPointerHeight, attackedPosition.z);
        GameObject instance = Instantiate(attackedPointerPrefab, pos, attackedPointerPrefab.transform.rotation);
        Destroy(instance, attackedPointerLifetime);
    }
}
