# 0365 — README/Docs 전체 갱신 (TODO 리스트 반영, 미니맵 범례, 랠리 버튼, 세션 신규 스크립트 문서화)

**날짜:** 2026-08-02

## 요청

사용자가 방대한 TODO 리스트(✓ 완료 표시 포함)를 붙여넣고 다음을 요청:
1. 새로 추가된 코드 문서를 `Docs/`에 추가
2. 기존 코드 문서도 갱신
3. `README.md`에 연결
4. 로드맵(미구현) 갱신, 전체적으로 정리
5. 게임 설명서에 미니맵 표시 범례 추가(아군유닛=초록 원, 적유닛=빨간 원, 건물=사각형(아군 초록/적
   빨강), 거점=노란 원) + 생산 건물 랠리 버튼 설명

## README.md 변경

- **스크립트 표**: `EnemyController` → `EnemyUnitController`(개명 반영, doc/0231), `CaptureSystem` 설명을
  감쇠/2배속/sticky owner 메커니즘으로 갱신 + 링크를 `doc/0146`에서 신규 `Docs/CaptureSystem.md`로 교체,
  `EnemyBuildingController`/`MinimapAlertController`/`FogVisibility` 행 신규 추가
- **프로젝트 구조 트리**: `Enemy/`를 `FogOfWar/Enemy/`로 경로 수정(실제 폴더 구조와 일치), `Effects`/
  `FogOfWar`/`Camera`/`CaptureSystem` 설명에 안개 기반 스폰 스킵/랠리/감쇠 언급 추가
- **구현 완료 기능**: 점령/영토(감쇠·2배속·sticky owner·안개 가림), UI(미니맵 명령/색상 마커/공격받음
  마커/안개 시 선택 해제/UI 클릭 관통), 건물·생산(랠리 버튼 슬롯6/단축키 Y), 사운드(메인 화면+Option
  패널 배치 완료로 갱신) 섹션에 이번 세션 작업분 반영
- **신규 `## 미니맵 범례` 섹션**: 아군 유닛(초록 원)/적 유닛(빨간 원)/아군 건물(초록 사각형)/적 건물
  (빨간 사각형)/공격받은 위치(3초 마커) 표는 "구현 완료"로, **거점(노란 원)은 아직 코드에 없음을
  확인하고 "미구현"으로 정확히 표기**(실제 프리팹/코드에 거점 전용 미니맵 마커가 없음을 grep으로 확인 후 반영)
- **키보드 단축키 표**: "생산 건물 선택 시 랠리 Y(슬롯 6)" 행 추가
- **로드맵(미구현)**: Enemy AI 항목을 "기초 AI는 이미 있음, 전략적 판단 AI만 미구현"으로 정정(기존
  문구가 실제 코드와 어긋나 있었음), 메인화면/볼륨설정 UI 배치 항목을 "완료"로 이동(`MainScene` 존재
  확인), 사운드 콘텐츠 보강·브리핑룸·거점 미니맵 마커 항목 신규 추가
- **시작하기**: `MainScene`(Play/Option/Exit 메인 메뉴) 안내 추가

## Docs/ 변경

**신규 파일**:
- `Docs/CaptureSystem.md` — 완전 점령 되돌리기(1배속 30+30초, sticky owner)/중립 진행중 재점령(2배속)/방치
  시 회복·감쇠 3가지 규칙을 표로 정리, 안개 가림 포함
- `Docs/FogVisibility.md` — 공용 안개 조회 헬퍼, 소비처 6곳(EnemyUnitController/EnemyBuildingController/
  HealthManager/CaptureSystem/EffectPlayer/UnitEffects) 전부 나열
- `Docs/MinimapAlertController.md` — 공격받음 3D 마커 스폰, doc/0349 UI 핑 방식이 완전히 대체됐음을 명시
- `Docs/EnemyUnitController.md` — `Docs/EnemyController.md`(구버전, AI 없음 시절 문서)를 대체하는 완전
  재작성. 기존 `EnemyController.md`는 삭제
- `Docs/EnemyBuildingController.md` — 신규(기존에 문서 자체가 없었음)

**갱신 파일**:
- `Docs/UIController.md` — `BuildingRallySlotIndex`(6)/`rallyIcon` 필드, `ShowBuildingRallyCommand`/
  `ShowLabPanel`/`ShowBuildingLiftCommand`/`ShowUnitSkillSlot` 계열/`ShowSkillSelectPanel` 메소드 추가.
  단, `ShowMainBasePanel`/`ShowBarracksPanel`/`ShowFactoryPanel`/`ShowAirportPanel`이 실제로는
  `ShowUnitTierPanel`(doc/0200)로 통합됐을 가능성이 있어 ⚠ 표시만 해두고 전면 재작성은 범위 밖으로 둠
- `Docs/RTSUnitController.md` — `ClearSelectedEnemyIfMatches`/`ClearSelectedEnemyBuildingIfMatches`/
  `RallyButtonAction`/`UpdateUnitSkillUI`(및 `skillSlotShown` 스티키 버그 수정) 추가
- `Docs/HealthManager.md` — `healthSlider` 필드 자체가 문서에 없었어서 추가, `Update()`의 안개 기반
  체력바 숨김 로직 추가
- `Docs/UnitAudio.md`, `Docs/BuildingAudio.md` — `HandleDamaged`에 `MinimapAlertController.SpawnAttackedPointer`
  훅(경고음 실제 재생 시에만) 설명 추가
- `Docs/SoundManager.md` — `PlayGlobalVoice`/`PlayUnitUnderAttackWarning`/`PlayBuildingUnderAttackWarning`이
  `bool` 반환하도록 바뀐 것 반영
- `Docs/MinimapController.md` — 완전 재작성(우클릭 명령/대기 명령 확정/실제 지형 레이캐스트로 갱신, 예전
  문서는 Y=0 평면 교차만 쓰던 구버전 동작을 설명하고 있었음)
- `Docs/UserControl.md` — "유닛 선택 중 Y=랠리모드" 문구가 이번 세션에 삭제된 코드를 가리키고 있어서
  정정, `ShowMovePointerAt`/`IsRevealedByFog`/`HasPendingGroundOrder`/`ConfirmPendingOrderAt`/
  `IssueRightClickMoveAt`/`GroundLayerMask` 추가

## 범위 밖으로 남긴 것

- `EffectPlayer`/`UnitEffects`/`BuildingEffects`/`ConstructionEffects`/`TrailRotationFollower`/
  `HoverBob`/`VehicleShake`/`AutoRotate`/`TerritoryZone`/`TerritoryManager`/`FogRevealerAgent`/
  `TerritoryFogReveal`/`DamageMultiplierTableSO`는 여전히 `Docs/` 전용 문서 없이 `doc/NNNN` 세션 로그로만
  연결됨(README 각주에 명시) — 이번 세션에서 직접 바뀌지 않은 스크립트라 범위 밖으로 둠
- `UIController.md`의 `ShowMainBasePanel` 계열 4개 메소드가 실제로 `ShowUnitTierPanel`로 통합됐는지
  최종 확인/전면 재작성은 후속 작업으로 남김(⚠ 표시만 해둠)
