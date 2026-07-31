using UnityEngine;

// 원형 범위를 잠깐 동안 바닥에 그려서 보여주는 범용 이펙트 (doc/0323 후속) - 스카이 랜서 "지상 폭격" 같은
// 범위형 스킬이 실제로 어디까지 피해를 입혔는지 눈으로 확인할 수 있게 하기 위한 용도.
// 텍스처/머티리얼 에셋 없이 LineRenderer + 내장 Sprites/Default 셰이더로 원을 그리므로 준비물이 필요 없다.
public class RadiusIndicator : MonoBehaviour
{
    [SerializeField] private int segmentCount = 48;
    [SerializeField] private float lineWidth = 0.2f;
    [SerializeField] private Color color = new Color(1f, 0.4f, 0.1f, 0.9f);

    private static Shader cachedLineShader; // Shader.Find 문자열 조회를 스킬 사용 때마다 반복하지 않도록 캐싱

    // 일회성 표시 - duration 뒤에 자동으로 사라진다 (스킬이 실제로 발동돼서 범위를 확인시켜줄 때).
    public static void Show(Vector3 center, float radius, float duration)
    {
        GameObject go = new GameObject("RadiusIndicator");
        go.transform.position = center;

        RadiusIndicator indicator = go.AddComponent<RadiusIndicator>();
        indicator.Draw(radius);

        Destroy(go, duration);
    }

    // 마우스를 따라다니는 지속형 표시 (범위 지정 대기 중) - 호출자가 SetPosition()으로 매 프레임 위치를
    // 갱신하고, 지정 모드가 끝나면 직접 Destroy(gameObject)해야 한다(Show()와 달리 자동 파괴 타이머 없음).
    public static RadiusIndicator CreateFollowing(float radius)
    {
        GameObject go = new GameObject("RadiusIndicator (Following)");

        RadiusIndicator indicator = go.AddComponent<RadiusIndicator>();
        indicator.Draw(radius);

        return indicator;
    }

    public void SetPosition(Vector3 position) => transform.position = position;

    private void Draw(float radius)
    {
        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = segmentCount;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        if (cachedLineShader == null) cachedLineShader = Shader.Find("Sprites/Default");
        line.material = new Material(cachedLineShader);
        line.startColor = color;
        line.endColor = color;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / segmentCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.1f, Mathf.Sin(angle) * radius)); // 지면에서 살짝 띄워 Z-fighting 방지
        }
    }
}
