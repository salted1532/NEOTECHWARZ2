# 0533 - EnemyAIDirector 웨이브 규모 점증(1.5배씩) 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
"그것도 진행되는 웨이브에 따라 더 늘려나가야할거 같아 추후엔 더 많은 적들을 모아서 가도록" →
"1.5배씩 늘어나도록"

→ 지금 `waveSize`(doc/0532)는 인스펙터에 넣은 값 그대로 매 웨이브 동일하게 쓰임(기본 4명 고정). 웨이브가
거듭될수록(시간이 지날수록) 한 번에 보내는 인원이 **1.5배씩 계속 불어나야** 함 - 이 문서는 그 제안일 뿐,
아직 코드 수정 안 함.

## 기존 코드 조사
`EnemyAIDirector.cs`의 `AttackWaveRoutine()`이 `waveTimes` 리스트를 순서대로 돈 뒤, 리스트가 끝나면
마지막 간격으로 무한 반복하며 매번 `LaunchWave()`를 부른다(`EnemyAIDirector.cs:85-119`, doc/0532 결정
사항 #2). `LaunchWave()`는 `TakeSquad(waveSize)`로 `garrison`에서 아직 `deployed`(원정 안 나간) 상태인
유닛을 앞에서부터 정확히 `waveSize`개(모자라면 있는 만큼만) 뽑아간다 - `waveSize`는 지금 상수 필드라
매번 같은 값이 나간다.

## 설계안

### 웨이브 번호 카운터 추가
`AttackWaveRoutine()` 안에서 웨이브를 보낼 때마다(리스트 순회든 반복이든 구분 없이) 증가하는
`int waveIndex`를 둔다(0부터 시작). 리스트가 끝나고 무한 반복으로 넘어가도 리셋하지 않고 계속 이어서
증가 - "웨이브가 거듭될수록"이라는 요청이 리스트 안/밖을 구분하지 않으므로 하나의 연속된 카운터로 충분.

### 이번 웨이브 인원 계산
```
int CurrentWaveSize() {
    int size = Mathf.RoundToInt(waveSize * Mathf.Pow(1.5f, waveIndex));
    return maxWaveSize > 0 ? Mathf.Min(size, maxWaveSize) : size;
}
```
- `waveIndex == 0`(첫 웨이브)이면 `waveSize` 그대로(1.5^0 = 1) - 기존 동작과 동일하게 시작.
- 이후 웨이브마다 1.5배: 예) `waveSize = 4`면 4 → 6 → 9 → 14 → 20 → 30 → 45 → ... (반올림)
- **`maxWaveSize`(신규 인스펙터 필드, 기본값 제안 20, 0이면 무제한)**: 1.5배씩 지수적으로 계속 불어나면
  웨이브 10번째쯤(약 30분~1시간 경과, `waveTimes` 간격에 따라 다름)부터 세 자릿수로 튀어서 사실상
  "매번 garrison 전체를 밀어넣는" 상태가 되고, 그 이후는 상한이 없으면 늘어나는 의미가 없어짐(어차피
  살아있는 인원 이상은 못 뽑음) - 상한을 둬서 "이 미션이 최종적으로 도달하는 최대 웨이브 규모"를 기획자가
  직접 정하게 하는 편이 안전.

### `garrisonTarget`과의 관계 (확인 필요)
`TakeSquad`는 그 시점에 `garrison`에 살아있고 아직 안 나간 유닛만 뽑으므로, `waveSize`가 커져도
`garrisonTarget`(대기 인원 목표, 기본 6)이 그대로면 실제로 나가는 인원은 `garrisonTarget` 근처에서
막힌다 - 계산상 20명을 보내야 하는데 대기 중인 병력이 6명뿐이면 6명만 나감. 1.5배 성장을 실제로
체감시키려면 `garrisonTarget`도 같이 늘어나야 하는지, 아니면 "보낼 수 있는 만큼만 보낸다"로 충분한지
아래 "확인이 필요한 부분"에서 확인.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **`waveSize`의 의미**: 첫 웨이브(`waveIndex == 0`) 인원수. 이후 웨이브마다 1.5배.
2. **`maxWaveSize` 상한**: 둔다 - 기본값 20.
3. **`garrisonTarget` 연동**: 같이 올라가게 한다 - 매 웨이브 발사 직전(또는 `ReinforceRoutine` 체크 시점)
   `garrisonTarget`을 `CurrentWaveSize() + 여유분(2)`로 갱신해서, 실제로 보낼 수 있는 대기 인원이 성장을
   따라가게 한다. 인스펙터에 넣은 `garrisonTarget` 초기값은 첫 웨이브 기준 하한선 역할만 하고, 그보다
   작으면 계산값으로 올려치기만 한다(사용자가 처음부터 더 크게 잡아뒀으면 그 값을 그대로 존중).

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`

## 코드 변경

### 기존 코드
```csharp
[SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
[SerializeField] private int waveSize = 4;
[SerializeField] private Transform attackTarget;
```
```csharp
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
```

### 변경 코드
```csharp
[SerializeField] private List<float> waveTimes; // 미션 시작 후 경과 시각(초), 오름차순 - ex: 300/600/900
[SerializeField] private int waveSize = 4; // 첫 웨이브(waveIndex==0) 인원수 - 이후 웨이브마다 1.5배(doc/0533)
[SerializeField] private int maxWaveSize = 20; // 1.5배씩 계속 커지는 걸 막는 상한(0이면 무제한, doc/0533)
[SerializeField] private Transform attackTarget;
```
```csharp
// 몇 번째 웨이브를 보냈는지(0부터) - waveTimes 리스트를 다 돌고 반복 구간에 들어가도 리셋하지 않고
// 계속 이어서 증가한다(doc/0533, "웨이브가 거듭될수록" 규모가 커져야 하므로).
private int waveIndex;
```
```csharp
private IEnumerator LaunchWave()
{
    int size = CurrentWaveSize();
    waveIndex++;

    // 다음 웨이브부터 커질 인원을 실제로 뽑을 수 있으려면 대기 인원 목표도 같이 올라가야 한다
    // (doc/0533 결정 사항 #3) - 내려가진 않고 필요할 때만 올려친다.
    garrisonTarget = Mathf.Max(garrisonTarget, size + 2);

    List<EnemyUnitController> squad = TakeSquad(size);
    if (squad.Count == 0)
        yield break;

    if (assembleBeforeAttack)
        yield return AssembleAtRally(squad);

    foreach (EnemyUnitController unit in squad)
        if (unit != null)
            unit.AttackMoveTo(attackTarget.position);
}

// 이번 웨이브에 보낼 인원수 - waveSize(첫 웨이브 인원)에서 시작해 웨이브마다 1.5배(doc/0533).
private int CurrentWaveSize()
{
    int size = Mathf.RoundToInt(waveSize * Mathf.Pow(1.5f, waveIndex));
    return maxWaveSize > 0 ? Mathf.Min(size, maxWaveSize) : size;
}
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 기존과 동일한 39개 - 전부 프로젝트 전역의 기존
`FindFirstObjectByType` obsolete 경고).

## 웨이브별 인원 예시 (waveSize=4, maxWaveSize=20 기준)
1회차 4명 → 2회차 6명 → 3회차 9명 → 4회차 14명 → 5회차 20명(상한 도달) → 6회차 이후 계속 20명.
