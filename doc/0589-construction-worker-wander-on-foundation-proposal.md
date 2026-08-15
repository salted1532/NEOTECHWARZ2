# 0589. 건설 중인 일꾼이 기반구조물 표면을 배회하는 연출 - 제안

**날짜:** 2026-08-16

## 요청 내용

> 일꾼이 건설중에 계속 해서 건설중인 기반구조물 근처를 이동했으면 좋곘는데 기반 구조물 표면중
> 랜덤한 위치로 이동했다가 도착하고 2초 기다렸다가 다음 랜덤 위치로 이동하는식으로 했으면 좋겠어

## 조사 내용 - 기술적 걸림돌 발견

`BaseStructure` 프리팹(`Assets/prefabs/NTA/Building/BaseStructure.prefab`)에는 파운데이션 크기만큼
스케일되는 `NavMeshObstacle`이 **건설 중 내내 활성화**되어 있다(`m_Enabled: 1, m_Carve: 1,
m_CarveOnlyStationary: 1`) - 다른 유닛들이 건설 중인 건물 자리를 통과해서 걷지 못하게 막기 위한
용도로 보인다. 즉 파운데이션이 서있는 동안 그 발밑 영역 전체가 NavMesh에서 "구멍"으로 카빙되어
있다.

이 말은 곧, `NavMeshAgent`로 그 표면 위 지점을 목적지로 잡아 평범하게 길찾기(`SetDestination`)를
시키면 **거의 항상 실패한다** - 카빙된 영역엔애초에 유효한 NavMesh가 없으니 도달할 경로 자체가
없다. 지금 그 자리에 일꾼이 서 있을 수 있는 이유는, 도착 시점(`BuildTick`)에 NavMesh 위 유효한
지점(`buildDestination`, 파운데이션 가장자리 바로 바깥)까지만 이동했었기 때문이다 - 지금 요청대로
"표면 위 아무 지점"을 오가려면 이 카빙된 영역 안으로 들어가야 하는데, 일반적인 NavMeshAgent 경로
탐색으로는 못 한다.

## 해결 방향

이미 이 프로젝트에 있는 패턴을 그대로 재사용한다 - 공중 유닛(`isAirUnit`)은 애초에 NavMeshAgent
경로탐색을 안 쓰고 `Vector3.MoveTowards`로 매 프레임 직접 좌표를 옮긴다(`UnitController.cs:407`).
건설 중인 일꾼도 같은 방식을 쓰면 NavMesh/카빙 문제를 완전히 우회할 수 있다:

1. 건설을 시작하는 순간(`BeginConstruction`) `navMeshAgent.enabled = false`로 꺼서, 에이전트가
   더 이상 위치를 관리하지 않게 한다(꺼져있는 동안은 카빙된 영역이든 어디든 `transform.position`을
   직접 옮겨도 충돌하지 않는다).
2. 매 프레임 파운데이션의 콜라이더(`BoxCollider`, 완공 건물과 동일하게 건물 크기만큼 스케일됨) 범위
   안에서 무작위 X/Z 지점을 하나 고르고, `moveSpeed`로 그 지점까지 이동 → 도착하면 2초 대기 → 다음
   무작위 지점, 반복.
3. 건설이 끝나면(완공 or 다른 일꾼으로 교체, `FinishConstruction`) 카빙된 영역 밖의 검증된 지점
   (`buildDestination` - 원래 도착 지점, 여전히 유효한 NavMesh 위)으로 위치를 되돌린 뒤
   `navMeshAgent.enabled = true`로 다시 켠다. 꺼져있던 위치(파운데이션 표면, NavMesh 밖) 상태로
   그냥 다시 켜면 에이전트가 자기 위치를 NavMesh 위로 못 찾아 재배치에 실패할 수 있어서 필요한 절차.
4. `IsCurrentlyMoving()`(이동 이펙트가 폴링하는 함수, `UnitController.cs:2052`)도 건설 중엔
   `navMeshAgent`가 꺼져있어 그 프로퍼티들을 그대로 읽으면 위험하므로, `isAirUnit`과 동일한 패턴으로
   전용 분기를 추가해 배회 이동 중일 때도 이동 이펙트가 정상 재생되게 한다.

## 변경 계획

### `Assets/Scripts/Unit/UnitController.cs`

**필드 추가:**
```csharp
private bool constructionWanderWaiting;
private float constructionWanderWaitRemaining;
private Vector3 constructionWanderTarget;
private bool hasConstructionWanderTarget;
private const float ConstructionWanderWaitSeconds = 2f;
private const float ConstructionWanderArriveDistance = 0.3f;
```

