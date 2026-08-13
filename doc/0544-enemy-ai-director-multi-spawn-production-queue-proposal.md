# 0544 - EnemyAIDirector 다중 스폰 지점 + 생산 대기열 + 웨이브 준비 완료 게이트 설계안 → 구현 완료

> 번호 안내: 이 문서는 원래 0541로 작성했으나, 같은 시각 다른 세션에서 EnemyAIDirector.cs를 동시에
> 편집하며 doc/0541~0543(인스펙터 디버그 리스트, AllyAIDirector)을 먼저 채번해서 0544로 옮김. 아래
> 구현은 그 세션이 추가한 `allEnemyUnits`/`allEnemyBuildings` 디버그 필드(doc/0542)를 보존한 채
> 그 위에 적용됨.

## 날짜
2026-08-13

## 요청 내용 (세 메시지에 걸쳐 도착, 하나로 정리)
1. "300초(5분)마다 웨이브인데, 20마리까지 늘어나면 1마리당 20초씩 걸려서 생산해서 모을 수 없어 보인다.
   spawnPoint도 리스트로 여러 곳 지정할 수 있게 하고, 진영별 유닛 생산 속도를 정해달라. OC는 NTA 유닛
   값을 그대로 쓰면 되고 Spore Brood는 임의로 정해서 관련 문서도 갱신해달라. spawnPoint가 3곳이면
   20마리를 3곳에 잘 나눠서 생산 명령을 내려 300초 안에 구성이 나와서 집결지로 모이게 하자."
2. "생산 건물이 부서지면 이전처럼 생산되기 힘들 것 같다(3곳→2곳으로 줄어드는 것) - 그것도 고려해달라."
3. "공격 웨이브가 300초라고 해도, 정해진 패턴 조합이 완성돼야 공격 가도록 하자 - 준비가 안 됐으면
   공격 가지 않는다."

이 문서는 제안일 뿐, 아직 코드 수정 안 함.

## 기존 구조의 한계 (실제 숫자로 확인)
지금 `FillPool()`은 부족분을 `Instantiate()`로 **즉시** 스폰한다 - 생산에 걸리는 시간이 전혀 없다.
`UnitData.productionTime`(OC/Spore Brood 둘 다 이미 SO에 값이 들어있음)을 실제로 반영해서 "1마리 생산에
몇 초 걸린다"를 넣으면, 스폰 지점 하나로는 시간이 안 맞는 웨이브가 실제로 나온다:

| 웨이브 | OC 구성(doc/0539) | 스폰 지점 1곳 총 생산 시간 |
|---|---|---|
| 2차 | Cyborg Soldier×8(15초) + Railgunner×3(50초) | 8×15 + 3×50 = **270초** |
| 4차 | Cyborg×6(15) + Heavy Tank×3(31) + Ironhawk×2(40) | 90+93+80 = **263초** |

| 웨이브 | Spore Brood 구성(doc/0539) | 스폰 지점 1곳 총 생산 시간 |
|---|---|---|
| 3차 | Spitter×8(20초) + Skitterwing×4(26초) | 160+104 = **264초** |
| 5차 | Ripfang×10(10) + Spitter×8(20) + Skitterwing×6(26) | 100+160+156 = **416초** |

`waveTimes` 간격이 300초라 5차 웨이브 같은 경우 스폰 지점 하나로는 웨이브 사이 300초 전체를 다 써도
못 채운다 - 여러 스폰 지점으로 나눠 병렬 생산해야 실제로 시간 안에 맞는다는 우려가 숫자로도 맞음.

## 설계안

### 1. `spawnPoint`(단일 Transform) → `spawnPoints`(리스트, 건물 연동)
```csharp
[System.Serializable]
public class EnemySpawnPoint
{
    public Transform point;
    public EnemyBuildingController productionBuilding; // 비워두면 파괴 불가능한 스폰 지점(그냥 마커)
}
```
`productionBuilding`을 넣어두면 "이 스폰 지점은 저 건물이 살아있는 동안만 쓸 수 있다"가 되고, 비워두면
(요청 2와 무관하게) 항상 쓸 수 있는 고정 마커로 취급 - 모든 스폰 지점에 건물을 강제하지 않음.

