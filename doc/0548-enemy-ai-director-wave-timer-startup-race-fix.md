# 0548 - EnemyAIDirector 웨이브 타이머가 아예 안 도는 버그 수정

## 날짜
2026-08-13

## 요청 내용
"이거 웨이브 타이머 작동안하는데 확인좀"

## 원인
doc/0547에서 추가한 `IsPlayerDefeated()` 체크를 **카운트다운 시작 전**에 넣었던 게 문제였다:
```csharp
for (int i = 0; i < waveTimes.Count; i++)
{
    if (IsPlayerDefeated())   // ← Start() 직후, 0프레임째 바로 이걸 확인
        yield break;
    ...
```
`BuildingController.Start()`가 `rtsController.BuildingList.Add(this)`로 자기 자신을 등록하는데
(`BuildingController.cs:114`), **Unity는 서로 다른 오브젝트의 `Start()` 실행 순서를 보장하지 않는다**.
즉 `EnemyAIDirector.Start()` → `AttackWaveRoutine()` 코루틴이 플레이어 메인기지의 `BuildingController.Start()`보다
먼저 실행되면, 그 순간 `BuildingList`가 아직 비어있어서 `IsPlayerDefeated()`가 무조건 `true`가 되고
웨이브 코루틴이 **첫 프레임에 바로 끝나버린다** - 카운트다운이 단 한 번도 시작조차 안 함.

## 수정
확인 시점을 카운트다운이 끝난 뒤로 옮김 - 실제 웨이브 시각(보통 300초 이상)까지 기다린 뒤에 확인하므로
그 시점엔 모든 건물의 `Start()`가 이미 끝나 있어 이 경쟁 상태가 발생할 수 없다.

## 코드 변경

### 기존 코드
```csharp
for (int i = 0; i < waveTimes.Count; i++)
{
    if (IsPlayerDefeated())
        yield break;

    float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
    yield return CountdownSeconds(wait, v => nextWaveCountdown = v);
    yield return WaitUntilReady(garrison, CurrentWaveComposition());
    yield return LaunchWave();
}
```
```csharp
private IEnumerator RaidRoutine()
{
    while (true)
    {
        if (IsPlayerDefeated())
            yield break;

        yield return CountdownSeconds(raidInterval, v => nextRaidCountdown = v);
        ...
```

### 변경 코드
```csharp
for (int i = 0; i < waveTimes.Count; i++)
{
    float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
    yield return CountdownSeconds(wait, v => nextWaveCountdown = v);

    if (IsPlayerDefeated())
        yield break;

    yield return WaitUntilReady(garrison, CurrentWaveComposition());
    yield return LaunchWave();
}
```
```csharp
private IEnumerator RaidRoutine()
{
    while (true)
    {
        yield return CountdownSeconds(raidInterval, v => nextRaidCountdown = v);

        if (IsPlayerDefeated())
            yield break;

        yield return WaitUntilReady(raidGarrison, RaidSquadComposition);
        ...
```
반복 구간(`while(true)`)도 동일하게 카운트다운 뒤로 옮김.

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존과 동일).

## 영향받는 파일
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
