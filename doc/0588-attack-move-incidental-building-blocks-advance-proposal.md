# 0588. 공격-이동 중 목적지가 아닌 다른 건물을 만나면 거기서 멈춰버리는 문제 - 원인 및 수정 제안

- 날짜: 2026-08-16

## 요청 내용

- "아군 OC 공격가는거 멈추는 현상에 대해서 확인해봤는데 목적지가 건물로 되어있는데 그사이에
  유닛싸움은 잘 치루고 이동하는데 공격가는 건물이 아닌 다른 건물을 만나면 그때 멈춰버리는거 같아
  내가 그 건물을 부수니깐 다시 가려던 건물로 이동하네 이걸 고쳐야겠어 공격 가는 건물이 중요한게
  아니라 그냥 그 위치가 중요한거지 그 사이에 있는 건물들 유닛들은 다 싸우면서 공격 갔으면 좋겠어"

정리하면: 목적지(공격 대상 건물)로 가는 도중 마주치는 다른 건물에게 발이 묶여 더는 전진하지 않는다.
그 건물을 파괴해야만(플레이어가 직접 부숨) 다시 원래 목적지로 이동을 재개한다. 원하는 동작: 목적지
"위치"로 계속 전진하는 게 우선이고, 도중에 마주치는 건물/유닛은 (이동을 멈추지 않고) 스치면서
싸우기만 하면 됨 - 유닛과의 교전은 이미 이렇게(멈추지 않고) 잘 되고 있음.

## 조사 내용

`AllyController.Attack()`은 대상이 유닛이든 건물이든 구분 없이 항상 `navMeshAgent.isStopped = true`를
건다:

```csharp
public void Attack(Vector3 end, GameObject target)
{
    if (!isAirUnit)
    {
        navMeshAgent.isStopped = true;
    }
    ...
}
```

`AttackMoveTick()`은 `HasTargetInRange`가 `true`인 동안은(교전 중이라 판단) 아무것도 안 하고 그대로
둔다:

```csharp
private void AttackMoveTick()
{
    if (attackMoveDestination == null) return;
    if (attackRange != null && attackRange.HasTargetInRange) return; // 교전 중이면 그대로 둔다
    ...
}
```

건물은 도망가지도 않고 체력도 많아 감지 범위 안에 계속 머무르므로, `HasTargetInRange`가 계속 `true`로
유지되고 `isStopped`도 계속 `true`로 유지된다 - 그 건물이 죽지 않는 한 `AttackMoveTick`의 "교전 끝났으니
복귀" 조건이 영원히 만족되지 않는다. 유닛은 대개 금방 죽거나(교전 상대가 사라짐) 도망가서
`HasTargetInRange`가 곧 `false`가 되니 눈에 덜 띄었을 뿐, 건물처럼 오래 버티는 대상을 만나면 그대로
발이 묶인다. 사용자가 그 건물을 직접 부수자 다시 움직였다는 관찰과 정확히 일치.

`navMeshAgent.destination`은 애초에 `attackMoveDestination`(진짜 목적지 건물의 좌표)으로 고정돼 있고,
도중에 마주친 건물은 `ChaseTarget()`으로 새로 목적지를 바꾸지 않는다(이미 사거리 안이라 `Attack()`만
호출됨, doc/0552 계열 로직과 무관) - 즉 `isStopped`만 안 걸리면 NavMeshAgent는 이미 설정된 목적지를
향해 알아서 계속 걸어간다. 유닛(이동형) 대상과 건물(고정형) 대상을 구분해서, "목적지 자체가 아닌
건물"은 멈추지 않고 스쳐 지나가며 싸우게 하면 된다.

## 수정 제안

`Attack()`에서, 지금 이 대상이 "공격-이동의 실제 목적지 건물"인지 아닌지 판정한다 - 대상 위치가
`attackMoveDestination`과 사실상 같으면(같은 건물, 도착해서 부숴야 할 진짜 목표) 기존처럼 완전히
멈춰서 싸우고, 다르면(그냥 스쳐 지나가는 다른 건물) 멈추지 않고 계속 이동하면서 공격한다. 유닛
대상은 지금처럼 항상 완전히 멈춰서 싸운다(위협이라 자리 잡고 처리하는 게 맞음, 사용자도 "유닛싸움은
잘 치룬다"고 확인함).

### AllyController.cs / EnemyUnitController.cs 공통 - `Attack()`

기존 코드 (AllyController.cs):
```csharp
    public void Attack(Vector3 end, GameObject target)
    {
        if (!isAirUnit)
        {
            navMeshAgent.isStopped = true;
        }
        else
        {
```

변경 코드:
```csharp
    // 공격-이동 목적지 건물 근처(같은 위치)가 아니면, 스쳐 지나가며 마주친 건물로 취급한다 - 이동을
    // 멈추지 않고 계속 목적지로 걸어가면서 사거리 안에 있는 동안은 계속 공격한다(doc/0588). 유닛은
    // 항상 멈춰서 싸운다(금방 끝나고, 위협이라 자리 잡고 처리하는 게 맞음) - 건물만 대상.
    private bool IsIncidentalBuilding(GameObject target)
    {
        if (attackMoveDestination == null || target.GetComponent<EnemyUnitController>() != null)
            return false;

        float sqrDist = (target.transform.position - attackMoveDestination.Value).sqrMagnitude;
        return sqrDist > arriveDistance * arriveDistance;
    }

    public void Attack(Vector3 end, GameObject target)
    {
        if (!isAirUnit)
        {
            // 스쳐 지나가는 건물이면 멈추지 않는다 - navMeshAgent.destination은 이미 attackMoveDestination
            // (진짜 목적지)을 가리키고 있으므로, isStopped만 false로 두면 알아서 계속 그 쪽으로 걸어간다.
            navMeshAgent.isStopped = !IsIncidentalBuilding(target);
        }
        else
        {
```

## 예상 영향

- 지상 아군 OC/적 AI가 공격-이동 중 목적지가 아닌 건물을 스칠 때 더는 발이 묶이지 않고, 사거리 안에
  머무는 동안 공격은 계속하되 목적지로 계속 전진한다. 목적지 건물 자체에 도착했을 때는 기존처럼
  완전히 멈춰서 파괴할 때까지 공격한다(위치 비교로 자동 구분).
- 공중 유닛(Raven/Ironhawk 등)은 애초에 `Attack()`에서 수평 이동을 멈추지 않는 구조라(고도만 보정)
  이 문제 자체가 없음 - 변경 불필요.
- 이동하면서 몸을 대상 쪽으로 돌리는(`RotateYOnly`) 연출과 NavMeshAgent의 이동 방향 회전이 동시에
  걸릴 수 있어 살짝 부자연스러워 보일 수 있음(사소한 시각적 트레이드오프) - 필요해지면 나중에 조정.
- doc/0581/0584와 동일한 이유로 `EnemyUnitController.cs`(적 AI 웨이브/별동대)에도 동일하게 적용 -
  적 AI가 플레이어 기지로 진군할 때도 같은 문제(다른 건물에 발이 묶임)를 겪을 수 있음.
- `UnitController.cs`(플레이어 직접 조종)는 별도 구조라 범위 밖(기존 판단과 동일).

## 변경 예정 파일

- Assets/Scripts/FogOfWar/Ally/AllyController.cs
- Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs

## 확인 필요
이대로 구현해도 될까?
