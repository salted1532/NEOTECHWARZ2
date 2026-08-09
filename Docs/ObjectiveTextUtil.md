# ObjectiveTextUtil

`Assets/Scripts/System/ObjectiveTextUtil.cs`

## 개요

스테이지 목표 체크리스트 텍스트 표시를 위한 공통 정적 헬퍼(Stage0~5Objectives가 공유). 완료 시 `<s>`(취소선)로 감싸고, 미완료면 그대로 표시한다. 매 프레임 다시 호출되는 것을 전제로 하므로 "한 번 완료되면 고정"하지 않는다 — 조건이 다시 깨지면 취소선도 자동으로 사라진다(생존형 오버로드는 예외, 아래 참고).

## 메소드

| 메소드 | 설명 |
|---|---|
| `SetObjectiveText(text, description, complete)` | 단순 완료/미완료형 — 완료면 취소선 적용 |
| `SetObjectiveText(text, description, current, target)` | 개수 비교형 — "설명 (현재/목표)" 형식으로 표시(예: 9/10). 현재값이 목표를 넘어도 표시는 목표치에서 고정(1050/1000이 아니라 1000/1000) |
| `SetSurvivalObjectiveText(text, description, failed)` | 생존형 — 살아있는 동안은 취소선 없이 그대로 표시하다가, 파괴되면 실패로 확정되므로 취소선을 긋고 `objective.fail.suffix` 문구를 덧붙인다(한 번 실패하면 되돌아가지 않음) |

## 연관 컴포넌트

- **Stage0Objectives ~ Stage5Objectives**: 매 프레임 목표 텍스트 갱신에 이 유틸을 사용
- **LocalizationManager**: `SetSurvivalObjectiveText`가 실패 접미사(`objective.fail.suffix`) 조회에 사용
