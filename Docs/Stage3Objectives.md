# Stage3Objectives

`Assets/Scripts/System/Stage3Objectives.cs`

## 개요

3스테이지("침공") 임무 목표 체크리스트. 주목표(외계 전초기지 제거)는 오브젝트 파괴(참조 null) 여부를 매 프레임 폴링해서 완료되면 승리 처리한다. 서브목표(생존자 구조)는 맵의 구조 비콘 트리거 콜라이더에 (구조 대상 자신을 제외한) 아무 아군 유닛이나 물리 트리거로 닿으면 완료 처리하고, 한 번 완료되면 되돌리지 않는다("구조했다"는 사실은 유닛이 다시 벗어나도 취소되지 않아야 하므로 — Stage0/1의 "재평가" 목표들과 다름). 완료되는 순간 미리 배치해둔 위장 OC(실제로는 `UnitController.isRescueUnit`이 붙은 조종 가능한 아군 유닛, doc/0458)의 억제도 코루틴으로 순차적으로 풀어준다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `alienOutpost` / `destroyOutpostText` | 주목표 — 외계 전초기지, 표시 텍스트 |
| `rescueBeacon` | 생존자 구조 지점의 트리거 콜라이더(doc/0459 후속) |
| `rescuedUnits` | 위장 OC(구조 대상) 유닛 목록 |
| `rescueSurvivorsText` | 서브목표 표시 텍스트 |
| `rescueStaggerInterval` | `rescuedUnits`를 같은 프레임에 한꺼번에 구조 처리하면 마커 깜빡임/SFX가 겹쳐 보이고 들리므로, 리스트 순서대로 이 간격만큼 텀을 두고 한 마리씩 처리(doc/0466) |
| `alienOutpostAssigned` / `survivorsRescued` | 할당 여부 캐시와 구조 완료 여부(한 번 true면 유지) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 텍스트 UI 자동 연결, `RTSUnitController` 캐싱, `alienOutpost` 할당 여부 기록 |
| `Update()` | 전초기지 파괴 여부 재평가, 아직 구조 안 됐으면 `IsAnyUnitTouchingBeacon()`으로 판정 후 완료 시 `RescueSequence()` 시작, 텍스트 갱신, 전초기지 파괴 시 승리 보고 |
| `RescueSequence()` (private) | `rescuedUnits`를 순회하며 `rescueStaggerInterval` 간격으로 한 마리씩 `Rescue()` 호출 |
| `IsAnyUnitTouchingBeacon()` (private) | `RTSUnitController.UnitList` 중 구조 대상 자신(`rescuedUnits`)을 제외한 유닛이 비콘에 물리적으로 닿아있는지 확인(`UnitController.IsTouching` — `MissionItem`/doc/0456과 동일한 패턴, doc/0459 후속으로 거리/반경 대신 실제 트리거 접촉으로 변경됨) |

## 연관 컴포넌트

- **StageManager**: `WireObjectiveTexts()`/`ReportVictory()` 호출 대상
- **UnitController**: 구조 대상 유닛의 `Rescue()`/`IsTouching()` 제공
- **ObjectiveTextUtil**: 목표 텍스트 취소선 표시 공통 헬퍼
