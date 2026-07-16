# 0157. 언덕 위에서 줌 범위(min/maxZoom)가 지형 높이만큼 같이 올라가도록 (제안)

## 날짜
2026-07-17

## 요청
"현재 언덕아래는 카메라 줌이 딱 적당한대 언덕위를 보게되면 그 언덕높이만큼 더 가까이 보이게 되는거라서 너무 가깝게 보이는데 언덕위에있을땐 카메라 높이랑 최대 줌 값을 언덕 높이만큼 높여주도록 해줄수 있어? 카메라 정 중앙을 레이로 찍어서 각 지형의 높이를 알면 될거 같아"

## 조사
`CameraControl.HandleZoom()`(`Assets/Scripts/Camera/CameraControl.cs:122-137`)은 카메라의 **절대 월드 Y 높이**를 `minZoom~maxZoom`(8~35) 범위로 제한한다. 이 범위는 평지(Y=0) 기준으로 잡혀 있어서, 카메라가 언덕(지형이 솟은 곳) 위를 내려다볼 때는 "지면으로부터의 실제 거리"가 언덕 높이만큼 줄어드는데도 절대 Y는 그대로 같은 범위 안에 있어 유효하다고 판단 — 그래서 언덕 위에서는 평지보다 훨씬 가깝게 보인다.

`Assets/Scripts/Unit/UnitController.cs:403-418`의 `SampleGroundHeight()`가 이미 같은 문제(공중 유닛이 언덕 위/아래에서 지면höhe를 실시간으로 알아야 함)를 `Physics.Raycast(..., groundLayer)`로 풀어놓은 전례가 있어 동일한 패턴을 재사용한다. 씬의 실제 지형은 `Ground` 레이어(레이어 7, 비트값 128)의 평면 오브젝트들로 구성되어 있음(`doc/0087` 참고).

**주의(과거에 실제로 겪은 함정, `doc/0087`)**: `LayerMask` 필드를 씬/프리팹 YAML에 직접 쓸 때 `groundLayer: 128`처럼 스칼라로 쓰면 Unity가 제대로 역직렬화하지 못해 빈 값(Nothing) 취급된다. 반드시
```yaml
groundLayer:
  serializedVersion: 2
  m_Bits: 128
```
구조체 형태로 써야 한다.

## 제안하는 코드 변경

**기존 코드** (`Assets/Scripts/Camera/CameraControl.cs`)
```csharp
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 25f;
    [SerializeField] private float minZoom = 8f;  // 카메라 높이(Y) 하한 - 너무 가깝게 못 들어가게
    [SerializeField] private float maxZoom = 35f; // 카메라 높이(Y) 상한 - 너무 멀리 못 나가게
```
```csharp
    // 마우스 휠 입력으로 카메라를 자신이 바라보는 방향(forward)으로 앞뒤로 이동시켜 확대/축소한다.
    // (이 카메라는 Perspective라 orthographicSize는 아무 효과가 없어서 위치를 직접 옮기는 방식으로 줌을 구현한다)
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Vector3 zoomStep = transform.forward * scroll * zoomSpeed;
        float nextY = targetPosition.y + zoomStep.y;

        // 높이(Y) 기준으로 줌 범위를 제한 (지면에 처박히거나 너무 멀리 빠지는 것 방지)
        if (nextY < minZoom || nextY > maxZoom)
            return;

        targetPosition += zoomStep;
    }
```

**변경 코드**
```csharp
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 25f;
    [SerializeField] private float minZoom = 8f;  // 카메라 높이(Y) 하한 - 너무 가깝게 못 들어가게 (기준 지형고도 0 기준)
    [SerializeField] private float maxZoom = 35f; // 카메라 높이(Y) 상한 - 너무 멀리 못 나가게 (기준 지형고도 0 기준)
    [SerializeField] private LayerMask groundLayer; // 화면 중앙 지형 높이 샘플링용 레이어 (지형/Ground)
```
```csharp
    // 마우스 휠 입력으로 카메라를 자신이 바라보는 방향(forward)으로 앞뒤로 이동시켜 확대/축소한다.
    // (이 카메라는 Perspective라 orthographicSize는 아무 효과가 없어서 위치를 직접 옮기는 방식으로 줌을 구현한다)
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        Vector3 zoomStep = transform.forward * scroll * zoomSpeed;
        float nextY = targetPosition.y + zoomStep.y;

        // 화면 중앙이 보고 있는 지형의 높이만큼 줌 범위 자체를 같이 올려서, 언덕 위/아래 어디를 보든
        // "지형으로부터의 거리감"이 평지에서와 동일하게 느껴지도록 한다.
        float groundHeight = GetGroundHeightAtScreenCenter();

        if (nextY < minZoom + groundHeight || nextY > maxZoom + groundHeight)
            return;

        targetPosition += zoomStep;
    }

    // 화면 정중앙이 바라보는 지점의 실제 지형(groundLayer) 높이를 레이캐스트로 알아낸다.
    // 레이어 미설정이거나 아무것도 맞지 않으면(허공 등) 0(기준 평지)을 반환.
    private float GetGroundHeightAtScreenCenter()
    {
        if (groundLayer == 0)
            return 0f;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
            return hit.point.y;

        return 0f;
    }
```

`GetScreenCenterGroundPoint()`(Q/E 회전 pivot용, 평면 기반)는 건드리지 않고, 줌 전용으로 별도 레이캐스트 메소드를 새로 추가하는 방식 — 회전 피벗 동작에 영향을 주지 않기 위해 범위를 좁혔다.

## TestScene.unity 반영
Main Camera의 `CameraControl` 컴포넌트에 `groundLayer` 필드를 `Ground` 레이어(비트값 128)로 채워 넣음:
```yaml
  groundLayer:
    serializedVersion: 2
    m_Bits: 128
```
(반드시 구조체 형태로 — 스칼라로 쓰면 `doc/0087`과 같은 문제로 조용히 안 먹음)

## 요약 / 영향받는 파일
- `Assets/Scripts/Camera/CameraControl.cs` — `groundLayer` 필드 추가, `HandleZoom()`에 지형 높이 오프셋 반영, `GetGroundHeightAtScreenCenter()` 추가
- `Assets/Scenes/TestScene.unity` — Main Camera의 `CameraControl.groundLayer`를 `Ground` 레이어로 설정

## 비고
[[confirm_before_implementing]] — 사용자가 "이대로 적용시켜줘"로 확인하여 제안대로 적용 완료. `TestScene.unity`의 실제 `maxZoom` 값은 그 사이 에디터에서 20→30으로 바뀌어 있어(`doc/0153`과 다른 값), 그 현재 값(30) 기준으로 `groundLayer` 필드만 추가했고 `minZoom`/`maxZoom` 자체는 건드리지 않음.
