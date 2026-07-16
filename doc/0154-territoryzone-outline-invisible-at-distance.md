# 0154. TerritoryZone 외곽선(LineRenderer)이 카메라가 멀 때 안 보이는 문제 (제안)

## 날짜
2026-07-16

## 요청
"라인 렌더러가 카메라가 너무 머니깐 라인이 제대로 안보이고 렌더링이 안되는거 같은데 어떻게 해결할수 있어?" — 줌 문제는 그대로 두고, 이 문제만 해결해달라고 확인됨.

## 조사
`Assets/Scripts/CaptureSystem/TerritoryZone.cs`의 `RefreshOutline()`(라인 106-120)이 외곽선 `LineRenderer`의 각 정점을 `pinPoints[i].position`(핀의 월드 좌표) 그대로 사용한다. `TestScene.unity`에서 실제 핀들(`PinPoint_0~5`)의 로컬 Y 좌표를 확인해보면 전부 `y: 0`으로, 지면과 정확히 같은 높이에 붙어 있다.

여기에 두 가지가 겹쳐서 "멀리서 보면 라인이 잘 안 보인다"는 증상이 나온다:

1. **지면과 z-fighting**: 라인이 지면 메시와 정확히 같은 Y(0)에 있고, Main Camera의 `near clip plane: 0.3` / `far clip plane: 1000`(비율 1:3333)로 far가 매우 커서 깊이버퍼 정밀도가 낮다. 카메라가 멀어질수록(현재 Y=30, 50° 기울기 → 지면까지 거리 약 39유닛) 정밀도 손실이 더 커져서 라인이 지면과 겹쳐 깜빡이거나 묻혀 보이지 않을 수 있다.
2. **고정 폭이 거리에 따라 화면상 얇아짐**: `outlineWidth`가 월드 단위 고정값 `0.3`이라(`Capture_Point.prefab`의 `LineRenderer.widthCurve`도 동일하게 `0.3`), 카메라가 멀어질수록 화면에 투영되는 두께가 점점 얇아진다.

이 카메라(`CameraControl.cs`)는 사용자가 확정한 대로 Y=30 근방에서 계속 쓸 것이므로, 거리 자체를 줄이는 대신 라인 쪽을 개선하는 방향으로 제안한다.

## 제안하는 코드 변경

### 1) 외곽선을 지면보다 살짝 띄워서 z-fighting 방지

**기존 코드** (`Assets/Scripts/CaptureSystem/TerritoryZone.cs`)
```csharp
[Header("외곽선 표시")]
[SerializeField] private Material outlineMaterial; // 원본 참고용 — 실제로 색이 바뀌는 건 런타임 복제본
[SerializeField] private float outlineWidth = 0.3f;
```
```csharp
    private void RefreshOutline()
    {
        if (outlineRenderer == null) outlineRenderer = GetComponent<LineRenderer>();

        Vector2[] v = GetPolygonXZ();
        if (v.Length < 2)
        {
            outlineRenderer.positionCount = 0;
            return;
        }

        outlineRenderer.positionCount = pinPoints.Count;
        for (int i = 0; i < pinPoints.Count; i++)
            outlineRenderer.SetPosition(i, pinPoints[i].position);
    }
```

**변경 코드**
```csharp
[Header("외곽선 표시")]
[SerializeField] private Material outlineMaterial; // 원본 참고용 — 실제로 색이 바뀌는 건 런타임 복제본
[SerializeField] private float outlineWidth = 0.6f; // 카메라가 멀리서(탑다운 줌아웃) 봐도 보이도록 폭 상향
[SerializeField] private float outlineHeightOffset = 0.15f; // 지면과 겹쳐 z-fighting 나는 것 방지용 살짝 띄우는 높이
```
```csharp
    private void RefreshOutline()
    {
        if (outlineRenderer == null) outlineRenderer = GetComponent<LineRenderer>();

        Vector2[] v = GetPolygonXZ();
        if (v.Length < 2)
        {
            outlineRenderer.positionCount = 0;
            return;
        }

        outlineRenderer.positionCount = pinPoints.Count;
        for (int i = 0; i < pinPoints.Count; i++)
        {
            Vector3 p = pinPoints[i].position;
            p.y += outlineHeightOffset;
            outlineRenderer.SetPosition(i, p);
        }
    }
```

### 2) 기존 씬/프리팹에 이미 배치된 인스턴스의 폭 값도 맞춰줌
`Assets/prefabs/Capture_Point/Capture_Point.prefab`의 `LineRenderer.m_Parameters.widthCurve` 값(`value: 0.3` 두 곳)과 `TerritoryZone` 컴포넌트의 `outlineWidth: 0.3`을 `0.6`으로 동일하게 맞춤 (스크립트 기본값과 프리팹 저장값이 어긋나면 프리팹 쪽이 우선 적용되므로, 코드 기본값만 바꿔서는 기존 프리팹엔 반영 안 됨).

## 요약 / 영향받는 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` — `outlineHeightOffset` 필드 추가, `outlineWidth` 기본값 상향, `RefreshOutline()`에서 Y 오프셋 적용
- `Assets/prefabs/Capture_Point/Capture_Point.prefab` — `LineRenderer` 폭 곡선 값과 `TerritoryZone.outlineWidth`를 `0.6`으로 갱신

수치(`0.15`, `0.6`)는 제안값이라 실제 적용 후 에디터에서 눈으로 보고 조정이 필요할 수 있음.

## 비고
[[confirm_before_implementing]] — 사용자가 "적용 (추천)"으로 확인하여 제안대로 적용 완료. 실제 적용된 값도 제안값(`outlineHeightOffset: 0.15`, `outlineWidth: 0.6`)과 동일.
