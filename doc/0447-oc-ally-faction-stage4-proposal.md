# 0447. 4스테이지 아군 OC 진영 처리 (제안)

**날짜:** 2026-08-07

## 요청 내용
> 이제 4스테이지에 사용할 아군 OC를 만들려고하는데 일단 NTA(플레이어)의 유닛들이 강제공격은
> 가능하지만 자동공격은 안해야해(적으로 인식을 안함). 그리고 플레이어가 클릭해서 선택하려고하면
> 중립 Ore와 같이 노란색으로 마우스가 변하고 그래야해

## 조사 내용

### 자동 공격/강제 공격이 어떻게 갈리는지

- `Assets/Scripts/Unit/AttackRange.cs`(플레이어 유닛의 사거리 감지)는 `OnTriggerEnter`에서
  **Tag가 "Enemy"인 것만** `enemiesInRange`에 넣음 — 자동교전은 순수 **Tag 기반**. 즉 어떤 오브젝트가
  Tag "Enemy"만 아니면 플레이어 유닛은 그걸 절대 자동으로 공격하지 않음(사거리 안에 서 있어도 무시).
- "강제공격"(A모드 + 좌클릭)은 완전히 다른 경로 — `UserControl.HandleLeftClick()`이 **Physics
  Layer**로 클릭 대상을 구분해서(`layerUnit`/`layerEnemy`/`layerBuilding`/`layerOre`/`layerGas`)
  해당 컴포넌트(`UnitController`/`EnemyUnitController`/`BuildingController`/`EnemyBuildingController`)를
  찾아 `RTSUnitController.AttackSelectedUnits`/`AttackEnemyBuildingSelectedUnits` 등을 호출함. 이건
  Tag와 무관 — **Layer**로 클릭 판별만 되면 Tag가 뭐든 강제공격이 걸림.
- 즉 **Tag와 Layer를 서로 다른 목적으로 분리해서 쓰면** "자동공격은 안 되지만 강제공격은 되는 대상"을
  자연스럽게 만들 수 있음: Tag는 "Enemy"가 아니게(자동공격 배제), Layer는 클릭에 잡히는 레이어로
  (강제공격 가능).

### 커서 색은 Layer 기반

`UserControl.GetHoveredTarget()`도 Layer로 판정함:
```csharp
if (Physics.Raycast(ray, ..., layerEnemy) && 보임) return CursorTarget.Enemy;   // 빨강(추정)
if (Physics.Raycast(ray, layerUnit | layerBuilding)) return CursorTarget.Ally;   // 초록(추정, 플레이어 자기 유닛)
if (Physics.Raycast(ray, ..., layerOre | layerGas) && 보임) return CursorTarget.Neutral; // 노랑
```
`layerEnemy`에 걸리면 무조건 빨강(Enemy) 커서가 뜸. 아군 OC를 `layerEnemy`에 두면 강제공격 클릭
경로는 그대로 재사용할 수 있지만 커서가 빨갛게 뜸(요청과 다름) — 그래서 **아군 OC 전용 레이어를
새로 하나 만들어서**, 클릭 판별과 커서 판별 양쪽에 그 레이어를 각각의 방식으로 연결해야 함.

### 자동전투(EnemyAttackRange)는 붙이면 안 됨

`EnemyAttackRange.cs`(OC/외계종족 유닛이 자기 사거리 안의 상대를 자동 감지하는 컴포넌트)는 Tag
목록(`Worker, AttackUnit, MainBase, Tier1~3, SupplyDepot, Lab` — **전부 플레이어 진영 Tag**)으로
대상을 찾음. 이 컴포넌트가 자식으로 붙어있으면 "아군"이라는 개념과 무관하게 **플레이어 유닛/건물을
자동으로 공격**해버림. 그래서 아군 OC 유닛/건물에는 **이 컴포넌트를 안 붙이거나 비활성화**해야
안전함 — 마침 `EnemyUnitController.Awake()`가 `GetComponentInChildren<EnemyAttackRange>()`로 찾는데
자식이 없거나 비활성이면 그냥 `null`로 남고, 이후 전투 관련 로직은 전부 `attackRange != null` 체크
뒤에 있어서 조용히 다 꺼짐(전투 능력 없이 이동/선택/피격만 가능한 상태) — 코드 변경 없이 프리팹
구성만으로 되는 부분.

## 제안하는 변경

### 1) 새 Unity Layer 추가: `AllyOC`

Project Settings에 빈 슬롯(13번)에 `AllyOC` 레이어 추가. 아군 OC 유닛/건물 프리팹(인스턴스)의
Layer를 여기로 지정.

### 2) Tag는 "Enemy"를 쓰지 않음

`Untagged`(기본)로 둠 — `AttackRange.OnTriggerEnter`의 `CompareTag("Enemy")`가 자연히 걸러줘서
플레이어 유닛의 자동교전 후보에서 제외됨. 코드 변경 없음.

