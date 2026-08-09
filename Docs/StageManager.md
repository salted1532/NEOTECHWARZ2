# StageManager

`Assets/Scripts/System/StageManager.cs`

## 개요

스테이지(미션)의 승리/패배 "결과"만 담당하는 최소 골격 싱글턴. 어떤 조건이 목표 달성/패배인지는 이 매니저가 판단하지 않는다 — 각 시스템(적 전멸 판정, `BaseStructure` 파괴 감지 등, 실제로는 `Stage0~5Objectives`)이 조건을 직접 확인한 뒤 `ReportVictory()`/`ReportDefeat()`를 호출해서 결과만 보고하면, 여기서 상태를 한 번만 고정하고 이벤트로 알린다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `Instance` | 정적 싱글턴 인스턴스 |
| `StageResult` (enum) | `InProgress` / `Victory` / `Defeat` |
| `Result` | 현재 스테이지 결과 (읽기 전용) |
| `OnVictory` / `OnDefeat` | 승리/패배 확정 시 발생하는 이벤트 |
| `objectiveRowPrefab` | 목표 텍스트 한 줄 프리팹 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 싱글턴 등록 (중복 인스턴스는 자기 자신을 파괴) |
| `CreateObjectiveRow()` | 이 오브젝트(StageObject, `VerticalLayoutGroup` 있음) 밑에 `objectiveRowPrefab`을 복제해 자식으로 붙임 — 레이아웃 그룹이 생성 순서대로 수직 나열해줌 |
| `WireObjectiveTexts(stageObjectives)` | 리플렉션으로 `stageObjectives`(각 `Stage0~5Objectives`)가 가진 `TextMeshProUGUI` 필드를 전부 찾아, 아직 비어있는(인스펙터 미연결 또는 참조 끊김) 필드마다 행을 새로 만들어 채운다. 이미 값이 있는 필드는 덮어쓰지 않음 — 각 스테이지 스크립트는 `Start()` 맨 앞에서 이 한 줄만 호출하면 됨 |
| `ReportVictory()` | 임무 목표 달성 시 호출 (조건 판단은 호출부 책임) — 이미 결과가 확정됐으면 무시, `Result`를 Victory로 고정하고 `OnVictory` 발행 |
| `ReportDefeat()` | 패배 조건 충족 시 호출 (예: 아군 본진 파괴) — 동일하게 한 번만 고정 후 `OnDefeat` 발행 |

## 연관 컴포넌트

- **Stage0Objectives ~ Stage5Objectives**: `WireObjectiveTexts()` 호출자이자 `ReportVictory()` 호출처
- **VictoryPanelController**: `OnVictory` 이벤트를 구독해 승리 패널 표시
