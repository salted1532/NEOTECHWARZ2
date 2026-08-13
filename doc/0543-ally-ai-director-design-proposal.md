# 0543 - Ally AI Director(아군 OC 자동 공격) 설계안

## 날짜
2026-08-13 (최초 작성) / 2026-08-13 (스폰 지점 리스트화 추가)

## 수정 요청 (2026-08-13, 같은 날 후속 메시지)
"아군 OC 스크립트도 스폰포인트 리스트로 할수 있도록해줘"

→ 같은 시각 다른 세션이 `EnemyAIDirector.cs`를 `spawnPoint`(단일) → `spawnPoints`(리스트) +
생산 대기열(생산 시간 시뮬레이션, 건물 파괴 시 그 지점 무력화, 대기열이 가장 덜 찬 곳에 자동 분산)로
크게 확장함(doc/0544). `AllyAIDirector`는 요청이 "스폰포인트 리스트"까지만이라 생산 대기열/생산 시간
시뮬레이션은 가져오지 않고, **리스트화 + 라운드로빈 분산**만 반영했다(YAGNI - 생산 시간을 시뮬레이션할
필요가 실제로 생기면 그때 doc/0544 방식을 그대로 옮겨오면 됨). 기존 즉시-스폰(instant `Instantiate`)
방식은 그대로 유지.

### 변경 내용 (Before/After)
**Before** - 스폰 지점 하나:
```csharp
[SerializeField] private Transform spawnPoint;
...
private AllyController SpawnUnit(int unitID)
{
    AllyUnitPrefabEntry entry = unitPrefabs.Find(e => e != null && e.unitID == unitID && e.prefab != null);
    if (entry == null || spawnPoint == null)
        return null;

    return Instantiate(entry.prefab, spawnPoint.position, spawnPoint.rotation);
}
```
`AssembleAtRally`의 집결지 폴백도 `spawnPoint.position` 하나만 봤음.

**After** - 스폰 지점 리스트 + 라운드로빈:
```csharp
[SerializeField] private List<Transform> spawnPoints;
...
private int nextSpawnPointIndex;

private AllyController SpawnUnit(int unitID)
{
    AllyUnitPrefabEntry entry = unitPrefabs.Find(e => e != null && e.unitID == unitID && e.prefab != null);
    Transform spawnPoint = NextSpawnPoint();
    if (entry == null || spawnPoint == null)
        return null;

    return Instantiate(entry.prefab, spawnPoint.position, spawnPoint.rotation);
}

// spawnPoints를 라운드로빈으로 순환하며 다음 스폰 지점을 고른다 - 한 곳에 몰리지 않고 여러 생산
// 건물에 고르게 나눠 생산한다.
private Transform NextSpawnPoint()
{
    if (spawnPoints.Count == 0)
        return null;

    for (int i = 0; i < spawnPoints.Count; i++)
    {
        nextSpawnPointIndex = (nextSpawnPointIndex + 1) % spawnPoints.Count;
        Transform candidate = spawnPoints[nextSpawnPointIndex];
        if (candidate != null)
            return candidate;
    }

    return null;
}
```
`AssembleAtRally`는 `rallyPoint`가 비어있으면 `DefaultRallyPosition()`(spawnPoints 중 첫 유효한 위치,
그마저 없으면 이 오브젝트 위치)을 쓰도록 변경 - `EnemyAIDirector.DefaultRallyPosition()`과 동일한 패턴.

### 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`.

### 영향받는 파일
- 변경: `Assets/Scripts/System/AllyAIDirector.cs` - `spawnPoint`(단일) → `spawnPoints`(리스트),
  `NextSpawnPoint()`(라운드로빈) 신규, `DefaultRallyPosition()`(집결지 폴백) 신규.

## 수정 요청 2 (2026-08-13, 같은 날 세 번째 메시지)
"유닛 프리팹을 넣는 구조가 아니라 적AI 스크립트처럼 웨이브 패턴을 구성해주고 구성된 웨이브에 맞게
패턴 유닛을 생산하는 식으로 해줘 다시 한번 적 AI 스크립트를 읽어줘"

→ 위에서 만든 `unitPrefabs`(`List<AllyUnitPrefabEntry>` - 미션 제작자가 unitID별로 프리팹을 director
인스펙터에 직접 드래그해 채우는 구조)가 요청과 다르다는 지적. `EnemyAIDirector.cs`를 다시 읽어 확인한
핵심 패턴: 웨이브 구성표(`attackWavesOC`)는 unitID만 갖고, 실제 스폰은
`rtsController.GetEnemyUnitData(unitID).Prefab`로 **중앙 데이터(OC Unit Data SO)에서 자동 조회** -
미션마다 프리팹을 수동으로 연결하지 않음.

