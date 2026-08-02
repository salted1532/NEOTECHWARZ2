# 0361 — 적 건물 미니맵 아이콘도 안개 속에서 안 보이도록

**날짜:** 2026-08-02

## 요청

"건물에 경우에도 미니맵아이콘 안개속에서 안보이도록 스프라이트 인스펙터 연결추가해줘"

사용자가 적 건물 프리팹 7개(`Enemy_MainBase`, `Enemy_Lab`, `Enemy_SupplyDepot`, `Enemy_Tier1/2/3`,
`BaseStructure`)에 이미 `MiniMapIcon` 자식(SpriteRenderer, 빨간 사각형)을 추가해둔 상태 - `doc/0356`
(적 유닛 미니맵 아이콘)과 동일한 패턴을 건물에도 적용.

## 적용

**`Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`**

- `EnemyUnitController.minimapIcon`과 동일한 `[SerializeField] private SpriteRenderer minimapIcon;`
  필드 추가.
- 기존 `Update()`(doc/0360에서 선택 해제용으로 추가한 안개 조회)가 이미 매 프레임
  `FogVisibility.IsRevealed(fogWar, transform.position)`를 계산하고 있으므로, 그 결과 하나를
  `minimapIcon.enabled` 토글에도 같이 씀(안개 조회 중복 없음) - `EnemyUnitController.UpdateFogVisibility()`와
  동일한 구성.

**프리팹 7개** — 각 `MiniMapIcon` 자식의 `SpriteRenderer` fileID를 찾아 `EnemyBuildingController`의
`minimapIcon` 필드에 직접 연결(YAML 편집 후 Unity 에디터에서 `SerializedObject`로 재조회해 7개 전부
정상 연결 확인):

- `Enemy_MainBase.prefab`, `Enemy_Lab.prefab`, `Enemy_SupplyDepot.prefab`, `Enemy_Tier1.prefab`,
  `Enemy_Tier2.prefab`, `Enemy_Tier3.prefab`, `BaseStructure.prefab`(OC)

`npx uloop-cli compile` 통과 (에러 0, 경고 33개 — 기존과 동일, 신규 경고 없음).
