# 0610. `EnemyAIDirector` 2방향 공격 레인(스폰 건물 + 랠리포인트 세트) 제안

**날짜:** 2026-08-19

## 요청 내용
> 서브미션4를 구성하면서 2방향에서 공격이 왔으면 좋겠어서 Rallypoint를 2개 두고 번갈아 가면서
> 유닛을 집결시켜서 그 랠리포인트에서 출발시키고 싶은데 그렇게 되면 스폰 건물도 렐리포인트당
> 따로 두고 싶은데 리스트를 통해서 각각 따로 지정할수 있도록 할수 있어?

## 조사 결과 (현재 코드 상태)

`EnemyAIDirector.cs`(공격 웨이브/별동대/기지방어를 담당하는 공용 AI 관제소, [[0602-sub-mission-4-planning|0602]]에서
서브미션4가 이 스크립트의 웨이브 스포너를 그대로 재사용하기로 확정함)엔 이미 스폰 지점을 여러 개
둘 수 있는 `List<EnemySpawnPoint> spawnPoints`가 있지만:

- **집결지(`rallyPoint`)는 director 전체에 딱 하나**(`Transform rallyPoint`, 비워두면
  `spawnPoints[0]`) - 스폰 지점이 여러 곳이어도 생산된 유닛은 전부 같은 한 곳에 모임.
- **생산 대기열(`spawnQueues`)도 director 전체가 공유하는 풀 하나** - `LeastLoadedQueue()`가 전체
  스폰 지점 중 가장 한가한 곳에 자동 분산 생산하므로, "이 스폰 건물은 이 방향 웨이브 전용"이라는
  구분이 없음.
- 즉 지금 구조로는 "스폰 건물 A → 집결지 A / 스폰 건물 B → 집결지 B"처럼 방향별로 짝을 지을 방법이
  없음 - 요청하신 기능은 새로 설계가 필요함.

## 설계안 - `AttackLane`(공격 레인) 리스트 도입

### 핵심 아이디어
"레인(방향)"이라는 새 단위를 만들어 `스폰 지점(+생산 건물) 리스트` + `그 레인 전용 집결지`를 한
세트로 묶는다. 웨이브는 `waveIndex % 레인 개수`로 레인을 라운드로빈 선택 - 한 웨이브는 통째로
한 레인에서만 생산되고 그 레인의 집결지에 모였다가 출발한다(요청하신 "번갈아 가면서" 그대로).

```csharp
// spawnPoints/rallyPoint를 방향(레인) 단위로 묶은 세트. 웨이브가 waveIndex 순서대로 이 레인들을
// 라운드로빈으로 골라 그 레인 전용 스폰 지점에서 생산하고 그 레인 전용 rallyPoint에 집결한다(doc/0610).
[System.Serializable]
public class AttackLane
{
    public List<EnemySpawnPoint> spawnPoints; // 이 레인 전용 스폰 지점(+생산 건물) - 비워두면 이 레인은 생산 불가
    public Transform rallyPoint; // 이 레인 전용 집결지 - 비워두면 spawnPoints[0] 위치를 집결지로 사용

    [Header("<디버그> 이 레인의 현재 웨이브 병력")]
    public List<EnemyUnitController> garrison = new List<EnemyUnitController>();
}
```

`EnemyAIDirector`에 추가:
```csharp
[Header("<공통> 공격 방향(레인) - 2개 이상 두면 웨이브가 waveIndex 순서대로 이 레인들을 번갈아가며(라운드로빈)\n"
      + "사용해 그 레인 전용 스폰 지점에서 생산하고 그 레인 전용 rallyPoint에 집결한 뒤 출발한다. 비워두면\n"
      + "기존 spawnPoints/rallyPoint/garrison으로 구성된 단일 레인 1개로 그대로 동작한다(doc/0610).")]
[SerializeField] private List<AttackLane> attackLanes;
```

### 기존 미션과의 하위 호환 (핵심 - 이 부분이 안전장치)

`attackLanes`가 비어 있으면(기본값), 기존 `spawnPoints`/`rallyPoint`/`garrison` 필드 3개를 그대로
묶어 "레인 1개"로 취급한다:

```csharp
List<AttackLane> configuredLanes = attackLanes.Count > 0
    ? attackLanes
    : new List<AttackLane> { new AttackLane { spawnPoints = spawnPoints, rallyPoint = rallyPoint, garrison = garrison } };
```

→ **Mission1~5, Sub_Mission1~3 등 기존에 `attackLanes`를 설정하지 않은 모든 미션은 인스펙터 값을
안 건드려도 지금과 100% 동일하게 동작함.** `EnemyAIDirector`는 이 프로젝트의 공용 적 AI 스크립트라(
[[0532]]~[[0582]] 등 수십 번 튜닝됨) 기존 미션에 영향이 가면 안 되므로, 이 하위 호환 경로가 이번
설계의 전제 조건임.

### 웨이브/생산 파이프라인 변경 범위

