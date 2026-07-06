# 0009. 버그 수정: MissingReferenceException (죽은 유닛이 선택 리스트에 남음)

## 날짜
2026-07-07

## 요청
지금 버그 발견했는데 아군유닛을 공격하고 나서 드래그로 여러유닛을 선택하면 여러유닛그대로 선택되는데 아군유닛이 죽으면 선택된 유닛 UI와 리스트가 갱신되지 않아서 MissingReferenceException: The object of type 'UnityEngine.AI.NavMeshAgent' has been destroyed but you are still trying to access it. ... 해당 오류가 발생하네 명령을 내리면 죽은유닛에 대한 갱신이 안되서 그런거 같아

## 원인
`UnitController`에 이미 올바른 `Die()`(`UnitList`/`selectedUnitList`에서 자신을 제거 후 `Destroy`)가 있었지만, 클래스가 `IDestructible`을 구현하지 않아서 `HealthManager.Die()`의 `TryGetComponent<IDestructible>`가 실패 → 매번 `Destroy(gameObject)`만 호출되는 fallback 경로를 타서 `Die()`가 아예 호출되지 않았음. 그 결과 죽은 유닛이 `selectedUnitList`에 계속 남아있다가, 이후 이동/공격 명령이 그 죽은 유닛의 파괴된 `NavMeshAgent`를 건드리며 `MissingReferenceException` 발생.

## 답변 / 변경사항
`UnitController : MonoBehaviour` → `UnitController : MonoBehaviour, IDestructible` 한 줄 변경. 이제 사망 시 `HealthManager`가 정상적으로 `UnitController.Die()`를 호출해 `UnitList`/`selectedUnitList`에서 즉시 제거됨.

## 변경 파일
- `Assets/Scripts/Unit/UnitController.cs`
