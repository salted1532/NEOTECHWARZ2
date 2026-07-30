using System.Collections;
using UnityEngine;

// 메인화면 배경 연출용: 대각선으로 날아간 뒤 시작점으로 텔레포트, 랜덤 대기 후 반복한다.
public class MainMenuFlyby : MonoBehaviour
{
    [SerializeField] private float startX = -100f;
    [SerializeField] private float startZ = -100f;
    [SerializeField] private float endX = 100f;
    [SerializeField] private float endZ = 100f;
    [SerializeField] private float speed = 20f;
    [SerializeField] private float minWaitSeconds = 10f;
    [SerializeField] private float maxWaitSeconds = 20f;

    private Vector3 startPoint;
    private Vector3 endPoint;

    private void OnEnable()
    {
        startPoint = new Vector3(startX, transform.position.y, startZ);
        endPoint = new Vector3(endX, transform.position.y, endZ);
        transform.position = startPoint;
        StartCoroutine(FlyLoop());
    }

    private IEnumerator FlyLoop()
    {
        while (true)
        {
            while (transform.position != endPoint)
            {
                transform.position = Vector3.MoveTowards(transform.position, endPoint, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = startPoint; // 텔레포트
            yield return new WaitForSeconds(Random.Range(minWaitSeconds, maxWaitSeconds));
        }
    }
}
