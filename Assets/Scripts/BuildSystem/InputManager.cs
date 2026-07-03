using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 건물 배치(BuildSystem) 전용 입력 처리기.
// 마우스 좌클릭/ESC 입력을 이벤트로 전달하고, 마우스가 가리키는 월드 좌표(지면)를 계산해준다.
public class InputManager : MonoBehaviour
{
    [SerializeField]
    private Camera sceneCamera;

    // 레이캐스트가 실패했을 때(마우스가 배치 가능 레이어를 벗어난 경우) 재사용할 마지막 유효 좌표
    private Vector3 lastPosition;

    [SerializeField]
    private LayerMask placementLayermask;

    // OnClicked: 좌클릭 시 발생 (건물 배치 확정용)
    // OnExit: ESC 입력 시 발생 (배치 모드 취소용)
    public event Action OnClicked, OnExit;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
            OnClicked?.Invoke();
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnExit?.Invoke();
        }

    }

    // 마우스 포인터가 UI 위에 있는지 여부 (UI 클릭 시 배치가 발생하지 않도록 방지)
    public bool IsPointerOverUI() => EventSystem.current.IsPointerOverGameObject();

    // 현재 마우스 위치에서 카메라 레이를 쏘아 배치 레이어(지면 등)와의 충돌 지점을 반환한다.
    // 레이가 아무것도 맞추지 못하면 마지막으로 유효했던 위치를 그대로 반환한다.
    public Vector3 GetSelectedMapPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        RaycastHit hit;
        Ray ray = sceneCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, placementLayermask))
        {
            lastPosition = hit.point;
        }
        return lastPosition;
    }
}
