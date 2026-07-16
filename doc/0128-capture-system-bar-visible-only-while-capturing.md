# 0128. CaptureSystem: 점령 진행 중일 때만 CaptureBar 표시

## 날짜
2026-07-16

## 요청
점령이 시작될 때만 CaptureBar가 보이도록 수정.

## 원인/현재 상태
`Assets/Scripts/CaptureSystem/CaptureSystem.cs`는 `captureBar`를 값만 갱신할 뿐 표시 여부는 전혀 건드리지 않아서, 인스펙터에서 슬라이더를 항상 켜둔 상태로 두면 점령 전/후에도 계속 보인다.

## 답변 / 변경사항
`HealthManager.cs`가 `healthSlider.gameObject.SetActive(current < max)`로 만피일 때 숨기는 것과 같은 패턴으로 적용:
1. `Awake()`에서 `captureBar.gameObject.SetActive(false)` — 시작 시 숨김.
2. `Update()`에서 "범위 안에 아군이 있어서 실제로 진행 중인지"(`alliesInRange.Count > 0`, 그리고 아직 점령 완료 전)에 따라 매 프레임 `captureBar.gameObject.SetActive(...)`로 켜고 끔.
   - 아군이 없어서 타이머가 멈춰있는 동안(범위 이탈)은 숨김.
   - 이미 점령 완료된 상태(`CurrentOwner == Ally`)에서도 숨김 — 진행 바이므로 다 채워진 뒤에는 볼 필요 없음.
3. 표시 토글 로직을 `SetCaptureBarVisible(bool)` 헬퍼로 뽑아서 `Update()`의 여러 반환 경로에서 중복 없이 재사용.

## 변경 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs`

## 비고
반영 완료.
