# 0145. Ally ↔ Neutral ↔ Enemy 3방향 순환 점령 설계

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **조사 결과 + 설계 제안**만 담고 있고
> `Assets/Scripts/CaptureSystem/CaptureSystem.cs`는 아직 고치지 않았다. 확인해 주면 그대로 반영한다.

## 날짜
2026-07-16

## 요청
현재 점령 시스템이 `Ally ↔ Neutral ↔ Enemy`처럼 중립→아군, 아군→중립, 중립→적 식으로 서로 오가며 동작하는지 확인하고, 아니면 그렇게 되도록 고쳐달라.

## 조사 결과: 지금은 "Neutral → Ally" 한 방향만 동작함
`CaptureSystem.cs` 현재 코드:
- `OnTriggerEnter`/`OnTriggerExit`(54~66행)가 `UnitController`(아군)만 인식한다. `EnemyController`(적 유닛)는 감지 대상이 아니다 — 그래서 `CaptureOwner.Enemy`는 게임플레이 중엔 절대 나올 수 없는 값이다(`doc/0143`에서 추가한 디버그 드롭다운으로 강제할 때만 가능).
- `Update()`(68~91행)가 `CurrentOwner == CaptureOwner.Ally`면 맨 위에서 바로 `return`한다 — 한 번 아군이 점령하면 그 뒤로 이 메서드는 사실상 아무 일도 안 하는 코드로 바뀐다. 즉 **Ally는 되돌릴 수 없는 최종 상태**이고, Ally→Neutral, Ally→Enemy, Neutral→Enemy 전환 로직 자체가 없다.
- 코드 주석("Neutral(흰색) -> Ally(초록) -> (추후) Enemy(빨강)로 전환된다")도 이걸 그대로 인정하고 있음 — "추후"라고 명시.

결론: **3방향 순환이 안 된다.** 지금 요청은 이 부분을 실제로 구현해달라는 것.

## 설계: 부호 있는 "쟁탈전(tug-of-war)" 값 하나로 통합

### 핵심 아이디어
지금처럼 "0→captureDuration" 한 방향 타이머 대신, **`-captureDuration`(완전 적 점령) ~ `+captureDuration`(완전 아군 점령)** 사이를 오가는 부호 있는 값(`controlValue`) 하나로 표현한다. 0을 반드시 거쳐야 반대 진영으로 넘어갈 수 있으므로 "중립을 반드시 거친다"는 요청의 순환 구조가 자연스럽게 보장된다.

```csharp
// Assets/Scripts/CaptureSystem/CaptureSystem.cs (수정안)

private readonly List<UnitController> alliesInRange = new List<UnitController>();
private readonly List<EnemyController> enemiesInRange = new List<EnemyController>(); // 신규

// -captureDuration(완전 적 점령) ~ +captureDuration(완전 아군 점령), 0 = 중립
private float controlValue;

private void OnTriggerEnter(Collider other)
{
    if (other.TryGetComponent<UnitController>(out var ally) && !alliesInRange.Contains(ally))
        alliesInRange.Add(ally);
    else if (other.TryGetComponent<EnemyController>(out var enemy) && !enemiesInRange.Contains(enemy))
        enemiesInRange.Add(enemy);
}

private void OnTriggerExit(Collider other)
{
    if (other.TryGetComponent<UnitController>(out var ally))
        alliesInRange.Remove(ally);
    else if (other.TryGetComponent<EnemyController>(out var enemy))
        enemiesInRange.Remove(enemy);
}

private void Update()
{
    alliesInRange.RemoveAll(u => u == null);
    enemiesInRange.RemoveAll(u => u == null);

    bool alliesPresent = alliesInRange.Count > 0;
    bool enemiesPresent = enemiesInRange.Count > 0;
    bool contested = alliesPresent && enemiesPresent; // 양쪽 다 있으면 교착 - 진행 정지(기존 "아무도 없으면 정지"와 동일 취급)

    if (!contested)
    {
        if (alliesPresent)
            controlValue = Mathf.Min(controlValue + Time.deltaTime, captureDuration);
        else if (enemiesPresent)
            controlValue = Mathf.Max(controlValue - Time.deltaTime, -captureDuration);
    }

    UpdateCaptureBar(alliesPresent, enemiesPresent, contested);
    UpdateOwnerFromControlValue();
}

// 기존 CompleteCapture()를 대체 - controlValue가 양 끝(±captureDuration)에 도달했을 때만 소유자가 바뀐다.
// 그 사이(0 포함) 값에서는 전부 Neutral - 기존 "Neutral→Ally" 로직도 원래 이 규칙(완료 전엔 전부 Neutral)이었으므로
// 대칭 확장일 뿐 기존 동작을 바꾸지 않는다.
private void UpdateOwnerFromControlValue()
{
    CaptureOwner newOwner =
        controlValue >= captureDuration ? CaptureOwner.Ally :
        controlValue <= -captureDuration ? CaptureOwner.Enemy :
        CaptureOwner.Neutral;

    if (newOwner == CurrentOwner) return;

    CurrentOwner = newOwner;
    ApplyEffect(newOwner);
    Debug.Log($"점령 상태 변경: {newOwner}");
}
```

