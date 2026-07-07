# 0023 - README TODO 체크리스트 검증 및 갱신

**날짜:** 2026-07-08

## 요청 내용
사용자가 붙여넣은 대규모 TODO 리스트(기능 체크리스트, 구현해야 할 기능, UI 설계, 유닛 분류, 일꾼 채취 로직 상세 설명 등)를 실제 코드 상태와 대조 확인하고, 프로젝트 루트 `README.md`를 갱신 — TODO 리스트에는 없지만 이미 구현된 추가 기능이 있으면 찾아서 정리해달라는 요청.

## 조사 내용 (체크 여부 검증)
코드를 직접 확인해 사용자가 이미 ✓ 표시한 항목들과, 표시하지 않은 항목들의 실제 구현 여부를 검증함:

- **오검증/누락 발견**:
  - "건물 건설, 유닛 생산 (자원 사용) 자원매니저랑 연결 + 인구수 추가"는 유닛 생산 쪽만 연결되어 있음. `RTSUnitController.SpawnUnit`/`TryProduceUnit`은 `ResourceManager.TrySpend(mineral, gas, population)`을 호출하지만, `PlacementSystem.PlaceStructure()`(건물 배치)는 `ResourceManager`를 전혀 호출하지 않아 건물이 자원 소모 없이 즉시 완공 상태로 생성됨.
  - "유닛 건설(일꾼이 건물 건설)"은 실제로 미구현 — `PlacementSystem.PlaceStructure()`가 클릭 즉시 `Instantiate`로 완성된 건물을 생성하며, "공사 진행 중" 상태나 일꾼이 짓는 로직 자체가 없음.
  - "건물 이동"은 코드베이스에 관련 로직 없음 (미구현).
  - "대기열 환불": `UnitSpawner.Cancel(index)`는 큐에서 제거만 하고 소모한 자원/인구수를 되돌려주지 않음 (미구현).
  - "부대지정 단축키(컨트롤 그룹)", "체력바 UI", "공격 이펙트", "전장의 안개", "Enemy AI", "사운드", "메인화면/설정창 UI" — 전부 grep으로 관련 클래스/키워드를 찾아봤으나 코드베이스에 존재하지 않아 미구현으로 확인.
- **이미 구현되어 있었지만 TODO 리스트에 아예 언급이 없던 기능**:
  - 아군 강제 공격("오인사격") — `UnitController.AttackFriendlyTarget`/`AttackFriendlySelectedUnits`/`AttackFriendlyBuildingSelectedUnits`, A 모드에서 아군 유닛/건물 좌클릭 시 끝까지 추격 공격 ([[0008]], [[0014]] 참고). 사용자가 준 TODO 리스트 어디에도 이 항목이 없어서 "추가 기능"으로 README에 명시.
  - 공중 유닛은 NavMesh를 아예 쓰지 않고 직접 좌표 보간으로 이동 — "이동 시 navmesh를 이용하지 않은 지형을 무시하는 이동"이라는 하위 메모가 있었는데, 이미 충족된 상태임을 코드로 확인(`UnitController.Update()`의 `isAirUnit` 분기).
- **프로젝트 루트 `README.md` 자체의 기존 오류 발견 및 수정**:
  - 프로젝트 구조 트리에 `Assets/Scripts/FogOfWar/` 폴더가 있다고 적혀 있었으나 실제로는 존재하지 않음(전장의 안개 미구현과 일치) — 트리에서 제거하고 미구현 상태를 각주로 명시.
  - "생산 대기열 UI에 대한 추가 설계 노트는 [Docs/ProductionQueue_UI_설계.md](Docs/ProductionQueue_UI_설계.md)"라는 링크가 깨져 있었음(실제 파일은 `Docs/`가 아니라 `doc/`(소문자) 폴더에 있음) — 링크 경로를 `doc/ProductionQueue_UI_설계.md`로 수정하고, 같은 폴더의 `doc/select-info-panel-design.md`, `doc/minimap-click-to-move-design.md`도 함께 링크.
  - "로드맵"에 이미 완료된 "미니맵 구현"이 남아있었음 — 제거(완료 섹션으로 이동).

## 변경 내용 (`README.md`)
- 프로젝트 구조 트리에서 존재하지 않는 `FogOfWar/` 제거 + 미구현 각주 추가.
- 깨진 문서 링크 경로 수정(`Docs/` → `doc/`), 관련 설계 노트 2개 추가 링크.
- "주요 기능" 섹션을 코드 기준으로 갱신(공격력/방어력, 오인사격, Squad Panel 페이지네이션 등 반영).
- "구현 완료 기능"을 5개 카테고리(선택/피드백, 유닛, 건물/생산, 자원/인구수, UI)로 나눠 체크박스 리스트로 새로 작성 — 각 항목은 코드 확인을 거쳐 상태 표기.
- "로드맵(미구현)" 섹션을 실제 미구현 항목으로 전면 갱신.
- 사용자가 원문으로 준 "UI 설계 노트", "유닛 분류", "일꾼 채취 로직"(상세 알고리즘), "이전작과 다른 점"을 README에 정식 섹션으로 정리해 추가.
- "개발 프로세스 메모" 섹션 추가 — `Docs/` 세션 로그 규칙([[docs_session_logging]] 메모리)을 README에서도 짧게 안내.

## 변경된 파일
- `README.md`