### 2. 스폰 지점마다 독립된 생산 대기열 (기존 `UnitSpawner`/`ProductionData` 패턴과 동일한 발상)
```csharp
private class EnemyProductionOrder
{
    public int unitID;
    public float remainTime;
    public float totalTime;
    public List<EnemyUnitController> destinationPool; // 완성되면 garrison/raidGarrison 중 어디로 갈지
}

private class SpawnQueue
{
    public EnemySpawnPoint spawnPoint;
    public bool hasProductionBuilding; // Start()에서 캐싱 - 건물이 파괴된 뒤에도 "원래 있었는지" 구분용
    public List<EnemyProductionOrder> orders = new List<EnemyProductionOrder>();

    public bool IsAvailable => !hasProductionBuilding || spawnPoint.productionBuilding != null;
}
```
`Update()`에서 매 프레임 각 큐의 맨 앞 항목만 시간을 줄이고(`UnitSpawner.Produce()`와 동일한 FIFO 방식),
0 이하가 되면 그 스폰 지점 위치에 `Instantiate`해서 `destinationPool`에 추가한다. **`IsAvailable`이
false인 큐는 그 프레임에 대기열을 통째로 비운다**(요청 2 - 생산 중이던 건물이 파괴되면 그 자리의
미완성 주문은 사라짐, 다음 `FillPool` 체크 때 살아있는 다른 스폰 지점에서 다시 주문됨 - 별도 재배치
로직 없이 "부족분 재확인"만으로 자연 복구).

### 3. 부족분을 "대기열이 가장 덜 찬 스폰 지점"에 분산 주문
```csharp
private SpawnQueue LeastLoadedQueue() // IsAvailable == true인 것만 후보, 남은 생산시간 합이 가장 적은 곳
```
가장 덜 찬 기준은 단순 개수가 아니라 **남은 생산 시간의 합**(유닛마다 생산 시간이 다 달라서 - 예:
Railgunner 50초 1개가 Cyborg 15초 3개보다 큐를 더 오래 묶어둠) - 개수 기준보다 정확함.

### 4. `FillPool` 버그 수정 - "이미 원정 나간(deployed) 유닛"을 보유량에서 빼야 함
지금 `FillPool`은 `pool`에 있는 유닛 수만 세는데, `deployed`된(이미 다른 웨이브로 나간, 아직 안 죽은)
유닛도 그대로 세고 있었다 - 다음 웨이브가 같은 종류의 유닛을 필요로 하면 "이미 충분하다"고 착각해서
실제로는 부족한데 생산을 안 시키는 조용한 버그였다(지금까진 "부족하면 부족한 대로 보낸다"라 티가 안
났지만, 이번에 "완성돼야 출발"을 넣으면 이 버그 때문에 영원히 준비 완료 판정이 안 나는 상황이 생길 수
있어 지금 같이 고침). `deployed`에 없는(=아직 대기 중인) 유닛만 "보유"로 센다.

### 5. 웨이브 발사 게이트 - "예정 시각이 됐어도 구성이 준비 안 됐으면 안 감"
```csharp
private IEnumerator AttackWaveRoutine()
{
    for (...)
    {
        yield return new WaitForSeconds(wait); // 기존과 동일 - "이 시각 이전엔 절대 안 감"
        yield return WaitUntilReady(CurrentWaveComposition()); // 신규 - 구성이 다 갖춰질 때까지 대기
        yield return LaunchWave();
    }
    // 반복 구간도 동일하게 WaitUntilReady 추가
}

private IEnumerator WaitUntilReady(List<UnitGroup> composition)
{
    while (!IsComposeReady(composition))
        yield return new WaitForSeconds(1f); // 생산은 초 단위로 진행되므로 매 프레임까진 불필요
}
```
`waveTimes[i]`는 이제 "이 시각이 지나야 갈 수 있다"는 하한선이고, 실제 출발은 그 이후 구성이 완성되는
순간. **주의(부작용)**: 한 웨이브가 준비 지연으로 늦게 나가면, 코루틴이 순차 진행이라 다음 웨이브
시각도 그만큼 뒤로 밀린다(절대 시각 `waveTimes`를 억지로 맞추지 않음) - "완성돼야 간다"는 요청을
그대로 따르면 자연히 그렇게 됨. 생산 건물이 다 파괴돼서 영원히 완성이 안 되면 **그 웨이브는 무기한
보류**됨(타임아웃 없음) - 요청 2(생산 건물 파괴로 생산력이 준다)와 요청 3(준비 안 되면 안 간다)을 같이
적용하면 자연히 나오는 결과라 의도된 동작으로 봄.

