# 0606 — 서브미션 1~4 임무 목표 스크립트 신규 작성

날짜: 2026-08-18

## 요청 내용

`Sub_Mission1`~`Sub_Mission4` 씬(게임매니저·맵은 이미 구성됨)에 들어갈 미션 오브젝트를 만들어달라는
요청. 조사 중 사용자가 "미션 오브젝트용 스크립트 만들면 돼"라고 범위를 좁혀줌 — 씬에 실제 프리팹을
배치/배선하는 작업은 제외하고, `Stage0~5Objectives`와 동일한 패턴의 임무 목표 체크리스트 스크립트만
작성.

## 조사 내용

- `Assets/Scripts/System/Stage0~5Objectives.cs`를 모두 확인 — 스테이지별로 스크립트 1개가 완결된
  구조(공통 헬퍼를 만들지 않고 각자 필요한 판정 로직을 그대로 복제하는 게 기존 컨벤션, Stage2의
  주석에 "스테이지당 스크립트 1개로 완결시키기 위함"이라고 명시돼 있음).
- `StageManager.ReportDefeat()`가 정의는 돼 있지만 기존 Stage0~5Objectives 어디에서도 아직 호출되지
  않음(패배 조건이 필요했던 스테이지가 지금까지 없었음) — 서브미션 1/4는 Campaign.md에 "부대가
  전멸하면 실패"라는 조건이 명시돼 있어 이번이 첫 `ReportDefeat()` 호출부가 됨. `VictoryPanelController`에
  대응하는 "패배 패널" UI는 아직 없음 — 이번 스크립트 범위 밖.
- `Sub_Mission1`~`4` 씬은 현재 `GameManager` + `Mission_SubN`(맵) 루트만 있고 `StageN Object` 같은
  목표 스크립트 컨테이너가 없음 — 스크립트 컴포넌트를 씬에 붙이고 필드(레이더 기지 GameObject,
  MissionItem, 비콘 Collider 등)를 연결하는 작업은 이번에 하지 않음(사용자 요청 범위 밖).
- `Docs/Campaign.md`의 서브미션 1~4 설명을 그대로 목표로 옮김.

## 코드 변경 (신규 파일)

### `Assets/Scripts/System/SubStage1Objectives.cs` — 서브미션 1(측면 기습)

`Stage1Objectives`와 동일한 패턴. 주목표: 레이더 기지 파괴(GameObject가 `null`이 되면 완료). 서브목표:
정찰병 전원 제거(`Stage0Objectives`처럼 0.5초마다 `EnemyUnitController` 스캔). 기지 없이 주어진
부대만으로 진행하므로, 배치된 부대(`RTSUnitController.UnitList`)가 전멸하면 `ReportDefeat()` 호출 —
이 프로젝트 최초의 패배 조건 구현.

### `Assets/Scripts/System/SubStage2Objectives.cs` — 서브미션 2(잔해 수색)

`Stage2Objectives`의 "일꾼이 주워서 비콘까지 나르기" 로직을 유물 파편 하나에만 적용(본편은 유물
본체+연구 데이터 두 개를 동시에 굴리지만, 이 서브미션은 파편 하나뿐). 서브목표(OC 회수팀 전멸)는
`SubStage1`과 동일하게 0.5초 스캔. Campaign.md에 명시된 실패 조건이 없어 패배 로직은 추가하지 않음.

### `Assets/Scripts/System/SubStage3Objectives.cs` — 서브미션 3(구조대 파견)

`Stage3Objectives`의 구조 판정(비콘 접촉 + 위장 OC 유닛 `Rescue()`)을 여러 지점(`RescuePoint` 리스트)으로
일반화. 서로 떨어진 지점을 전부 구조해야 승리 — 적 기지/건물 파괴는 목표에 없다는 Campaign.md 설명대로
주목표가 이것 하나뿐.

### `Assets/Scripts/System/SubStage4Objectives.cs` — 서브미션 4(최후의 저지선)

고정 배치된 방어부대로 시작, `defenseDurationSeconds`(기본 20분)를 버티면 승리. 서브목표(최소 병력
이상 생존)는 체크리스트 표시만 하고 승리 조건에는 포함하지 않음. 부대가 시간 안에 전멸하면
`ReportDefeat()`.

## 로컬라이제이션 키 추가

`ko.json`/`en.json`에 `objective.substage1.main1/sub1`, `objective.substage2.main1/sub1`,
`objective.substage3.main1`, `objective.substage4.main1/sub1` 8개 키 추가(`objective.stage5.main2` 다음).

## 검증

`npx uloop-cli compile` — `ErrorCount: 0`. 경고는 전부 기존 `Stage0~3Objectives.cs`에도 이미 있던
`FindFirstObjectByType`/`FindObjectsByType(FindObjectsSortMode)` deprecated 경고와 동일한 종류(신규
스크립트가 기존 컨벤션을 그대로 따랐기 때문) — 새로 발생한 경고 유형은 없음.

## 요약/남은 작업

서브미션 1~4의 임무 목표 스크립트 4개를 신규 작성 완료. 아직 남은 작업(이번 범위 밖):

- 각 `Sub_MissionN` 씬에 `SubStageNObjectives` 컴포넌트를 붙일 GameObject 생성
- 레이더 기지/파편/구조 비콘/방어부대 등 실제 프리팹을 맵에 배치하고 인스펙터 필드에 연결
- 서브미션 1/4의 `ReportDefeat()`를 받아 보여줄 "패배 패널" UI(`VictoryPanelController`의 패배판)는
  아직 없음 — 필요해지면 별도 작업

## 변경된 파일

- `Assets/Scripts/System/SubStage1Objectives.cs` (신규)
- `Assets/Scripts/System/SubStage2Objectives.cs` (신규)
- `Assets/Scripts/System/SubStage3Objectives.cs` (신규)
- `Assets/Scripts/System/SubStage4Objectives.cs` (신규)
- `Assets/Resources/Localization/ko.json`
- `Assets/Resources/Localization/en.json`
