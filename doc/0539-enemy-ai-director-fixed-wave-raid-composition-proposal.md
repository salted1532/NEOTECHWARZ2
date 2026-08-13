# 0539 - EnemyAIDirector 웨이브/별동대 고정 구성(스타1 AI 스타일) 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
- (부가) "Attack Unit 리스트가 스폰 가능한 유닛의 종류로서 쓰이는 거면, `faction`에 맞춰 자동으로
  채워주는 것도 좋겠다"
- **본 요청**: 스타크래프트1 Custom/Insane AI처럼, 종족(진영)별로 **웨이브마다 정해진 유닛 구성**이
  있어야 한다. 예시로 테란/프로토스/저그의 1~5차 웨이브 구성(예: 테란 1차 Marine 12, 2차 Marine 12 +
  Tank 3...)을 제시함. "웨이브별 구성은 미리 정해져 있고, 진영별로도 정해져 있다"는 방향으로,
  - 공격 웨이브: 웨이브 번호별 구성 패턴을 이 문서가 직접 정해서 진영별로 정리
  - 별동대(점령지 탈환): 매번 3마리씩 나가는 그 구성도 정리
  - "별동대는 공격/점령 각각 따로 리스트로 관리" - doc/0538에서 이미 `garrison`/`raidGarrison`으로
    분리해둔 것을 재확인

이 문서는 제안(+ 콘텐츠 초안)일 뿐, 아직 코드 수정 안 함.

## 기존 구조의 한계
- 지금 웨이브는 "무작위 유닛 N명"(`waveSize × 1.5^waveIndex`, doc/0533)이라 "이 웨이브엔 전차가
  많다" 같은 설계된 편성이 불가능 - 매번 `attackUnitIDs`에서 완전 무작위로 뽑힘.
- 별동대(`raidSquadSize`)도 헤드카운트만 있고 구성은 무작위.
- `attackUnitIDs`(`List<int>`)가 웨이브/별동대 공용 "랜덤 로스터"로만 쓰여서, 유닛 **종류의 비율**을
  전혀 통제할 수 없음.

## 실제 유닛 로스터 조사 (설계 콘텐츠 작성을 위해 확인)

### OC (`OC Unit Data SO.asset`) - 8종 전투 유닛 + 1종 일꾼
| ID | 이름 | 티어 | 지상/공중 | HP | 공격력 | 사거리 | 인구 |
|---|---|---|---|---|---|---|---|
| 1 | Nanobot Repair | 0 | 지상만 | 40 | 5 | 4 | 1 | (일꾼 - 전투 편성 제외) |
| 2 | Cyborg Soldier | 1 | 지상+공중 | 40 | 5 | 12 | 1 |
| 3 | Striker | 1 | 지상만 | 75 | 6 | 14 | 2 |
| 4 | Railgunner | 1 | 지상+공중 | 45 | 10 | 20 | 1 |
| 5 | Brute Mech | 2 | 지상만 | 180 | 14 | 6 | 2 |
| 6 | Heavy Assault Tank | 2 | 지상만 | 150 | 20 | 20 | 2 |
| 7 | Ironhawk | 2 | 공중만 | 125 | 16 | 18 | 2 |
| 8 | Raven | 3 | 지상+공중 | 150 | 8 | 18 | 2 |
| 9 | Strike Drone | 3 | 지상+공중 | 400 | 25 | 20 | 6 |

### Spore Brood (`Spore Brood Unit Data SO.asset`) - 3종, 전부 티어 0
| ID | 이름 | 근접/원거리 | 지상/공중 | HP | 공격력 | 사거리 | 인구 |
|---|---|---|---|---|---|---|---|
| 10 | Ripfang(립팽) | 근접 | 지상만 | 60 | 9 | 2 | 1 |
| 11 | Spitter(스피터) | 원거리 | 지상+공중 | 50 | 11 | 13 | 2 |
| 12 | Skitterwing(스키터윙) | 원거리, 유일한 비행 | 지상+공중 | 65 | 8 | 11 | 2 |

