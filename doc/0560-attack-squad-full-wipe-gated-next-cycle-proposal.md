# 0560 - 공격 별동대(웨이브)를 "전멸 후에만 다음 타이머가 도는" 방식으로 변경 (제안)

**날짜:** 2026-08-13

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 40개(전부 기존
  `FindFirstObjectByType` obsolete 경고 - 이번 변경과 무관).

## 요청 내용

> 공격 별동대가 공격 시간이 지나고도 유닛 구성이 안맞춰지면 계속 생산하고
> 시간이 지났고 유닛 구성도 맞다 그러면 그때 공격을 가서
> 그 별동대가 다 죽으면 그 때 다시 공격 시간(현재 300초) 시간이 다시 돌면서 유닛을 생산하는 식으로 해줘
> 별동대로 생산된 유닛이 죽었다고 그 유닛을 다시 뽑아서 위치에 배치하는 위 요청내용은 배제해줘

적용 대상 확인 결과(질문/답변): **`AllyAIDirector`와 `EnemyAIDirector` 둘 다**의 "공격 웨이브"
(`AttackWaveRoutine`/`LaunchWave`, 코드 내에서도 부대를 "squad"로 부름)에 적용한다. 점령지 탈환
별동대(`EnemyAIDirector.RaidRoutine`)는 대상에서 제외 — 이미 "전멸해야 재편성"하는 로직이 있다
(doc/0544/0549, 아래 "조사 결과" 참고).

## 조사 결과

### 1. "시간 지나고도 구성 안 맞으면 계속 생산 → 구성 맞으면 공격"은 `EnemyAIDirector`에만 있었다

`EnemyAIDirector.AttackWaveRoutine()` (`Assets/Scripts/System/EnemyAIDirector.cs:354~386`)은 이미
시각 대기 후 `WaitUntilReady(garrison, CurrentWaveComposition())`으로 구성이 다 갖춰질 때까지
폴링 대기한 뒤에만 `LaunchWave()`를 부른다(doc/0546 결정). 그동안도 `ReinforceRoutine()`이 계속
부족분을 생산 대기열에 채워 넣으므로 "시간 지나고도 구성 안 맞으면 계속 생산"은 이미 만족한다.

반면 `AllyAIDirector.AttackWaveRoutine()`(`AllyAIDirector.cs:135~154`)은 이 대기가 아예 없다 —
시각이 되면 구성 여부와 무관하게 바로 `LaunchWave()`를 부른다. 구성이 부족하면 `TakeSquad`가 있는
만큼만(또는 0마리) 뽑아 그대로 내보내 버린다. **이 부분은 `EnemyAIDirector`와 동일하게
`WaitUntilReady`/`IsComposeReady`를 새로 추가해야 한다.**

### 2. "별동대가 다 죽어야 다음 타이머가 돈다"는 둘 다 없다

두 director 모두 `LaunchWave()`가 부대를 내보낸 뒤 전멸 감시(`RunWaveSquad`)를
`StartCoroutine(RunWaveSquad(squad))`로 **fire-and-forget** 실행한다:

```csharp
// EnemyAIDirector.cs:440~442 (AllyAIDirector.cs:183~185도 동일한 패턴/이유 주석)
// 목표 파괴 시 재조준/전멸 시 종료 감시는 별도 코루틴 - 여기서 기다리면 이 부대가 다 죽을 때까지
// AttackWaveRoutine의 다음 웨이브 스케줄이 막혀버린다(doc/0534).
StartCoroutine(RunWaveSquad(squad));
```

즉 `LaunchWave()`는 부대를 내보내자마자 바로 반환되고, `AttackWaveRoutine()`은 `waveTimes`에 미리
정해둔 절대 시각(예: 300/600/900초) 스케줄을 그대로 따라간다 — 이전 부대가 살아있든 전멸했든
상관없이 다음 웨이브가 시각에 맞춰 또 나간다(doc/0534에서 의도적으로 이렇게 결정했었음). 이번
요청은 이 결정을 뒤집는 것 — **부대가 전멸할 때까지 다음 사이클을 시작하지 않는다.**