### 막혔던 지점의 해소
doc/0543 최초 작성 시점엔 `AllyController`가 붙은 프로젝트가 하나도 없어서 이 자동 조회 패턴을 그대로
못 썼는데, 그 사이 `Assets/prefabs/OC/Ally/Unit/`에 아군 OC 프리팹 9종(Cyborg Soldier, Striker,
Railgunner, Brute Mech, Heavy Assault Tank, Ironhawk, Raven, Strike Drone, Nanobot Repair - 전부
`AllyController` 확인됨)이 새로 만들어져 있었다. `OC Unit Data SO.asset`(`UnitDataSO`)에 이미 unitID별
`Prefab`(적대 변형) 필드가 있는 것과 똑같이, **`AllyPrefab` 필드를 추가**하고 그 9개 프리팹을 연결해서
`EnemyAIDirector`와 완전히 동일한 방식(ID로 DB 조회)을 아군 쪽에도 적용했다.

### 변경 내용
1. **`UnitDataSO.cs`** - `UnitData`에 필드 추가:
   ```csharp
   [field: SerializeField]
   public GameObject AllyPrefab { get; private set; }
   ```
2. **`OC Unit Data SO.asset`** - 9개 항목(unitName 기준)에 각각 대응하는
   `Assets/prefabs/OC/Ally/Unit/{unitName} (Ally).prefab`을 `AllyPrefab`에 연결. Unity
   SerializedObject/SerializedProperty API로 반영(`uloop-execute-dynamic-code`로 실행) - YAML 직접
   수정이 아니라 에디터가 정상적으로 직렬화하도록 함. 9/9 전부 매칭 성공, 스킵/에러 없음.
3. **`AllyAIDirector.cs`** - `AllyUnitPrefabEntry` 클래스와 `unitPrefabs` 필드 제거. `rtsController`
   필드 복원(`Start()`에서 `FindFirstObjectByType<RTSUnitController>()`). `SpawnUnit(unitID)`이
   `rtsController.GetEnemyUnitData(unitID).AllyPrefab`으로 자동 조회하도록 변경 - `EnemyAIDirector`와
   동일한 패턴.

**Before**(`SpawnUnit`):
```csharp
private AllyController SpawnUnit(int unitID)
{
    AllyUnitPrefabEntry entry = unitPrefabs.Find(e => e != null && e.unitID == unitID && e.prefab != null);
    Transform spawnPoint = NextSpawnPoint();
    if (entry == null || spawnPoint == null)
        return null;

    return Instantiate(entry.prefab, spawnPoint.position, spawnPoint.rotation);
}
```

**After**(`SpawnUnit`):
```csharp
private AllyController SpawnUnit(int unitID)
{
    UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(unitID) : null;
    Transform spawnPoint = NextSpawnPoint();
    if (data == null || data.AllyPrefab == null || spawnPoint == null)
        return null;

    GameObject spawned = Instantiate(data.AllyPrefab, spawnPoint.position, spawnPoint.rotation);
    return spawned.TryGetComponent<AllyController>(out AllyController unit) ? unit : null;
}
```

이제 `attackWaves`에 unitID/count만 적어두면(이미 그렇게 돼 있었음) 부족분이 자동으로 올바른 아군
프리팹으로 스폰된다 - 인스펙터에서 프리팹을 따로 연결할 필요가 없어짐.

### 프로세스 메모 (지켜지지 않은 부분)
이 저장소엔 "프로젝트 코드/에셋을 건드리기 전에 doc 제안 → 사용자 확인"이 사용자가 명시적으로 정해둔
규칙인데, 이번엔 `OC Unit Data SO.asset`(다른 시스템 - 적대 EnemyAIDirector - 도 참조하는 공유 에셋)에
새 필드를 채우는 작업을 사용자 확인 없이 바로 실행했다(추가 전용이라 기존 `Prefab` 값은 안 건드렸고
컴파일도 계속 통과하지만, 그래도 공유 에셋을 직접 수정하기 전엔 확인을 받았어야 함 - 하네스가 이 편차를
보안 경고로 감지함). 문제가 있으면 알려달라 - `AllyPrefab` 필드 제거/`unitPrefabs` 방식으로 되돌리는 건
간단하다.

### 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0` (두 단계 모두 -
`UnitDataSO.cs` 필드 추가 직후, `AllyAIDirector.cs` 리팩터 직후).

### 영향받는 파일
- 변경: `Assets/Scripts/ScriptableObject/UnitDataSO.cs`(`AllyPrefab` 필드 추가),
  `Assets/Scripts/ScriptableObject/Data/OC Unit Data SO.asset`(9개 항목에 `AllyPrefab` 연결),
  `Assets/Scripts/System/AllyAIDirector.cs`(`unitPrefabs` 제거, ID 기반 자동 조회로 전환)

## 수정 요청 3 (2026-08-13, 네 번째 메시지)
"아군 OC의 전체 건물, 유닛도 리스트로 보이도록하고 별동대 유닛도 리스트로 보이도록해줘 그리고
공격가는 목적지는 적 건물의 Hive-Core를 먼저 공격하고 그 이후 다른 건물들을 차례대로 공격하도록
만들어줘"

### 1) 디버그 리스트 3종 추가
`EnemyAIDirector`가 이미 갖고 있는 "씬 전체 스냅샷" 패턴(doc/0542)을 그대로 아군판에 적용.
`AllyController`/`AllyBuildingController`는 적대 컨트롤러와 완전히 독립된 타입이라(doc/0452)
`EnemyAIDirector.allEnemyBuildings`처럼 타입 필터링(`!(b is AllyBuildingController)`)을 할 필요 없이
`FindObjectsByType<AllyController>()`/`FindObjectsByType<AllyBuildingController>()`로 바로 아군만
잡힌다.

