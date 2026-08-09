# Stage5Objectives

`Assets/Scripts/System/Stage5Objectives.cs`

## 개요

5스테이지("최후의 원정") 임무 목표 체크리스트. 주목표 2개(에너지 코어 3개 파괴 + 외계 지휘 코어 제거)가 모두 완료되면 승리 처리한다. 두 목표 모두 대상 오브젝트의 파괴(참조 null) 여부를 매 프레임 폴링해서 확인하는 방식이다. OC는 다른 전선에서 별도로 공격 중이라 이 전장에는 등장하지 않으므로(NTA 단독 작전) 서브목표가 없다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `energyCores` / `destroyEnergyCoresText` | 파괴해야 할 에너지 코어 목록, 표시 텍스트 |
| `alienCommandCore` / `destroyCommandCoreText` | 외계 지휘 코어, 표시 텍스트 |
| `trackedEnergyCores` | `Start()`에서 null이 아닌 코어만 필터링해 캐싱한 추적용 리스트 |
| `alienCommandCoreAssigned` | 할당 여부 캐시 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 텍스트 UI 자동 연결, `trackedEnergyCores` 초기화, 지휘 코어 할당 여부 기록 |
| `Update()` | `trackedEnergyCores` 중 파괴된(null) 개수를 세어 "현재/목표" 형식으로 표시, 전부 파괴됐는지와 지휘 코어 파괴 여부를 재평가, 둘 다 완료되면 승리 보고 |

## 연관 컴포넌트

- **StageManager**: `WireObjectiveTexts()`/`ReportVictory()` 호출 대상
- **ObjectiveTextUtil**: 개수 비교형(`current/target`) 목표 텍스트 표시에 사용
