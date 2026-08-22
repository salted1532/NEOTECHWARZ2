using UnityEngine;

// 게임 시작 시 Y축 회전을 0~360 사이 무작위 값으로 적용한다. 나무/풀 등 장식 오브젝트를
// 배치할 때 매번 같은 방향으로 보이지 않게 하기 위한 용도 (doc/0659).
public class RandomYRotation : MonoBehaviour
{
    private void Awake()
    {
        transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
    }
}
