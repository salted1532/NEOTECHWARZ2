# 0579. 스테이지4 외계종족 공격 대상에 아군 OC 메인기지(오메가코어) 포함

**날짜:** 2026-08-14

## 요청 내용
> 스테이지4에서 아군OC의 메인기지 오메가코어도 플레이어의 메인기지 처럼 공격 받는 리스트 안에
> 포함되어있었으면 좋겠어

(제안 확인 질문에 대한 답으로 추가 요청) > 인스펙터 상의 공격 리스트안에 들어간 메인기지를
실시간으로 볼수 있도록도 해줘

## 조사 내용

### 지금 웨이브가 목표를 고르는 방식

`Assets/Scripts/System/EnemyAIDirector.cs`의 `PickAttackTarget()`(공격 웨이브가 행군할 목적지를
고르는 함수)은 `rtsController.BuildingList`(플레이어 소유 `BuildingController` 목록)만 본다:

```csharp
private BuildingController PickAttackTarget()
{
    if (rtsController == null)
        return null;

    List<BuildingController> mainBases = rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase"));
    if (mainBases.Count > 0)
        return mainBases[Random.Range(0, mainBases.Count)];

    List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
    return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
}
```

아군 OC 건물(오메가코어 포함)은 `AllyBuildingController`(`EnemyBuildingController` 상속, doc/0452)
타입이라 `rtsController.BuildingList`에 아예 들어가지 않는다 — 그래서 웨이브가 행군할 "주 목표"로는
절대 뽑히지 않는다. (참고: 사거리 안에 우연히 들어오면 `EnemyAttackRange`의 `targetTags`에 이미
`AllyOC`가 포함돼 있어 자동 교전 자체는 됨, doc/0452 — 이건 "지나가다 마주치면 싸움"이고, 이번
요청은 "행군 목적지로 아예 지정될 수 있어야 한다"는 것이라 다른 지점의 변경이 필요함.)

`RunWaveSquad()`에서 `target`은 `target.transform.position`(행군 목적지)으로만 쓰이고 플레이어
전용 멤버는 쓰지 않으므로, 타입을 `BuildingController` 대신 `Component`(둘의 공통 상위 타입)로
바꾸면 플레이어 건물과 아군 OC 건물을 같은 후보 풀에 넣을 수 있음.

### 대상 지정 방식

이미 `EnemyAIDirector`엔 `homeBuildings`(자기 기지 방어용), `defenseUnits`(배치형 방어 유닛),
`raidTargets`(점령지) 등 인스펙터에서 직접 지정하는 리스트 패턴이 여러 개 있음 — 같은 패턴을
재사용해서 "웨이브 공격 후보에 포함할 아군 OC 메인기지" 리스트를 새로 하나 추가하는 게 가장
간단하고 기존 코드와 일관됨. 어떤 OC 유닛/건물 ID가 오메가코어인지 자동으로 판별하는 로직은 만들지
않음 — 씬에 배치한 아군 OC 오메가코어 건물 인스턴스를 인스펙터에서 직접 끌어다 놓는 방식(스테이지4
전용 Spore Brood `EnemyAIDirector`에만 설정, 다른 스테이지는 빈 리스트라 기존 동작 100% 유지).

## 제안하는 변경

### `EnemyAIDirector.cs`

**Before (필드 선언부, `raidTargets` 근처):**
```csharp
    [Header("<공통> 점령지 탈환 타이밍")]
    [SerializeField] private List<CaptureSystem> raidTargets;
    [SerializeField] private float raidInterval = 45f;
```

**After (새 필드 추가):**
```csharp
    [Header("<공통> 점령지 탈환 타이밍")]
    [SerializeField] private List<CaptureSystem> raidTargets;
    [SerializeField] private float raidInterval = 45f;

    [Header("<공통> 웨이브 공격 후보에 포함할 아군 OC 메인기지 (doc/0579) - 지정 시 플레이어 MainBase와 함께 무작위로 목표가 됨")]
    [SerializeField] private List<EnemyBuildingController> allyMainBaseTargets;
```

**Before (`RunWaveSquad`/`PickAttackTarget`):**
```csharp
    private IEnumerator RunWaveSquad(List<EnemyUnitController> squad)
    {
        BuildingController target = null;

        while (true)
        {
            squad.RemoveAll(u => u == null);
            if (squad.Count == 0)
                yield break; // 전멸 - 이 웨이브 종료

            if (target == null)
            {
                target = PickAttackTarget();
                if (target == null)
                    yield break; // 플레이어 건물이 하나도 안 남음 - 더 공격할 곳이 없음

                foreach (EnemyUnitController unit in squad)
                    if (unit != null)
                        unit.AttackMoveTo(target.transform.position);
            }

            yield return null;
        }
    }

    // 플레이어 MainBase 중 무작위 하나, MainBase가 하나도 없으면 플레이어 건물 아무거나(doc/0534).
    private BuildingController PickAttackTarget()
    {
        if (rtsController == null)
            return null;

        List<BuildingController> mainBases = rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase"));
        if (mainBases.Count > 0)
            return mainBases[Random.Range(0, mainBases.Count)];

        List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
        return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
    }
```