- `waveIndex % 레인 개수`로 이번 웨이브가 쓸 레인을 고르는 `CurrentLane()` 추가.
- 생산 완료 후 이동 목적지: `DefaultRallyPosition()`(전역 단일 집결지) → `LaneRallyPosition(레인)`(그
  레인의 집결지, 없으면 그 레인 `spawnPoints[0]` 위치)로 교체.
- 생산 대기열(`spawnQueues`)과 웨이브 병력 풀(`garrison`)을 레인별로 분리 - `LeastLoadedQueue()`가
  "그 레인 소속 스폰 지점 중에서만" 가장 한가한 곳을 고르도록 스코프 한정. → 이게 있어야
  "스폰 건물 A는 레인 A 웨이브만 생산"이 실제로 보장됨(안 그러면 전역 최적화 때문에 레인 A 몫이
  레인 B의 스폰 건물에서 생산돼버릴 수 있음).
- 점령지 탈환 별동대(`raidGarrison`)와 기지방어 대체 생산(`defenseSlots`)은 레인 개념과 무관하므로
  **모든 레인의 스폰 지점을 합친 전체 풀**에서 그대로 지금처럼 자동 분산 생산(레인이 하나뿐이면
  기존과 동일).
- `AttackWaveRoutine`/`LaunchWave`/`AssembleAtRally`/`ReinforceRoutine`이 전역 `garrison`/`spawnPoints`/
  `rallyPoint` 대신 "이번 웨이브가 쓸 레인"의 값을 사용하도록 수정.

### 부수 효과 (레인을 실제로 2개 이상 설정했을 때만 해당)

점령 별동대/기지방어 대체 생산 유닛은 생산 직후 "생산된 그 레인의 집결지"로 이동하게 됨(기존엔
항상 전역 단일 집결지였음). 레인이 1개(기존 미션)면 지금과 완전히 동일 - 2개 이상 설정한 미션(이번
서브미션4)에서만 나타나는 차이이고, "생산된 곳 근처로 모인다"는 의미상 자연스러운 부수 효과라고
판단함(문제가 되면 후속으로 이 부분만 전역 집결지로 되돌리는 것도 가능).

## 씬 구성 (Sub_Mission4, 실제 적용 시)

`EnemyAIDirector` 컴포넌트에서:
1. `Attack Lanes` 리스트에 엔트리 2개 추가.
2. 레인 0: 방향 A의 스폰 지점(생산 건물 포함) + 방향 A 집결지(Transform).
3. 레인 1: 방향 B의 스폰 지점(생산 건물 포함) + 방향 B 집결지(Transform).
4. 기존 `Spawn Points`/`Rally Point` 필드는 레인을 쓰는 순간 더 이상 안 쓰임(비워두거나 그대로
   남겨둬도 무해함 - 참조되지 않음).

`waveTimes`/`attackWavesOC`(또는 `attackWavesSporeBrood`)는 기존과 동일하게 설정하면 되고, 웨이브가
0, 1, 2, 3...번째로 발사될 때마다 레인 0 → 1 → 0 → 1... 순서로 자동으로 번갈아감.

## 변경 예정 파일
- `Assets/Scripts/System/EnemyAIDirector.cs`

## 상태
**적용 완료** — 사용자 확인 후 위 설계안 그대로 `EnemyAIDirector.cs`에 반영함(설계와 구현 간 차이
없음): `AttackLane` 클래스 추가, `attackLanes` 필드 추가, `spawnQueues`/`garrison`을 레인별
(`LaneRuntime`)로 분리, `DefaultRallyPosition()` → `LaneRallyPosition(레인)`으로 교체,
`FillPool`/`EnqueueOrder`/`LeastLoadedQueue`/`PendingCount`가 대상 큐 목록(`List<SpawnQueue>`)을
인자로 받도록 변경 + 레인 전체를 합치는 `AllSpawnQueues()` 추가(별동대/방어 슬롯 생산용).

`npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
`WarningCount: 49`(직전 커밋과 동일 - 새로 생긴 경고 없음). `attackLanes`는 다른 `List<T>` 필드들
(`spawnPoints`/`homeBuildings` 등)과 동일하게 초기화자 없이 선언 - 이 프로젝트 관례상 Unity
역직렬화 시 항상 빈 리스트로 채워지므로 기존 씬(이 필드가 아예 없던 씬)에서도 null이 아님을
코드 리딩으로 확인함.

씬 배치(Sub_Mission4에 `Attack Lanes` 리스트 2개 채우기)는 아직 진행 전 - 사용자가 직접 진행할지,
이어서 요청할 항목.

**후속 권장**: 이 스크립트는 Mission1~5가 공유하는 핵심 AI 시스템이라, 실제 플레이 테스트 시
기존 미션(레인 미설정) 중 하나에서 웨이브가 정상적으로 발사/집결/전멸까지 도는지 한 번 재확인해두는
것을 권장함(설계상 회귀는 없어야 하지만, 이 파일은 doc/0532~0582에 걸쳐 세밀하게 튜닝된 이력이 있음).
