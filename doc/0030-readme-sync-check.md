# 0030 - README와 GitHub/코드 상태 동기화 점검

**날짜:** 2026-07-08

## 요청 내용

현재 GitHub에서 보여질 README.md를 확인하고, 변경된 사항이 있는지 확인해서 문서를 갱신해달라는 요청.

## 조사 내용

1. **git 상태 확인** — `git status`는 clean, `origin/main`과 동일. 로컬 README.md는 이미 최신 커밋(`b7adca7 Update README.md`)과 100% 일치 → 로컬에서 보는 파일이 곧 GitHub에서 보이는 파일과 같음.
2. **최근 커밋 히스토리 확인** — 직전 몇 개 커밫:
   - `b7adca7` Update README.md — 제목만 `# NEOTECHWARZ2` → `# NEOTECHWARZ2.0`으로 수정 (1줄 diff, GitHub 웹에서 직접 수정한 것으로 보임)
   - `0e6fbbd` 세션 로그 문서를 `Docs/` → `doc/`로 통합 이동 + `Rules/` 폴더 신설
   - `ac7ec20` README 대규모 갱신 ([[0023]] 문서 참고, 이미 완료된 작업)
   - 그 이전(`627b39c`, `31e523c` 등)은 Squad Panel 페이지네이션, 공격력/방어력 툴팁, 오인사격 등 기능 커밋 — 모두 [[0023]] 갱신 시점에 이미 README에 반영됨.
3. **README의 "구현 완료" / "로드맵(미구현)" 체크리스트를 실제 코드로 재검증**:
   - `PlacementSystem`, `BuildSystem/*` 전체에서 `ResourceManager` 참조 없음 → 건물 배치 자원 소모 미연결, README 기술과 일치.
   - `HealthBar`/`WorldSpace` 관련 코드 없음 → 체력바 UI 미구현, README와 일치.
   - `ControlGroup`/`Alpha1` 등 부대지정 단축키 관련 코드 없음 → 미구현, README와 일치.
   - `Refund`/환불/`Cancel...Resource` 관련 코드 없음 → 대기열 환불 미구현, README와 일치.
   - `Construct`/공사 진행/`MoveBuilding` 관련 코드 없음(건설 명령 텍스트만 존재) → 건물 건설·이동 미구현, README와 일치.
4. **결론** — README는 이미 최신 코드 상태와 정확히 일치하며, 마지막 변경(제목 수정)도 이미 로컬/원격 양쪽에 반영되어 있음. 추가로 갱신할 내용이 없음을 확인.

## 변경 내용

없음 — 점검 결과 README.md는 수정이 필요하지 않음(순수 확인 요청, 코드/문서 변경 없음).

## 요약/남은 작업

- README는 현재 시점 기준 최신 상태. 이후 코드가 바뀌면(특히 건물 자원 소모, 체력바, 건설 시스템 등 로드맵 항목이 구현될 때) README의 해당 체크박스를 갱신할 것.

## 변경된 파일

- 없음 (`doc/0030-readme-sync-check.md` 세션 로그만 신규 추가)
