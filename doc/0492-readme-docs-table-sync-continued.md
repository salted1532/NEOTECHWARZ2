# 0492 — README/Docs 갱신 작업 이어서 진행 (전날 중단분)

**날짜:** 2026-08-09

## 요청

"어제 중단된 Readme파일 갱신작업 다시 진행해줘"

## 조사 내용

`git status`상 `README.md`/`게임설명서.md`가 이미 대부분 갱신된 상태(unstaged)였고, `Docs/` 폴더에는
아직 커밋되지 않은 신규 스크립트 문서 37개가 untracked로 쌓여 있었다(`AllyController`,
`MissionItem`, `Stage0~5Objectives`, `LocalizationManager` 등 — 최근 세션들에서 캠페인/로컬라이제이션/
아군 OC/애니메이션 관련 스크립트가 추가되며 함께 작성된 문서들).

`README.md`의 스크립트 표(`Docs/` 링크 나열)와 실제 `Docs/*.md` 파일 목록을 diff해서 확인한 결과, 이
37개 파일이 표에 전혀 연결되어 있지 않았다 — 이것이 "중단된 작업"의 실체였다. 추가로:

- `Docs/Campaign.md`, `Docs/EnemyUnitAndBuildingStats.md`(둘 다 기존에 있던 문서, `doc/0294`에서 생성)도
  README 어디에도 링크되어 있지 않았음을 확인
- `doc/0490`(미션 오브젝트 유물/연구데이터 이름·설명 번역) 제안이 실제로는 이미 코드에 구현되어 있는데
  (`MissionItem.cs`에 `itemID`/`description`/`GetDescription()` 확인), `Docs/MissionItem.md`는 구현 전
  버전 그대로 남아있어 내용이 낡아 있었음
- `게임설명서.md`는 diff를 확인해보니 이미 스테이지 0~5 시놉시스, 미션 선택 화면, 언어 전환, 아군 OC
  합류 등 이번 사이클 내용이 전부 반영되어 있어 추가 변경 불필요

## 변경 내용

### `README.md`
- 스크립트 표에 신규 37개 행 추가(관련 있는 기존 행 옆으로 그룹핑): 전투 지원(`TurretController`,
  `UnitAnimatorDriver`, `Projectile`, `DamageOverTimeEffect`, `StealthVisual`, `RadiusIndicator`), 스킬
  (`SharpshooterSkill`/`SkyLancerSkill`/`GuardianDroneSkill`), 생산/연구(`ResearchQueue`,
  `UpgradeManager`), 적 진영 데이터(`EnemyUnitDataSO`/`EnemyBuildingDataSO`), 적/아군 AI
  (`EnemyAttackRange`, `AllyController`/`AllyBuildingController`/`AllyAttackRange`), 로컬라이제이션
  (`LocalizationManager`/`LocalizedText`), UI(`TooltipContentFitter`, `ControlGroupPanel`), 애니메이션
  (`InfantryIdleLookAround`, `VehicleIdleAnimation`, `ItemHover`), 캠페인/미션(`MainMenuController`,
  `MainMenuFlyby`, `SceneMenuController`, `MissionSelectManager`, `MissionItem`, `ObjectiveTextUtil`,
  `StageManager`, `Stage0~5Objectives`, `MinimapObjectiveMarker`, `MinimapObjectiveOverlay`,
  `VictoryPanelController`)
- "유닛/건물 수치 문서" 섹션에 `Docs/EnemyUnitAndBuildingStats.md`, `Docs/Campaign.md` 링크 추가
- "로컬라이제이션" 구현 완료 목록에 미션 오브젝트 이름·설명 번역(doc/0490) 항목 추가

### `Docs/MissionItem.md`
- `itemID`/`description` 필드, `GetItemName()`/`GetDescription()`의 번역 조회 동작을 실제 구현
  (`MissionItem.cs`)에 맞춰 갱신 (기존엔 구현 전 버전 그대로 남아 있었음)

### `게임설명서.md`
- 이미 완료 상태 확인, 변경 없음

## 요약 / 확인 사항

`Docs/*.md` 파일 목록과 README 표 링크를 재대조해 전부 일치함을 확인(Stage1~5는 "외" 표기로 통합
링크). 코드/에셋 변경 없음 — 문서 전용 세션.

## 영향받는 파일
- `README.md`
- `Docs/MissionItem.md`
