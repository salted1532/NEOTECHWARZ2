# AttackRange 타겟 선정 로직 개선안

작성일: 2026-07-03
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 현재 문제

`Assets/Scripts/Unit/AttackRange.cs`의 `OnTriggerStay(Collider other)`는 사거리(트리거
볼륨) 안에 "Enemy" 태그 콜라이더가 여러 개 겹쳐 있을 때, **콜라이더 개수만큼 매 프레임
각각 별도로 호출**된다.

```csharp
void OnTriggerStay(Collider other)
{
    if (!other.CompareTag("Enemy"))
        return;
    ...
    unitController.Attack(enemyPos, AttackDamage, enemyObject);
}
```

- 어떤 적의 콜백이 먼저 오는지는 코드가 정하는 게 아니라 물리 엔진 내부 순서에 달려있어서,
  "가장 가까운 적" 같은 의도된 타겟팅 규칙이 없다.
- `OnTriggerStay`만 쓰고 `OnTriggerExit`가 없어서, 적이 사거리를 벗어나도 별도로 목록에서
  빠지는 처리가 없다 (현재는 애초에 목록 자체가 없어서 문제가 드러나지 않지만, 목록을
  도입하면 반드시 같이 처리해야 함).
- `UnitController.Attack()`(`UnitController.cs:210`)은 `RotateYOnly(end)`를
  `alreadyAttacked` 체크보다 먼저 실행하기 때문에, 겹친 적이 여러 명이면 실제 공격은
  한 놈에게만 적용돼도 회전은 매 호출마다 갱신되어 시선이 여러 타겟 사이로 튈 수 있다.

## 2. 역할 분리 원칙

- **`AttackRange` (인식/perception)**: 사거리 안에 누가 있는지 감지하고, 그중 공격할
  타겟 하나를 고른다.
- **`UnitController` (행동/action)**: 정해진 타겟 하나를 받아서 이동/회전/공격/쿨다운만
  처리한다. 시그니처(`Attack(Vector3 end, int damage, GameObject enemy)`)는 그대로
  유지 — 여러 후보 중 고르는 로직은 여기 들어오면 안 됨.

## 3. 개선안: AttackRange가 사거리 내 적 목록을 유지하고 최근접 타겟만 전달

```csharp
using System.Collections.Generic;
using UnityEngine;

public class AttackRange : MonoBehaviour
{
    public int UnitRange;
    public int AttackDamage;

    private UnitController unitController;
    private readonly List<GameObject> enemiesInRange = new List<GameObject>();

    private void Awake()
    {
        unitController = transform.parent.GetComponent<UnitController>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        if (!enemiesInRange.Contains(other.gameObject))
            enemiesInRange.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        enemiesInRange.Remove(other.gameObject);
    }

    private void Update()
    {
        enemiesInRange.RemoveAll(enemy => enemy == null); // 이미 죽어서 destroy된 대상 정리

        GameObject target = GetClosestEnemy();
        if (target == null)
            return;

        float distance = Vector3.Distance(transform.position, target.transform.position);

        if (unitController.IsAttack() || unitController.IsIdle())
        {
            if (distance <= UnitRange)
            {
                unitController.Attack(target.transform.position, AttackDamage, target);
            }
            else if (unitController.IsIdle())
            {
                unitController.ChaseTarget(target.transform.position);
            }
        }
    }

    private GameObject GetClosestEnemy()
    {
        GameObject closest = null;
        float closestSqrDist = float.MaxValue;

        foreach (GameObject enemy in enemiesInRange)
        {
            if (enemy == null)
                continue;

            float sqrDist = (enemy.transform.position - transform.position).sqrMagnitude;
            if (sqrDist < closestSqrDist)
            {
                closestSqrDist = sqrDist;
                closest = enemy;
            }
        }

        return closest;
    }
}
```

### 변경 포인트 요약

| 항목 | 기존 | 개선안 |
|---|---|---|
| 감지 방식 | `OnTriggerStay`만 사용, 콜라이더 수만큼 매 프레임 중복 호출 | `OnTriggerEnter`/`OnTriggerExit`로 `enemiesInRange` 목록 관리 |
| 타겟 선정 | 없음 (물리 콜백 순서에 우연히 결정) | `GetClosestEnemy()`로 최근접 적 1명 고정 선정 |
| `UnitController.Attack()` 호출 횟수 | 겹친 적 수만큼(프레임당 N회) | 프레임당 최대 1회 |
| 죽은 대상 처리 | 없음 | `Update()`에서 `null` 정리 |
| `UnitController` 쪽 변경 | - | 없음 (시그니처 유지) |

### 트레이드오프 / 검토 필요 사항

- **"항상 최근접" vs "한 번 물면 죽을 때까지 고정(sticky target)"**: 위 코드는 매 프레임
  가장 가까운 적으로 다시 계산하므로, 여러 적이 계속 위치를 바꾸면 공격 대상이 자주
  바뀔 수 있음(스타크래프트 등에서 흔한 "낚이는" 현상). 고정 타겟팅을 원하면
  `currentTarget`을 필드로 들고 있다가, 죽거나(`null`) 사거리를 벗어났을 때만
  `GetClosestEnemy()`로 재선정하는 방식으로 바꿔야 함.
- `OnTriggerStay` → `Update` 기반으로 옮기면 트리거 이벤트와 무관하게 매 프레임
  거리 계산이 도니 약간의 연산 비용 증가(유닛 수가 매우 많을 때만 유의미).
- `enemiesInRange`에 파괴된 오브젝트가 남아있다가 `Update`에서 정리되므로, 정리 전
  한 프레임 정도는 죽은 대상이 목록에 남아있을 수 있음 (`GetClosestEnemy`에서
  `enemy == null` 체크로 방어는 되어 있음).
