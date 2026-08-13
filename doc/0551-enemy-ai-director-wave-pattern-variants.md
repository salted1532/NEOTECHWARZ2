# 0551 - EnemyAIDirector 웨이브별 3가지 패턴 중 무작위 선택

## 날짜
2026-08-13

## 요청 내용
"각 웨이브별로 유닛 패턴을 3가지정도로 만들어주고 해당 웨이브일때 랜덤하게 정해서 해당 웨이브를
준비하도록 해줘"

## 설계
### 데이터 구조 - `WaveVariants`(웨이브 하나당 패턴 여러 개)
```csharp
[System.Serializable]
public class WaveVariants
{
    public List<WaveComposition> variants;
}
```
`attackWavesOC`/`attackWavesSporeBrood`의 타입을 `List<WaveComposition>`(웨이브당 구성 1개)에서
`List<WaveVariants>`(웨이브당 구성 3개 중 무작위)로 변경. doc/0550에서 절반으로 줄인 마릿수를 그대로
"기준 총원"으로 삼고, 유닛 종류 비율만 다른 2가지 변형을 추가로 만듦(총원은 웨이브마다 거의 동일하게
유지 - 난이도 곡선은 그대로 두고 "어떤 유닛이 오는지"만 매번 달라지게).

### 같은 웨이브 주기 안에서는 같은 패턴을 써야 함 (버그 방지)
`CurrentWaveComposition()`은 한 웨이브가 준비되는 동안 여러 번 불린다(`ReinforceRoutine`이 주기적으로
생산 체크, `WaitUntilReady`가 완성 여부 확인, `LaunchWave`가 실제 차출) - 호출마다 다시 무작위로 뽑으면
"이 종류를 생산해뒀는데 발사할 땐 다른 조합을 요구"하는 불일치가 생김. `waveIndex`가 바뀔 때만 다시
뽑고, 같은 `waveIndex` 동안은 캐싱된 값을 그대로 재사용하도록 함:
```csharp
private int cachedWaveIndex = -1;
private List<UnitGroup> cachedWaveComposition;

private List<UnitGroup> CurrentWaveComposition()
{
    if (cachedWaveIndex == waveIndex && cachedWaveComposition != null)
        return cachedWaveComposition;

    List<WaveVariants> waves = AttackWaves;
    List<UnitGroup> result = new List<UnitGroup>();

    if (waves.Count > 0)
    {
        int index = Mathf.Min(waveIndex, waves.Count - 1);
        List<WaveComposition> variants = waves[index].variants;
        if (variants != null && variants.Count > 0)
            result = variants[Random.Range(0, variants.Count)].units;
    }

    cachedWaveIndex = waveIndex;
    cachedWaveComposition = result;
    return result;
}
```
`LaunchWave()`가 `composition`을 캐싱된 값으로 읽어온 **뒤에** `waveIndex++`를 하므로, 다음 호출부터는
캐시가 자동으로 무효화되어 다음 웨이브의 패턴이 새로 뽑힌다.

## 콘텐츠 (doc/0550 기준 총원 유지, 유닛 비율만 다른 3가지)
### OC
| 웨이브 | 패턴1(기존) | 패턴2 | 패턴3 |
|---|---|---|---|
| 1차(5명) | Cyborg×5 | Cyborg×3+Striker×2 | Cyborg×2+Railgunner×2+Striker×1 |
| 2차(6명) | Cyborg×4+Railgunner×2 | Cyborg×3+Striker×3 | Cyborg×2+Railgunner×1+Brute×1+Striker×2 |
| 3차(7명) | Cyborg×4+Striker×2+Brute×1 | Cyborg×3+Railgunner×2+Brute×2 | Striker×3+Brute×2+Railgunner×2 |
| 4차(6명) | Cyborg×3+HeavyTank×2+Ironhawk×1 | Brute×2+HeavyTank×2+Railgunner×2 | Cyborg×2+Ironhawk×2+HeavyTank×1+Striker×1 |
| 5차(4명, 반복) | HeavyTank×2+Raven×1+StrikeDrone×1 | HeavyTank×1+Raven×2+Ironhawk×1 | StrikeDrone×1+HeavyTank×1+Brute×2 |

### Spore Brood
| 웨이브 | 패턴1(기존) | 패턴2 | 패턴3 |
|---|---|---|---|
| 1차(7명) | Ripfang×7 | Ripfang×5+Spitter×2 | Ripfang×4+Spitter×2+Skitterwing×1 |
| 2차(8명) | Ripfang×5+Spitter×3 | Ripfang×6+Skitterwing×2 | Spitter×4+Skitterwing×2+Ripfang×2 |
| 3차(6명) | Spitter×4+Skitterwing×2 | Ripfang×4+Spitter×2 | Ripfang×2+Spitter×2+Skitterwing×2 |
| 4차(10명) | Ripfang×6+Spitter×4 | Ripfang×5+Skitterwing×3+Spitter×2 | Spitter×5+Skitterwing×3+Ripfang×2 |
| 5차(12명, 반복) | Ripfang×5+Spitter×4+Skitterwing×3 | Ripfang×6+Spitter×3+Skitterwing×3 | Spitter×5+Skitterwing×4+Ripfang×3 |

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존과 동일).

## 참고
doc/0550과 동일한 주의사항 - 필드 기본값(C# 코드)이라 씬에 이미 배치된 `EnemyAIDirector`(Mission1)에는
자동 반영 안 됨. 새로 배치하거나 인스펙터에서 직접 채워야 함.

별동대 구성(`raidSquadCompositionOC`/`SporeBrood`)은 "웨이브"가 아니라서 이번 변경 대상에서 제외 -
여전히 고정 구성 하나.

## 영향받는 파일
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
