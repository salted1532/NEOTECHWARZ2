# EnemyBuildingController

`Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`

## 개요

적 건물 "껍데기". 캠페인은 정해진 스크립트/트리거로 적 유닛을 직접 배치·스폰할 예정이라, 적 건물이
실제로 생산 큐/자원 소모/건설 그리드를 가질 필요가 없다 — 지금은 체력만 갖고 있다가 파괴되면 사라지는
오브젝트로만 동작한다("적 전초기지 파괴" 같은 미션 목표의 대상 정도). `HealthManager`가 데미지/사망을
처리하고, 이 컴포넌트는 그 사망 처리(`IDestructible`)와 선택/마커/미니맵 표시만 담당한다.

> 나중에 스커미시에서 OC를 실제로 플레이 가능한 진영으로 만들 때 생산 큐 등 실제 기능이 필요해지면,
> `BuildingController`를 참고해서 이 클래스를 확장하면 된다 — 클래스 이름을 미리 맞춰둬서 나중에
> 갈아엎지 않아도 되게 함.

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `buildingMarker` | `GameObject` (SerializeField) | 선택 표시(`EnemyUnitController.enemyMarker`와 동일한 패턴) |
| `buildingName`, `icon` | (SerializeField) | Info_panel 표시용 이름/아이콘 |
| `minimapIcon` | `SpriteRenderer` (SerializeField) | 미니맵 전용 y20대 스프라이트 마커(빨간 사각형) — 안개에 가려지면 `Update()`에서 매 프레임 꺼짐/켜짐 |
| `enemyBuildingID` | `int` (SerializeField) | OC Building Data SO(`EnemyBuildingDataSO`)와 매칭되는 ID |
| `groundLayer` | `LayerMask` (SerializeField) | 씬에 직접 배치된 건물의 지면 정렬용 |
| `fogWar` | `csFogWar` (private) | 미니맵 마커/선택 해제 판정용 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 마커 비활성화, `rtsController`/`placementSystem`/`fogWar` 캐싱, 지면 정렬(`SnapToGround`), 그리드 등록(`RegisterToGridIfPossible`), OC Building Data SO로 스탯 자가 적용 |
| `Update()` | 안개 조회 결과 하나를 미니맵 마커 토글과 `RTSUnitController.ClearSelectedEnemyBuildingIfMatches()`(선택 중이면 안개 속으로 들어갈 때 자동 해제)에 함께 사용 |
| `RegisterToGridIfPossible()` (private) | 그리드 셀 좌표로 XZ 중앙정렬 후 `PlacementSystem`의 그리드 점유 정보에 등록 |
| `SnapToGround()` (private) | 현재 XZ의 지면 높이 + 메쉬 피벗 오프셋으로 Y 재정렬 |
| `ApplyBuildingData(data)` | OC Building Data SO 값으로 이름/체력 덮어쓰기 |
| `SelectEnemyBuilding()` / `DeselectEnemyBuilding()` / `FlashMarker()` | 선택 마커 on/off, 공격 지정 피드백 깜빡임 |
| `Die()` | 선택 상태 정리(`ClearSelectedEnemyBuildingIfMatches`) 후 파괴 |

## 연관 컴포넌트

- **RTSUnitController**: `selectedEnemyBuilding` 등록/해제, OC 데이터 조회
- **HealthManager**: 사망 시 `IDestructible.Die()` 호출
- **FogVisibility**: 미니맵 마커 표시 여부, 선택 해제 판정
- **PlacementSystem**: 그리드 셀 등록/지면 좌표 계산