`RunWaveSquad`는 이미 부대가 전멸하면(`squad.Count == 0`, null만 남음) 스스로 종료한다
(`EnemyAIDirector.cs:478~501`, `AllyAIDirector.cs:200~223`) — 즉 "전멸 감지" 자체는 이미 있고,
그저 `LaunchWave()`가 그 종료를 기다리지 않을 뿐이다. 그러므로 `StartCoroutine(...)` 대신
`yield return RunWaveSquad(squad);`로 바꿔 `LaunchWave()` 자신이 전멸까지 기다리게 하는 것만으로
충분하다.

### 3. "죽은 유닛을 개별로 다시 뽑아 자리 채우기"는 애초에 없다 (요청의 "배제" 항목)

그런 개별 보충 로직은 배치형 방어 유닛(`defenseUnits`/`RespawnDeadDefenseUnits`, doc/0552/0558)
전용이고, 웨이브/별동대(`garrison`/`deployed`)에는 애초에 적용된 적이 없다. `ReinforceRoutine()`은
항상 "다음에 나갈 웨이브의 구성 부족분"을 `garrison` 풀 기준으로 채울 뿐, 이미 파견되어 죽은
유닛을 그 유닛 개별로 다시 만들어 원래 자리에 놓는 로직이 아니다. 즉 **요청의 "배제" 항목은 이미
지켜지고 있고, 이번 수정으로도 그런 로직을 추가하지 않는다** — 새 부대는 항상 `garrison`에서 통째로
새로 차출한다.

## 제안하는 수정

### 공통 원리 (두 파일 동일 패턴)

`AttackWaveRoutine()`을 "`waveTimes` 절대 시각 스케줄을 따라가는 for문"에서 "매 사이클: 대기 →
구성 완성 대기 → 공격 → **전멸까지 대기** → 다음 사이클"을 도는 무한 루프로 바꾼다. 사이클 간격
값 자체(`waveTimes[0]`, 이후 `waveTimes[i]-waveTimes[i-1]`, 리스트를 넘으면 마지막 간격 반복)는
기존 규칙을 그대로 유지 — 다만 그 간격을 "미션 시작 시각 기준"이 아니라 "이전 부대가 전멸한 시점
기준"으로 다시 잰다.

### `Assets/Scripts/System/EnemyAIDirector.cs`

`AttackWaveRoutine()` (354~386번째 줄) 교체:

```csharp
private IEnumerator AttackWaveRoutine()
{
    while (true)
    {
        yield return CountdownSeconds(WaveIntervalFor(waveIndex), v => nextWaveCountdown = v);

        if (IsPlayerDefeated())
            yield break; // 더 공격할 대상이 없음 - 웨이브 스케줄 보류(doc/0547)

        yield return WaitUntilReady(garrison, CurrentWaveComposition());
        yield return LaunchWave(); // doc/0560: 별동대가 전멸할 때까지 여기서 대기 (구 doc/0534 결정 번복)
    }
}

// waveTimes[index] 기준 이번 사이클의 대기 시간 - 리스트를 넘어서면 마지막 두 항목 간격을 반복
// (기존 waveTimes 반복 규칙과 동일, doc/0532 결정 사항 #2).
private float WaveIntervalFor(int index)
{
    if (waveTimes.Count == 0)
        return 0f;
    if (index == 0)
        return waveTimes[0];
    if (index < waveTimes.Count)
        return waveTimes[index] - waveTimes[index - 1];

    return Mathf.Max(1f, waveTimes.Count >= 2 ? waveTimes[^1] - waveTimes[^2] : waveTimes[0]);
}
```

`LaunchWave()` (428~443번째 줄)의 마지막 줄만 교체:

```csharp
        // doc/0560: fire-and-forget이던 것을 직접 대기로 바꿔 AttackWaveRoutine이 전멸까지 기다리게 한다.
        yield return RunWaveSquad(squad);
```

### `Assets/Scripts/System/AllyAIDirector.cs`

`AttackWaveRoutine()` (135~154번째 줄) 교체:

