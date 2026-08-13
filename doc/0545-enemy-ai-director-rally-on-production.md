# 0545 - EnemyAIDirector 생산 즉시 집결지 이동

## 날짜
2026-08-13

## 요청 내용
"Mission1에다가 적용시켜봤는데 유닛이 생산은 되는데 집결지로 집결을 안하네" → "생산되자마자 집결지도
다들 모였으면 좋겠어 생산된 유닛 (별동대) 들은"

## 원인
doc/0532~0544 설계상 "집결"은 **웨이브가 실제로 출발하는 순간에만** 일어난다 - `LaunchWave()`가
`TakeSquad()`로 인원을 뽑은 뒤 `AssembleAtRally()`를 호출해서 그때 처음 `MoveTo(rally)`를 명령한다.
그 전까지, 즉 생산 직후부터 웨이브 시각이 될 때까지는 유닛이 스폰 지점에 가만히 서 있기만 했다 -
"생산되자마자 모인다"가 아니라 "출발 직전에 모인다"였던 것. 별동대(`raidGarrison`)도 동일하게
`RaidRoutine()`이 차출할 때만 이동 명령을 받았다.

## 수정
생산 완료 시점(`Update()`의 대기열 처리부, `EnemyProductionOrder`가 완성돼 `Instantiate`한 직후)에
바로 `MoveTo(DefaultRallyPosition())`를 명령한다 - `garrison`/`raidGarrison` 어느 쪽으로 가든 동일하게
적용(요청에서 "별동대들도"라고 명시함). 이후 `LaunchWave()`의 `AssembleAtRally()`는 그대로 둔다 - 이미
집결지에 도착해 있으면 즉시 통과(`allArrived`가 바로 true)라 무해하고, 마침 웨이브가 뜨는 순간 아직
걸어오는 중인 유닛이 있으면 그 도착까지 마저 기다려주는 안전장치 역할은 유지된다.

## 코드 변경

### 기존 코드
```csharp
GameObject spawned = Instantiate(data.Prefab, sq.spawnPoint.point.position, sq.spawnPoint.point.rotation);
if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
    front.destinationPool.Add(unit);
```

### 변경 코드
```csharp
GameObject spawned = Instantiate(data.Prefab, sq.spawnPoint.point.position, sq.spawnPoint.point.rotation);
if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
{
    front.destinationPool.Add(unit);
    unit.MoveTo(DefaultRallyPosition()); // 생산되자마자 집결지로 - 웨이브/별동대 공통(doc/0545)
}
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존과 동일).

## 영향받는 파일
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
