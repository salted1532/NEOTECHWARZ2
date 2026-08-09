# Stage4Objectives

`Assets/Scripts/System/Stage4Objectives.cs`

## 개요

4스테이지("공동 전선") 임무 목표 체크리스트. 주목표(외계 사령기지 파괴)는 오브젝트 파괴(참조 null) 여부를 매 프레임 폴링해서 완료되면 승리 처리한다. 서브목표(OC 사령부 생존)는 살아있는 동안 계속 완료 상태로 표시되다가, 파괴되는 순간부터는 다시 살아나지 않으므로 그 이후로는 영구히 미완료(실패)로 고정한다(요청사항) — `ObjectiveTextUtil.SetSurvivalObjectiveText`의 생존형 표시를 사용하는 대표 사례.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `alienCommandBase` / `destroyCommandBaseText` | 주목표 — 외계 사령기지, 표시 텍스트 |
| `ocCommandCenter` / `survivalOcCommandText` | 서브목표 — OC 사령부, 표시 텍스트 |
| `alienCommandBaseAssigned` / `ocCommandCenterAssigned` | 각 오브젝트가 애초에 연결돼 있었는지 |
| `ocCommandCenterDestroyedPermanently` | 한 번 true가 되면 계속 유지되는 영구 실패 플래그 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 텍스트 UI 자동 연결, 두 오브젝트의 할당 여부 기록 |
| `Update()` | 사령기지 파괴 여부 재평가, OC 사령부가 파괴되면(아직 영구 플래그가 안 켜졌으면) 플래그를 켬(한 번 켜지면 되돌리지 않음), 텍스트 갱신 후 사령기지 파괴 시 승리 보고 |

## 연관 컴포넌트

- **StageManager**: `WireObjectiveTexts()`/`ReportVictory()` 호출 대상
- **ObjectiveTextUtil**: `SetSurvivalObjectiveText`로 생존형 목표(실패 후 되돌리지 않음) 표시