### 3) `UserControl.cs`에 `layerAllyOC` 필드 + 클릭 분기 + 커서 분기 추가

**Before (`HandleLeftClick`, "2. 적 클릭" 블록 바로 뒤):**
```csharp
        // 3. 건물 클릭 = 선택 또는 아군 건물 강제 공격 (A 모드 중이면 해당 건물을 강제로 공격, 아니면 선택)
        if (clickedBuilding)
```

**After (사이에 새 블록 삽입):**
```csharp
        // 2.5. 아군 OC 클릭 = 선택(중립 커서와 동일 취급) 또는 강제 공격 (A 모드 중이면 강제로 공격)
        // EnemyUnitController/EnemyBuildingController를 그대로 재사용하되, Tag가 "Enemy"가 아니라서
        // 자동교전 대상에서는 빠지고, 이 전용 레이어를 통한 명시적 클릭에서만 반응한다 (doc/0447).
        if (clickedAllyOC)
        {
            EnemyUnitController allyUnit = allyOcHit.transform.GetComponent<EnemyUnitController>();
            if (allyUnit != null)
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackSelectedUnits(allyUnit);
                    allyUnit.FlashMarker();
                    ShowAttackPointer(allyUnit.transform.position);
                    UsercurrentState = OrderState.None;
                    return;
                }

                pendingLeftClickSelect = () => { if (allyUnit != null) rtsUnitController.ClickSelectEnemy(allyUnit); };
                return;
            }

            EnemyBuildingController allyBuilding = allyOcHit.transform.GetComponent<EnemyBuildingController>();
            if (allyBuilding != null)
            {
                if (UsercurrentState == OrderState.Attack)
                {
                    rtsUnitController.AttackEnemyBuildingSelectedUnits(allyBuilding);
                    allyBuilding.FlashMarker();
                    ShowAttackPointer(allyBuilding.transform.position);
                    UsercurrentState = OrderState.None;
                    return;
                }

                pendingLeftClickSelect = () => { if (allyBuilding != null) rtsUnitController.ClickSelectEnemyBuilding(allyBuilding); };
                return;
            }
        }

        // 3. 건물 클릭 = 선택 또는 아군 건물 강제 공격 (A 모드 중이면 해당 건물을 강제로 공격, 아니면 선택)
        if (clickedBuilding)
```
(`clickedAllyOC`/`allyOcHit`는 함수 상단의 다른 `clicked*` 변수들과 같은 자리에 `layerAllyOC`로
레이캐스트해서 선언 — 기존 패턴 그대로.)

**`GetHoveredTarget()` — Before:**
```csharp
        if (Physics.Raycast(ray, out RaycastHit resourceHit, Mathf.Infinity, layerOre | layerGas) && IsRevealedByFog(resourceHit.transform.position))
            return CursorTarget.Neutral;

        return CursorTarget.None;
```

**After:**
```csharp
        if (Physics.Raycast(ray, out RaycastHit resourceHit, Mathf.Infinity, layerOre | layerGas | layerAllyOC) && IsRevealedByFog(resourceHit.transform.position))
            return CursorTarget.Neutral; // 중립 자원 + 아군 OC 전부 노란 커서 (doc/0447)

        return CursorTarget.None;
```

### 4) 아군 OC 프리팹/인스턴스 구성

기존 "OC Unit/Building Data SO"(기존 OC 로스터, ID 1~9)를 그대로 재사용 — 스탯/이름은 이미 있는
것 그대로, 이번엔 "적이 아니라 아군으로 배치"하는 차이만 있음. `EnemyUnitController`/
`EnemyBuildingController`도 그대로 재사용(스탯 자가 조회 로직은 진영과 무관하게 동일하게 동작).
4스테이지 씬에 배치할 때:
- Layer → `AllyOC`
- Tag → `Untagged`
- 자식의 `AttackRange`(EnemyAttackRange가 붙은 감지 콜라이더) 오브젝트는 **비활성화**(전투 안 함,
  가만히 서서 선택/강제공격만 가능한 상태)

## 확인 필요 사항 → 결정

1. **프리팹 방식**: 기존 OC 프리팹을 그대로 두고 씬 배치 시 인스턴스 오버라이드(Layer/Tag/
   AttackRange 비활성화)로 처리 vs. **Prefab Variant**로 아군 전용 버전을 따로 만들기 — 사용자가
   **Prefab Variant**를 선택함. 원본 OC 프리팹(모델/스탯 조회 로직)을 상속하면서 아군 전용 설정만
   오버라이드로 저장되므로, 나중에 원본이 바뀌어도 자동으로 따라오고 씬에 배치할 때마다 수동으로
   설정할 필요가 없음.
2. **전투 여부**: 완전 수동 배치물 vs. **외계종족(Spore Brood)과 자동교전** — 사용자가
   **자동교전**을 선택함. `EnemyAttackRange`는 그대로 붙여두되(비활성화 안 함), 대상 Tag 목록을
   손봐야 함(아래 5번 참고) — 그대로 두면 원래 목록(플레이어 진영 Tag)대로 플레이어를 자동
   공격해버림.

