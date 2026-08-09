# Stage1Objectives

`Assets/Scripts/System/Stage1Objectives.cs`

## 개요

1스테이지("국경 분쟁") 임무 목표 체크리스트. `Stage0Objectives`와 동일한 패턴으로 완료 조건은 매 프레임 다시 평가해 취소선을 표시하고, 주목표(OC 전초기지 파괴)가 완료되면 `StageManager.ReportVictory()`를 호출한다. 서브목표(광물 확보/레이더 기지 점령/적 건물 전멸)는 체크리스트 표시만 하고 승리 조건에는 포함하지 않는다(Docs/Campaign.md 미션 1). 대부분의 조건은 매 프레임 폴링하지만, "적 건물 모두 파괴"만 예외적으로 이벤트 기반이다 — `EnemyBuildingController.ActiveBuildings`가 등록/파괴될 때만 이벤트(`OnActiveBuildingsChanged`)를 쏘므로 그 이벤트가 올 때만 다시 계산한다(요청사항).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `ocMainBase` | OC 전초기지(메인기지) — 파괴되면 null이 되어 완료 판정 |
| `destroyMainBaseText` | 주목표 텍스트 |
| `radarBaseZone` | 점령해야 할 레이더 기지 (`TerritoryZone`) |
| `secureOreText` / `captureRadarBaseText` / `destroyAllEnemyBuildingsText` | 서브목표 3개 텍스트 |
| `RequiredOre` | 서브목표 광물 목표량(2000) |
| `ocMainBaseAssigned` | `ocMainBase`가 애초에 연결돼 있었는지 (파괴 판정 기준) |
| `allEnemyBuildingsDestroyed` | 이벤트로만 갱신되는 적 건물 전멸 여부 캐시 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 텍스트 UI 자동 연결, `RTSUnitController` 캐싱, `ocMainBase` 할당 여부 기록 |
| `OnEnable()` / `OnDisable()` | `EnemyBuildingController.OnActiveBuildingsChanged` 이벤트 구독/해제 |
| `RefreshAllEnemyBuildingsDestroyed()` (private) | 이벤트 콜백 — `ActiveBuildings.Count == 0`인지 재계산 |
| `Update()` | 메인기지 파괴/광물량/레이더 기지 점령 여부를 매 프레임 재평가(적 건물 전멸 여부는 이벤트 캐시값 사용), 텍스트 갱신 후 메인기지 파괴 시 승리 보고 |

## 연관 컴포넌트

- **StageManager**: `WireObjectiveTexts()`/`ReportVictory()` 호출 대상
- **ObjectiveTextUtil**: 목표 텍스트 취소선 표시 공통 헬퍼
- **EnemyBuildingController**: `ActiveBuildings`/`OnActiveBuildingsChanged`로 적 건물 전멸 여부를 이벤트 기반으로 추적
- **TerritoryZone**: 레이더 기지 점령 여부 판정
