using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 아군 유닛/건물이 적에게 공격받으면 미니맵에 짧게 핑을 표시한다 (doc/0349).
// UnitAudio/BuildingAudio의 기존 HealthManager.OnDamaged 구독 지점(doc/0292, 화면 밖일 때만 경고음 재생)에서
// 함께 호출된다 - 새 구독 시스템을 따로 만들지 않고 이미 있는 연결 지점에 얹는다.
public class MinimapAlertController : MonoBehaviour
{
    public static MinimapAlertController Instance { get; private set; }

    [SerializeField] private RectTransform minimapRect;
    [SerializeField] private Camera minimapCamera;
    [SerializeField] private Color pingColor = Color.red;
    [SerializeField] private float pingSize = 18f;
    [SerializeField] private float pingDuration = 2.5f; // 핑이 갱신 없이 유지되는 시간(초)

    private class PingEntry
    {
        public RectTransform rect;
        public float remainingTime;
    }

    // 대상(Transform) 기준으로 관리 - 같은 대상이 연속으로 맞아도(기관총 세례 등) 핑을 새로 쌓지 않고
    // 타이머만 갱신하며, 대상이 움직이면(퇴각하는 유닛 등) 매 프레임 최신 위치를 따라간다.
    private readonly Dictionary<Transform, PingEntry> activePings = new Dictionary<Transform, PingEntry>();
    private readonly List<Transform> expiredBuffer = new List<Transform>();

    private void Awake()
    {
        Instance = this;
    }

    public void ShowAttackPing(Transform target)
    {
        if (target == null)
            return;

        if (activePings.TryGetValue(target, out PingEntry entry))
        {
            entry.remainingTime = pingDuration;
            return;
        }

        GameObject iconObj = new GameObject("AttackPing", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)iconObj.transform;
        rect.SetParent(minimapRect, false);
        rect.sizeDelta = new Vector2(pingSize, pingSize);

        Image image = iconObj.GetComponent<Image>();
        image.color = pingColor;
        image.raycastTarget = false;

        activePings[target] = new PingEntry { rect = rect, remainingTime = pingDuration };
    }

    private void Update()
    {
        if (activePings.Count == 0)
            return;

        Rect rect2D = minimapRect.rect;
        expiredBuffer.Clear();

        foreach (var pair in activePings)
        {
            Transform target = pair.Key;
            PingEntry entry = pair.Value;

            entry.remainingTime -= Time.deltaTime;

            if (target == null || entry.remainingTime <= 0f)
            {
                if (entry.rect != null)
                    Destroy(entry.rect.gameObject);

                expiredBuffer.Add(target);
                continue;
            }

            Vector3 viewportPoint = minimapCamera.WorldToViewportPoint(target.position);
            entry.rect.anchoredPosition = new Vector2(
                rect2D.xMin + viewportPoint.x * rect2D.width,
                rect2D.yMin + viewportPoint.y * rect2D.height);
        }

        for (int i = 0; i < expiredBuffer.Count; i++)
            activePings.Remove(expiredBuffer[i]);
    }
}
