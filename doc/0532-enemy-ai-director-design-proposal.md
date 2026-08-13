# 0532 - Enemy AI Director 설계안 → 구현 완료

## 날짜
2026-08-12 (최초 작성) / 2026-08-13 (수정 + 구현)

## 수정 요청 (2026-08-13)
"0532에서 적이 시간에 맞춰서 공격병력을 모아서 공격을 하는 위치에 대한 설명이 없는거 같은데 + 점령지의
위치 등 위치 정보에 대해 어떻게 처리할지도 생각해서 수정해줘"

→ 최초안은 "웨이브 시각이 되면 garrison에서 뽑아 attackTarget으로 보낸다"까지만 있었고, **어디서
모여서 출발하는지**(집결지)가 빠져 있었음. 또 director가 다루는 위치 정보(스폰/집결/공격 목표/점령지)가
타입도 제각각이고 왜 그렇게 골랐는지 설명이 없었음. 아래 "집결지(rallyPoint)" 관련 내용과 "위치 정보
처리" 절을 추가로 반영.

## 요청 내용
"Enemy AI 구현(스크립트로 동작하는) Enemy Controller를 조종하는 스크립 제작"
- 시간에 맞춰서 공격병력을 모아서 공격 (ex: 5분/10분/15분 간격)
- 점령지에 별동대를 보내서 점령지 탈환
- 공격받았을 시 주변 적 유닛을 해당 지역으로 보내서 방어
- 적 유닛이 죽으면 다시 생산하여 추가 병력을 보내 죽은 유닛을 매꿈
- 적 OC / 외계종족(Spore Brood) 2가지 진영에 따라 다른 유닛(프리팹)이 생산되고 다른 방식으로 작동

사용자가 먼저 설계안 검토를 원함 → **이 문서는 제안일 뿐, 아직 코드 작성 안 함.**

## 기존 코드 조사

### 지금 "적"이 어떻게 동작하는가 (AI 없음)
`EnemyBuildingController.cs` 상단 주석에 명시: *"캠페인은 정해진 스크립트/트리거로 적 유닛을 직접
배치·스폰할 예정이라, 적 건물이 실제로 생산 큐/자원 소모/건설 그리드 같은 걸 가질 필요가 없다"* -
지금은 미션 제작자가 씬에 적 유닛/건물을 손으로 배치해두는 것이 전부이고, "AI 관제소" 같은 자동 판단
주체는 존재하지 않는다. 이번 요청은 그 빈 자리를 처음 채우는 것.

### 진영 2개가 이미 데이터 레벨에서 분리돼 있음
- **OC**(오메가 코퍼레이션, 인간형) - `Assets\prefabs\OC\Unit`, `Assets\prefabs\OC\Building`.
  `RTSUnitController.enemyUnitDatabase` / `enemyBuildingDatabase` (`EnemyUnitDataSO`/`EnemyBuildingDataSO`).
- **Spore Brood**(외계종족, 유기체) - `Assets\prefabs\Spore_Brood\Unit`(Ripfang/Skitterwing/Spitter),
  `Assets\prefabs\Spore_Brood\Building`(Hive Core/Spawning Pit/Bio-Reactor).
  `RTSUnitController.sporeBroodUnitDatabase` / `sporeBroodBuildingDatabase`, ID 대역이 OC와 겹치지 않게
  분리돼 있고(`RTSUnitController.cs:77-82`), `GetEnemyUnitData()`/`GetEnemyBuildingData()`가 OC 쪽에서
  못 찾으면 자동으로 Spore Brood 쪽을 조회한다(doc/0444).
- 두 진영 모두 **같은 컴포넌트**(`EnemyUnitController`/`EnemyBuildingController`)를 쓴다 - 차이는 어떤
  SO 데이터베이스의 ID를 참조하느냐뿐, 클래스가 분리돼 있지 않다.
- `UnitData`(`UnitDataSO.cs:133`)에 이미 `Prefab` 필드가 있어서, `enemyUnitID`만 있으면
  `rtsController.GetEnemyUnitData(id).Prefab`로 바로 Instantiate 가능 (`UnitSpawner.Spawn()`과 동일한 패턴).

### 재사용 가능한 기존 기능
- `EnemyUnitController.MoveTo(Vector3)` / `AttackMoveTo(Vector3)` - 이동 중 사거리 안에 들어오는 상대와
  자동 교전(`EnemyAttackRange`). 공격대/별동대 이동 명령은 이 둘로 충분하고, 새 이동 로직이 필요 없다.
