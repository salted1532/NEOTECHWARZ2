# 0073 — cellIndicator(셀 커서)가 건물 가운데에 떠 보이는 문제 수정

## 질문
"cellindicator y축 위치좀 조정해줘 지금 건물의 가운데에 위치하는 느낌이네"

## 원인

`PlacementSystem.Update()`에서 매 프레임 계산하는 `previewPos`는 원래 **건물 프리뷰(고스트) 오브젝트**의
피벗 위치를 위한 값으로, `GetGroundOffsetY(prefab)`(메쉬 바운드 기준으로 "메쉬 바닥이 지면에 닿도록" 필요한
높이 보정값 — 대략 건물 높이의 절반)를 더한 것.

문제는 `PreviewSystem.UpdatePosition()`이 이 **건물 높이만큼 이미 들어올려진 `previewPos`를 셀 커서(cellIndicator)
위치 계산에도 그대로 재사용**하고 있었다는 것 (`MoveCursor`에서 여기에 고정값 `-0.9`만 빼서 보정).
건물마다 높이(따라서 `GetGroundOffsetY` 보정값)가 다른데 보정값은 `-0.9` 고정이라서, 건물 높이가
0.9×2(≈1.8)와 차이가 나면 커서가 지면이 아니라 건물 중간 높이 즈음에 떠 보이게 됨 — 사용자가 느낀
"건물 가운데에 위치하는 느낌"이 바로 이것.

## 수정 내용

셀 커서는 건물 프리뷰와 별개로, **항상 지면 기준 위치**만 받아야 함. `GetGroundPosition()`이 이미
그 자체로 "건물 높이 보정 없는 순수 지면 위치"이므로 이걸 그대로 셀 커서에 넘기도록 분리함.

- `Assets/Scripts/BuildSystem/PlacementSystem.cs` (`Update()`):
  - `groundPos`(순수 지면 위치)와 `previewPos`(건물 높이 보정된 프리뷰용 위치)를 분리해서 계산.
  - `preview.UpdatePosition(previewPos, groundPos, valid)`로 변경 (인자 2개 → 3개).
- `Assets/Scripts/BuildSystem/PreviewSystem.cs`:
  - `UpdatePosition(Vector3 previewPosition, Vector3 groundPosition, bool validity)`로 시그니처 변경.
    프리뷰 고스트는 `previewPosition`(건물 높이 보정 포함), 셀 커서는 `groundPosition`(순수 지면)을 사용.
  - `MoveCursor()`의 고정 오프셋을 `-0.9`(건물 높이 보정값을 억지로 되돌리던 값) → `+cellIndicatorYOffset`
    (기본 `0.02`, 지면과 겹쳐서 z-fighting 나지 않을 정도로만 살짝 띄움)으로 교체.
  - 새 `[SerializeField] private float cellIndicatorYOffset = 0.02f;` 필드 추가 (인스펙터에서 미세 조정 가능).
- 다른 호출부/프리팹에서 `PreviewSystem`을 쓰는 곳이 `SampleScene.unity` 하나뿐임을 확인했으므로 시그니처
  변경으로 인한 다른 참조 깨짐 없음.

## 확인 필요 사항
Unity 에디터에서 빌드 모드로 여러 종류(높이가 서로 다른) 건물을 선택해보면서, 셀 커서가 건물 높이와 무관하게
항상 바닥에 딱 붙어 보이는지 확인 부탁. 만약 살짝 파묻히거나 뜨는 느낌이 있으면 `PreviewSystem` 인스펙터의
`Cell Indicator Y Offset` 값(기본 0.02)을 조정하면 됨.
