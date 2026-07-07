# 0024 - doc 폴더 코드 변경 전/후 문서화 (현재 세션 소급 적용 + 기존 3개 문서 정리)

**날짜:** 2026-07-08

## 요청 내용
[[doc_code_diff_logging]] 규칙에 따라, 이번 대화(현재 세션)에서 코드가 실제로 바뀐 요청들([[0017]]~[[0022]])에 대해 `doc/` 폴더에 "기존 코드 → 변경 코드" 형식의 문서를 새로 작성하고, 기존에 `doc/` 폴더에 있던 3개 설계 문서(`ProductionQueue_UI_설계.md`, `select-info-panel-design.md`, `minimap-click-to-move-design.md`)도 같은 형식으로 다시 정리해달라는 요청.

## 처리 방법
- **현재 세션의 코드 변경 6건**: 이미 Edit 도구로 직접 적용한 변경이라 정확한 old_string/new_string을 그대로 갖고 있어, 각 파일에 실제 있었던 코드를 그대로 "기존 코드"/"변경 코드"로 옮겨 적음.
- **기존 3개 설계 문서**: 이 문서들은 2026-07-04에 "코드 수정 전, 설계만 정리한 검토 문서"로 작성된 것이었고, 이후 실제 구현이 완료된 상태였다. 이 저장소에는 git 커밋 이력이 없어서(빈 저장소) 진짜 git diff를 재구성할 수 없으므로, **"설계 당시 제안했던 코드(기존 코드)" → "실제로 구현된 현재 코드(변경 코드)"** 형식으로 재정리하는 방식을 택함 — 각 파일 서두에 이 프레이밍을 명시해뒀다. 실제 코드는 이번 대화에서 해당 스크립트(`UIController.cs`, `UnitSpawner.cs`, `BuildingController.cs`, `RTSUnitController.cs`, `UnitController.cs`, `CameraControl.cs`, `MinimapController.cs`, `BuildingDataSO.cs`, `HealthManager.cs`)를 직접 읽어서 확인.
- 세 문서 모두 설계안과 실제 구현이 다른 지점들을 발견해 "차이점" 섹션으로 명시적으로 남김. 예:
  - 생산 대기열: `ProductionQueueSlot` 신규 스크립트 대신 기존 `ProductionSlot` 재사용, 슬롯별 progress 대신 공용 `progressSlider` 하나로 단순화.
  - 아이콘: `UnitData`/`BuildingData`에서 스폰 시점에 주입하는 대신, 각 프리팹에 아이콘을 직접 미리 꽂아두는 방식으로 감.
  - 미니맵: `JumpToWorldXZ`가 Lerp 보간 대신 즉시 순간이동으로 바뀌었고, `groundPoint.z -= 20f` 보정이 추가됨(설계 당시엔 없던 부분).

## 생성/수정된 파일 (`doc/` 폴더)
- `doc/0017-squad-panel-pagination.md` (신규)
- `doc/0018-unitcontroller-armor-and-attackdamage-move.md` (신규)
- `doc/0019-info-panel-attack-armor-hover-tooltip.md` (신규)
- `doc/0020-tooltip-compact-resize.md` (신규)
- `doc/0021-enemycontroller-armor-attackdamage.md` (신규)
- `doc/0022-hide-combat-stats-on-resource-select.md` (신규)
- `doc/ProductionQueue_UI_설계.md` (재작성)
- `doc/select-info-panel-design.md` (재작성)
- `doc/minimap-click-to-move-design.md` (재작성)

## 참고
- [[0016]](RTS 그래픽 리서치)과 [[0023]](README 갱신)은 코드 변경이 아니라 각각 리서치/문서 작업이라 `doc/` diff 문서를 만들지 않음 — [[doc_code_diff_logging]] 규칙과 일치.
- 이번 요청 자체는 `doc/` 문서만 다뤘고 게임 코드(.cs/prefab)는 건드리지 않아서, 이 요청 자체에 대한 `doc/0024-*.md` 짝 파일은 만들지 않음.

## 변경된 파일
- `doc/0017-squad-panel-pagination.md`, `doc/0018-unitcontroller-armor-and-attackdamage-move.md`, `doc/0019-info-panel-attack-armor-hover-tooltip.md`, `doc/0020-tooltip-compact-resize.md`, `doc/0021-enemycontroller-armor-attackdamage.md`, `doc/0022-hide-combat-stats-on-resource-select.md`, `doc/ProductionQueue_UI_설계.md`, `doc/select-info-panel-design.md`, `doc/minimap-click-to-move-design.md`
