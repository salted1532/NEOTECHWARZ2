# 0357 — 제안: 점령 진행치 감쇠(방치 시 되돌아감) + 재점령 시 2배속 지우기

**날짜:** 2026-08-02

## 요청

1. "capture 포인트에 경우 아군, 적이 점령중일때 남은 시간에 대한 슬라이더가 보이는데 만약 점령중에
   점령범위 밖으로 나오게 되면 타이머가 다시 뒤로 돌아가는게 보이도록 바꿔줘 그리고 시간이 0이 되면
   그때 안보이도록 해줘"
2. "적유닛이 절반정도 점령했는데 적유닛은 없고 아군유닛이 재점령 하려고 하면 적이 점령한 시간에서
   적유닛없이 원래 줄어드는 시간 + 아군이 점령중이기 떄문에 적 점령을 줄어들이는 시간 까지 해서 2배로
   빠르게 줄어들고 다시 30초를 아군 유닛이 점령하는 시간으로 채운 다음 점령 되도록"
3. "적 유닛이 채운 시간 - (적유닛이 안 밟고 있어서 줄어드는 시간 + 아군 유닛이 밟고 있어서 적유닛이
   점령한 시간만큼 줄어들게 만드는 시간) 이런 방식으로 중립 점령상태일때는 작동하게 해줘"

## 현재 동작 (`Assets/Scripts/CaptureSystem/CaptureSystem.cs`)

`controlValue`가 `-captureDuration`(완전 적점령, 기본 -30) ~ `+captureDuration`(완전 아군점령, 기본
+30) 사이를 오간다.

```csharp
// Update() 현재 로직
if (!contested)
{
    if (alliesPresent)
        controlValue = Mathf.Min(controlValue + Time.deltaTime, captureDuration);
    else if (enemiesPresent)
        controlValue = Mathf.Max(controlValue - Time.deltaTime, -captureDuration);
    // 둘 다 없으면 그 자리에서 멈춘다 (리셋하지 않음)
}
```

- 아무도 없으면 `controlValue`가 그 자리에 정지 — 이게 아니라 `UpdateCaptureBar()`가 그 즉시 바를
  숨겨버려서, 사용자 입장에선 "점령 중이던 게 그냥 사라짐"으로 보인다(요청 1이 고치려는 부분).
- 아군만 있으면 무조건 `+Time.deltaTime`(초당 1). `controlValue`가 음수(적이 채워둔 진행치)여도
  똑같이 초당 1로만 올라간다 — "적 진행치를 지우는 중"이라는 개념 자체가 없다(요청 2, 3이 추가하려는
  부분).
- `captureBar.value`는 아군이 있으면 `Clamp(controlValue, 0, duration)`이라서, `controlValue`가
  음수인 동안은 무조건 0으로 보인다 — 적 진행치가 줄어드는 과정 자체가 화면에 안 보임.

## 제안: "감쇠(decay)" 하나로 세 요청을 통합

세 요청 모두 "초당 일정량씩 진행치가 0(중립) 쪽으로 줄어든다"는 동일한 개념의 다른 상황일 뿐이다:

- **요청 1** = 아무도 없을 때 감쇠
- **요청 2, 3** = 아군만 있는데 `controlValue`가 음수(적 진행치)일 때, "아무도 없을 때의 감쇠" +
  "아군이 그 자리를 밟고 있어서 추가로 미는 힘"이 **겹쳐서 2배 속도**로 감쇠. 0을 넘어서면 이후는
  평소처럼(감쇠 없이) 아군 점령 속도로 30초를 채운다. 적만 있고 `controlValue`가 양수일 때도 대칭.

```csharp
[SerializeField] private float captureDuration = 30f;
[SerializeField] private float decayRate = 1f; // 아무도 없거나(자연 감쇠) 반대 진영이 지우는 중일 때 초당 감소량 - 기본값은 점령 속도(초당 1)와 같아서, "재점령 시 2배 속도"가 정확히 2배가 됨
```

