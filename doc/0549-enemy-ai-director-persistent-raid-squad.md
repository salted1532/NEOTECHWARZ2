# 0549 - EnemyAIDirector 점령 별동대 재사용(전멸 전까지 새로 생산 안 함) → 구현 완료

## 날짜
2026-08-13

## 요청 내용
"점령 별동대가 출발해서 전멸 당하지 않는 이상 새로운 점령 별동대를 생산하지 않는거로 하자. 점령별동대가
출발해서 성공적으로 점령하게 되면 생존한 별동대가 다시 쿨타임 이후에 다음 점령지도 이동하는식으로"

## 기존 동작의 문제
지금은 `raidInterval`마다 `RaidRoutine`이 매번 새로 `TakeSquad(raidGarrison, RaidSquadComposition)`를
불러서 별동대를 뽑아 보낸다 - 이전에 보낸 부대가 아직 살아서 활동 중이어도 상관없이 계속 새 부대를 만들어
보냄. 게다가 `ReinforceRoutine`이 `raidGarrison`을 항상 `RaidSquadComposition`만큼 채워두려 하는데,
스폰 지점의 생산 대기열은 `garrison`(웨이브)과 공유되므로(doc/0544 `LeastLoadedQueue`), 이미 나간 부대가
멀쩡히 활동 중인데도 "예비 부대"를 계속 생산하느라 웨이브용 생산과 자원을 나눠 쓰게 됨.

## 설계
### 부대를 재사용하는 상태로 전환
```csharp
// 현재 파견된(또는 다음에 파견할) 점령 별동대 - 전멸하기 전까진 새로 편성하지 않고 이 부대를 그대로
// 재사용해서 다음 점령지로 보낸다(doc/0549).
private readonly List<EnemyUnitController> currentRaidSquad = new List<EnemyUnitController>();
```

### `RaidRoutine` - 전멸했을 때만 새로 편성
```csharp
private IEnumerator RaidRoutine()
{
    while (true)
    {
        yield return CountdownSeconds(raidInterval, v => nextRaidCountdown = v);
        if (IsPlayerDefeated()) yield break;

        currentRaidSquad.RemoveAll(u => u == null);
        if (currentRaidSquad.Count == 0) // 전멸함 - 이때만 새로 편성
        {
            yield return WaitUntilReady(raidGarrison, RaidSquadComposition);
            currentRaidSquad.AddRange(TakeSquad(raidGarrison, RaidSquadComposition));
            if (currentRaidSquad.Count == 0) continue; // 뽑을 병력이 없음 - 다음 주기 재시도
        }

        CaptureSystem target = PickRaidTarget();
        if (target == null) continue;

        foreach (var unit in currentRaidSquad)
            if (unit != null) unit.AttackMoveTo(target.transform.position);
    }
}
```
`raidInterval`이 그대로 "쿨타임" 역할을 한다 - 살아있으면 매번 다음 점령지로 재이동, 전멸했을 때만
`raidGarrison`에서 새로 뽑음. **이동 시점은 점령 성공을 실제로 감지하지 않고 그냥 `raidInterval`이
지나면**으로 확정(기본값 45초 > `CaptureSystem` 점령 소요 30초라 보통은 이미 점령이 끝나 있는 상태에서
다음으로 넘어감 - 사용자 확인 완료, 감시 로직 추가 안 함).

### `ReinforceRoutine` - 별동대가 살아있는 동안은 생산 안 함
```csharp
currentRaidSquad.RemoveAll(u => u == null);

FillPool(garrison, CurrentWaveComposition());
if (currentRaidSquad.Count == 0) // 전멸했을 때만 다음 편성을 미리 생산(doc/0549) - 살아있는 동안은 생산 안 함
    FillPool(raidGarrison, RaidSquadComposition);
```

## 확인 완료 (2026-08-13)
1. 다음 점령지로 넘어가는 시점: 실제 점령 완료를 감시하지 않고, 일정한 쿨타임(`raidInterval`)만 지나면
   넘어가는 방식으로 확정.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존과 동일).

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
