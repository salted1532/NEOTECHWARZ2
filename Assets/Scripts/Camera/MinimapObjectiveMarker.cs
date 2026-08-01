using UnityEngine;

// 미션 목표와 관련된 오브젝트에 붙이면 미니맵에 아이콘으로 표시된다 (doc/0349).
// 예: 점령해야 할 거점(TerritoryZone), 방어해야 할 건물 등에 그냥 붙이기만 하면 되고, 스테이지별
// 목표 스크립트(Stage0Objectives 등)를 건드릴 필요가 없다. 오브젝트가 비활성화/파괴되면(예: 거점을
// 점령해서 더 이상 목표가 아니게 됨) 아이콘도 자동으로 사라진다.
public class MinimapObjectiveMarker : MonoBehaviour
{
    [SerializeField] private Color iconColor = Color.yellow;
    [SerializeField] private float iconSize = 14f;

    public Color IconColor => iconColor;
    public float IconSize => iconSize;

    private void OnEnable() => MinimapObjectiveOverlay.Instance?.Register(this);
    private void OnDisable() => MinimapObjectiveOverlay.Instance?.Unregister(this);
}