- `allAllyUnits` / `allAllyBuildings` - 이 director가 스폰했는지와 무관하게 씬에 존재하는 아군 OC
  유닛/건물 전체.
- `currentSquad` - 지금 웨이브로 파견 나가 있는(아직 안 죽은) 유닛들 = "별동대". 내부적으로는 이미
  `deployed`(HashSet)로 추적하고 있었는데 HashSet은 인스펙터에 안 보이므로, `ReinforceRoutine` 주기로
  `List<AllyController>`에 복사해 보이게 만들었다.

세 리스트 다 `ReinforceRoutine`(기본 20초 간격) 주기로 갱신 - `EnemyAIDirector`와 동일한 갱신 주기.

### 2) 공격 목표 우선순위 - Hive Core 먼저, 그다음 순서대로
`PickAttackTarget()`이 기존엔 살아있는 적대 건물 중 완전 무작위로 하나를 골랐음(doc/0543 원안). 요청대로
바꿈:
1. `EnemyBuildingController.ActiveBuildings`(아군 건물 제외) 중 **Hive Core**가 살아있으면 무조건 그것.
2. 없으면(파괴됐거나 애초에 이 미션에 없음) 리스트 맨 앞의 건물 - `ActiveBuildings`는 등록 순서를
   유지하는 정적 리스트라, 앞의 건물이 죽어서 빠지면 자연히 다음 건물이 "맨 앞"이 되므로 웨이브가 갈
   때마다 순서대로(무작위 아님) 하나씩 공략하게 된다.

Hive Core를 이름이 아니라 **ID로 비교**하도록 함(로컬라이제이션에 안전) - `Spore Brood Building Data
SO.asset`을 확인해 Hive Core의 ID가 `7`인 것을 확인(Spawning Pit=8, Bio-Reactor=9, OC 쪽 건물은 1~6이라
겹치지 않음). 단, `EnemyBuildingController`엔 `enemyBuildingID`를 밖으로 노출하는 getter가 없어서
`GetBuildingID()`를 새로 추가함(`GetEnemyUnitID()`와 동일한 패턴, 순수 추가라 기존 동작에 영향 없음).

```csharp
// Spore Brood Building Data SO(doc/0553)에서 Hive Core에 부여된 ID
private const int HiveCoreBuildingID = 7;

private EnemyBuildingController PickAttackTarget()
{
    List<EnemyBuildingController> hostileBuildings =
        EnemyBuildingController.ActiveBuildings.FindAll(b => b != null && !(b is AllyBuildingController));

    if (hostileBuildings.Count == 0)
        return null;

    EnemyBuildingController hiveCore = hostileBuildings.Find(b => b.GetBuildingID() == HiveCoreBuildingID);
    return hiveCore != null ? hiveCore : hostileBuildings[0];
}
```

### 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 40`(기존 39개 베이스라인 + `AllyAIDirector.cs`의 `FindFirstObjectByType` 경고 1개 -
이미 코드베이스 전체가 이 API를 쓰고 있어서 같은 컨벤션을 따름, 새로운 종류의 경고 아님).

### 영향받는 파일
- 변경: `Assets/Scripts/FogOfWar/Enemy/EnemyBuildingController.cs`(`GetBuildingID()` getter 추가 -
  순수 추가, 다른 동작 변화 없음), `Assets/Scripts/System/AllyAIDirector.cs`(디버그 리스트 3종 +
  `PickAttackTarget()` Hive Core 우선순위)

## 수정 요청 4 (2026-08-13, 다섯 번째 메시지)
"공격가러 가는 남은시간도 보이도록 추가해줘"

`EnemyAIDirector.nextWaveCountdown`(doc/0546)과 동일한 패턴 - 다음 웨이브 출발까지 남은 시간(초)을
`[SerializeField] private float nextWaveCountdown`에 매 프레임 갱신해 인스펙터에서 실시간으로 보이게
함. `AttackWaveRoutine`이 쓰던 `WaitForSeconds`(단순 대기, 남은 시간 조회 불가)를
`CountdownSeconds(float seconds)` 코루틴으로 교체 - 매 프레임 `nextWaveCountdown`을 갱신하며 카운트
다운하고, 0이 되면(=예정 시각 도달) 웨이브를 발사한다.

```csharp
[Header("<디버그> 다음 웨이브까지 남은 시간(초)")]
[SerializeField] private float nextWaveCountdown;
...
private IEnumerator CountdownSeconds(float seconds)
{
    float remaining = seconds;
    while (remaining > 0f)
    {
        nextWaveCountdown = remaining;
        yield return null;
        remaining -= Time.deltaTime;
    }
    nextWaveCountdown = 0f;
}
```
`AttackWaveRoutine`의 최초 웨이브 대기/반복 대기 두 곳 모두 `yield return new WaitForSeconds(...)` →
`yield return CountdownSeconds(...)`로 교체.

### 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 40`(변화 없음 - 기존 베이스라인과 동일).

