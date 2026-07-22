## 2026-07-22

### 요청 내용

GitHub README를 갱신해달라는 요청. 오늘 세션에서 추가한 내용(tier 필드, 장갑/크기/공격방식 데미지 배율 시스템, 유닛 스탯 SO 자가 동기화, Sharpshooter/SkyLancer 신규 유닛)과 방금 만든 `Docs/UnitAndBuildingStats.md` 문서를 반영해달라고 함. 또한 사용자가 갖고 있던 오래된 프로젝트 TODO 체크리스트(선택 UI, Follow, 건설 시스템, 리프트, 부대지정, 체력바, 모델링, 스카이박스/포스트프로세싱, 점령 시스템, 이펙트, 맵, 미니맵, 전장의 안개, 건물 다중선택, 테크트리, 연구소 업그레이드 등 대부분 ✓ 표시)를 참고해서 불필요한 내용은 빼고 로드맵도 갱신해달라고 함.

README 갱신은 순수 문서화 작업(코드 변경 없음)이라 `confirm-before-implementing` 게이트 없이 바로 진행함.

### 조사 내용

- 대조해보니 사용자가 붙여넣은 TODO 체크리스트의 ✓ 항목 대부분은 README의 기존 "구현 완료 기능" 섹션에 이미 더 상세하게 서술되어 있어 중복 — 그대로 옮겨적지 않고, 빠진 게 있는지 교차 확인하는 용도로만 사용함.
- 다만 교차 확인 중 **README 자체가 stale한 부분을 발견**: README는 "`FogOfWar/` 폴더는 아직 없다"며 전장의 안개를 로드맵(미구현)으로 분류하고 있었는데, 실제로는 `Assets/Scripts/FogOfWar/`(`FogRevealerAgent.cs`, `TerritoryFogReveal.cs`)가 이미 존재하고 3rd-party 플러그인 `csFogWar`(`AssetFolder/AOSFogWar/`)와 완전히 연동되어 있었음. 사용자의 TODO 체크리스트는 이 부분에 대해 "✓전장의 안개 구현"이라고 정확하게 표시하고 있었음 — 즉 사용자 체크리스트가 맞고 README가 밀린 상태였음.
- 이 발견 때문에, 나머지 로드맵/구현완료 항목들도 사용자 체크리스트만 믿지 않고 코드로 재검증(fork 서브에이전트 위임): Fog of War(구현완료, 유닛 9종+건물 6종+`BaseStructure` 전부 연동, `doc/0166`~`0197`), 유닛/건물 모델링(유닛은 전부 프리미티브 메시 그대로, 건물은 병영에 실제 모델 1개만 적용 시작 — 부분구현), 래그돌/사망애니메이션(미구현, 여전히 `Destroy(gameObject)`뿐), Enemy AI(미구현), 사운드 매니저(미구현), 메인화면/설정창(미구현), 캠페인 맵(미구현, Mission2~5 프리팹은 있지만 어느 씬에서도 미사용), `AttackRange`의 `BaseStructure` 자동타겟팅(여전히 안 됨, `BaseStructure`가 `Untagged`).
- 오늘 세션(`doc/0200`~`0211`)에서 추가된 내용도 함께 반영 대상으로 확인: tier 필드 기반 생산 패널 자동분류(`doc/0200`), 장갑/크기/공격방식 데미지 배율 시스템 + `DamageMultiplierTableSO`(`doc/0201`), 유닛이 자기 `unitID`로 SO를 스스로 조회해 스탯 적용(`doc/0203`→`0205`로 아키텍처 개선), `attackSpeed` 필드(`doc/0207`), 프리팹↔SO 동기화(`doc/0209`~`0210`), Sharpshooter/SkyLancer 신규 유닛.

### 코드 변경

없음(README.md만 수정).

### README 변경 요약

- 프로젝트 구조 트리에 `FogOfWar/` 폴더 추가, "폴더 없음" 안내 문구를 "구현 완료, doc/0069·0166~0197 참고"로 교체
- `AssetFolder/` 설명에 `AOSFogWar`(csFogWar) 플러그인 추가, 모델링 에셋 적용 현황 갱신(건물 1개만 적용 시작)
- 핵심 스크립트 표에 `DamageMultiplierTableSO`, `FogRevealerAgent`, `TerritoryFogReveal` 행 추가, `UnitDataSO` 행 설명을 tier/자가동기화 내용으로 갱신
- "유닛/건물 수치 문서" 절 신설 — `Docs/UnitAndBuildingStats.md`(최신 스탯 시트), `Docs/UnitBalanceReference.md`(설계값 vs 실측값 감사 기록) 링크
- 주요 기능에 "데미지 배율 시스템", "유닛 생산(자동 분류 + 자가 동기화)", "전장의 안개" 불릿 추가
- 구현 완료 기능: "유닛" 절에 tier 자동분류/자가동기화/신규유닛 2종 추가, "데미지 시스템" 신규 절, "전장의 안개" 신규 절 추가, "그래픽/비주얼"의 모델링 서술을 건물 1개 적용 시작으로 갱신
- 로드맵: 전장의 안개 항목 제거(완료로 이동), 모델링 항목을 유닛/건물로 구분해서 재서술, 캠페인 맵·지원기(Support Ship) 항목 신규 추가, 사운드/메인화면 항목에 근거 보강
- 키보드 단축키 표에 Sharpshooter(S, 스카웃드론과 중복)/SkyLancer(S) 추가

### 요약/영향받는 파일

- `README.md`

### 남은 이슈 (문서에만 기록)

- 병영 생산 패널의 스카웃 드론/Sharpshooter 단축키가 둘 다 `S`로 중복 — 같은 패널에 동시에 떠서 실제 충돌함(README에 명시해둠, 코드 수정은 안 함).
