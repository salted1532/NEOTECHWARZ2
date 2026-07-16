# 0146. Ally ↔ Neutral ↔ Enemy 3방향 순환 점령 실제 반영 (`doc/0145` 적용)

## 날짜
2026-07-16

## 요청
`doc/0145` 제안대로 교착 시 진행 정지 + 점령바는 현재 방향 기준 단순 표시로 확정 반영.

## 변경 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정)

## 코드 변경 요약

### 감지 대상 확장
`OnTriggerEnter`/`OnTriggerExit`가 이제 `UnitController`(아군)뿐 아니라 `EnemyController`(적)도 인식해서 각각 `alliesInRange`/`enemiesInRange`에 담는다.

### `captureTimer`(0~D, 단방향) → `controlValue`(-D~+D, 양방향) 로 교체
```csharp
private void Update()
{
    alliesInRange.RemoveAll(unit => unit == null);
    enemiesInRange.RemoveAll(unit => unit == null);

    bool alliesPresent = alliesInRange.Count > 0;
    bool enemiesPresent = enemiesInRange.Count > 0;
    bool contested = alliesPresent && enemiesPresent; // 양쪽 다 있으면 교착 - 진행 정지

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
```

### 소유자 판정 (기존 `CompleteCapture` 대체)
```csharp
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
양 끝(`±captureDuration`)에 도달했을 때만 소유자가 바뀌고, 그 사이(0 포함)는 전부 `Neutral` — 기존 "Neutral→Ally" 규칙(완료 전엔 전부 Neutral)을 반대쪽에도 대칭 적용한 것.

### 점령바(UI) — 진행 중인 방향 기준 단순 표시
```csharp
private void UpdateCaptureBar(bool alliesPresent, bool enemiesPresent, bool contested)
{
    bool progressing = !contested && (alliesPresent || enemiesPresent)
        && !(alliesPresent && controlValue >= captureDuration)
        && !(enemiesPresent && controlValue <= -captureDuration);

    SetCaptureBarVisible(progressing);
    if (!progressing || captureBar == null) return;

    captureBar.value = alliesPresent
        ? Mathf.Clamp(controlValue, 0f, captureDuration)
        : Mathf.Clamp(-controlValue, 0f, captureDuration);
}
```

### 디버그 드롭다운(`doc/0143`) 연동
`OnValidate`의 강제 전환 블록에서 `captureTimer` 대신 `controlValue`를 해당 극값(Ally→+D, Enemy→-D, Neutral→0)으로 맞춰서, 강제 전환 후에도 다음 프레임 `UpdateOwnerFromControlValue()`가 같은 결과를 내도록(깜빡임 없이) 함.

## 요약
- 아군 유닛과 적 유닛(`EnemyController`)을 모두 감지해서 점령치를 서로 밀고 당기는 구조로 변경.
- 한쪽이 점령한 거점을 반대 진영이 되찾으려면 반드시 `Neutral`을 거쳐야 함 — 요청하신 `Ally ↔ Neutral ↔ Enemy` 순환 구조 그대로 구현됨.
- 양쪽이 동시에 있으면(교착) 진행 정지, 점령바는 현재 진행 중인 방향 기준으로 단순 표시.
- 기존 `TerritoryManager`/건설·채취·생산 제한 로직은 `CurrentOwner`만 보고 판정하므로 별도 수정 없이 그대로 연동됨.

## 확인/테스트 필요
유니티 에디터에서:
1. 아군이 점령 → 적 유닛이 들어와서 점령치를 깎기 시작 → `Neutral`을 거쳐 `Enemy`까지 실제로 넘어가는지.
2. 아군/적이 동시에 있을 때 점령치가 그 자리에서 멈추는지.
3. 점령바가 진행 방향에 맞게 표시되는지.

## 비고
[[confirm_before_implementing]] — `doc/0145`에서 제시한 2가지(교착 처리, 점령바 표시 방식) 모두 권장안으로 확인받은 뒤 반영함.
