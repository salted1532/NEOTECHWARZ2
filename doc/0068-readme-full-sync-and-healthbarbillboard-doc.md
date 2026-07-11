# 0068. README 전체 동기화 + HealthBarBillboard 문서 신규 추가

**날짜:** 2026-07-12

## 요청 내용

사용자가 대규모 TODO 리스트(기능 체크리스트, 구현해야 할 기능, UI 설계, 유닛 분류, 일꾼 채취 로직 등 — [[0023-readme-todo-checklist-update|0023]]에서 쓰던 것과 같은 원문 리스트, 이번엔 상단에 건물 이동/시작위치/부대지정/체력바 UI 등이 추가로 ✓ 표시됨)를 다시 붙여넣고, 이 내용을 읽고 현재 프로젝트의 `README.md`를 갱신 + 새로 추가된 스크립트가 있으면 그것도 추가해달라고 요청.

## 조사 내용

`doc/0030-readme-sync-check.md`(마지막 README 동기화 시점, 2026-07-08) 이후 `doc/0031`~`doc/0067`까지 세션이 쌓이는 동안 다음 기능들이 실제로 구현·적용 완료됐는데도 README에는 전혀(또는 일부만) 반영되지 않은 상태였음을 코드/세션 로그 대조로 확인:

- **건물 이동(리프트)** — [[0054-building-lift-relocate|0054]] 신규 구현 → [[0056-bugfix-lift-relocate-stale-descending-flag|0056]]/[[0057-lift-freeflight-land-lock-shortcut|0057]]/[[0058-bugfix-lift-shortcuts-slot-positions|0058]]/[[0061-bugfix-lift-altitude-and-production-guard|0061]]에서 보정. 최종 동작: `BuildingController.canLift`가 켜진 건물은 `L`로 이륙(그리드 해제)/착륙, 공중에선 우클릭 자유이동(공중유닛 패턴) 또는 `M` Move 버튼, 착륙 위치 지정 시 착륙 고스트+그리드 재예약, 공중 상태에선 생산 패널 전체 숨김(Land/Move만 노출) + 생산 대기열 있으면 이륙 차단. README `README.md`엔 "건물 이동"이 여전히 `[ ]`(미구현)로 남아있었음.
- **시작 위치(StartPoint) 메인기지 자동 생성** — [[0055-startpoint-mainbase-spawn|0055]]. `PlacementSystem.startPoint` 필드 + `SpawnStartingMainBase()`로 게임 시작 시 그리드 등록까지 포함해 메인기지 스폰. README에 전혀 언급 없었음.
- **부대 지정(컨트롤 그룹)** — [[0059-control-group-assignment|0059]] 신규 구현(Ctrl+숫자 저장/Shift+숫자 병합/숫자만 선택) → [[0065-control-group-select-cancels-order-state|0065]]/[[0066-control-group-select-cancels-order-state-scoped|0066]]/[[0067-control-group-cancel-hides-pointer-marker|0067]]에서 "부대 재선택 시 A/M/P 대기 모드 및 포인터 마커 취소" 동작으로 다듬어짐. README엔 여전히 `[ ]`(미구현)로 남아있었음.
- **유닛/건물 체력바 UI** — [[0060-healthbar-slider-and-billboard|0060]] 신규 구현. `HealthManager.healthSlider` 필드(`OnHealthChanged` 구독으로 자동 갱신) + 신규 `HealthBarBillboard.cs`(카메라 X축만 따라가는 빌보드 회전) → [[0062-bugfix-hide-healthbar-on-preview-ghost|0062]]/[[0063-bugfix-duplicate-healthbar-billboard|0063]]/[[0064-bugfix-healthbar-billboard-wrong-object|0064]]에서 프리뷰 고스트에 체력바 안 보이게 하는 `SetHealthBarVisible()` 등 보정. README엔 `[ ]`(미구현)로 남아있었고, **`HealthBarBillboard.cs`는 `Docs/` 폴더에 문서 자체가 없었음(신규 스크립트 누락)**.
- **유닛 선택 중 Shift+드래그 추가 선택** — [[0047-shift-drag-additional-selection|0047]]. README "주요 기능"/체크리스트에 문구가 없었음.
- **일꾼 자원 반납 대상 = 메인기지로 한정** — [[0048-worker-return-to-mainbase-only|0048]]. README "일꾼 채취 로직" 3단계가 여전히 "최근접 반납 건물"이라고만 적혀 있어 실제 동작(메인기지 전용)과 불일치했음.
- **메인기지 건설 시 자원 최소 이격 거리 규칙** — [[0049-building-min-distance-from-resource|0049]] → [[0050-mainbase-only-resource-distance-rule|0050]](메인기지 전용으로 축소) → [[0052-mainbase-resource-distance-7-and-inspector-expose|0052]](4→7칸, 인스펙터 노출). README에 전혀 언급 없었음.
- **건설 중 피해가 완공 건물에 이어짐** — [[0053-basestructure-health-carryover-on-completion|0053]]. README "건설 진행 시스템" 6단계에 반영 안 돼 있었음.
- **아군 강제 공격에 `BaseStructure`(건설 중 건물) 포함** — [[0051-friendly-fire-basestructure-support|0051]]. README "구현 완료" 문구가 "건물"만 언급하고 `BaseStructure` 포함 여부가 불명확했음.