OC는 티어가 나뉘어 있어 SC1 테란처럼 "보병 → 기갑 → 공중"으로 단계적 확장이 자연스럽고, Spore Brood는
티어 구분이 아직 없어(전부 0티어) SC1 저그 예시처럼 "적은 종류를 비율/물량으로 계속 바꿔가며 미는" 쪽이
현재 로스터에 맞음.

## 설계안

### 데이터 구조 (신규)
```csharp
[System.Serializable]
public class UnitGroup
{
    public int unitID;
    public int count;
}

[System.Serializable]
public class WaveComposition
{
    public List<UnitGroup> units;
}
```

### 필드 변경
| 기존 | 변경 |
|---|---|
| `int waveSize`, `int maxWaveSize`(doc/0533, 1.5배 자동 성장) | **삭제** - 성장 자체를 웨이브별 구성에 손으로 미리 심어둠(SC1처럼) |
| `int raidSquadSize`(헤드카운트만) | **삭제** - `List<UnitGroup> raidSquadComposition` 하나로 대체(총 인원 = 그 리스트 합) |
| (신규) | `List<WaveComposition> attackWaves` - `waveTimes[i]`에 대응하는 `attackWaves[i]` |
| `List<int> attackUnitIDs`(랜덤 로스터) | **역할 축소 또는 삭제** - 아래 "attackUnitIDs를 어떻게 할지" 참고 |

### 웨이브 타이밍과 결합
`waveTimes[i]`가 오면 `attackWaves[i]`를 보낸다. `attackWaves`가 `waveTimes`보다 짧으면(둘 다 리스트를
다 쓰고 반복 구간에 들어가면) **마지막 구성을 계속 반복** - `waveTimes` 자체가 이미 그렇게 동작하도록
되어 있으므로(doc/0532 결정 사항 #2) 같은 패턴을 구성 쪽에도 적용.

### `garrison`/`raidGarrison` 재고 관리 - "숫자"에서 "구성"으로
지금까지는 "머릿수만 맞으면 아무 유닛"이었지만, 이제 "다음에 나갈 정확한 유닛 조합"을 갖추고 있어야
한다. `FillPool`을 구성 인식형으로 바꿔서, 다음에 필요한 `WaveComposition`/`raidSquadComposition`이
요구하는 유닛 종류별 개수를 풀에서 세어보고 부족한 종류만 그 ID로 스폰한다(현재처럼 무작위 ID가 아니라
구성이 지정한 ID로).

### 별동대(`raidGarrison`)는 이미 분리되어 있음 (doc/0538 재확인)
"공격/점령 각각 따로 리스트" 요청은 doc/0538에서 이미 구현 완료됨(`garrison` vs `raidGarrison`,
`TakeSquad(pool, size)`). 이번 변경은 그 위에 "각 풀이 어떤 구성을 채워야 하는지"만 추가하는 것 -
풀 자체를 다시 나눌 필요는 없음.

## 콘텐츠 초안 - OC 공격 웨이브 (SC1 테란 예시와 같은 형식)
| 웨이브 | 구성 | 의도 |
|---|---|---|
| 1차 | Cyborg Soldier×10 | 저렴한 보병 러시(테란 "Marine 12"에 대응) |
| 2차 | Cyborg Soldier×8 + Railgunner×3 | 대장갑 화력 추가 |
| 3차 | Cyborg Soldier×8 + Striker×3 + Brute Mech×2 | 2티어 근접 기갑 등장 |
| 4차 | Cyborg Soldier×6 + Heavy Assault Tank×3 + Ironhawk×2 | 중전차 + 대공기 투입 |
| 5차(이후 반복) | Heavy Assault Tank×3 + Raven×2 + Strike Drone×1 | 3티어 공중 전력 포함 대편성 |

## 콘텐츠 초안 - Spore Brood 공격 웨이브 (SC1 저그 예시와 같은 형식 - 티어 없이 물량/비율로 변화)
| 웨이브 | 구성 | 의도 |
|---|---|---|
| 1차 | Ripfang×14 | 근접 물량 러시(저그 "Zergling 12"에 대응) |
| 2차 | Ripfang×10 + Spitter×5 | 원거리 지원 추가 |
| 3차 | Spitter×8 + Skitterwing×4 | 원거리/공중 견제 위주로 전환 |
| 4차 | Ripfang×12 + Spitter×8 | 대규모 복합 스웜 |
| 5차(이후 반복) | Ripfang×10 + Spitter×8 + Skitterwing×6 | 세 종류 총동원 |

## 콘텐츠 초안 - 별동대(점령지 탈환) 고정 구성
현재 기본값(`raidSquadSize = 3`)에 맞춰 총 3명으로 제안 - doc/0532의 "OC 소규모정예 vs SporeBrood
다수(4~6)" 제안 표와는 달리, 이번 요청은 "매번 3마리씩"이라 두 진영 다 3명으로 통일함(다르게 하고
싶으면 리스트 내용만 조정하면 됨).