```diff
     private void Update()
     {
         alliesInRange.RemoveAll(unit => unit == null);
         enemiesInRange.RemoveAll(unit => unit == null);

         bool alliesPresent = alliesInRange.Count > 0;
         bool enemiesPresent = enemiesInRange.Count > 0;
         bool contested = alliesPresent && enemiesPresent;

         if (!contested)
         {
             if (alliesPresent)
-                controlValue = Mathf.Min(controlValue + Time.deltaTime, captureDuration);
+            {
+                // controlValue가 음수(적 진행치가 남아있음)면 자연 감쇠(decayRate)와 아군이 밟고 있어서
+                // 미는 힘(점령 속도 1/sec)이 겹쳐 2배 속도로 지워진다. 0을 넘어서면(적 진행치를 다
+                // 지우면) 이후는 평소처럼 초당 1로 아군 진행치를 채운다.
+                float rate = controlValue < 0f ? decayRate + 1f : 1f;
+                controlValue = Mathf.Min(controlValue + rate * Time.deltaTime, captureDuration);
+            }
             else if (enemiesPresent)
-                controlValue = Mathf.Max(controlValue - Time.deltaTime, -captureDuration);
-            // 둘 다 없으면 그 자리에서 멈춘다 (리셋하지 않음)
+            {
+                float rate = controlValue > 0f ? decayRate + 1f : 1f;
+                controlValue = Mathf.Max(controlValue - rate * Time.deltaTime, -captureDuration);
+            }
+            else if (Mathf.Abs(controlValue) < captureDuration)
+            {
+                // 아무도 없을 때: 아직 어느 쪽도 완전히 점령 못 한 진행치만 중립(0)으로 서서히 되돌아간다.
+                // 이미 완전히 점령된(±captureDuration) 거점은 방치해도 저절로 안 뺏긴다(그대로 유지).
+                controlValue = Mathf.MoveTowards(controlValue, 0f, decayRate * Time.deltaTime);
+            }
         }

         UpdateCaptureBar(alliesPresent, enemiesPresent, contested);
         UpdateOwnerFromControlValue();
     }
```

```diff
     private void UpdateCaptureBar(bool alliesPresent, bool enemiesPresent, bool contested)
     {
-        bool progressing = !contested && (alliesPresent || enemiesPresent)
+        // 아무도 없어도 아직 중립으로 다 안 돌아간(0이 아닌) 진행치가 남아있으면 바를 계속 보여준다
+        // (뒤로 돌아가는 애니메이션이 보이도록) - 0에 도달하는 순간 아래 조건에서 자동으로 숨겨짐.
+        bool decaying = !alliesPresent && !enemiesPresent && controlValue != 0f;
+
+        bool progressing = !contested && (alliesPresent || enemiesPresent || decaying)
             && !(alliesPresent && controlValue >= captureDuration)
             && !(enemiesPresent && controlValue <= -captureDuration);

         SetCaptureBarVisible(progressing);

         if (!progressing || captureBar == null) return;

-        captureBar.value = alliesPresent
-            ? Mathf.Clamp(controlValue, 0f, captureDuration)
-            : Mathf.Clamp(-controlValue, 0f, captureDuration);
+        // 절댓값 하나로 통일 - 적 진행치가 줄어드는 것도, 아군 진행치가 느는 것도 같은 슬라이더가
+        // 자연스럽게 이어서 보여준다(줄어들다 0에서 방향이 바뀌어 다시 느는 것도 끊김없이 표현됨).
+        captureBar.value = Mathf.Clamp(Mathf.Abs(controlValue), 0f, captureDuration);
     }
```

### 동작 확인 (예시)

- **요청 1**: 아군이 15초 점령하다 범위 밖으로 나감 → 아무도 없음, `controlValue`(15) → `decayRate`
  속도로 0을 향해 줄어드는 게 바에 그대로 보임 → 0 도달 시 바 숨김.
