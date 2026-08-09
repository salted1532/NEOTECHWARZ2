# Stage0Objectives

`Assets/Scripts/System/Stage0Objectives.cs`

## 개요

0스테이지(튜토리얼) 임무 목표 체크리스트. 주목표 3개(거점 점령, 어썰트 트루퍼 10기 생산, 병영 건설)와 서브목표 2개(적 전멸, 광물 1000 확보)를 추적한다. 목표별 완료 조건은 `Update()`에서 매 프레임 다시 평가한다 — 자원을 다시 쓰거나 유닛이 죽는 등으로 조건이 깨지면 취소선도 다시 사라져야 하므로("한 번 완료되면 고정"하지 않는다). 거점 점령/유닛 수/광물량은 매 프레임 폴링으로, 적 전멸 여부만 0.5초 간격 스캔으로 확인한다(서브목표라 초당 갱신이 불필요하기 때문). 주목표 3개가 모두 완료되면 `StageManager.ReportVictory()`를 호출하고, 서브목표는 체크리스트 표시만 하며 승리 조건에는 포함하지 않는다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `targetZone` | 점령해야 할 거점 (`TerritoryZone`) |
| `captureZoneText` / `produceTroopersText` / `buildBarracksText` | 주목표 3개의 표시 텍스트 |
| `clearEnemiesText` / `secureOreText` | 서브목표 2개의 표시 텍스트 |
| `AssaultTrooperUnitID` / `RequiredTrooperCount` | 생산해야 할 유닛 ID(Marine, 표시명 "Assault Trooper")와 목표 수(10) |
| `BarracksBuildingID` | 건설해야 할 건물 ID |
| `RequiredOre` | 서브목표 광물 목표량(1000) |
| `enemyScanTimer` / `enemiesCleared` | 0.5초 간격 적 스캔 타이머와 결과 캐시 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | `StageManager.WireObjectiveTexts()`로 텍스트 UI 자동 연결, `RTSUnitController` 캐싱 |
| `Update()` | 이미 승패가 갈렸으면 스킵. 거점 소유자/트루퍼 수/병영 완공 여부/광물량을 매 프레임 재평가하고, 적 전멸 여부는 0.5초마다만 재스캔. 텍스트 갱신 후 주목표 3개 완료 시 승리 보고 |
| `CountAliveUnits(unitID)` (private) | `RTSUnitController.UnitList`에서 지정 ID의 생존 유닛 수를 센다 |

## 연관 컴포넌트

- **StageManager**: `WireObjectiveTexts()`/`ReportVictory()` 호출 대상
- **ObjectiveTextUtil**: 목표 텍스트 취소선 표시 공통 헬퍼
- **TerritoryZone**: 거점 점령 여부(`Owner`) 판정
- **RTSUnitController**: 유닛 목록/광물량/건물 완공 여부 조회
