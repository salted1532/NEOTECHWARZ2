# 0072 — 빌드 그리드 칸(셀) 크기 키우기

## 질문
"그리드 크기를 키울수는 있어?"

## 조사 내용

- 그리드 관련 코드는 `Assets/Scripts/BuildSystem/PlacementSystem.cs`, `GridData.cs`가 핵심.
- 실제 "칸 하나의 크기"는 코드가 아니라 씬(`Assets/Scenes/SampleScene.unity`)에 있는 Unity `Grid` 컴포넌트의
  `m_CellSize`로 결정됨 (GameObject `Grid`, fileID `1427386396`, 현재 `{x: 1, y: 1, z: 1}`).
- `GridData`(맵 전체 점유 정보)는 `Dictionary<Vector3Int, PlacementData>` 기반이라 애초에 크기 제한이 없음(사실상 무한 그리드) —
  따라서 "빌드 가능 영역 크기"는 이번 요청과 무관.
- `PlacementSystem.cs`의 배치 좌표/충돌 박스 계산(`GetGroundPosition`, `IsBlocked` 등)은 전부 `grid.cellSize`를
  런타임에 읽어서 계산하므로, 셀 크기를 바꿔도 배치 간격·충돌 판정 자체는 코드 수정 없이 셀 크기에 맞춰 자동으로 늘어남.
- 다만 건물/유닛 프리팹의 실제 메쉬 크기는 셀 크기와 별개로 고정되어 있고(`BuildingData.Size` 기본값이 `Vector2Int.one`,
  즉 "1칸 = 건물 1개" 기준으로 모델링됨), 셀 크기만 키우면 건물 모델 자체는 커지지 않으므로 **인접 건물 사이에 빈 공간이 생김**.
  → 사용자에게 확인한 결과, 이번엔 셀 크기만 변경하고 빈 공간은 그대로 두기로 함(추후 필요하면 별도 요청).

### 참고로 발견한 기존 잠재 버그 (이번엔 손대지 않음)
`GetGroundPosition`은 건물의 Z축(그리드 depth, `size.y`) 중앙 정렬 오프셋 계산에 `cellSize.y`를 사용하는데
(`PlacementSystem.cs:293`), 반면 `IsBlocked`의 충돌 박스는 같은 축에 `cellSize.z`를 사용함(`PlacementSystem.cs:332`).
지금까지는 `cellSize`가 `{1,1,1}`이라 `y == z`라서 문제가 드러나지 않았음. 이번 변경에서도 `x=y=z`로 동일하게 키워서
이 불일치가 드러나지 않게 함. 만약 나중에 `y`와 `z`를 다른 값으로 바꿀 일이 생기면 이 버그부터 고쳐야 함.

## 적용한 변경

`Assets/Scenes/SampleScene.unity`의 `Grid` 컴포넌트 `m_CellSize`를 `{1, 1, 1}` → `{2, 2, 2}`로 변경.

- 빌드 배치 간격이 가로/세로 모두 2배로 넓어짐 (칸 하나가 차지하는 실제 월드 공간이 커짐).
- 건물/유닛 판정 로직은 자동으로 이 값을 따라가므로 추가 코드 수정 없음.
- 건물 프리팹 자체 크기는 그대로라 건물 사이 빈 공간이 이전보다 넓어 보일 수 있음(요청대로 이번엔 그대로 둠).

### 추가로 발견 및 수정한 것: 배치 커서 하이라이트 크기 불일치

`PreviewSystem.cs:49`의 `cellIndicator`(빌드 모드에서 마우스에 붙는 흰색/빨간색 셀 하이라이트)는 지금까지
`localScale = (size.x, 1, size.y)`처럼 `grid.cellSize`를 전혀 곱하지 않고 있었음. 셀 크기가 1이었을 때는
우연히 맞았지만, 셀 크기를 2로 키우면 하이라이트가 실제 칸보다 작게(절반 크기로) 보이는 문제가 있어서
사용자 확인 후 같이 수정함.

- `Assets/Scripts/BuildSystem/PreviewSystem.cs`: `Grid grid` 필드를 추가하고, `PrepareCursor()`에서
  `size.x * cellSize.x`, `size.y * cellSize.z`로 셀 크기를 반영하도록 수정 (Z축에 `cellSize.z`를 쓰는 건
  `PlacementSystem.IsBlocked()`의 기존 관례를 따른 것).
- `Assets/Scenes/SampleScene.unity`: `PreviewSystem` 컴포넌트(fileID `2056467141`)에 `grid: {fileID: 1427386398}` 참조를 새로 연결.
- 씬/프리팹 전체를 검색해 `PreviewSystem`을 쓰는 곳이 이 씬 하나뿐임을 확인했으므로 다른 곳에서 `grid` 필드가
  비어서 NullReferenceException이 나는 경우는 없음.

### 확인 필요 사항
Unity 에디터에서 SampleScene을 열고 빌드 모드로 건물을 배치해서:
1. 칸 간격이 원하는 만큼(2배) 넓어졌는지
2. 마우스를 따라다니는 셀 하이라이트 사각형이 이제 실제 칸 크기와 맞게 보이는지
3. 건물 사이 빈 공간이 허용 가능한 수준인지 (거슬리면 건물 프리팹 스케일 확대나 `BuildingData.Size` 조정을
   후속 작업으로 별도 요청하면 됨)

를 확인 부탁.
