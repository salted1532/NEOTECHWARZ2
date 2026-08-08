using UnityEngine;
using DG.Tweening;

// 조건/상태 판정 전혀 없이 붙이기만 하면 계속 위아래로 둥실거리고 천천히 회전하는 순수 장식용
// 컴포넌트. HoverBob(공중 유닛/리프트 건물 상태를 판정해서 재생 여부를 결정)과 달리, 미션 아이템처럼
// "그냥 항상 떠 있어야 하는" 오브젝트에 붙인다.
public class ItemHover : MonoBehaviour
{
    [SerializeField] private float bobHeight = 0.25f;   // 위/아래 각각 이동하는 폭
    [SerializeField] private float bobDuration = 1.4f;  // 한쪽 방향 이동에 걸리는 시간
    [SerializeField] private Ease bobEase = Ease.InOutSine;

    [SerializeField] private float rotationDuration = 8f; // Y축 한 바퀴(360도) 도는 데 걸리는 시간

    private void Start()
    {
        float baseY = transform.localPosition.y;

        transform.DOLocalMoveY(baseY + bobHeight, bobDuration)
            .SetEase(bobEase)
            .SetLoops(-1, LoopType.Yoyo);

        transform.DOLocalRotate(new Vector3(0f, 360f, 0f), rotationDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void OnDestroy() => transform.DOKill();
}
