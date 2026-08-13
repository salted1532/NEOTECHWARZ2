# 0546 - EnemyAIDirector 별동대별 유닛 리스트 + 웨이브/별동대 남은 시간 인스펙터 노출

## 날짜
2026-08-13

## 요청 내용
"공격 별동대, 점령 별동대 별 현재 유닛리스트를 좀 보이도록 해주고 공격웨이브까지 남은 시간도
인스펙터에 보이도록해줘 점령 웨이브도 시간 보이게"

## 변경

### 1. 풀별 유닛 리스트 노출
`garrison`(공격 웨이브용)/`raidGarrison`(점령 별동대용)은 그동안 `readonly` private라 인스펙터에
안 보였음(Unity는 `readonly` 필드를 직렬화 안 함) - `readonly` 제거 + `[SerializeField]` 추가, 헤더로
구분:
```csharp
[Header("<디버그> 공격 별동대(웨이브) 현재 병력")]
[SerializeField] private List<EnemyUnitController> garrison = new List<EnemyUnitController>();

[Header("<디버그> 점령 별동대 현재 병력")]
[SerializeField] private List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();
```
(참고: doc/0542에서 추가된 "씬 전체 적" 디버그 리스트 `allEnemyUnits`/`allEnemyBuildings`와는 별개 -
그쪽은 이 director의 내부 풀과 무관하게 씬 전체를 보여주는 용도, 이번 건 "이 director가 지금 데리고
있는 병력"만 따로 보여주는 용도.)

### 2. 다음 웨이브/별동대까지 남은 시간
```csharp
[Header("<디버그> 다음 웨이브/별동대까지 남은 시간(초)")]
[SerializeField] private float nextWaveCountdown;
[SerializeField] private float nextRaidCountdown;
```
기존엔 `yield return new WaitForSeconds(wait)`로 대기 구간을 통째로 건너뛰어서 "남은 시간"을 알 방법이
없었음 - 매 프레임 카운트다운하며 값을 갱신하는 `CountdownSeconds()`로 교체:
```csharp
private IEnumerator CountdownSeconds(float seconds, System.Action<float> setRemaining)
{
    float remaining = seconds;
    while (remaining > 0f)
    {
        setRemaining(remaining);
        yield return null;
        remaining -= Time.deltaTime;
    }
    setRemaining(0f);
}
```
`AttackWaveRoutine`/`RaidRoutine`의 `WaitForSeconds` 대기를 전부 이걸로 교체. **0으로 표시되는 동안**은
예정 시각은 지났지만 `WaitUntilReady`(doc/0544)가 구성 완성을 기다리는 중이라는 뜻 - 그 구간은 길이를
미리 알 수 없어서(생산이 언제 끝날지 모름) 별도 카운트다운 없이 0으로만 표시.

## 코드 변경

### 기존 코드
```csharp
private readonly List<EnemyUnitController> garrison = new List<EnemyUnitController>();
private readonly List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();
```
```csharp
for (int i = 0; i < waveTimes.Count; i++)
{
    float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
    yield return new WaitForSeconds(wait);
    yield return WaitUntilReady(garrison, CurrentWaveComposition());
    yield return LaunchWave();
}

float repeatInterval = waveTimes.Count >= 2 ? waveTimes[^1] - waveTimes[^2] : waveTimes[0];
WaitForSeconds repeatWait = new WaitForSeconds(Mathf.Max(1f, repeatInterval));
while (true)
{
    yield return repeatWait;
    yield return WaitUntilReady(garrison, CurrentWaveComposition());
    yield return LaunchWave();
}
```
```csharp
private IEnumerator RaidRoutine()
{
    WaitForSeconds wait = new WaitForSeconds(raidInterval);
    while (true)
    {
        yield return wait;
        yield return WaitUntilReady(raidGarrison, RaidSquadComposition);
        ...
```

### 변경 코드
```csharp
[Header("<디버그> 공격 별동대(웨이브) 현재 병력")]
[SerializeField] private List<EnemyUnitController> garrison = new List<EnemyUnitController>();

[Header("<디버그> 점령 별동대 현재 병력")]
[SerializeField] private List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();

[Header("<디버그> 다음 웨이브/별동대까지 남은 시간(초)")]
[SerializeField] private float nextWaveCountdown;
[SerializeField] private float nextRaidCountdown;
```
```csharp
for (int i = 0; i < waveTimes.Count; i++)
{
    float wait = i == 0 ? waveTimes[0] : waveTimes[i] - waveTimes[i - 1];
    yield return CountdownSeconds(wait, v => nextWaveCountdown = v);
    yield return WaitUntilReady(garrison, CurrentWaveComposition());
    yield return LaunchWave();
}

float repeatInterval = Mathf.Max(1f, waveTimes.Count >= 2 ? waveTimes[^1] - waveTimes[^2] : waveTimes[0]);
while (true)
{
    yield return CountdownSeconds(repeatInterval, v => nextWaveCountdown = v);
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
        yield return WaitUntilReady(raidGarrison, RaidSquadComposition);
        ...
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개.

## 영향받는 파일
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
