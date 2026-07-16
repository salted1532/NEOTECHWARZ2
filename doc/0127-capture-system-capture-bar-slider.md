# 0127. CaptureSystem: 점령 진행도 슬라이더(CaptureBar) 연결

## 날짜
2026-07-16

## 요청
CaptureBar 슬라이더(UI)를 만들어뒀는데, 점령 시간(`captureTimer`/`captureDuration`) 진행 상황과 연결되도록 `CaptureSystem`에 슬라이더 필드를 추가해서 인스펙터에서 직접 연결할 수 있게 해달라.

## 조사한 기존 코드 관례
`Assets/Scripts/Unit/HealthManager.cs`가 이미 같은 패턴을 씀:
```csharp
[SerializeField] private Slider healthSlider;
...
healthSlider.maxValue = max;
healthSlider.value = current;
```
`UnityEngine.UI.Slider`를 `[SerializeField]`로 노출하고, 값이 바뀔 때 `maxValue`/`value`만 갱신하는 방식. CaptureSystem도 동일하게 맞춤.

## 답변 / 변경사항
`Assets/Scripts/CaptureSystem/CaptureSystem.cs`:
1. `using UnityEngine.UI;` 추가
2. `[SerializeField] private Slider captureBar;` 필드 추가 (인스펙터에서 CaptureBar 슬라이더 연결용)
3. `Awake()`에서 `captureBar.maxValue = captureDuration` 1회 설정
4. `Update()`에서 타이머를 누적한 뒤 `captureBar.value = captureTimer`로 매 프레임 갱신 (아군이 없어서 타이머가 멈춰있을 때도 마지막 값 그대로 유지되는 게 자연스러움)
5. 점령 완료(`CompleteCapture`) 시 `captureBar.value = captureDuration`으로 꽉 채워서 마무리

null 체크는 `HealthManager`처럼 `captureBar == null`이면 건너뛰는 방식으로 안전하게 처리(슬라이더를 아직 안 끼워도 에러 안 남).

반영 완료.

## 변경 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs`
