using UnityEngine;

// 체력바가 항상 카메라를 향하도록 하는 빌보드 회전. X(위아래 기울기)만 카메라 각도를 따라가고
// Y/Z는 항상 0으로 고정한다 - 유닛/건물이 Y축으로 회전해도 체력바 자체는 방향을 따라 돌지 않는다.
// 체력바 UI 오브젝트(Slider가 붙은 Canvas 등)에 직접 붙여서 사용한다.
public class HealthBarBillboard : MonoBehaviour
{
    private Camera targetCamera;

    private void Start()
    {
        targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
                return;
        }

        float cameraPitch = targetCamera.transform.eulerAngles.x;
        transform.rotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }
}
