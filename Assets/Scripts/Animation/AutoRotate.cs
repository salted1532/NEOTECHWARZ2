using UnityEngine;
using DG.Tweening;

// 레이더 접시, 포탑 머리처럼 계속 제자리에서 자전하는 파츠에 부착한다. 건물은 유닛과 달리 루트
// 트랜스폼을 매 프레임 덮어쓰는 이동 로직이 없으므로, 회전시키고 싶은 오브젝트에 직접 붙이면 된다
// (HoverBob/VehicleShake처럼 별도 자식으로 피할 필요 없음).
public class AutoRotate : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // 회전축 (레이더/포탑 대부분 Y축)
    [SerializeField] private float secondsPerRotation = 2f;     // 한 바퀴(360도) 도는 데 걸리는 시간
    [SerializeField] private bool clockwise = true;

    private Tween rotateTween;

    private void Start()
    {
        Vector3 axis = (clockwise ? 1f : -1f) * rotationAxis.normalized * 360f;

        rotateTween = transform.DOLocalRotate(axis, secondsPerRotation, RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1, LoopType.Incremental);
    }

    private void OnDestroy() => rotateTween?.Kill();
}