**After:**
```csharp
    private IEnumerator RunWaveSquad(List<EnemyUnitController> squad)
    {
        Component target = null;

        while (true)
        {
            squad.RemoveAll(u => u == null);
            if (squad.Count == 0)
                yield break; // 전멸 - 이 웨이브 종료

            if (target == null)
            {
                target = PickAttackTarget();
                if (target == null)
                    yield break; // 공격할 곳이 하나도 안 남음

                foreach (EnemyUnitController unit in squad)
                    if (unit != null)
                        unit.AttackMoveTo(target.transform.position);
            }

            yield return null;
        }
    }

    // 플레이어 MainBase + allyMainBaseTargets(지정돼 있으면 아군 OC 메인기지, doc/0579) 중 무작위 하나,
    // 후보가 하나도 없으면 플레이어 건물 아무거나(doc/0534).
    private Component PickAttackTarget()
    {
        if (rtsController == null)
            return null;

        List<Component> candidates = new List<Component>();
        candidates.AddRange(rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase")));
        candidates.AddRange(allyMainBaseTargets.FindAll(b => b != null));

        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
        return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
    }
```

### 실시간 인스펙터 확인용 디버그 필드 추가

기존에도 `allEnemyUnits`/`allEnemyBuildings`처럼 씬 상태를 실시간으로 보여주는 `<디버그>` 헤더의
`[SerializeField]` 리스트 패턴이 있었음 — 같은 패턴으로 `attackTargetCandidates`
(`List<Component>`)를 추가하고, `Update()`에서 매 프레임 `GetMainBaseCandidates()`(후보 풀 계산을
`PickAttackTarget()`과 공유하는 새 헬퍼)로 다시 채움. Play 모드에서 인스펙터를 보면 지금 이 웨이브가
고를 수 있는 후보(플레이어 MainBase들 + 등록된 아군 OC 메인기지들)가 실시간으로 그대로 보임.

## 실제 변경 (코드 diff)

**`Assets/Scripts/System/EnemyAIDirector.cs`**

1) 필드 추가 (`raidTargets` 근처):
```csharp
    [Header("<공통> 웨이브 공격 후보에 포함할 아군 OC 메인기지 (doc/0579) - 지정 시 플레이어 MainBase와 함께 무작위로 목표가 됨")]
    [SerializeField] private List<EnemyBuildingController> allyMainBaseTargets;
```

2) 디버그 필드 추가 (`nextWaveCountdown`/`nextRaidCountdown` 근처):
```csharp
    [Header("<디버그> 현재 웨이브 공격 후보 (플레이어 MainBase + allyMainBaseTargets, 실시간 갱신)")]
    [SerializeField] private List<Component> attackTargetCandidates = new List<Component>();
```

3) `Update()` 맨 앞에서 매 프레임 갱신:
```csharp
    private void Update()
    {
        attackTargetCandidates.Clear();
        attackTargetCandidates.AddRange(GetMainBaseCandidates());

        foreach (SpawnQueue sq in spawnQueues)
        ...
```

4) `RunWaveSquad`의 `target` 타입을 `BuildingController` → `Component`로 일반화, `PickAttackTarget()`이
   `GetMainBaseCandidates()`를 쓰도록 변경, 후보 풀 계산을 새 헬퍼로 분리:
```csharp
    // 플레이어 MainBase + allyMainBaseTargets(지정돼 있으면 아군 OC 메인기지, doc/0579) 중 무작위 하나,
    // 후보가 하나도 없으면 플레이어 건물 아무거나(doc/0534).
    private Component PickAttackTarget()
    {
        if (rtsController == null)
            return null;

        List<Component> candidates = GetMainBaseCandidates();
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        List<BuildingController> anyBuildings = rtsController.BuildingList.FindAll(b => b != null);
        return anyBuildings.Count > 0 ? anyBuildings[Random.Range(0, anyBuildings.Count)] : null;
    }

    // PickAttackTarget()과 attackTargetCandidates(디버그 실시간 표시)가 공유하는 후보 풀 계산(doc/0579).
    private List<Component> GetMainBaseCandidates()
    {
        List<Component> candidates = new List<Component>();
        if (rtsController != null)
            candidates.AddRange(rtsController.BuildingList.FindAll(b => b != null && b.CompareTag("MainBase")));
        candidates.AddRange(allyMainBaseTargets.FindAll(b => b != null));
        return candidates;
    }
```

## 요약 / 남은 작업

- 새 인스펙터 필드 `allyMainBaseTargets`(`List<EnemyBuildingController>`) 추가 — 기본값 빈 리스트라
  기존 스테이지(1~3)는 아무것도 안 바뀜.
- 스테이지4의 Spore Brood `EnemyAIDirector`에서만 이 리스트에 아군 OC 오메가코어 건물 인스턴스를
  등록하면, 그 director가 보내는 공격 웨이브가 플레이어 메인기지와 오메가코어 중 무작위로 목표를
  고르게 됨(둘 다 없으면 기존처럼 플레이어 아무 건물).
- 웨이브가 목적지에 도착하기 전에 지나가다 다른 대상과 마주치면 기존 `EnemyAttackRange` 자동교전
  로직이 그대로 우선 적용됨(이번 변경과 무관, 이미 동작 중).
- 별동대(점령지 탈환, `raidTargets`)나 기지 방어 소집(`homeBuildings`)에는 영향 없음 — 이번 변경은
  `PickAttackTarget()`(웨이브 행군 목적지)에만 해당.
- **남은 작업**: 스테이지4 씬에 아군 OC 오메가코어 건물이 배치돼 있다면, 그 씬의 Spore Brood
  `EnemyAIDirector` 인스펙터에서 `allyMainBaseTargets` 리스트에 그 건물을 직접 등록해야 실제로
  적용됨(코드만으로는 자동 등록되지 않음, 의도된 수동 지정 방식).

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 0`.

## 변경된 파일

- `Assets/Scripts/System/EnemyAIDirector.cs` — `allyMainBaseTargets`/`attackTargetCandidates` 필드
  추가, `RunWaveSquad`/`PickAttackTarget` 타입을 `BuildingController` → `Component`로 일반화,
  후보 풀 계산을 `GetMainBaseCandidates()`로 분리해 실시간 디버그 표시와 공유.
