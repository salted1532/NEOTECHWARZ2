# 0343 — 버그수정: Capture Point Ally 설정이 빌드에서 Neutral로 초기화됨

**날짜:** 2026-07-31

## 요청 내용

"Samplescene에서 capture포인트하나를 ally로 설정해뒀는데도 게임 빌드해서 samplescene으로 오면 중립으로 바뀌는데 왜그러지? 이것좀 수정해줘"

## 조사 내용

`Assets/Scripts/CaptureSystem/CaptureSystem.cs`를 확인한 결과:

- `CurrentOwner`(`public CaptureOwner CurrentOwner { get; private set; } = CaptureOwner.Neutral;`)는 `[SerializeField]`가 아닌 **일반 auto-property**라서 씬에 저장되지 않는다. 오브젝트가 새로 생성될 때마다(Play 진입, 씬 재로드, 빌드 실행) 항상 기본값 `Neutral`로 시작한다.
- 인스펙터의 `debugOwner` 필드(`[SerializeField] private CaptureOwner debugOwner`)는 직렬화되지만, 이 값을 실제 상태에 반영하는 `OnValidate()`는 `#if UNITY_EDITOR`로 감싸져 있어 **에디터 전용**이다. 에디터에서 `debugOwner`를 Ally로 바꾸면 `OnValidate → delayCall`이 `CurrentOwner = Ally`로 맞추고 `ApplyEffect(Ally)`를 호출해 `territoryZone.Owner = Ally`까지 반영해준다 — 그래서 에디터 안에서는 정상으로 보인다.
- 문제는 `Awake()`가 항상 이렇게 되어 있다는 것:
  ```csharp
  private void Awake()
  {
      if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);
      ApplyEffect(CurrentOwner); // ← CurrentOwner는 위 이유로 항상 Neutral
      ...
  }
  ```
  `Awake()`는 `debugOwner`를 전혀 참고하지 않고, 항상 기본값인 `CurrentOwner(Neutral)`를 `ApplyEffect()`에 넘긴다. `ApplyEffect()`는 내부에서 `territoryZone.Owner = owner`를 실행하므로, **에디터에서 저장해둔 `TerritoryZone`의 Ally 상태를 게임 시작 시점에 무조건 Neutral로 덮어쓴다.**
  - 에디터 Play 모드에서 도메인/씬 리로드가 꺼져 있으면 이 버그가 안 드러날 수 있지만, 빌드는 항상 완전히 새로 초기화되므로 매번 재현된다 — 사용자가 보고한 증상과 정확히 일치.

## 코드 변경 (제안)

**`Assets/Scripts/CaptureSystem/CaptureSystem.cs` — `Awake()`**

기존 코드:
```csharp
private void Awake()
{
    if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);

    ApplyEffect(CurrentOwner);

    if (captureBar != null)
    {
        captureBar.maxValue = captureDuration;
        captureBar.gameObject.SetActive(false);
    }
}
```

변경 코드:
```csharp
private void Awake()
{
    if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);

    // 씬에 미리 설정해둔 시작 소유 상태(debugOwner)를 게임 시작 시점(Play 모드/빌드 모두)에 반영한다.
    // 기존엔 OnValidate()가 에디터에서만 이 값을 CurrentOwner/territoryZone에 동기화했기 때문에,
    // 빌드에서는 Awake()가 항상 기본값(Neutral)으로 시작해 에디터에서 저장한 상태를 덮어쓰는 버그가 있었다.
    CurrentOwner = debugOwner;
    controlValue = debugOwner == CaptureOwner.Ally ? captureDuration
        : debugOwner == CaptureOwner.Enemy ? -captureDuration
        : 0f;

    ApplyEffect(CurrentOwner);

    if (captureBar != null)
    {
        captureBar.maxValue = captureDuration;
        captureBar.gameObject.SetActive(false);
    }
}
```

`debugOwner`가 기본값 `Neutral`인 거점은 지금과 완전히 동일하게 동작한다(`CurrentOwner = Neutral`, `controlValue = 0`). 인스펙터에서 `debugOwner`를 Ally/Enemy로 미리 설정해둔 거점만 그 값 그대로 시작하도록 바뀐다 — 게임플레이 중 점령전으로 소유권이 바뀌는 로직(`Update()`/`UpdateOwnerFromControlValue()`)은 손대지 않는다.

## 요약 / 영향받는 파일

- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (1개 파일, `Awake()` 메서드만 수정)
- 부작용: 지금까지 "에디터에서만 정상으로 보이던" 모든 Capture Point의 사전 설정(Ally/Enemy로 미리 세팅해둔 거점)이 Play 모드/빌드에서도 올바르게 적용된다. `SampleScene`뿐 아니라 다른 씬에 미리 설정해둔 거점이 있다면 그것도 함께 고쳐진다(의도한 대로 동작하게 되는 것이므로 부작용이 아니라 수정 범위 내 정상 효과).