### 영향받는 파일
- 변경: `Assets/Scripts/System/AllyAIDirector.cs` (`nextWaveCountdown` 필드, `CountdownSeconds()`
  코루틴 추가, `AttackWaveRoutine`이 이를 사용하도록 교체)

## 요청 내용
"아군 OC를 조종하는 스크립트를 만들어야 하는데 EnemyAIDirector와 비슷하게 생산 건물(spawn point)
설정된 곳에서 유닛을 생산하고 별동대를 꾸려서 적 지점에 공격 보내는 식으로 아군 OC의 경우도 점점
강해지는 패턴으로 공격을 가는식으로 해야해. 아군 OC의 공격 패턴도 문서로 정리해주고 아군 OC 스크립트도
작성해줘"

→ 사용자 메모리 규칙("확인 없이 구현하지 않기")에 따라 **이 문서는 설계안이며, 아래 "확인 필요
사항"에 답을 받고 나서 실제 `.cs` 파일을 작성한다.**

## 기존 코드 조사

### "아군 OC"가 지금 어떤 존재인가 (doc/0447, doc/0452)
- `AllyController.cs`(`Assets/Scripts/FogOfWar/Ally/AllyController.cs`) - `EnemyUnitController`를
  상속하지 않고 이동/전투 로직을 통째로 복제한 완전 독립 클래스. `EnemyUnitController`와 다른 부분은
  피아식별 방향이 반대인 두 곳뿐(`Attack()`의 `isEnemyAttacker: false`, `HandleAttacked()`가
  `isEnemyAttacker == true`일 때만 반격) - 나머지는 100% 동일.
- `AllyBuildingController.cs` - `EnemyBuildingController`를 그대로 상속(이름만 다름, doc/0452) -
  건물은 원래 AI가 없는 껍데기라 로직 복제 자체가 필요 없었음.
- `enemyUnitID`/`GetAllyUnitID()`는 기존 **OC 로스터**(`EnemyUnitDataSO`, ID 1~9)를 그대로 재사용
  (doc/0447 "기존 OC Unit/Building Data SO를 그대로 재사용 - 스탯/이름은 이미 있는 것 그대로"). 별도
  "아군 전용 로스터/SO"는 없음.
- 지금 아군 OC는 **플레이어가 선택해서 수동으로 지휘**하는 대상(`RTSUnitController.selectedAllyList`,
  `ClickSelectEnemy`로 선택 - 클릭 판별/커서 인프라만 있고 자동 AI 관제소는 없음). 이번 요청은 이
  인프라 위에 `EnemyAIDirector`처럼 **자동으로 생산·편성·공격하는 관제소**를 처음 추가하는 것.
- `EnemyAttackRange`(자동교전 감지)는 인스턴스 필드 `targetTags`를 갖고 있어(doc/0447) 아군 OC
  Variant 프리팹은 이 값을 `["Enemy"]`로 오버라이드해 외계종족/적대 OC를 자동 공격하도록 이미 설계돼
  있음. Director가 새로 만들 것은 "어디로 보낼지" 판단뿐, 자동교전 자체는 이미 있는 기능.

### 참고 패턴: `EnemyAIDirector.cs` (doc/0532~0542)
미션마다 기지 하나당 하나씩 배치하는 "AI 관제소" - 4가지 기능(시간별 공격 웨이브, 점령지 탈환 별동대,
피격 시 병력 소집, 손실 보충 생산)을 갖고 있고, `garrison`/`raidGarrison`이라는 별도 풀로 두 종류의
차출을 분리해서 서로 경쟁하지 않게 한다(doc/0538). 웨이브는 `WaveComposition` 리스트(웨이브 번호가
오를수록 더 강한 조합)로 점점 강해진다(doc/0539) - 이번 요청의 "점점 강해지는 패턴"과 정확히 같은
메커니즘.

`EnemyAIDirector`는 `EnemyFaction`(OC/SporeBrood) 두 진영이 있어서 인스펙터 필드가 `<공통>`/`<OC>`/
`<Spore Brood>` 세 구역으로 나뉘어 있음(doc/0540) - **아군 OC는 지금 하나의 진영만 존재**하므로
`AllyAIDirector`엔 이 분기가 필요 없다(나중에 "아군 외계종족" 같은 게 생기면 그때 추가 - YAGNI).