| 진영 | 구성 |
|---|---|
| OC | Cyborg Soldier×2 + Striker×1 (정찰 겸 처리조) |
| Spore Brood | Ripfang×2 + Spitter×1 |

## `attackUnitIDs`를 어떻게 할지 (확인 필요 #1)
구성 리스트(`attackWaves`/`raidSquadComposition`)가 어떤 유닛을 쓸지 전부 명시하므로, 스폰 로직이
더는 `attackUnitIDs`를 무작위로 뽑아 쓸 필요가 없어짐 - 즉 이 필드가 **사실상 안 쓰이게 됨**. 두 선택지:
1. **필드 삭제** - 죽은 코드를 안 남기는 쪽(ponytail 원칙상 기본으로 권장).
2. **유지 + `faction` 기반 자동 채움** - 스폰 로직엔 안 쓰이지만, "이 director가 다룰 수 있는 유닛
   전체 목록" 참고용/검증용으로 남겨두고, `RTSUnitController`에 새 메서드
   `GetEnemyUnitRoster(EnemyFaction faction)`를 추가해 `Start()`에서 비어있으면 자동 채움.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **`attackUnitIDs`**: 필드 삭제(스폰 로직에서 안 쓰이므로 죽은 코드로 안 남김). `RTSUnitController`
   변경 없음(선택 사항 2 채택 안 함).
2. **doc/0533의 1.5배 자동 성장**: 완전히 폐기 - `waveSize`/`maxWaveSize`/`CurrentWaveSize()` 전부
   삭제, `attackWaves`(구성 리스트) + `CurrentWaveComposition()`으로 대체.