**적용 범위(결정)**: 별동대(`RaidRoutine`)도 동일하게 완성 대기 적용 - "그때 있는 만큼만"이 아니라
`raidSquadComposition`이 다 갖춰져야 출발.

## 진영별 유닛 생산 속도
### OC - 이미 SO에 있는 값 그대로 사용(요청대로 NTA 기준 값 그대로, 변경 없음)
| 유닛 | ID | productionTime |
|---|---|---|
| Cyborg Soldier | 2 | 15 |
| Striker | 3 | 19 |
| Railgunner | 4 | 50 |
| Brute Mech | 5 | 25 |
| Heavy Assault Tank | 6 | 31 |
| Ironhawk | 7 | 40 |
| Raven | 8 | 37 |
| Strike Drone | 9 | 63 |

### Spore Brood - SO에 이미 값이 들어있음(확인 결과, 임의로 새로 정할 필요 없어 보임)
| 유닛 | ID | productionTime |
|---|---|---|
| Ripfang | 10 | 10 |
| Spitter | 11 | 20 |
| Skitterwing | 12 | 26 |

요청은 "spore는 임의로 정해달라"였지만 확인해보니 `Spore Brood Unit Data SO.asset`에 이미 값이
들어있었음(10/20/26 - 위 "기존 구조의 한계" 표 계산에도 이 값을 그대로 씀).

## 관련 문서 갱신
doc/0539(콘텐츠 초안)에 "생산 시간" 관점이 없었으므로, 이 문서가 그 보완 역할 - doc/0539 자체는 구성
표(누가/몇 마리)를 그대로 유지하고, 이 문서(0544)가 "그 구성이 실제로 몇 초 안에 나오는지"를 검증하는
문서로 추가됨.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **Spore Brood `productionTime`**: SO에 이미 있는 값(10/20/26) 그대로 사용.
2. **웨이브 준비 게이트 적용 범위**: 공격 웨이브뿐 아니라 별동대도 완성될 때까지 대기(위 "웨이브 발사
   게이트" 절의 "적용 범위(결정)" 참고).
3. **생산 건물 전멸로 인한 무기한 보류**: 타임아웃 없이 그대로 허용.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs` - `EnemySpawnPoint`/`EnemyProductionOrder`/`SpawnQueue`
  신규 타입, `spawnPoint`(단일) → `spawnPoints`(리스트) 필드 변경, `Update()`에 생산 틱 추가, `FillPool`이
  생산 대기열에 주문을 넣는 방식으로 재작성(+ deployed 제외 버그 수정), `AttackWaveRoutine`/`RaidRoutine`에
  `WaitUntilReady` 게이트 추가, `AssembleAtRally`의 `spawnPoint` 폴백을 `spawnPoints[0]`으로 변경.
  doc/0541~0542에서 추가된 `allEnemyUnits`/`allEnemyBuildings` 디버그 리스트는 그대로 보존.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존 베이스라인과 동일 - 신규 경고 없음).

## 병합 메모
구현 도중 다른 세션이 같은 파일을 동시에 편집 중인 걸 발견함(doc/0541~0543 채번 - 인스펙터 디버그
리스트 추가/교체, `AllyAIDirector` 신규 파일). 최종 구현은 그 세션이 마지막으로 남긴 상태
(`allEnemyUnits`/`allEnemyBuildings` 필드 + `ReinforceRoutine` 갱신 로직)를 기준으로 이 문서의 변경을
그 위에 얹어 작성함 - 두 세션의 변경이 전부 남아있음.
