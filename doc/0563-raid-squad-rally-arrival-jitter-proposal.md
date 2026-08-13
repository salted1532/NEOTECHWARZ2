# 0563 - 별동대 집결지 도착 후 밀림으로 인한 지속 이동(비비적거림) 문제 (제안)

## 요청 내용

> 별동대 유닛들 집결지에 도착시 다른 유닛이 밀치면 계속 비비적거리며 계속 움직이는거 막기

## 조사 결과 - doc/0559(순찰 크라우딩)와 동일한 원인, 다른 증상

### 1. 별동대는 전원이 정확히 같은 집결지 좌표로 이동 명령을 받는다

`Assets/Scripts/System/EnemyAIDirector.cs:526-532` (`AssembleAtRally`):
```csharp
private IEnumerator AssembleAtRally(List<EnemyUnitController> squad)
{
    Vector3 rally = DefaultRallyPosition();

    foreach (EnemyUnitController unit in squad)
        if (unit != null)
            unit.MoveTo(rally);   // 전원 동일한 rally 좌표
    ...
```
`Assets/Scripts/System/AllyAIDirector.cs:247-253`도 동일 패턴.

doc/0559에서 `PatrolSelectedUnits`가 선택된 유닛 전부에게 동일한 순찰 좌표를 주던 것과 정확히 같은
구조다. NavMeshAgent 충돌 회피 때문에 그 좌표를 실제로 점유할 수 있는 유닛은 사실상 1마리뿐이고,
나머지 별동대원은 그 주변에 밀려난다.

### 2. 도착 판정이 너무 엄격해서, 밀려난 유닛은 "도착"으로 인정받지 못한다

`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:196` / `Assets/Scripts/FogOfWar/Ally/AllyController.cs:198`
(둘 다 동일):
```csharp
if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
{
    arrived = true;
    ...
    navMeshAgent.isStopped = true;   // 도착 판정을 통과해야만 여기서 정지가 걸림 (doc/0399)
    ...
}
```
`arriveDistance`는 두 파일 모두 기본값 `0.5f`(line 69 / line 71).

밀려난 유닛은 `remainingDistance`가 회피 우회로 때문에 `0.5`보다 큰 채로 좀처럼 안 떨어지므로
`arrived`가 영원히 `true`가 안 된다. 그 결과:
- `navMeshAgent.isStopped`가 `true`로 걸리는 지점은 오직 이 블록 안이므로, **밀려난 유닛은 절대
  정지하지 않는다.**
- `isStopped == false`인 채로 `destination`은 여전히 붐비는 집결지 좌표를 가리키고 있어서, 다른
  유닛이 스치기만 해도 NavMeshAgent가 다시 그 좌표로 파고들려 하고, 그러다 또 밀려나고... 를
  반복한다 → "계속 비비적거리며 계속 움직이는" 증상과 일치.
- 이 상태가 `EnemyAIDirector.AssembleAtRally`/`AllyAIDirector`의 집결 대기 코루틴이 도는 동안
  (`rallyTimeout`까지, 보통 대기 시간이 김) 계속 지속된다.

doc/0559(순찰)의 증상은 "영원히 멈춰서 다음 구간을 못 감"이었고, 이번(집결)은 "영원히 완전히
멈추질 못하고 계속 흔들림"으로 겉모습은 다르지만, 근본 원인(여러 유닛이 같은 한 점으로 몰릴 때
도착 판정이 너무 엄격함)은 완전히 동일하다.

## 제안하는 수정

doc/0559와 같은 방향(전용 허용 오차를 넉넉하게 - `gatherInteractRange`/`patrolInteractRange`와 동일한
`2f` 관행값), 다만 이번엔 `remainingDistance`(경로 기반 - 회피로 우회하면 값이 튈 수 있어 doc/0559에서
이미 문제로 지목됨) 대신 실제 목적지까지의 직선 거리로 비교한다.

`arriveDistance`는 각 파일에서 이 도착 판정 한 곳에만 쓰이므로(다른 용도로 재사용되지 않음),
필드를 새로 추가하지 않고 그대로 값과 비교 방식만 바꾼다.

### `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

```diff
-    [SerializeField] private float arriveDistance = 0.5f;
+    [SerializeField] private float arriveDistance = 2f; // 별동대 등 여러 유닛이 같은 집결지로 몰릴 때
+                                                          // 밀려난 유닛도 도착 판정을 통과하도록 넉넉하게 (doc/0563)
```
```diff
-            if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
+            // remainingDistance(경로 기반) 대신 실제 목적지까지의 직선 거리로 비교 - 여러 유닛이 몰려
+            // 회피로 우회하면 remainingDistance가 실제 거리보다 크게 튈 수 있다 (doc/0559와 동일 이유).
+            if (!arrived && !navMeshAgent.pathPending &&
+                (transform.position - navMeshAgent.destination).sqrMagnitude <= arriveDistance * arriveDistance)
```

### `Assets/Scripts/FogOfWar/Ally/AllyController.cs`

동일한 두 군데(line 71, line 198)를 그대로 동일하게 수정.

## 영향 범위

- 이 판정은 각 컨트롤러의 일반 이동/공격-이동 도착 판정이라 별동대 집결뿐 아니라 EnemyUnitController/
  AllyController가 받는 모든 이동 명령에 적용된다. 다만 AI가 지시하는 이동은 픽셀 단위 정밀도가
  필요 없고(정지 위치가 목적지에서 2m 이내면 시각적으로 차이 없음), 오히려 여러 유닛이 한 지점으로
  향할 때(집결 포함) 전원이 실제로 멈출 수 있게 되는 쪽이 이득이라 판단.
- 플레이어 유닛(`UnitController.cs`)의 `arriveDistance`(line 161)는 건드리지 않음 - 요청 범위(별동대)
  밖이고, 플레이어의 개별 이동 명령은 doc/0559에서 이미 "한 번만 대충 도착하면 끝이라 문제가 안
  드러난다"고 확인됨.
- Unity 특성상 프리팹 인스펙터에 `arriveDistance`가 이미 `0.5`로 직렬화돼 있으면 스크립트 기본값
  변경이 자동 반영되지 않을 수 있음 - 필요 시 프리팹에서 수동으로 갱신 확인 필요.

## 확인 요청

이 방향으로 구현해도 될지 확인 부탁드립니다.

## 구현 결과 (사용자 승인 후)

제안대로 두 파일 그대로 적용, 컴파일 성공(0 errors, 기존 경고 40개만 그대로).

**`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`**:
```diff
-    [SerializeField] private float arriveDistance = 0.5f;
+    [SerializeField] private float arriveDistance = 2f; // 별동대 등 여러 유닛이 같은 집결지로 몰릴 때
+                                                          // 밀려난 유닛도 도착 판정을 통과하도록 넉넉하게 (doc/0563)
```
```diff
-            if (!arrived && !navMeshAgent.pathPending && navMeshAgent.remainingDistance <= arriveDistance)
+            // remainingDistance(경로 기반) 대신 실제 목적지까지의 직선 거리로 비교 - 여러 유닛이 몰려
+            // 회피로 우회하면 remainingDistance가 실제 거리보다 크게 튈 수 있다 (doc/0559와 동일 이유).
+            if (!arrived && !navMeshAgent.pathPending &&
+                (transform.position - navMeshAgent.destination).sqrMagnitude <= arriveDistance * arriveDistance)
```

**`Assets/Scripts/FogOfWar/Ally/AllyController.cs`** - 동일한 두 군데를 동일하게 수정.

컴파일 확인: `npx uloop-cli compile --wait-for-domain-reload true` → `Success: true, ErrorCount: 0`.
