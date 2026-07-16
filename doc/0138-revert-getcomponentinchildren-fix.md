# 0138. `doc/0137` 수정 되돌림 (컴파일러 이상 보고)

## 날짜
2026-07-16

## 요청
`doc/0137`에서 적용한 `GetComponentInChildren<TerritoryZone>(true)` 변경 이후 컴파일러가 제대로 동작하지 않는다는 보고 — 되돌려달라는 요청.

## 변경
### 기존 코드 (`doc/0137`에서 반영한 상태)
```csharp
if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);
```

### 되돌린 코드 (`doc/0136` 이전 상태로 복원)
```csharp
if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();
```

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (되돌림)

## 참고
`GetComponentInChildren<T>(bool includeInactive)` 자체는 유니티 표준 API라 문법적으로 컴파일 에러가 날 코드는 아니다 — 어떤 에러/증상이었는지(콘솔 메시지, 어느 스크립트에서 나는지) 알려주면 진짜 원인을 다시 확인하겠다. 되돌린 지금 상태로는 `doc/0137`에서 찾은 문제(TerritoryZone이 자식 오브젝트에 있어 자동 연결 안 되는 것)가 다시 그대로 남아 있다는 점은 참고 바람.

## 비고
[[confirm_before_implementing]] — 사용자가 직접 되돌려달라고 명시적으로 요청해서 바로 반영.
