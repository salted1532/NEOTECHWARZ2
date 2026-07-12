# 0091. README 전체 동기화 — 모델링/포스트프로세싱/버그 수정 반영

**날짜:** 2026-07-12

## 요청 내용

사용자가 대규모 TODO 리스트(기존 [[0068-readme-full-sync-and-healthbarbillboard-doc|0068]]에서 쓰던 것과 같은 원문 리스트)를 다시 붙여넣고, "오늘은 유닛,건물 모델링 추가하고 포스트프로세싱 그리고 게임중 발생하는 버그 수정 및 디테일 추가했어" 라며 `README.md` 갱신을 요청.

## 조사 내용

`doc/0068`(마지막 README 동기화, 2026-07-12 오전) 이후 `doc/0069`~`doc/0090`까지 세션이 쌓였는데 README에는 전혀 반영되지 않은 상태였음을 확인:

- **전장의 안개(Fog of War)** — [[0069-fog-of-war-design|0069]]는 설계 + 제안 코드 문서일 뿐, 실제 `Assets/Scripts`에 반영된 적 없음(`FogOfWarManager` 등 신규 클래스가 프로젝트에 존재하지 않음을 grep으로 확인) → README에 "설계만 있고 미구현"임을 명시.
- **체력바 만피 시 자동 숨김** — [[0070-healthbar-hide-at-full-hp|0070]] 적용 완료.
- **3rd-party 모델링 에셋 임포트 + URP 머티리얼 변환** — [[0071-canopus-materials-broken-in-urp|0071]](Canopus-III, 10개 `.mat`), [[0075-yoge-materials-broken-in-urp|0075]](Yoge Stylized Nature, 30개 `.mat`). 사용자가 말한 "유닛/건물 모델링 추가"의 실체 — 다만 실제 게임플레이 프리팹(`prefabs/NTA/`)의 메시는 여전히 Unity 기본 프리미티브(캡슐/큐브/구)이고, 새 에셋은 아직 연결되지 않았음을 프리팹 grep으로 확인.
- **빌드 그리드 셀 크기 2로 확대 + 셀 커서 위치/스케일 버그 수정** — [[0072-increase-build-grid-cell-size|0072]], [[0073-cellindicator-y-position-fix|0073]].
- **공중 유닛 분리 반경을 유닛 크기 비례로** — [[0074-air-unit-separation-scaled-by-size|0074]].
- **포스트프로세싱(Bloom/Tonemapping/Color Adjustments) + SSAO 확인, 프리뷰/포인터를 포스트프로세싱에서 제외** — [[0076-exclude-preview-pointer-from-postprocessing|0076]]. 오버레이 카메라(`Indicator Camera`) + 전용 레이어(`Indicators`) 도입.
- **`BaseStructure` 크기를 건물 크기(2x2/3x3)에 맞춰 자동 스케일** — [[0077-basestructure-scale-to-building-footprint|0077]].
- **건물 리프트 이동 고도 버그 3연쇄 수정** — [[0078-building-lift-move-vertical-not-rising|0078]](수직/수평 독립 보간) → [[0079-air-altitude-stacking-bug|0079]](고도 중첩 방지) → [[0083-air-altitude-relative-to-terrain|0083]](절대 고정 대신 지형 기준 상대 고도).
- **공중 유닛/건물 지형 추적 비행("terrain hugging")** — [[0084-air-unit-terrain-hugging-altitude|0084]](유닛) → [[0085-building-terrain-hugging-altitude|0085]](건물) → [[0086-terrain-hugging-arrival-check-fix|0086]](도착 판정 버그) → [[0087-groundlayer-serialization-format-fix|0087]](LayerMask 직렬화 버그) → [[0088-building-flight-missing-pivot-offset|0088]](메쉬 피벗 오프셋 반영).
- **유닛 사망 시 인구수 반환** — [[0080-release-population-on-unit-death|0080]].
- **Lab 체력바 미연결 + 체력바 슬라이더 드래그 가능 버그** — [[0081-lab-healthbar-not-wired-and-slider-draggable|0081]].
- **Follow(따라가기) 정지 거리 — 유닛 크기(반경) 비례로 계산** — [[0082-follow-stop-distance|0082]](공중/지상 3단계 후속 수정 포함).
- **건설 이동 중 취소/일꾼 사망 시 건물 가격 환불** — [[0089-refund-on-build-move-cancelled|0089]], [[0090-refund-on-builder-death-in-transit|0090]].

`Assets/Scripts/**/*.cs`의 최근 수정 시각을 `Docs/*.md` 목록과 대조해 신규 스크립트 파일 여부도 확인 — 이번 구간(0069~0090)은 전부 **기존 스크립트의 수정**이었고, 신규 `.cs` 파일은 없었음(따라서 `Docs/` 폴더에 새로 추가할 문서는 없음).

## 변경 내용 (`README.md`)

- **기술 스택 표**: "그래픽" 행 신규 추가(포스트프로세싱/SSAO/오버레이 카메라).
- **프로젝트 구조**: `AssetFolder/`(3rd-party 모델링 에셋) 설명 추가, `Settings/` 설명에 포스트프로세싱 언급 추가, FogOfWar 안내 문구에 설계 문서 링크(`doc/0069`) 추가.
- **주요 기능**: 공중 유닛 지형 추적 비행, Follow 정지 거리(반경 비례), 건설 이동 중 취소/사망 환불, 건물 리프트 지형 추적+피벗 보정, `BaseStructure` 크기별 스케일, 체력바 만피 숨김, 신규 "그래픽/비주얼" 항목(포스트프로세싱/SSAO/3rd-party 에셋) 추가.
- **구현 완료 기능**: 유닛/건물/생산/자원 섹션에 위 변경사항 반영, 신규 "그래픽 / 비주얼" 소섹션 추가.
- **로드맵**: "유닛/건물 모델링 추가" → "실제 적용"으로 세분화(에셋 임포트는 완료, 프리팹 교체는 미완료), Fog of War 항목에 설계 문서 링크 추가.
- **건물 이동(리프트) 시스템**: 지형 추적 비행 + 피벗 오프셋 반영 내용으로 3번 항목 재작성, 고도 중첩 방지 6번 항목 신규 추가.
- **해결된 이슈**: 3rd-party 머티리얼 깨짐, 셀 커서 위치, 공중 고도 중첩, Follow 밀어붙이기, 지형 추적 착륙 버그, LayerMask 직렬화 버그, Lab 체력바 버그 — 6개 항목 신규 추가.

## 요약 / 남은 작업

README가 `doc/0090`까지 전부 동기화됨. 다음 세션 시작 시 `doc/0091` 이후 번호부터 이어가면 됨. Fog of War는 여전히 설계 문서 단계이고, 3rd-party 모델링 에셋은 임포트/머티리얼 변환만 끝난 상태로 실제 유닛/건물 프리팹 교체가 남아있음 — 둘 다 README 로드맵에 정확히 반영해둠.

## 변경된 파일
- `README.md` (문서 갱신, 코드 변경 없음)
- `doc/0091-readme-sync-modeling-postprocessing-bugfixes.md` (본 세션 로그, 신규)