- `EnemyUnitController.HandleAttacked()` (`EnemyUnitController.cs:133-142`) - 개별 유닛은 이미 "사거리
  밖에서 맞으면 공격자 쪽으로 반격하러 감"을 자체적으로 한다. 이번에 새로 필요한 건 유닛 단위가 아니라
  **건물(기지)이 공격받았을 때 주변 유닛을 소집**하는 상위 레벨 반응 - 건물은 `HealthManager`만 있고
  반응 로직이 없음.
- `HealthManager.OnDamaged(int damage, Vector3 attackerPosition, AttackEffectType type, bool isEnemyAttacker)` -
  `isEnemyAttacker == false`일 때만 "플레이어에게 맞았다"는 뜻(`EnemyUnitController.HandleAttacked`와
  동일한 판정 재사용).
- `CaptureSystem` / `TerritoryZone` / `TerritoryManager.Zones` - 점령지 소유 상태(`CaptureOwner`)를 이미
  들고 있고, 유닛이 트리거 콜라이더 안에 서 있기만 하면 자동으로 점령이 진행된다. 별동대는 그냥 그 위치로
  `AttackMoveTo()` 시키면 끝 - 새 점령 로직 불필요.
- `EnemyBuildingController.ActiveBuildings`(static list) - 참고용 패턴. 이번엔 director가 "자기가 만든
  유닛"만 추적하면 되므로 전역 리스트는 새로 안 만들고 director 인스턴스 안에 로컬 리스트로 둔다(아래 참고).
- `BuildingController.RallyPosition` / `SetRallyPosition()` / `GetRallyPos()`(`BuildingController.cs:32-33,
  444-458`, doc/0529~0531) - 플레이어 건물이 이미 "생산된 유닛이 모이는 위치(집결지)"를 갖고 있고, 기본값은
  `transform.position + (0,0,-5)`(건물 바로 앞), 미션 제작자/플레이어가 씬에서 옮길 수 있다. 이번 요청에서
  빠져 있던 "공격병력이 모이는 위치"가 정확히 이 개념 - 새로 발명하지 않고 같은 패턴(기본값 = 스폰 지점
  근처, 인스펙터에서 재배치 가능)을 director에도 적용한다.

## 설계 개요

### 컴포넌트: `EnemyAIDirector` (신규, MonoBehaviour 1개)
**미션(씬)마다, 적 기지 하나당 1개** 배치하는 방식 - 지금 게임이 "미션별로 손으로 세팅"하는 캠페인
구조이므로, 이 director도 인스펙터에서 미션별로 값을 채워 넣는 스크립트형 트리거에 가깝다(완전 자동
범용 RTS AI 경제 시뮬레이터를 새로 만드는 게 아님).

```
EnemyAIDirector
├─ [진영] EnemyFaction faction  (OC | SporeBrood)
├─ [스폰] Transform spawnPoint, List<int> attackUnitIDs (이 진영이 생산할 enemyUnitID 목록)
├─ [기지 방어] EnemyBuildingController homeBuilding (공격받으면 방어 소집 트리거)
├─ [공격 웨이브] List<float> waveTimes (초, 미션 시작 후 경과 시각 - ex: 300/600/900),
│               int waveSize, Transform attackTarget (보통 플레이어 본진)
├─ [집결지] Transform rallyPoint (assembleBeforeAttack == true일 때만 사용 - 웨이브 병력이 출발 전
│           모이는 위치. 기본값은 spawnPoint 근처, BuildingController 랠리 포인트와 동일한 발상),
│           float rallyRadius(도착 판정 거리, 기본 3), float rallyTimeout(뒤처지는 유닛을 무한정
│           기다리지 않고 그냥 출발하는 최대 대기 시간, 기본 15초)
├─ [수비대 유지] int garrisonTarget, float reinforceCheckInterval
├─ [점령지 탈환] List<CaptureSystem> raidTargets, float raidInterval, int raidSquadSize
└─ [진영별 동작 차이] bool assembleBeforeAttack 등 튜닝값 (아래 "진영별 차이" 참고)
```

내부적으로 자기가 스폰한 유닛만 담는 로컬 리스트(`List<EnemyUnitController> garrison`)를 들고 관리한다
(전역 레지스트리 불필요 - director 하나가 자기 기지 병력만 책임지는 구조).