- **요청 2/3**: 적이 절반(`controlValue = -15`) 채운 상태에서 적은 없고 아군만 옴 → `rate = decayRate
  + 1 = 2`/초로 -15 → 0까지 7.5초 만에 지워짐(바에 줄어드는 게 보임) → 0을 넘는 순간부터는 `rate = 1`로
  전환돼 0 → 30까지 30초 걸려 아군 점령 완료.
- 교착(양쪽 다 있음)은 기존과 동일하게 그대로 정지 — 이번 변경 범위 밖.

## 확인 필요 사항 (1차)

- 위 통합 규칙(감쇠 개념 하나로 세 요청 처리)으로 진행해도 되는지
- `decayRate` 기본값을 점령 속도와 동일한 1(초당 1)로 잡아서 "2배" 요청과 정확히 맞춘 것— 이대로
  괜찮은지, 아니면 감쇠 속도를 점령 속도보다 느리게/빠르게 다르게 갈 지
- **이미 완전히 점령된 거점(±30 도달, 소유자 이미 전환됨)은 아무도 없어도 감쇠 없이 그대로 유지**하도록
  가정했음 — 방치했다고 저절로 중립으로 안 돌아가게 한 것. 이 부분이 의도와 맞는지 (혹시 완전 점령
  거점도 시간이 지나면 흔들리길 원하시면 다른 얘기)

## 개정 (2026-08-02) — "완전 점령"과 "중립 진행중"은 다른 상태였다

사용자 답변:

> "현재 적유닛이 점령을 해서 적 점령이 되었을때 그걸 다시 중립으로 되돌리려면 30초 점령하고 중립
> 점령지가 되면 또 30초 점령하고 내꺼로 바뀌는 방식으로 해야할거 같아. 적 유닛도 내가 점령한 점령지의
> 경우 30초 점령 후 중립으로 변하고. 중립에 경우는 상대가 점령한 시간 만큼을 지우고 내꺼로 30초를
> 채워야 되는 시스템으로"

이걸로 알게 된 것: 사용자가 말하는 상황이 **두 가지 다른 상태**였다.

1. **완전 점령 상태**(`CurrentOwner`가 이미 Ally/Enemy로 확정, `controlValue == ±30`) — 상대가 되찾으려면
   **30초(1배속) 밀어서 중립으로**, 거기서 **또 30초(1배속)** 채워야 자기 것이 됨. 총 60초, 보너스 없음.
2. **중립 진행중 상태**(한 번도 끝까지 점령된 적 없음, `CurrentOwner == Neutral`인데 `controlValue`가
   0이 아님, 예: 적이 절반쯤 채운 -15) — 이 경우는 지난 답변 그대로 **상대 진행치를 2배속(감쇠+미는 힘)
   으로 지우고 0을 넘으면 30초 채움**.

### 왜 지금 코드로는 이 둘을 구분 못 하는가

```csharp
// CaptureSystem.cs:110-115 (현재)
CaptureOwner newOwner =
    controlValue >= captureDuration ? CaptureOwner.Ally :
    controlValue <= -captureDuration ? CaptureOwner.Enemy :
    CaptureOwner.Neutral; // <- 극값에서 "조금이라도" 벗어나면 그 즉시 Neutral로 바뀜
```

`Owner`가 매 프레임 `controlValue`의 "현재 구간"만 보고 다시 계산되기 때문에, 완전 점령된 -30에서
아군이 단 한 프레임만 밀어도(-29.99) 그 즉시 `Owner`가 Neutral로 바뀐다 — "30초 밀어야 중립이 됨"이
아니라 "닿자마자 중립"이 되는 셈이라, 완전 점령 상태와 중립 진행중 상태를 서로 구별할 방법이 없다
(둘 다 그냥 `Owner == Neutral`이 되어버림).

