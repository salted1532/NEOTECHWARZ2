# 0077 — BaseStructure(건설 중 기반)를 실제 건물 크기(2x2/3x3)에 맞춰 스케일

## 질문
"현재 BaseStructure가 3x3짜리 건물을 기준으로 크기가 정해져있는데 2x2건물을 지을때 건물기반이 커서 2x2에 맞지 않고
튀어 나오는 문제가 있어 2x2건물을 지을땐 BaseStructure의 크기를 이에 맞춰서 조절해줬으면 좋겠어"

## 원인 확인

- `Assets/prefabs/NTA/Building/BaseStructure.prefab`의 루트 Transform `m_LocalScale`이 `{x: 6, y: 2, z: 6}`으로
  고정돼 있음. 그리드 셀 크기가 2([[0072-increase-build-grid-cell-size]]에서 1→2로 변경됨)이므로 `6 = 3칸 × 2`,
  즉 **항상 3x3 건물 기준으로만** 크기가 맞게 만들어져 있고, 건물 종류에 따라 달라지지 않음.
- `Assets/Scripts/ScriptableObject/New Building Data SO.asset`에서 실제 `Size` 값 확인:
  `CommandCenter/Barracks/Factory/Spaceport`는 `{3,3}`, **`SupplyDepot`/`Lab`은 `{2,2}`**. 사용자가 말한 "2x2 건물"이
  정확히 이 둘.
- `BaseStructure.cs`는 스폰 시(`Initialize()`) 건물 크기를 전혀 반영하지 않고 프리팹에 박제된 스케일을 그대로 씀 —
  그래서 2x2 건물(SupplyDepot/Lab)을 지어도 항상 3x3 크기의 기반이 나와서 옆 칸을 침범하는 것처럼 튀어나와 보임.

## 수정 내용

- **`Assets/Scripts/Building/BaseStructure.cs`**: `Initialize()` 시그니처에 `Vector2Int buildingSize`, `Vector3 cellSize`
  파라미터를 추가. 함수 시작부에서
  `transform.localScale = new Vector3(buildingSize.x * cellSize.x, 기존Y유지, buildingSize.y * cellSize.z)`로
  가로/세로(X/Z)를 실제 건물 칸 수에 맞게 다시 계산해서 덮어씀. 세로(Y, 두께)는 프리팹에 원래 세팅된 값을 그대로 둠.
  (자식인 BoxCollider/NavMeshObstacle/마커/슬랩 장식은 전부 로컬 좌표 기준이라 부모 스케일 변경에 따라 자동으로 같이
  늘어나거나 줄어듦 — 별도 처리 불필요.)
- **`Assets/Scripts/BuildSystem/PlacementSystem.cs`**의 `StartConstruction()`: `structure.Initialize(...)` 호출에
  `data.Size`(건물의 그리드 칸 크기)와 `grid.cellSize`를 추가로 전달.
- `Initialize()`를 호출하는 곳이 `StartConstruction()` 한 곳뿐임을 확인했으므로 시그니처 변경으로 인한 다른 호출부
  깨짐 없음.
- `BaseStructure.CompleteConstruction()`이 완공 시 생성하는 최종 건물(`data.Prefab`)은 `BaseStructure`의 스케일과
  무관하게 자기 자신의 원래 스케일로 생성되므로(별도 `Instantiate` 호출), 이번 변경이 완공된 건물 크기에 영향을 주지
  않음 — 오직 건설 중에만 보이는 반투명/진행률 표시용 기반의 크기만 바뀜.

## 확인 필요 사항
Unity 에디터에서 SupplyDepot 또는 Lab(2x2)을 지어서 건설 중 기반이 2칸에 딱 맞게 나오는지, CommandCenter/Barracks 등
3x3 건물은 기존과 동일하게 3칸에 맞는지 확인 부탁.