**`BeginConstruction`** - 에이전트 끄고 배회 상태 초기화:
```diff
     public void BeginConstruction(BaseStructure structure)
     {
         if (structure == null)
             return;

         attachedStructure = structure;
         isConstructing = true;

         structure.AttachBuilder(this);
+
+        // 파운데이션은 NavMeshObstacle로 카빙되어 있어 정상 경로탐색이 불가능하다 - 공중 유닛과 동일한
+        // 방식(MoveTowards 직접 이동)으로 전환한다 (doc/0589).
+        if (!isAirUnit)
+            navMeshAgent.enabled = false;
+
+        hasConstructionWanderTarget = false;
+        constructionWanderWaiting = false;
     }
```

**`FinishConstruction`** - 검증된 지점으로 복귀 후 에이전트 재활성화:
```diff
     public void FinishConstruction()
     {
         isConstructing = false;
         attachedStructure = null;
+
+        hasConstructionWanderTarget = false;
+        constructionWanderWaiting = false;
+
+        if (!isAirUnit && !navMeshAgent.enabled)
+        {
+            transform.position = buildDestination; // 배회 중 NavMesh 밖(파운데이션 표면)에 있었을 수 있으므로 복귀 후 재활성화
+            navMeshAgent.enabled = true;
+        }
     }
```

**새 Tick 메서드 추가, `Update()`의 Tick 목록에 등록:**
```csharp
    // 건설 중인 일꾼이 파운데이션 표면 위 무작위 지점을 배회하게 한다 - 도착하면 2초 대기 후 다음
    // 지점으로. 파운데이션이 NavMeshObstacle로 카빙되어 있어 NavMeshAgent 경로탐색을 쓸 수 없으므로,
    // 공중 유닛과 동일하게 에이전트를 끄고 직접 좌표를 옮긴다 (doc/0589).
    private void ConstructionWanderTick()
    {
        if (!isConstructing || attachedStructure == null)
            return;

        if (constructionWanderWaiting)
        {
            constructionWanderWaitRemaining -= Time.deltaTime;
            if (constructionWanderWaitRemaining > 0f)
                return;

            constructionWanderWaiting = false;
            hasConstructionWanderTarget = false;
        }

        if (!hasConstructionWanderTarget)
        {
            constructionWanderTarget = PickRandomConstructionSurfacePoint();
            hasConstructionWanderTarget = true;
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, constructionWanderTarget, moveSpeed * Time.deltaTime);

        if ((transform.position - constructionWanderTarget).sqrMagnitude <= ConstructionWanderArriveDistance * ConstructionWanderArriveDistance)
        {
            constructionWanderWaiting = true;
            constructionWanderWaitRemaining = ConstructionWanderWaitSeconds;
        }
    }

    // attachedStructure의 콜라이더(파운데이션 표면) 범위 안에서 무작위 지점을 하나 고른다 - 높이(Y)는
    // 일꾼의 현재 지면 높이를 그대로 유지한다.
    private Vector3 PickRandomConstructionSurfacePoint()
    {
        if (attachedStructure != null && attachedStructure.TryGetComponent<Collider>(out var col))
        {
            Bounds b = col.bounds;
            return new Vector3(Random.Range(b.min.x, b.max.x), transform.position.y, Random.Range(b.min.z, b.max.z));
        }

        return transform.position;
    }
```

`Update()`의 Tick 목록에 추가:
```diff
         GatherTick();
         PatrolTick();
         AttackOrderTick();
         FriendlyAttackTick();
         SkillOrderTick();
         FollowTick();
         FollowBuildingTick();
         BuildTick();
+        ConstructionWanderTick();
```

**`IsCurrentlyMoving()`** - 배회 중엔 꺼진 `navMeshAgent` 프로퍼티 대신 배회 상태로 판정:
```diff
     public bool IsCurrentlyMoving()
     {
         if (isAirUnit)
             return isMovingAirUnit;

+        if (isConstructing)
+            return hasConstructionWanderTarget && !constructionWanderWaiting;
+
         return navMeshAgent != null && !navMeshAgent.isStopped && navMeshAgent.velocity.sqrMagnitude > 0.01f;
     }
```

## 범위 밖 (참고)
- `BaseStructure`가 `Update()`에서 매 프레임 건설 진행률을 계산할 때 `builder == null`이면 일시정지되는
  로직(doc 기존 동작)은 이번 변경과 무관 - `builder` 참조 자체는 그대로 유지된다.
- 건설이 취소/완료 등 어떤 경로로 끝나든 전부 `FinishConstruction()`을 거치므로 위치 복귀 로직은
  한 곳에서만 처리하면 충분하다.

## 변경 예정 파일
- `Assets/Scripts/Unit/UnitController.cs`

---

## 적용 (사용자 승인 후)

(승인 대기 중)