### 제안: `Owner`를 "구간"이 아니라 "경계를 실제로 통과했을 때"만 바꾸도록 변경

`Owner`가 `+captureDuration`(Ally 확정), `-captureDuration`(Enemy 확정), `0`(중립 확정) **세 경계값에
실제로 도달했을 때만** 바뀌고, 그 사이를 지나가는 동안은 이전 `Owner`를 그대로 유지한다(sticky).
이러면 "완전 점령된 -30에서 0까지 밀리는 30초 동안은 여전히 Owner=Enemy로 표시"되다가, 정확히 0에
도달하는 순간 Owner=Neutral로 바뀌고, 거기서 다시 +30까지 30초를 채워야 Owner=Ally로 바뀐다 —
말씀하신 "30초 + 30초"가 그대로 재현됨.

같은 프레임에 0을 정확히 밟지 못하고 지나칠 수 있으므로(예: -0.5 → +0.3으로 한 프레임에 건너뜀),
부호가 바뀌는 순간을 감지해서 그 프레임엔 정확히 0으로 스냅한다(같은 프레임에 반대쪽으로 더 진행하는
오차는 한 프레임(16ms) 수준이라 무시 가능).

### 통합 규칙 재정리 (감쇠/2배속은 `Owner == Neutral`일 때만)

"2배속 지우기" 보너스는 **한 번도 끝까지 점령된 적 없는 상태(`Owner == Neutral`)** 에서만 적용되고,
완전 점령된 상태를 되돌릴 땐 항상 1배속만 적용된다:

```csharp
private const float BaseRate = 1f;

private float CurrentRate(bool towardAlly) // towardAlly: 지금 세력을 늘리려는 쪽이 아군인지
{
    if (CurrentOwner != CaptureOwner.Neutral)
        return BaseRate; // 완전 점령을 되돌리는 중 - 항상 1배속

    bool erasingOpponentProgress = towardAlly ? controlValue < 0f : controlValue > 0f;
    return erasingOpponentProgress ? BaseRate + decayRate : BaseRate; // 중립 상태에서 상대 진행치 지우는 중이면 2배속
}
```

방치 시 감쇠(요청 1)도 "쉬는 지점"이 `Owner`에 따라 달라지도록 일반화한다 — 완전 점령된 거점을
공격하다 상대도 나도 다 빠지면, 그 거점은 원래 주인 쪽으로 서서히 "회복"되고(자연스러운 확장, 아래
확인 필요), 중립 진행중이었으면 기존 요청 1대로 0으로 되돌아간다:

```csharp
else // 아무도 없음
{
    float restPoint = CurrentOwner == CaptureOwner.Ally ? captureDuration
        : CurrentOwner == CaptureOwner.Enemy ? -captureDuration
        : 0f;
    controlValue = Mathf.MoveTowards(controlValue, restPoint, decayRate * Time.deltaTime);
}
```

### 두 예시로 검증

- **메시지 2 (완전 점령 되돌리기)**: `Owner=Enemy`, `controlValue=-30`. 아군만 30초 계속 점령 →
  `Owner`는 그 30초 동안 계속 Enemy로 표시되다가 `controlValue`가 정확히 0에 도달하는 순간 Neutral로
  전환. 이후 아군이 계속 있으면 또 30초 걸려 `controlValue=+30`, `Owner=Ally`로 전환. 총 60초, 중간에
  적이나 아군이 빠지면 그 시점 `Owner`(회복 방향)로 서서히 되돌아감.
- **메시지 1 (중립 진행중, 절반 점령)**: `Owner=Neutral`, `controlValue=-15`(적이 절반 채움, 한 번도
  끝까지 안 감). 아군만 오면 2배속(-15→0을 7.5초)으로 지우고, 0을 넘는 순간부터는 1배속으로 30초 채워
  `controlValue=+30`, `Owner=Ally`.