### 왜 이 방식인가
- 기존 "Neutral→Ally"도 원래 이런 규칙이었다: `captureTimer`가 `captureDuration`에 도달하기 **전까지는 전부 Neutral 취급**(진행 중이라고 Ally로 안 바뀜), 도달한 순간만 Ally로 바뀜. 이번 설계는 이 규칙을 반대쪽(Enemy)에도 그대로 대칭 적용한 것뿐이라 기존 동작과 철학이 어긋나지 않는다.
- 아군이 이미 점령한 곳에 적이 들어와서 값을 깎기 시작하면, `captureDuration`(만점)에서 조금이라도 내려가는 즉시 `CurrentOwner`가 `Neutral`로 바뀐다 — `doc/0144`에서 확인한 "영토를 잃으면 그 자리에서 바로 반응해야 한다"는 방향과 일치.
- 둘 다 없으면(기존과 동일) 값이 그 자리에 멈춘다. **양쪽 다 있으면(교착) 서로 진행을 막는다** — 이 부분이 유일한 설계 판단이 필요한 지점(아래 결정 사항 참고).

### 자원/건설/생산 제한 시스템에 미치는 영향
`doc/0142`/`0144`에서 만든 `TerritoryManager.IsInsideAlliedTerritory()`는 `CaptureOwner.Ally`만 보고 판정하므로, 위 설계로 `CurrentOwner`가 즉시 `Neutral`/`Enemy`로 바뀌면 건설/채취/생산/회복 제한 로직이 지금처럼 그대로 즉시 반응한다 — 별도 수정 불필요.

## 결정이 필요한 부분
1. **양쪽 다 있을 때(교착) 처리**: 위 설계는 "둘 다 있으면 진행 정지"(아무도 못 이김)로 제안했다. 대신 "인원수 차이만큼 순 진행"(예: 아군 3명 vs 적 1명이면 순수하게 +2명분 속도로 진행)으로 하고 싶은지.
2. **점령 바(UI) 표시 방식**: 기존 `captureBar`는 0~captureDuration 한 방향(아군 점령 진행도)만 표현했다. 이제 값이 양쪽으로 움직이므로:
   - (A) 슬라이더는 "현재 진행 중인 방향으로 얼마나 남았는지"만 단순 표시(방향에 따라 매 프레임 min/max 다시 계산) — 적이 밀어붙이는 중에도 슬라이더가 그대로 보이되 의미만 "적 점령 진행도"로 바뀜.
   - (B) 지금은 아군 점령 진행만 보여주고, 적이 밀어붙이는 상황(Ally→Neutral로 깎이는 중)은 슬라이더 색을 다르게 하거나 아예 숨기는 등 후속 과제로 미루기.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정 예정)

## 다음 단계
위 2가지에 답을 주면 실제로 반영한다.