`Assets/Scripts/**/*.cs` 전체를 `Docs/*.md` 목록과 대조해 신규 스크립트 여부도 확인 — `HealthBarBillboard.cs` 1개만 `Docs/`에 누락돼 있었고, 나머지는 기존 파일의 수정(신규 파일 아님)이었음.

## 변경 내용

### 신규 파일: `Docs/HealthBarBillboard.md`
기존 `Docs/*.md`들과 동일한 형식(역할/주요 필드/메소드/연관 컴포넌트)으로 신규 작성.

### `README.md`
- **핵심 스크립트 표**: `HealthBarBillboard` 행 추가.
- **주요 기능**: Shift+드래그 추가 선택, 부대 지정(컨트롤 그룹), 건물 이동(리프트), StartPoint 자동 메인기지, 메인기지-자원 이격 거리, 체력바 UI, 건설 중 피해 이월, 오인사격에 `BaseStructure` 포함 등을 반영해 대부분의 항목을 갱신.
- **구현 완료 기능 체크리스트**:
  - `[ ] 건물 이동` → `[x]`로 변경 + 설명 보강.
  - `[ ] 부대지정 단축키(컨트롤 그룹)` → `[x]`로 변경 + 설명 보강 (선택/피드백 섹션과 UI 섹션 양쪽).
  - `[ ] 유닛/건물별 체력바 UI` → `[x]`로 변경 + 설명 보강.
  - 건물/생산 섹션에 StartPoint 자동 생성, 자원 이격 거리 항목 신규 추가.
  - 유닛 섹션의 일꾼 채취 항목에 "메인기지로만 반납" 명시.
- **로드맵(미구현)**: 위에서 구현 완료로 옮긴 3개 항목(건물 이동/부대지정/체력바) 제거. 마지막 항목("BaseStructure가 전투로 파괴되는 실제 경로")은 "자동 사거리 탐지(`AttackRange`)가 `BaseStructure`를 대상으로 삼지 않는다"는 점으로 문구를 더 정확하게 다듬음(강제 공격 자체는 [[0051-friendly-fire-basestructure-support|0051]]로 이미 가능해졌으므로).
- **일꾼 채취 로직** 3/5단계: "최근접 반납 건물" → "최근접 메인기지"로 실제 동작에 맞게 수정.
- **건설 진행 시스템** 6단계: 건설 중 피해가 완공 건물로 이어진다는 문장 추가.
- **신규 섹션 "건물 이동(리프트) 시스템"**: 이륙/자유이동/착륙 흐름을 [[0054-building-lift-relocate|0054]]/[[0057-lift-freeflight-land-lock-shortcut|0057]]/[[0061-bugfix-lift-altitude-and-production-guard|0061]] 최종 동작 기준으로 5단계 정리.
- **신규 섹션 "부대 지정(컨트롤 그룹)"**: Ctrl/Shift+숫자, 숫자만 선택, A/M/P 모드 자동 취소 동작을 [[0059-control-group-assignment|0059]]/[[0066-control-group-select-cancels-order-state-scoped|0066]]/[[0067-control-group-cancel-hides-pointer-marker|0067]] 기준으로 정리.
- **키보드 단축키 표**: 건물 리프트(`L`), 공중 건물 이동(`M`), 부대 지정(`Ctrl`/`Shift`+숫자) 행 추가.

## 요약/남은 작업

- README와 `Docs/`가 2026-07-12 시점 코드 상태와 일치하도록 갱신 완료. `Docs/PlacementSystem.md`, `Docs/BuildingController.md` 등 기존 스크립트 문서 자체의 세부 필드/메소드 내용까지 이번에 전부 다시 훑어 갱신하지는 않았음(요청 범위는 README + 신규 스크립트 문서였음) — 필요하면 별도 요청으로 처리.
- 이후 새 기능이 추가되면 이번처럼 누락되지 않도록, README 갱신을 해당 기능 세션 자체에서 같이 처리하는 편이 나음(이번처럼 한번에 몰아서 하면 사실 확인에 시간이 더 걸림).

## 변경된 파일

- `README.md`
- `Docs/HealthBarBillboard.md` (신규)
- `doc/0068-readme-full-sync-and-healthbarbillboard-doc.md` (이 세션 로그, 신규)