## 추가 확인 (2026-08-02) — 밀어내는 과정도 슬라이더로

"밀어 내는 과정을 슬라이더로도 보이도록 해줄래 슬라이더가 꽉 채워져있다가 아군유닛의 점령으로
되돌아가는 느낌으로" — 이미 위 1차 개정의 `captureBar.value = Mathf.Clamp(Mathf.Abs(controlValue), 0f,
captureDuration)` 공식으로 충족됨. 완전 점령 상태(`controlValue = ±30`)에서 시작하면
`Abs(controlValue) = captureDuration`(=슬라이더 최댓값)이라 처음엔 꽉 찬 채로 보이고, 아군이 밀어서
0으로 갈수록 그대로 줄어든다. 추가 코드 변경 불필요 — 이 요청은 별도 항목 아니라 기존 계획에 이미
포함됨.

## 확인 필요 사항 (2차) — 답변 완료

- "적 점령 방치 -> 서서히 회복, 중립 방치 -> 줄어듬" — 두 질문 모두에 대한 답으로 확인(sticky 전환이
  전제돼야 "적 점령"과 "중립"을 구분해서 서로 다른 회복 지점을 정의할 수 있으므로, 1번 질문도 함께
  승인된 것으로 처리).

## 적용 (2026-08-02)

`Assets/Scripts/CaptureSystem/CaptureSystem.cs`에 위 설계 그대로 적용.

- `decayRate`(기본 1) 필드 추가.
- `Update()`: 아군만 있으면 `AllyRate()`, 적만 있으면 `EnemyRate()`, 아무도 없으면
  `Mathf.MoveTowards(controlValue, RestPoint(), decayRate * Time.deltaTime)`로 변경.
- `AllyRate()`/`EnemyRate()`: `Owner == Neutral`이고 반대 진영 진행치를 지우는 중이면 `1 + decayRate`
  (기본 2배속), 그 외엔 `1`.
- `RestPoint()`: `Owner == Ally`면 `+captureDuration`, `Owner == Enemy`면 `-captureDuration`,
  `Neutral`이면 `0`.
- `UpdateOwnerFromControlValue()`: 매 프레임 구간 재계산 대신, `±captureDuration` 도달 또는(이전
  Owner가 Ally/Enemy일 때) `controlValue`가 0을 지나는 순간에만 `Owner`를 sticky하게 전환하도록 변경.
- `UpdateCaptureBar()`: 아무도 없어도 `controlValue`가 아직 `RestPoint()`에 도달 못했으면
  (`returningToRest`) 바를 계속 보여줌. 값은 `Mathf.Abs(controlValue)`로 통일 — "밀어내는 과정을
  슬라이더로도" 요청(완전 점령 상태에서 꽉 찬 채로 시작해 0으로 줄었다가 반대쪽으로 다시 차오름)이
  이 통일된 값 하나로 자연스럽게 표현됨.

`npx uloop-cli compile` 통과 (에러 0, 경고 28개 — 전부 이번 변경과 무관한 기존 경고, 신규 경고 없음).

**확인 필요 사항**: Unity 에디터에서 실제 거점을 켜고 아래 시나리오들을 확인 부탁드립니다.
- 적이 완전 점령한 거점에 아군만 30초 서 있으면 → 30초 뒤 중립, 다시 30초 뒤 아군 점령 (중간엔 계속
  적 색/이펙트로 보이다가 정확히 중립 도달 시 흰색으로 전환)
- 적이 절반쯤(예: 15초) 채운 미완료 거점에 아군만 오면 → 2배속으로 지워지고 0을 넘으면 이후 30초 걸려
  아군 점령
- 진행 중(아군만 또는 적만) 유닛이 범위를 벗어나면 → 바가 계속 보이며 되돌아가다가, 완전 점령
  거점이면 원래 소유자 쪽으로 회복 / 중립 진행중이었으면 0에서 바가 사라짐
