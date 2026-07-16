# 0140. `GetComponentInChildren` 수정 재적용

## 날짜
2026-07-16

## 요청
`doc/0139`에서 확인된 대로, 이전 컴파일 멈춤(`doc/0138`에서 되돌렸던 원인)은 실제로는 `GetComponentInChildren` 코드와 무관하고 `bee_backend` 유령 락 때문이었음을 확인 — 이 수정을 다시 적용해달라는 요청.

## 변경
### 기존 코드 (`doc/0138`에서 되돌린 상태)
```csharp
if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();
```

### 변경 코드 (`doc/0137`과 동일하게 재적용)
```csharp
if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);
```

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정)

## 요약
`CaptureSystem`(부모, `Capture_Point`)이 자식 오브젝트(`Capture_territory`)에 있는 `TerritoryZone`을 자동으로 찾도록 복원 — 이제 점령 완료 시 `territoryZone.Owner`가 갱신되어 외곽선 색이 흰색/초록/빨강으로 자동 전환될 것으로 기대됨.

## 확인/테스트 필요
유니티 에디터에서 점령 완료 시 `Capture_territory`의 외곽선 색이 실제로 바뀌는지 확인 필요.

## 비고
[[confirm_before_implementing]] — 재적용 여부를 사용자에게 확인 후(재적용 선택) 반영함.