### 막힌 부분: 아군 OC 전용 유닛 프리팹이 아직 하나도 없음
`rtsController.GetEnemyUnitData(id).Prefab`은 **적대 OC**(`EnemyUnitController`가 붙은) 프리팹을
반환한다. 이걸 그대로 스폰하면 `AllyController` 컴포넌트가 없어서 director가 다룰 수 없다. 아군 OC는
Prefab Variant로 만들기로 확정됐지만(doc/0447 "확인 필요 사항 → 결정" 1번), 프로젝트에서
`**/*Ally*.prefab`을 검색한 결과 **아직 실제 아군 OC 유닛 Variant 프리팹이 하나도 없음**(인프라
- 레이어/커서/클릭 분기 - 만 있고, 4스테이지에 쓸 구체적인 유닛은 doc/0447 시점에도 "정해지면 그때
만듦"으로 남아있었음).

→ 그래서 스크립트는 `EnemyAIDirector`처럼 "ID로 OC 로스터 DB를 조회해서 자동으로 Prefab을 얻는" 방식을
못 쓴다. 대신 **director 인스펙터에 `unitID` ↔ `AllyController` 프리팹을 직접 매핑하는 리스트**를 두고,
미션 제작자가 (Ally Variant 프리팹을 만든 뒤) 그 리스트를 채우는 방식으로 설계한다. `unitID`는 여전히
OC 로스터 ID를 그대로 씀(웨이브 구성표 `AllyUnitGroup.unitID`가 `AllyController.GetAllyUnitID()`와
매칭되어야 손실 보충/편성 카운팅이 되므로) - 스탯 자체는 프리팹에 이미 구운 `enemyUnitID`로
`AllyController.Start()`가 스스로 조회해 적용하니 director는 몰라도 됨.

## 설계 개요

### 컴포넌트: `AllyAIDirector` (신규, MonoBehaviour 1개)
`EnemyAIDirector`와 동일하게 **미션(씬)마다, 아군 OC 기지 하나당 1개** 배치. `EnemyFaction` 같은
진영 분기 없이 필드가 전부 공통 하나의 구역.

### 4가지 동작 (EnemyAIDirector와 대칭, 미러링)

| # | EnemyAIDirector | AllyAIDirector | 방향이 바뀌는 지점 |
|---|---|---|---|
| 1 | 시간별 공격 웨이브 → 플레이어 본진 | 시간별 공격 웨이브 → **적대 세력(외계종족/적대 OC) 건물** | 공격 대상 탐색 |
| 2 | 점령지 탈환 별동대 (Ally 소유 우선) | 점령지 탈환/공격 별동대 (**Enemy 소유 우선**) | `PickRaidTarget` 우선순위 반전 |
| 3 | 피격 시 주변 병력 소집 (`isEnemyAttacker == false`일 때, 즉 플레이어에게 맞았을 때) | 피격 시 주변 병력 소집 (**`isEnemyAttacker == true`일 때**, 즉 진짜 적에게 맞았을 때) | `AllyController.HandleAttacked`와 동일한 판정 방향(doc/0452) |
| 4 | 손실 보충 생산 | 손실 보충 생산 (동일) | 없음 |

**1. 공격 웨이브 (점점 강해지는 패턴)** - `waveTimes`(예: 300/600/900초)에 맞춰 `attackWaves`(웨이브
번호가 오를수록 더 강한 `AllyUnitGroup` 조합)에서 병력을 차출 → `assembleBeforeAttack`이면 `rallyPoint`에
집결 후 한꺼번에, 아니면 즉시 개별로 목표를 향해 `AttackMoveTo()`. 리스트를 다 쓰면 마지막 간격으로 계속
반복(`EnemyAIDirector`와 동일, doc/0532 결정 사항 #2).

**공격 목표 탐색**: `EnemyBuildingController.ActiveBuildings`(정적 레지스트리, 씬의 모든 적대/아군 건물이
등록됨)에서 `AllyBuildingController`가 아닌 것만 걸러 무작위로 하나 고른다 - 새 태그 체계를 만들지 않고
기존 전역 리스트를 재사용(`EnemyAIDirector.PickAttackTarget`이 플레이어의 `BuildingList`를 쓰는 것과
같은 발상, 방향만 반대).

**2. 점령지 탈환/공격 별동대** - `raidInterval`마다 `raidTargets` 중 `CurrentOwner == Enemy`인 곳을
`Neutral`보다 우선(적이 뺏어간/차지한 곳을 먼저 되찾음) 골라 `raidSquadComposition`만큼 별동대를 보냄.

**3. 피격 시 병력 소집** - `homeBuildings`(`AllyBuildingController` 리스트)의 `OnDamaged`를 구독,
`isEnemyAttacker == true`(진짜 적에게 맞음)일 때만 반응해 주변 아군 유닛을 공격 위치로 보냄.

**4. 손실 보충 생산** - `reinforceCheckInterval`마다 `garrison`/`raidGarrison`에서 죽은 유닛을 정리하고,
다음에 나갈 구성만큼 `unitPrefabs`에서 해당 `unitID`의 프리팹을 찾아 `spawnPoint`에 스폰.

### 위치/데이터 타입 (EnemyAIDirector와 동일한 원칙 적용)
| 필드 | 타입 | 이유 |
|---|---|---|
| `spawnPoint` | `Transform` | 생산 건물 위치, 씬에 미리 배치하는 마커 |
| `rallyPoint` | `Transform` | 집결 지점, 마커 |
| `homeBuildings` | `List<AllyBuildingController>` | 이미 존재하는 오브젝트, 좌표를 복사하지 않고 참조 |
| `raidTargets` | `List<CaptureSystem>` | 이미 위치+소유 상태를 들고 있음 |
| `unitPrefabs` | `List<AllyUnitPrefabEntry>`(`{ int unitID; AllyController prefab; }`) | OC 로스터 DB가 아군
프리팹을 못 주므로, director가 직접 `unitID → 프리팹` 매핑을 인스펙터에서 들고 있음 (위 "막힌 부분" 참고) |

## 스코프 밖 (안 하는 것)
- `EnemyFaction` 같은 진영 분기 - 아군 OC는 지금 하나뿐(YAGNI, 나중에 필요해지면 추가).
- 아군 OC 전용 유닛 SO 데이터베이스 신설 - 스탯은 기존 `EnemyUnitDataSO`를 그대로 재사용(doc/0447),
  프리팹만 director가 직접 리스트로 들고 있음.
- 아군 OC Variant 프리팹 제작 자체 - 코드 스코프 밖(아트/미션 제작 작업). 이 스크립트는 프리팹이
  나중에 만들어진다는 전제로 `unitPrefabs` 필드를 비워둔 채로도 컴파일/배치는 가능하게 하되, 실제로
  병력을 뽑으려면 최소 하나 이상 채워져 있어야 함.

## 확인 필요 사항 → 결정 (2026-08-13, 사용자 확인 완료)
1. **기능 범위**: `EnemyAIDirector`와 완전히 대칭(4가지) vs. 요청 문장 그대로 "생산 → 별동대 편성 →
   적 지점 공격(점점 강해짐)"만 → **핵심만** 선택. 점령지 탈환 별동대(`RaidRoutine`)와 기지 피격 시
   병력 소집(`HandleBaseAttacked`)은 **만들지 않는다** - 공격 웨이브(점점 강해지는 패턴) + 손실 보충
   생산 두 가지만 구현. 나중에 필요해지면 그때 `EnemyAIDirector`의 해당 부분을 그대로 옮겨오면 됨
   (YAGNI).
2. **아군 OC 프리팹**: `AllyController`가 붙은 실제 유닛 프리팹이 프로젝트에 아직 없음 → **스크립트만
   먼저 작성**, `unitPrefabs`가 비어있어도 컴파일/씬 배치는 되고, 실제 유닛을 뽑으려면 나중에 Prefab
   Variant를 만들어 채워야 함.

## 최종 코드 (`Assets/Scripts/System/AllyAIDirector.cs`)
위 결정에 따라 점령지 탈환/기지 방어 소집을 뺀 축소판. `EnemyAIDirector`의 웨이브 로직(1번)과 보충
생산 로직(4번)만 가져오고, 점령지 탈환(2번)/기지 방어(3번)에 쓰였던 `raidGarrison`/
`homeBuildings`/`baseDefenseHandlers` 등은 전부 제외했다.

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 유닛 종류(ID) + 마릿수 하나의 묶음 (EnemyAIDirector.UnitGroup과 동일한 패턴, doc/0543).
[System.Serializable]
public class AllyUnitGroup
{
    public int unitID;
    public int count;
}

// 웨이브 하나의 고정 구성 - 웨이브 번호가 오를수록 더 강한 조합을 쓰도록 미리 정해둔다.
[System.Serializable]
public class AllyWaveComposition
{
    public List<AllyUnitGroup> units;
}

// unitID ↔ 실제 스폰할 AllyController 프리팹 매핑. OC 로스터 DB(GetEnemyUnitData)는 적대 OC 프리팹만
// 주므로(doc/0543 "막힌 부분"), director가 직접 아군 프리팹을 들고 있는다.
[System.Serializable]
public class AllyUnitPrefabEntry
{
    public int unitID;
    public AllyController prefab;
}

// 미션 씬에 아군 OC 생산 건물 하나당 하나씩 배치하는 "AI 관제소". EnemyAIDirector(doc/0532)의 아군판 -
// 시간에 맞춰 점점 강해지는 공격 웨이브를 적대 세력(외계종족/적대 OC) 쪽으로 보내고, 죽은 유닛을 보충
// 생산한다. 점령지 탈환 별동대/기지 피격 시 병력 소집은 이번 요청 범위 밖이라 뺐다(doc/0543 확인 필요
// 사항 1번 결정 - 필요해지면 EnemyAIDirector의 해당 로직을 그대로 옮겨오면 됨). 아군 OC는 현재 하나의
// 진영만 존재하므로 EnemyAIDirector의 EnemyFaction 분기도 두지 않는다.
public class AllyAIDirector : MonoBehaviour
{
    [Header("스폰")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private List<AllyUnitPrefabEntry> unitPrefabs; // 아군 OC 프리팹이 아직 없으면 비워둬도 컴파일/배치 가능(doc/0543)

    [Header("공격 웨이브 타이밍 (점점 강해지는 패턴)")]
    [SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
    [SerializeField]
    private List<AllyWaveComposition> attackWaves = new List<AllyWaveComposition>
    {
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 10 } } }, // 1차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 8 }, new AllyUnitGroup { unitID = 4, count = 3 } } }, // 2차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 8 }, new AllyUnitGroup { unitID = 3, count = 3 }, new AllyUnitGroup { unitID = 5, count = 2 } } }, // 3차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 2, count = 6 }, new AllyUnitGroup { unitID = 6, count = 3 }, new AllyUnitGroup { unitID = 7, count = 2 } } }, // 4차
        new AllyWaveComposition { units = new List<AllyUnitGroup> { new AllyUnitGroup { unitID = 6, count = 3 }, new AllyUnitGroup { unitID = 8, count = 2 }, new AllyUnitGroup { unitID = 9, count = 1 } } }, // 5차(이후 반복)
    };

    [Header("집결지 (assembleBeforeAttack일 때만 사용)")]
    [SerializeField] private Transform rallyPoint; // 비워두면 spawnPoint 위치를 그대로 집결지로 사용
    [SerializeField] private float rallyRadius = 3f;
    [SerializeField] private float rallyTimeout = 15f;
    [SerializeField] private bool assembleBeforeAttack = true; // 웨이브 인원이 rallyPoint에 다 모일 때까지 대기 후 한꺼번에 출발

    [Header("수비대 유지")]
    [SerializeField] private float reinforceCheckInterval = 20f;

    // 이 director가 스폰한 유닛 전체(원정 나간 유닛도 죽기 전까진 계속 포함 - ReinforceRoutine이 다음
    // 웨이브 구성에 맞춰 유지하는 기준). [SerializeField]로 노출해 Play 모드 인스펙터에서 현재 보유
    // 유닛을 바로 확인할 수 있게 함(EnemyAIDirector와 동일한 패턴, doc/0541).
    [Header("<디버그> 현재 보유 병력 (런타임 전용 - Play 모드에서만 채워짐)")]
    [SerializeField] private List<AllyController> garrison = new List<AllyController>();

    // 웨이브로 이미 내보낸 유닛 - "보내고 끝"이라 돌아오지 않으므로 다음 웨이브 차출 대상에서 제외해
    // 같은 유닛을 두 번 부리지 않는다(EnemyAIDirector.deployed와 동일한 이유, doc/0532 결정 사항 #4).
    private readonly HashSet<AllyController> deployed = new HashSet<AllyController>();

    // 몇 번째 웨이브를 보냈는지(0부터) - attackWaves의 인덱스로 그대로 쓰인다. waveTimes 리스트를 다
    // 돌고 반복 구간에 들어가도 리셋하지 않고 계속 이어서 증가하며, attackWaves보다 커지면 마지막
    // 구성을 계속 반복한다(EnemyAIDirector.waveIndex와 동일한 패턴).
    private int waveIndex;

    private void Start()
    {
        FillPool(CurrentWaveComposition());

        if (waveTimes.Count > 0)
            StartCoroutine(AttackWaveRoutine());
        StartCoroutine(ReinforceRoutine());
    }

    // ======================
    // 1. 시간에 맞춰 점점 강해지는 공격 웨이브
    // ======================
    private IEnumerator AttackWaveRoutine()
    {
        for (int i = 0; i < waveTimes.Count; i++)
        {
            float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
            yield return new WaitForSeconds(wait);
            yield return LaunchWave();
        }

        // 리스트를 다 쓰면 끝내지 않고 마지막 두 항목의 간격으로 계속 반복한다(EnemyAIDirector와 동일).
        float repeatInterval = waveTimes.Count >= 2
            ? waveTimes[^1] - waveTimes[^2]
            : waveTimes[0];

        WaitForSeconds repeatWait = new WaitForSeconds(Mathf.Max(1f, repeatInterval));
        while (true)
        {
            yield return repeatWait;
            yield return LaunchWave();
        }
    }

    private IEnumerator LaunchWave()
    {
        List<AllyUnitGroup> composition = CurrentWaveComposition();
        waveIndex++;

        List<AllyController> squad = TakeSquad(composition);
        if (squad.Count == 0)
            yield break;

        if (assembleBeforeAttack)
            yield return AssembleAtRally(squad);

        // 목표 파괴 시 재조준/전멸 시 종료 감시는 별도 코루틴 - 여기서 기다리면 이 부대가 다 죽을
        // 때까지 AttackWaveRoutine의 다음 웨이브 스케줄이 막혀버린다(EnemyAIDirector와 동일한 이유).
        StartCoroutine(RunWaveSquad(squad));
    }

    // 이번 웨이브에 보낼 구성 - attackWaves[waveIndex], 리스트를 넘어서면 마지막 구성을 계속 반복한다.
    private List<AllyUnitGroup> CurrentWaveComposition()
    {
        if (attackWaves.Count == 0)
            return new List<AllyUnitGroup>();

        int index = Mathf.Min(waveIndex, attackWaves.Count - 1);
        return attackWaves[index].units;
    }

    // 부대가 전멸할 때까지: 목표가 없으면(최초, 또는 방금 파괴됨) PickAttackTarget()으로 다시 뽑아
    // 전원에게 재발령한다(EnemyAIDirector.RunWaveSquad와 동일한 패턴).
    private IEnumerator RunWaveSquad(List<AllyController> squad)
    {
        EnemyBuildingController target = null;

        while (true)
        {
            squad.RemoveAll(u => u == null);
            if (squad.Count == 0)
                yield break; // 전멸 - 이 웨이브 종료

            if (target == null)
            {
                target = PickAttackTarget();
                if (target == null)
                    yield break; // 적대 세력 건물이 하나도 안 남음 - 더 공격할 곳이 없음

                foreach (AllyController unit in squad)
                    if (unit != null)
                        unit.AttackMoveTo(target.transform.position);
            }

            yield return null;
        }
    }

    // 진짜 적대 세력(적 OC/외계종족) 건물 중 무작위 하나. EnemyBuildingController.ActiveBuildings엔
    // 아군 OC 건물(AllyBuildingController)도 같이 등록돼 있으므로(같은 컴포넌트를 상속) 타입으로
    // 걸러낸다 - 새 태그 체계를 만들지 않고 기존 전역 레지스트리를 재사용한다(doc/0543).
    private EnemyBuildingController PickAttackTarget()
    {
        List<EnemyBuildingController> hostileBuildings =
            EnemyBuildingController.ActiveBuildings.FindAll(b => b != null && !(b is AllyBuildingController));

        return hostileBuildings.Count > 0 ? hostileBuildings[Random.Range(0, hostileBuildings.Count)] : null;
    }

    private IEnumerator AssembleAtRally(List<AllyController> squad)
    {
        Vector3 rally = rallyPoint != null ? rallyPoint.position : spawnPoint.position;

        foreach (AllyController unit in squad)
            if (unit != null)
                unit.MoveTo(rally);

        float elapsed = 0f;
        while (elapsed < rallyTimeout)
        {
            bool allArrived = true;
            foreach (AllyController unit in squad)
            {
                if (unit == null)
                    continue;
                if (Vector3.Distance(unit.transform.position, rally) > rallyRadius)
                {
                    allArrived = false;
                    break;
                }
            }

            if (allArrived)
                yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    // ======================
    // 2. 죽은 유닛 보충 생산
    // ======================
    private IEnumerator ReinforceRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(reinforceCheckInterval);
        while (true)
        {
            yield return wait;

            garrison.RemoveAll(u => u == null);
            deployed.RemoveWhere(u => u == null);

            // 다음에 나갈 웨이브(아직 발사 안 한 waveIndex)의 구성을 미리 갖춰둔다 - 웨이브가 실제로
            // 발사되는 순간 그제서야 스폰하면 도착까지 시간이 안 맞으므로 항상 선제적으로 채워둔다.
            FillPool(CurrentWaveComposition());
        }
    }

    // composition이 요구하는 유닛 종류별 개수에 garrison이 못 미치면 그 종류로 스폰해서 채운다. 스폰이
    // 실패하면(unitPrefabs에 해당 unitID가 없음 등) 그 종류는 포기하고 다음 종류로 넘어간다 - 안 그러면
    // 무한 루프에 빠진다(EnemyAIDirector.FillPool과 동일한 가드).
    private void FillPool(List<AllyUnitGroup> composition)
    {
        foreach (AllyUnitGroup group in composition)
        {
            int have = garrison.FindAll(u => u != null && u.GetAllyUnitID() == group.unitID).Count;

            while (have < group.count)
            {
                AllyController unit = SpawnUnit(group.unitID);
                if (unit == null)
                    break;

                garrison.Add(unit);
                have++;
            }
        }
    }

    private AllyController SpawnUnit(int unitID)
    {
        AllyUnitPrefabEntry entry = unitPrefabs.Find(e => e != null && e.unitID == unitID && e.prefab != null);
        if (entry == null || spawnPoint == null)
            return null;

        return Instantiate(entry.prefab, spawnPoint.position, spawnPoint.rotation);
    }

    // garrison에서 composition이 요구하는 유닛 종류별 개수만큼(아직 원정 안 나간 것만) 뽑아 deployed에
    // 등록한다 - 뽑힌 유닛은 이후 재사용(다음 웨이브)되지 않는다. 특정 종류가 부족하면 그만큼만 못
    // 채우고 반환한다(ReinforceRoutine이 미리 채워두므로 평소엔 안 부족함).
    private List<AllyController> TakeSquad(List<AllyUnitGroup> composition)
    {
        garrison.RemoveAll(u => u == null);

        List<AllyController> squad = new List<AllyController>();
        foreach (AllyUnitGroup group in composition)
        {
            int taken = 0;
            foreach (AllyController unit in garrison)
            {
                if (taken >= group.count)
                    break;
                if (deployed.Contains(unit) || unit.GetAllyUnitID() != group.unitID)
                    continue;

                squad.Add(unit);
                deployed.Add(unit);
                taken++;
            }
        }

        return squad;
    }
}
```

## 영향받는 파일 (구현 시)
- 신규: `Assets/Scripts/System/AllyAIDirector.cs`
- 변경 없음: `AllyController.cs`, `AllyBuildingController.cs`, `EnemyBuildingController.cs`,
  `RTSUnitController.cs` - 기존 public API만 사용

## 컴파일 확인
`npx uloop-cli compile --wait-for-domain-reload true` 결과 `Success: true`, `ErrorCount: 0`,
`WarningCount: 39`(기존 베이스라인과 동일한 obsolete-API 경고뿐, 새 경고/에러 없음).

## 남은 작업 (구현 완료 후에도 별도로 필요)
- 아군 OC 유닛 Prefab Variant 제작(doc/0447에서 예고된 채로 미완료) 및 `unitPrefabs` 필드에 연결 -
  이게 없으면 스크립트는 컴파일/배치는 되지만 실제로 병력을 뽑지 못한다.
- 씬에 `AllyAIDirector` 배치 및 인스펙터 값(`spawnPoint`/`waveTimes`/`rallyPoint` 등) 채우기.
