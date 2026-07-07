# 0028 - 세션 로그를 doc 폴더로 통합 (Docs → doc 이동 + 3개 설계 문서 번호 부여)

**날짜:** 2026-07-08

## 요청 내용
[0024](0024-doc-folder-before-after-diffs.md)에서 세운 "요청마다 `Docs/NNNN-*.md`(요청/답변) + `doc/NNNN-*.md`(코드 diff) 2개 파일" 규칙을 뒤집고, 대신:
1. `Docs/` 폴더에 있는 변경사항 로그 문서(0001~0024)를 전부 `doc/` 폴더로 이동.
2. `doc/` 폴더에 있던, 세션 번호가 없는 기존 3개 설계 문서(`ProductionQueue_UI_설계.md`, `select-info-panel-design.md`, `minimap-click-to-move-design.md`)도 — 이전 세션 정보가 없어 정확한 순번을 알 수 없지만 — 임의의 번호를 부여하고 내용을 보강해서 `doc/` 안으로 정리해달라는 요청.

## 처리 내용

### 1. Docs → doc 이동
- 코드 변경이 없던 순수 로그(0001, 0002, 0003, ..., 0016, 0023, 0024 등 18개)는 내용 그대로 `Docs/`에서 `doc/`로 이동(PowerShell `Move-Item`).
- 이미 `doc/`에 코드 diff만 담긴 파일이 존재하던 0017~0022(6개)는, `Docs/`의 요청/답변 로그와 `doc/`의 코드 diff를 하나의 파일로 병합(요청 내용 → 조사 내용 → 코드 변경 전/후 → 요약/남은 작업 → 변경된 파일 순서로 재구성) 후 `Docs/` 쪽 원본은 삭제.
- 결과적으로 `Docs/` 폴더에는 스크립트별 참고 문서(`UnitController.md`, `RTSUnitController.md` 등 번호 없는 파일)만 남음 — 이 문서들은 세션 로그가 아니라 이번 이동 대상이 아니었으므로 그대로 둠.

### 2. 기존 3개 설계 문서 번호 부여 + 보강
- `doc/ProductionQueue_UI_설계.md` → `doc/0025-ProductionQueue_UI_설계.md`
- `doc/select-info-panel-design.md` → `doc/0026-select-info-panel-design.md`
- `doc/minimap-click-to-move-design.md` → `doc/0027-minimap-click-to-move-design.md`
- 세 문서 모두 원래 2026-07-04에 "코드 수정 전 설계 문서"로 작성된 것이라 세션 번호 체계(0013에서 수립, 2026-07-07)보다 먼저 존재했음 — 정확한 순번을 재구성할 수 없어 사용자 지시대로 임의로 0025~0027을 이어 붙임. 각 파일 서두에 이 사정을 명시.
- **보강한 부분**: 각 설계 문서가 제안한 내용을 실제로 어느 세션이 구현했는지 교차 확인해서 구체적으로 링크를 달았다.
  - `0027`(미니맵): 설계는 `targetPosition`만 갱신하는 Lerp 방식을 제안했지만, 실제로는 `0002-minimap-click-teleport.md`가 "순간이동으로 바꿔줘"라고 요청해서 현재의 즉시 이동 코드가 됨 — 정확한 인과관계를 확인해 링크로 남김.
  - `0026`(Info/Squad Panel): 적/자원 선택(`0003`), 채취 마커 깜빡임(`0004`), `UnitController`/`BuildingController`의 `IDestructible` 누락 버그 수정(`0009`, `0014` — 이 설계 문서가 3-2절에서 미리 우려했던 문제와 정확히 일치), Squad Panel 페이지네이션(`0017`), 공격력/방어력 툴팁(`0019`)까지 실제 구현이 여러 세션에 걸쳐 이뤄졌음을 확인해 각각 링크.
  - `0025`(생산 대기열 UI): 실제 구현 세션이 번호 매김 시작(2026-07-06) 이전이라 특정 `doc/NNNN` 문서로 연결할 수 없음 — 그 사실을 문서에 명시.

### 3. README.md 갱신
- 프로젝트 구조 트리의 `doc/`/`Docs/` 설명을 새 구조에 맞게 수정 (`doc/` = 세션 로그+코드 diff+설계 노트 전부 번호로 통합, `Docs/` = 스크립트별 코드 문서만).
- "주요 기능" 아래 안내 문구와 "개발 프로세스 메모" 섹션의 경로/설명을 `Docs/0001-...` → `doc/0001-...`로 갱신.

### 4. 메모리(향후 세션 규칙) 갱신
- 기존에 별개로 있던 두 개의 feedback 메모리(`docs_session_logging`, `doc_code_diff_logging`)를 하나로 합쳐 `docs_session_logging.md`에 "요청마다 `doc/NNNN-*.md` 한 개, 필요 시 코드 diff 포함"으로 다시 작성. `doc_code_diff_logging.md`는 삭제하고 `MEMORY.md` 인덱스에서도 제거.

## 변경된 파일
- `doc/0001-*.md` ~ `doc/0016-*.md`, `doc/0023-*.md`, `doc/0024-*.md` (이동됨, 18개)
- `doc/0017-*.md` ~ `doc/0022-*.md` (병합 재작성, 6개)
- `doc/0025-ProductionQueue_UI_설계.md`, `doc/0026-select-info-panel-design.md`, `doc/0027-minimap-click-to-move-design.md` (이름 변경 + 내용 보강)
- `Docs/0001-*.md` ~ `Docs/0024-*.md` (전부 삭제 — `doc/`로 이동/병합 완료)
- `README.md`
- 메모리: `docs_session_logging.md`(재작성), `doc_code_diff_logging.md`(삭제), `MEMORY.md`(인덱스 갱신)