### 4가지 요청 동작 → 구현 매핑

1. **시간에 맞춰 공격 웨이브** (`AttackWaveRoutine`, 코루틴) - **집결 위치 포함**
   `waveTimes` 리스트를 순서대로 대기 → 리스트를 다 쓰면 끝내지 않고 **마지막 두 항목의 간격
   (`waveTimes[last] - waveTimes[last-1]`)으로 계속 반복**(사용자 확정 - "결정 사항" #2). 매 웨이브마다
   `garrison`에서 `waveSize`만큼(모자라면 새로 스폰) 뽑아 `waveSquad`로 지정. 이후 `assembleBeforeAttack`
   값에 따라 두 갈래:
   - **OC (`assembleBeforeAttack == true`)**: `waveSquad` 전원에게 `MoveTo(rallyPoint)`(자동교전 없는
     이동 - 집결 중에 옆에서 싸움 걸려서 대열이 흩어지는 것 방지). 매 프레임 `rallyRadius` 안에 들어온
     인원을 세다가, **전원 도착** 또는 **`rallyTimeout` 경과** 중 먼저 오는 시점에 `waveSquad` 전체를
     `AttackMoveTo(attackTarget)`으로 한꺼번에 출발시킨다("뭉쳐서 진군하는" 인간형 군대 느낌).
   - **Spore Brood (`assembleBeforeAttack == false`)**: 집결 단계 없이, 스폰/차출되는 즉시 개별적으로
     `AttackMoveTo(attackTarget)`(이미 "진영별 차이" 표에 있던 내용 - `rallyPoint`는 이 진영에선 안 쓰임).

   두 경우 모두 이동 중 자동교전 되므로 도중에 만나는 아군과도 알아서 싸운다. `rallyPoint`의 기본 위치는
   `spawnPoint` 바로 앞(예: +Z 5m, `BuildingController`의 기본 랠리 오프셋과 동일한 발상) - 미션 제작자가
   씬에서 직접 옮기면 "본진 앞 공터에서 집결" 대신 "고갯길 어귀에서 집결" 같은 지형 활용도 가능하다.

2. **점령지 탈환 별동대** (`RaidRoutine`, 코루틴)
   `raidInterval`마다 `raidTargets` 중 `CurrentOwner != Enemy`인 곳을 하나 골라(Ally가 뺏어간 곳을
   Neutral보다 우선), `garrison`에서 `raidSquadSize`만큼 떼어 그 지점으로 `AttackMoveTo()`. 도착해서
   트리거 콜라이더 안에 서 있기만 하면 `CaptureSystem`이 알아서 점령을 진행시킨다 - 별도 로직 불필요.

3. **공격받으면 주변 병력 소집** (`homeBuilding.GetHealthManager().OnDamaged += HandleBaseAttacked`)
   `isEnemyAttacker == false`(플레이어에게 맞음)일 때만 반응. `garrison` 중 현재 Idle인 유닛들을 공격
   받은 위치로 `AttackMoveTo()`. (참고: 개별 유닛 자체도 `EnemyUnitController.HandleAttacked`로 이미
   자기 방어를 하므로, 이건 그 위에 "건물이 맞았을 때 근처 병력을 부른다"는 상위 반응만 추가하는 것.)

4. **죽은 유닛 보충 생산** (`ReinforceRoutine`, 코루틴)
   `reinforceCheckInterval`마다 `garrison`에서 null(죽은 유닛) 정리 → 부족한 만큼
   (`garrisonTarget - garrison.Count`) `attackUnitIDs`에서 골라 `spawnPoint`에 Instantiate, `garrison`에
   추가. 웨이브/별동대로 나간 병력도 죽으면 자연히 이 루틴이 다시 채워준다(별도 분기 불필요 - "죽어서
   빈 자리"와 "원정 나가서 빈 자리"를 구분하지 않고 그냥 목표 인원수만 유지).

### 위치 정보 처리 (신규 추가)
director가 다루는 위치는 성격이 서로 달라서, 하나의 타입으로 통일하지 않고 **"누가 그 위치를 소유하고
있는가"** 기준으로 고른다 - 이미 이 프로젝트에 있는 두 가지 관례를 그대로 따른다(스폰류는
`UnitSpawner`/`BuildingController`처럼 `Transform`, 이미 존재하는 게임플레이 오브젝트는 그 컴포넌트
자체를 참조).

| 필드 | 타입 | 왜 이 타입인가 |
|---|---|---|
| `spawnPoint` | `Transform` | 미션 씬에 미리 놓아두는 고정 마커. 기존 `UnitSpawner` 패턴과 동일. |
| `rallyPoint` | `Transform` | 마찬가지로 미션 씬의 고정 마커 - 단, **이 director가 직접 소유하는 좌표가
아니라 "이 지점으로 모여라"라는 지시일 뿐**이라 `BuildingController.RallyPosition`처럼 `Vector3`
필드로 둘 수도 있었지만, 미션 제작자가 씬 뷰에서 드래그해 옮기는 워크플로우(현재 캠페인 전체가 "미션별로
손으로 세팅") 상 `Transform`이 다루기 더 쉬워서 `Transform`으로 통일. `GetRallyPos()`처럼 접근자 뒤에
숨길 필요도 없음 - director 하나만 이 값을 읽으므로 캡슐화 이점이 없다. |
| `attackTarget` | `Transform` | 위와 동일한 이유(고정 마커). 확인 필요 사항 #1에서 "동적으로 찾아야
하는지"를 별도로 묻고 있음 - 그 경우엔 `Transform` 필드를 없애고 매 웨이브 시점에 계산하는 함수로 대체. |
| `raidTargets` | `List<CaptureSystem>` | 점령지는 **이미 존재하는 컴포넌트**(`CaptureSystem`)이고
`transform.position`으로 위치를, `CurrentOwner`로 소유 상태를 동시에 제공한다. 별도 `Vector3`/`Transform`
필드로 위치만 복사해두면 점령지가 옮겨지거나(현재는 없지만) 사라졌을 때 참조가 어긋날 수 있어서, 좌표를
따로 들지 않고 항상 `raidTargets[i].transform.position`을 그 자리에서 읽는다. |
| 피격 위치(항목 3) | `Vector3` (필드 아님, 콜백 파라미터) | `HealthManager.OnDamaged(int, Vector3
attackerPosition, ...)`가 이미 위치를 실어서 넘겨준다 - 저장할 필요 없이 이벤트 핸들러 안에서 바로
`AttackMoveTo(attackerPosition)`에 쓰고 버림. |
| `homeBuilding` | `EnemyBuildingController` | 위치가 목적이 아니라 "이 건물의 `OnDamaged` 이벤트를
구독하기 위한" 참조. 위치가 필요하면(예: 수비 실패 후 복귀 지점) `homeBuilding.transform.position`을
그때그때 읽으면 되고 별도 좌표 필드 불필요. |

**원칙**: 미션 제작자가 씬에서 손으로 배치/재배치하는 지점(스폰/집결/공격 목표)은 `Transform`, 이미
위치를 들고 있는 게임플레이 오브젝트(점령지, 피격 이벤트, 기지 건물)는 별도 좌표를 복사하지 않고 그
오브젝트/이벤트 파라미터를 그대로 참조한다 - 위치를 이중으로 저장하는 필드가 하나도 없다.

### 진영별 차이 (OC vs Spore Brood)
클래스를 두 개로 나누지 않고, `EnemyFaction` enum 하나로 값/분기를 나눈다 - 실제로 갈라지는 부분이
"판단 로직 몇 줄" 수준이라 상속 구조까지는 과함(나중에 진짜 알고리즘 자체가 갈라지면 그때 분리):

| 항목 | OC (인간형) | Spore Brood (외계, 무리형) |
|---|---|---|
| 유닛/건물 프리팹 | `attackUnitIDs`가 OC ID 대역 참조 | Spore Brood ID 대역 참조 (같은 필드, 값만 다름) |
| `assembleBeforeAttack` | true - `rallyPoint`에 웨이브 인원이 다 모일 때까지(또는 `rallyTimeout`까지) 대기 후 한꺼번에 출발 | false - `rallyPoint` 안 쓰고 스폰되는 즉시 개별적으로 `AttackMoveTo` (물량으로 끊임없이 밀어붙이는 느낌) |
| `reinforceCheckInterval` | 상대적으로 느림(예: 20초) | 빠름(예: 8초) - 유충 번식 컨셉 |
| `raidSquadSize` | 소규모 정예(예: 2~3) | 다수(예: 4~6) |

이 표의 구체 수치는 전부 인스펙터 필드라 밸런싱은 기획 값 조정만으로 가능 - 코드에 하드코딩하지 않음.

## 스코프 밖 (안 하는 것)
- 자원 채집/실제 생산 큐/테크트리 기반 AI 경제 시뮬레이션 - `EnemyBuildingController` 자체가 "생산 큐가
  필요 없는 껍데기"로 설계돼 있고(doc 상단 주석), 이번 요청도 "정해진 시간에 병력을 모아 보낸다"는
  스크립트형 동작이라 여기 안 맞음.
- 여러 적 기지가 서로 협조하는 상위 AI(다중 director 간 통신) - 미션에 기지가 여럿이면 director를 여러
  개 배치하면 되고, 지금 단계에서 서로 알 필요는 없음.
- 진형(포메이션)/그룹 경로탐색 - `AttackMoveTo()`를 유닛별로 개별 호출하는 것으로 충분(기존 플레이어
  유닛 이동도 개별 NavMeshAgent 방식).
- 난이도 자동 스케일링(플레이어 병력 규모에 따라 웨이브 크기 조절 등) - 필요해지면 나중에 추가.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
이전 초안의 열린 질문 6가지 전부 답변받아 아래로 확정. **이 문서 기준으로 구현 진행.**

1. **`attackTarget`**: 인스펙터 고정 지정 - 동적 탐색(가장 가까운 플레이어 건물) 안 함. 설계 변경 없음.
2. **웨이브 반복**: `waveTimes`를 다 쓰면 끝나는 게 아니라 **항상 마지막 간격으로 계속 반복**. 사용자가
   "반복 안 함" 옵션을 원하지 않았으므로 별도 `loopIntervalAfterLast` 토글 필드는 안 만들고, 리스트 소진
   후 `waveTimes[last] - waveTimes[last-1]` 간격으로 무한 반복하는 것으로 고정(설정 불필요 = 필드도 불필요).
3. **다중 진영**: 한 미션엔 항상 하나의 진영만 등장. `EnemyFaction faction` 필드가 director 하나당
   하나의 값만 갖는 현재 설계 그대로 - 여러 진영이 필요해지면 그때 director를 더 배치하면 되므로 지금
   구조를 바꿀 이유 없음.
4. **원정 실패 시**: 복귀 로직 없음 - "보내고 끝", 죽으면 `ReinforceRoutine`이 채움. 설계 변경 없음.
5. **`rallyPoint` 기본값**: 제안값 그대로(`spawnPoint` 앞 +Z 5m, `rallyRadius` 3, `rallyTimeout` 15초) 사용.
6. **별동대 집결**: 집결 없이 차출 즉시 개별 출발. 설계 변경 없음(위 "4가지 요청 동작 → 구현 매핑" 2번
   그대로).

## 영향받는 파일 (구현 완료, 2026-08-13)
- 신규: `Assets\Scripts\System\EnemyAIDirector.cs`
- 변경 없음: `EnemyUnitController.cs`, `EnemyBuildingController.cs`, `RTSUnitController.cs`,
  `CaptureSystem.cs` - 설계대로 기존 public API만 사용해서 손댈 필요 없었음

## 구현 코드 (신규 파일이라 기존 코드 없음)

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 어떤 진영(OC/Spore Brood)의 유닛을 이 director의 attackUnitIDs로 표시하는지 구분하는 용도.
// 실제 동작 차이는 이 값으로 분기하지 않고 전부 인스펙터 필드(assembleBeforeAttack 등)로 미션마다
// 직접 세팅한다(doc/0532 "진영별 차이" 참고) - 여기선 식별용 라벨일 뿐.
public enum EnemyFaction { OC, SporeBrood }

// 미션 씬에 적 기지 하나당 하나씩 배치하는 "AI 관제소". 시간에 맞춰 공격 웨이브를 보내고, 점령지에
// 별동대를 보내고, 기지가 공격받으면 병력을 소집하고, 죽은 유닛을 보충 생산한다 (doc/0532 설계안).
public class EnemyAIDirector : MonoBehaviour
{
    [Header("진영")]
    [SerializeField] private EnemyFaction faction;

    [Header("스폰")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private List<int> attackUnitIDs; // GetEnemyUnitData(id)로 조회할 enemyUnitID 목록

    [Header("기지 방어")]
    [SerializeField] private EnemyBuildingController homeBuilding;

    [Header("공격 웨이브")]
    [SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
    [SerializeField] private int waveSize = 4;
    [SerializeField] private Transform attackTarget;

    [Header("집결지 (assembleBeforeAttack일 때만 사용)")]
    [SerializeField] private Transform rallyPoint; // 비워두면 spawnPoint 위치를 그대로 집결지로 사용
    [SerializeField] private float rallyRadius = 3f;
    [SerializeField] private float rallyTimeout = 15f;

    [Header("수비대 유지")]
    [SerializeField] private int garrisonTarget = 6;
    [SerializeField] private float reinforceCheckInterval = 20f;

    [Header("점령지 탈환")]
    [SerializeField] private List<CaptureSystem> raidTargets;
    [SerializeField] private float raidInterval = 45f;
    [SerializeField] private int raidSquadSize = 3;

    [Header("진영별 동작 차이")]
    [SerializeField] private bool assembleBeforeAttack = true; // OC: true, Spore Brood: false 권장(doc/0532)

    private RTSUnitController rtsController;

    // 이 director가 스폰한 유닛 전체(원정 나간 유닛도 죽기 전까진 계속 포함 - ReinforceRoutine이 목표
    // 인원수를 유지하는 기준). null(죽은 유닛)은 각 루틴에서 그때그때 정리한다.
    private readonly List<EnemyUnitController> garrison = new List<EnemyUnitController>();

    // 웨이브/별동대로 이미 내보낸 유닛 - "보내고 끝"(doc/0532 결정 사항 #4)이라 돌아오지 않으므로,
    // 다음 웨이브/별동대 차출이나 기지 방어 소집 대상에서 제외해 같은 유닛을 두 번 부리지 않는다.
    private readonly HashSet<EnemyUnitController> deployed = new HashSet<EnemyUnitController>();

    private void OnEnable()
    {
        if (homeBuilding != null && homeBuilding.GetHealthManager() != null)
            homeBuilding.GetHealthManager().OnDamaged += HandleBaseAttacked;
    }

    private void OnDisable()
    {
        if (homeBuilding != null && homeBuilding.GetHealthManager() != null)
            homeBuilding.GetHealthManager().OnDamaged -= HandleBaseAttacked;
    }

    private void Start()
    {
        rtsController = FindFirstObjectByType<RTSUnitController>();

        while (garrison.Count < garrisonTarget)
            SpawnUnit();

        if (waveTimes.Count > 0)
            StartCoroutine(AttackWaveRoutine());
        if (raidTargets.Count > 0)
            StartCoroutine(RaidRoutine());
        StartCoroutine(ReinforceRoutine());
    }

    // ======================
    // 1. 시간에 맞춰 공격 웨이브
    // ======================
    private IEnumerator AttackWaveRoutine()
    {
        for (int i = 0; i < waveTimes.Count; i++)
        {
            float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
            yield return new WaitForSeconds(wait);
            yield return LaunchWave();
        }

        // 리스트를 다 쓰면 끝내지 않고 마지막 두 항목의 간격으로 계속 반복한다(doc/0532 결정 사항 #2).
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
        List<EnemyUnitController> squad = TakeSquad(waveSize);
        if (squad.Count == 0)
            yield break;

        if (assembleBeforeAttack)
            yield return AssembleAtRally(squad);

        foreach (EnemyUnitController unit in squad)
            if (unit != null)
                unit.AttackMoveTo(attackTarget.position);
    }

    private IEnumerator AssembleAtRally(List<EnemyUnitController> squad)
    {
        Vector3 rally = rallyPoint != null ? rallyPoint.position : spawnPoint.position;

        foreach (EnemyUnitController unit in squad)
            if (unit != null)
                unit.MoveTo(rally);

        float elapsed = 0f;
        while (elapsed < rallyTimeout)
        {
            bool allArrived = true;
            foreach (EnemyUnitController unit in squad)
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
    // 2. 점령지 탈환 별동대
    // ======================
    private IEnumerator RaidRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(raidInterval);
        while (true)
        {
            yield return wait;

            CaptureSystem target = PickRaidTarget();
            if (target == null)
                continue;

            List<EnemyUnitController> squad = TakeSquad(raidSquadSize);
            foreach (EnemyUnitController unit in squad)
                if (unit != null)
                    unit.AttackMoveTo(target.transform.position);
        }
    }

    // Ally가 뺏어간 곳을 Neutral보다 우선(doc/0532).
    private CaptureSystem PickRaidTarget()
    {
        CaptureSystem allyOwned = raidTargets.Find(t => t != null && t.CurrentOwner == CaptureOwner.Ally);
        if (allyOwned != null)
            return allyOwned;

        return raidTargets.Find(t => t != null && t.CurrentOwner == CaptureOwner.Neutral);
    }

    // ======================
    // 3. 공격받으면 주변 병력 소집
    // ======================
    private void HandleBaseAttacked(int damage, Vector3 attackerPosition, AttackEffectType type, bool isEnemyAttacker)
    {
        if (isEnemyAttacker)
            return; // 플레이어에게 맞았을 때만 반응

        garrison.RemoveAll(u => u == null);

        foreach (EnemyUnitController unit in garrison)
            if (!deployed.Contains(unit) && unit.IsIdle())
                unit.AttackMoveTo(attackerPosition);
    }

    // ======================
    // 4. 죽은 유닛 보충 생산
    // ======================
    private IEnumerator ReinforceRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(reinforceCheckInterval);
        while (true)
        {
            yield return wait;

            garrison.RemoveAll(u => u == null);
            deployed.RemoveWhere(u => u == null);

            while (garrison.Count < garrisonTarget)
                SpawnUnit();
        }
    }

    private void SpawnUnit()
    {
        if (attackUnitIDs.Count == 0 || rtsController == null)
            return;

        int id = attackUnitIDs[Random.Range(0, attackUnitIDs.Count)];
        UnitData data = rtsController.GetEnemyUnitData(id);
        if (data == null || data.Prefab == null)
            return;

        GameObject spawned = Instantiate(data.Prefab, spawnPoint.position, spawnPoint.rotation);
        if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
            garrison.Add(unit);
    }

    // ======================
    // 공용
    // ======================

    // garrison 중 아직 원정 나가지 않은(deployed에 없는) 유닛을 앞에서부터 최대 size개 뽑아 deployed에
    // 등록한다 - 뽑힌 유닛은 이후 재사용(다른 웨이브/별동대/기지 방어 소집)되지 않는다.
    private List<EnemyUnitController> TakeSquad(int size)
    {
        garrison.RemoveAll(u => u == null);

        List<EnemyUnitController> squad = new List<EnemyUnitController>();
        foreach (EnemyUnitController unit in garrison)
        {
            if (squad.Count >= size)
                break;
            if (deployed.Contains(unit))
                continue;

            squad.Add(unit);
            deployed.Add(unit);
        }

        return squad;
    }
}
```

## 설계안 대비 구현 시 추가된 부분
설계 문서엔 없었지만 코딩 중 발견해 추가한 것 하나: **`deployed`(HashSet) 추적**.
`AttackMoveTo()`는 자동교전을 위해 이동 중에도 `currentState`를 `Idle`로 유지한다(`EnemyUnitController.cs:251`
주석 참고) - 즉 웨이브로 이미 내보낸 유닛도 `IsIdle()`이 계속 `true`를 반환해서, 이 추적이 없으면
①다음 웨이브가 같은 유닛을 다시 뽑아가거나 ②기지가 공격받았을 때 이미 원정 나간 유닛까지 방어 소집에
잡혀버림. "보내고 끝"(결정 사항 #4)이라는 확정된 설계 의도를 실제로 지키려면 필요한 구현 디테일이라
별도 재확인 없이 반영함 - `garrison`엔 계속 남아있고(그래야 `ReinforceRoutine`이 인원수를 유지) `deployed`에만
추가로 표시되는 방식.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고 39개는 전부 기존 코드에 이미 있던 `FindFirstObjectByType`
obsolete 경고 - 이 프로젝트 전체가 아직 이 API를 쓰고 있어서 새 파일도 같은 컨벤션을 따름, 별도 수정 안 함).

## 남은 작업
씬에 실제로 `EnemyAIDirector`를 배치하고 인스펙터 필드(`spawnPoint`/`attackTarget`/`rallyPoint`/
`raidTargets`/`homeBuilding`/`attackUnitIDs` 등)를 채우는 건 미션 제작 단계 - 스크립트만으로는 아무 씬
오브젝트에도 아직 붙어있지 않음.