## 제안하는 변경 (최종)

### 1) 새 Unity Layer 추가: `AllyOC`

Project Settings 빈 슬롯에 `AllyOC` 레이어 추가. 아군 OC Variant 프리팹의 **루트** Layer를 여기로
지정(자식인 `AttackRange` 감지 콜라이더는 원래 Layer 그대로 둠 — 외계종족 감지용 Physics 충돌
매트릭스에 영향 없게).

### 2) Tag는 "Enemy"를 쓰지 않음

루트 Tag는 `Untagged`로 둠 — `AttackRange.OnTriggerEnter`의 `CompareTag("Enemy")`가 자연히 걸러줘서
플레이어 유닛의 자동교전 후보에서 제외됨. 코드 변경 없음.

### 3) `UserControl.cs`에 `layerAllyOC` 필드 + 클릭 분기 + 커서 분기 추가

(3)번 항목은 위 초안과 동일 — `HandleLeftClick()`에 "2.5. 아군 OC 클릭" 블록 추가,
`GetHoveredTarget()`의 Neutral 판정에 `layerAllyOC` 포함.

### 4) `EnemyAttackRange.cs` — 대상 Tag 목록을 인스턴스별로 설정 가능하게 변경

지금은 `private static readonly string[] TargetTags`로 **모든** `EnemyAttackRange`(적대 OC, 외계종족,
그리고 새로 만들 아군 OC까지)가 하나의 전역 목록을 공유함. 아군 OC Variant만 "외계종족(Enemy 태그)"을
공격 대상에 추가하려면, 전역 배열을 그대로 두고 아군 OC에만 추가하는 게 불가능함(정적 필드라 인스턴스별
차이를 못 둠) — 그래서 인스턴스 필드로 바꾸고 기본값은 기존 목록 그대로 유지함(적대 OC/외계종족은
아무 설정도 안 바꾸면 지금과 100% 동일하게 작동).

**Before:**
```csharp
    // 감지 대상: 플레이어 유닛(Worker/AttackUnit) + 플레이어 건물(MainBase/Tier1~3/SupplyDepot/Lab)
    private static readonly string[] TargetTags =
        { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab" };
    ...
    private static bool IsValidTarget(Collider other)
    {
        foreach (string tag in TargetTags)
        {
            if (other.CompareTag(tag))
                return true;
        }

        return false;
    }
```

**After:**
```csharp
    // 감지 대상 Tag 목록 - 기본값은 플레이어 진영(Worker/AttackUnit/MainBase/Tier1~3/SupplyDepot/Lab).
    // 인스턴스 필드라서 프리팹(Variant)별로 다르게 설정 가능 - 예: 아군 OC Variant는 이 목록을
    // ["Enemy"]로 바꿔서 플레이어 대신 외계종족을 자동교전 대상으로 삼는다 (doc/0447).
    [SerializeField]
    private string[] targetTags =
        { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab" };
    ...
    private bool IsValidTarget(Collider other)
    {
        foreach (string tag in targetTags)
        {
            if (other.CompareTag(tag))
                return true;
        }

        return false;
    }
```

### 5) 아군 OC Prefab Variant 만들 때 설정값

- 루트 Layer → `AllyOC`
- 루트 Tag → `Untagged`
- 자식 `AttackRange`(`EnemyAttackRange`)의 `targetTags`를 `["Enemy"]`로 오버라이드(외계종족만 자동
  교전 대상 — 플레이어 Tag는 전부 뺌)
- 나머지(스탯 조회용 `enemyUnitID`/`enemyBuildingID`, 모델, `HealthManager` 등)는 원본 그대로 상속

구체적으로 어떤 OC 유닛/건물을 4스테이지 아군으로 쓸지는 아직 정해지지 않았으므로, 이번엔 재사용
가능한 인프라(레이어/커서/클릭 분기/Tag 목록 설정 가능화)만 만들어두고, 실제 Variant 프리팹은
어떤 유닛/건물을 쓸지 정해지면 그때 만듦.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 37`(기존 베이스라인과 동일한 obsolete-API 경고뿐, 새 경고/에러 없음).
- Unity 콘솔 Error 로그 0건.

## 변경된 파일

- Unity 프로젝트 설정(`ProjectSettings/TagManager.asset`) — `AllyOC` 레이어(13번) 추가
- `Assets/Scripts/UserControl/UserControl.cs` — `layerAllyOC` 필드, `HandleLeftClick()`의
  "2.5. 아군 OC 클릭" 분기, `GetHoveredTarget()`의 Neutral 커서 판정에 `layerAllyOC` 포함
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` — `TargetTags`를 정적 배열 → 인스턴스
  필드(`targetTags`)로 변경(기본값 동일, 기존 프리팹 동작 변화 없음)
- (아군 OC Prefab Variant 자체는 구체적인 유닛/건물이 정해지면 별도로 진행 — 인프라만 완료)