3. **예시 구성표 수치**: 제안한 그대로 사용(OC/Spore Brood 각 1~5차 + 별동대 구성).
4. **`attackWaves` 리스트 소진 후**: 마지막 구성을 계속 반복(`waveTimes`와 동일한 패턴, doc/0532
   결정 사항 #2 재사용).

부수 변경(설계안에 명시한 대로 자연스럽게 같이 정리됨): `raidSquadSize`/`raidGarrisonTarget`(doc/0538,
헤드카운트만 있던 필드)도 `raidSquadComposition`으로 대체되면서 삭제 - 별동대 총 인원은 이제 그
리스트의 합으로 결정됨. `garrisonTarget`(doc/0532/0533, 고정 헤드카운트)도 삭제 - "다음 웨이브가
필요로 하는 구성"이 그 자리를 대신함.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`

## 코드 변경

### 기존 코드 (필드)
```csharp
public enum EnemyFaction { OC, SporeBrood }
```
```csharp
[Header("스폰")]
[SerializeField] private Transform spawnPoint;
[SerializeField] private List<int> attackUnitIDs; // GetEnemyUnitData(id)로 조회할 enemyUnitID 목록
```
```csharp
[Header("공격 웨이브")]
[SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
[SerializeField] private int waveSize = 4; // 첫 웨이브(waveIndex==0) 인원수 - 이후 웨이브마다 1.5배(doc/0533)
[SerializeField] private int maxWaveSize = 20; // 1.5배씩 계속 커지는 걸 막는 상한(0이면 무제한, doc/0533)
```
```csharp
[Header("수비대 유지")]
[SerializeField] private int garrisonTarget = 6;
[SerializeField] private float reinforceCheckInterval = 20f;
```
```csharp
[Header("점령지 탈환")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;
[SerializeField] private int raidSquadSize = 3;
[SerializeField] private int raidGarrisonTarget = 3; // 별동대 전용 대기 인원 - 웨이브와 안 겹치게 별도 풀(doc/0538), 고정값(늘거나 줄지 않음)
```

### 변경 코드 (필드)
```csharp
public enum EnemyFaction { OC, SporeBrood }

// 유닛 종류(ID) + 마릿수 하나의 묶음. 웨이브/별동대 구성의 최소 단위(doc/0539).
[System.Serializable]
public class UnitGroup
{
    public int unitID;
    public int count;
}

// 웨이브 하나의 고정 구성(예: "Cyborg Soldier 8 + Railgunner 3") - 스타크래프트1 Custom AI처럼
// 웨이브 번호별로 미리 정해진 편성을 쓴다(doc/0539).
[System.Serializable]
public class WaveComposition
{
    public List<UnitGroup> units;
}
```
```csharp
[Header("스폰")]
[SerializeField] private Transform spawnPoint;
```
```csharp
[Header("공격 웨이브")]
[SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
[SerializeField] private List<WaveComposition> attackWaves; // waveTimes[i]에 대응하는 고정 구성(doc/0539) - 리스트가 짧으면 마지막 구성 반복
```
```csharp
[Header("수비대 유지")]
[SerializeField] private float reinforceCheckInterval = 20f;
```
```csharp
[Header("점령지 탈환")]
[SerializeField] private List<CaptureSystem> raidTargets;
[SerializeField] private float raidInterval = 45f;
[SerializeField] private List<UnitGroup> raidSquadComposition; // 별동대 고정 구성(doc/0539) - 매번 항상 이 조합 그대로
```

### 기존 코드 (스폰/취합 로직)
```csharp
private IEnumerator LaunchWave()
{
    int size = CurrentWaveSize();
    waveIndex++;

    garrisonTarget = Mathf.Max(garrisonTarget, size + 2);

    List<EnemyUnitController> squad = TakeSquad(garrison, size);
    ...
}

private int CurrentWaveSize()
{
    int size = Mathf.RoundToInt(waveSize * Mathf.Pow(1.5f, waveIndex));
    return maxWaveSize > 0 ? Mathf.Min(size, maxWaveSize) : size;
}
```
```csharp
List<EnemyUnitController> squad = TakeSquad(raidGarrison, raidSquadSize); // RaidRoutine()
```
```csharp
FillPool(garrison, garrisonTarget);
FillPool(raidGarrison, raidGarrisonTarget);
```
```csharp
private void FillPool(List<EnemyUnitController> pool, int target)
{
    while (pool.Count < target)
    {
        EnemyUnitController unit = SpawnUnit();
        if (unit == null) break;
        pool.Add(unit);
    }
}

private EnemyUnitController SpawnUnit()
{
    if (attackUnitIDs.Count == 0 || rtsController == null) return null;

    int id = attackUnitIDs[Random.Range(0, attackUnitIDs.Count)];
    UnitData data = rtsController.GetEnemyUnitData(id);
    if (data == null || data.Prefab == null) return null;

    GameObject spawned = Instantiate(data.Prefab, spawnPoint.position, spawnPoint.rotation);
    return spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit) ? unit : null;
}
```
```csharp
private List<EnemyUnitController> TakeSquad(List<EnemyUnitController> pool, int size)
{
    pool.RemoveAll(u => u == null);

    List<EnemyUnitController> squad = new List<EnemyUnitController>();
    foreach (EnemyUnitController unit in pool)
    {
        if (squad.Count >= size) break;
        if (deployed.Contains(unit)) continue;

        squad.Add(unit);
        deployed.Add(unit);
    }

    return squad;
}
```

### 변경 코드 (스폰/취합 로직)
```csharp
private IEnumerator LaunchWave()
{
    List<UnitGroup> composition = CurrentWaveComposition();
    waveIndex++;

    List<EnemyUnitController> squad = TakeSquad(garrison, composition);
    ...
}

// 이번 웨이브에 보낼 구성 - attackWaves[waveIndex], 리스트를 넘어서면 마지막 구성을 계속 반복한다
// (doc/0539, waveTimes 반복과 동일한 패턴).
private List<UnitGroup> CurrentWaveComposition()
{
    if (attackWaves.Count == 0)
        return new List<UnitGroup>();

    int index = Mathf.Min(waveIndex, attackWaves.Count - 1);
    return attackWaves[index].units;
}
```
```csharp
List<EnemyUnitController> squad = TakeSquad(raidGarrison, raidSquadComposition); // RaidRoutine()
```
```csharp
// 다음에 나갈 웨이브(아직 발사 안 한 waveIndex)의 구성을 미리 갖춰둔다 - 웨이브가 실제로 발사되는
// 순간 그제서야 스폰하면 도착까지 시간이 안 맞으므로 항상 선제적으로 채워둔다.
FillPool(garrison, CurrentWaveComposition());
FillPool(raidGarrison, raidSquadComposition);
```
```csharp
// composition이 요구하는 유닛 종류별 개수에 pool이 못 미치면 그 종류로 스폰해서 채운다(doc/0539) -
// 예전처럼 "머릿수만" 채우는 게 아니라 "정확한 조합"을 유지한다. 스폰이 실패하면(데이터를 못 찾음
// 등) 그 종류는 포기하고 다음 종류로 넘어간다 - 안 그러면 무한 루프에 빠진다(doc/0538에서 도입한
// 가드와 동일한 이유).
private void FillPool(List<EnemyUnitController> pool, List<UnitGroup> composition)
{
    foreach (UnitGroup group in composition)
    {
        int have = pool.FindAll(u => u != null && u.GetEnemyUnitID() == group.unitID).Count;

        while (have < group.count)
        {
            EnemyUnitController unit = SpawnUnit(group.unitID);
            if (unit == null)
                break;

            pool.Add(unit);
            have++;
        }
    }
}

private EnemyUnitController SpawnUnit(int unitID)
{
    if (rtsController == null)
        return null;

    UnitData data = rtsController.GetEnemyUnitData(unitID);
    if (data == null || data.Prefab == null)
        return null;

    GameObject spawned = Instantiate(data.Prefab, spawnPoint.position, spawnPoint.rotation);
    return spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit) ? unit : null;
}
```
```csharp
// pool에서 composition이 요구하는 유닛 종류별 개수만큼(아직 원정 안 나간 것만) 뽑아 deployed에
// 등록한다 - 뽑힌 유닛은 이후 재사용(다른 웨이브/별동대/기지 방어 소집)되지 않는다. 특정 종류가
// 부족하면 그만큼만 못 채우고 반환한다(ReinforceRoutine이 미리 채워두므로 평소엔 안 부족함). 웨이브는
// garrison, 별동대는 raidGarrison을 넘겨 서로 다른 풀에서 뽑는다(doc/0538).
private List<EnemyUnitController> TakeSquad(List<EnemyUnitController> pool, List<UnitGroup> composition)
{
    pool.RemoveAll(u => u == null);

    List<EnemyUnitController> squad = new List<EnemyUnitController>();
    foreach (UnitGroup group in composition)
    {
        int taken = 0;
        foreach (EnemyUnitController unit in pool)
        {
            if (taken >= group.count)
                break;
            if (deployed.Contains(unit) || unit.GetEnemyUnitID() != group.unitID)
                continue;

            squad.Add(unit);
            deployed.Add(unit);
            taken++;
        }
    }

    return squad;
}
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 기존과 동일한 39개 - 전부 프로젝트 전역의 기존
`FindFirstObjectByType` obsolete 경고).

## 남은 작업
씬의 `EnemyAIDirector` 인스펙터에 실제로 `attackWaves`/`raidSquadComposition` 리스트를 위 콘텐츠
초안(OC/Spore Brood 표)대로 채워 넣는 건 미션 제작 단계 - 코드는 구성을 읽어 쓸 준비만 됐고, 데이터
자체는 아직 인스펙터에 입력 안 됨.