```csharp
private IEnumerator AttackWaveRoutine()
{
    while (true)
    {
        yield return CountdownSeconds(WaveIntervalFor(waveIndex));

        yield return WaitUntilReady(CurrentWaveComposition());
        yield return LaunchWave(); // doc/0560: 별동대가 전멸할 때까지 여기서 대기
    }
}

private float WaveIntervalFor(int index)
{
    if (waveTimes.Count == 0)
        return 0f;
    if (index == 0)
        return waveTimes[0];
    if (index < waveTimes.Count)
        return waveTimes[index] - waveTimes[index - 1];

    return Mathf.Max(1f, waveTimes.Count >= 2 ? waveTimes[^1] - waveTimes[^2] : waveTimes[0]);
}

// EnemyAIDirector.WaitUntilReady/IsComposeReady와 동일한 패턴 - 시각이 지나도 구성이 안 갖춰지면
// 계속 폴링 대기(그동안 ReinforceRoutine이 계속 채움). doc/0560에서 신규 추가.
private IEnumerator WaitUntilReady(List<AllyUnitGroup> composition)
{
    while (!IsComposeReady(composition))
        yield return new WaitForSeconds(1f);
}

private bool IsComposeReady(List<AllyUnitGroup> composition)
{
    garrison.RemoveAll(u => u == null);

    foreach (AllyUnitGroup group in composition)
    {
        int available = 0;
        foreach (AllyController unit in garrison)
            if (unit != null && !deployed.Contains(unit) && unit.GetAllyUnitID() == group.unitID)
                available++;

        if (available < group.count)
            return false;
    }

    return true;
}
```

`LaunchWave()` (171~186번째 줄)의 마지막 줄만 교체:

```csharp
        // doc/0560: fire-and-forget이던 것을 직접 대기로 바꿔 AttackWaveRoutine이 전멸까지 기다리게 한다.
        yield return RunWaveSquad(squad);
```

## 영향받는 파일 (예정)

- `Assets/Scripts/System/EnemyAIDirector.cs` (`AttackWaveRoutine`, `LaunchWave`, `WaveIntervalFor` 신규)
- `Assets/Scripts/System/AllyAIDirector.cs` (`AttackWaveRoutine`, `LaunchWave`, `WaveIntervalFor`/
  `WaitUntilReady`/`IsComposeReady` 신규)

## 영향받지 않는 부분

- `EnemyAIDirector.RaidRoutine()`(점령지 탈환 별동대) - 이번 요청 범위 밖, 변경 없음.
- 배치형 방어 유닛(`defenseUnits`/`RespawnDeadDefenseUnits`) - 변경 없음, 요청의 "배제" 항목과도
  무관(애초에 웨이브/별동대엔 적용된 적 없음).
- `ReinforceRoutine()`, `FillPool`, `TakeSquad` - 변경 없음, 기존 그대로 재사용.

## 요약

- `AttackWaveRoutine()`을 절대 시각(`waveTimes`) 스케줄에서 "대기 → 구성 완성 대기 → 공격 → 전멸까지
  대기 → 다음 사이클" 무한 루프로 바꾼다. 사이클 간격 값 자체는 기존 `waveTimes` 규칙 그대로(첫 판은
  `waveTimes[0]`, 이후 구간 간격, 리스트를 넘으면 마지막 간격 반복) 재사용하되, 기준 시점이 "이전
  부대가 전멸한 순간"으로 바뀐다.
- `EnemyAIDirector`는 구성 완성 대기(`WaitUntilReady`)가 이미 있어 그대로 재사용, `AllyAIDirector`는
  같은 패턴을 신규 추가.
- 두 파일 모두 `LaunchWave()`가 `RunWaveSquad`를 fire-and-forget이 아니라 직접 대기하도록 바꿔
  "전멸해야 다음 사이클"을 구현한다.
- 개별 유닛 사망 시 그 자리를 다시 채우는 로직은 원래 없었고(배치형 방어 유닛 전용), 이번 수정으로도
  추가하지 않는다 - 새 부대는 항상 `garrison`에서 통째로 새로 차출.
- 아직 코드에 반영하지 않음 - 승인 대기.
