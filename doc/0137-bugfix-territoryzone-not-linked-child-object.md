# 0137. 버그 수정: TerritoryZone이 자식 오브젝트라 자동 연결 안 되던 문제

## 날짜
2026-07-16

## 요청
`doc/0136` 반영 후에도 점령 시 외곽선 색깔이 안 바뀐다는 보고 — 원인 확인 요청.

## 조사 내용
`Assets/prefabs/Capture_Point/Capture_Point.prefab`을 직접 열어보니 실제 구조가 다음과 같았다:
```
Capture_Point (CaptureSystem, SphereCollider)  ← 부모
 └ Capture_territory (TerritoryZone, LineRenderer)  ← 자식
```
`CaptureSystem.Awake()`의 자동 연결 코드:
```csharp
if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();
```
`GetComponent<T>()`는 **같은 게임오브젝트에서만** 컴포넌트를 찾는다 — 자식은 검색하지 않는다. `TerritoryZone`이 부모(`Capture_Point`)가 아니라 자식(`Capture_territory`)에 붙어 있으므로 이 호출은 항상 `null`을 반환했고, 프리팹 YAML에도 `territoryZone` 필드가 비어있는 채였다. 결과적으로 `territoryZone`이 계속 `null`로 남아 `ApplyEffect()`의 `territoryZone.Owner = owner;`가 한 번도 실행되지 않아 색이 안 바뀐 것.

## 수정

### 기존 코드 (`Assets/Scripts/CaptureSystem/CaptureSystem.cs`)
```csharp
if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();
```

### 변경 코드
```csharp
if (territoryZone == null) territoryZone = GetComponentInChildren<TerritoryZone>(true);
```
`GetComponentInChildren<T>(true)`는 자기 자신과 모든 자식(비활성 오브젝트 포함)에서 컴포넌트를 찾는다 — 지금 프리팹처럼 부모(CaptureSystem)와 자식(TerritoryZone) 구조로 나뉜 경우도, 같은 오브젝트에 두 컴포넌트를 함께 붙인 경우도 둘 다 자동으로 연결된다.

## 요약
`Capture_Point` 프리팹에서 `TerritoryZone`이 `Capture_territory` 자식 오브젝트에 있어 `GetComponent`로는 못 찾던 것을 `GetComponentInChildren`으로 바꿔 해결.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정)

## 확인/테스트 필요
유니티 에디터에서 점령 완료 시 `Capture_territory`의 외곽선이 실제로 흰색→초록(→빨강)으로 바뀌는지 직접 확인 필요.

## 비고
[[confirm_before_implementing]] — `GetComponentInChildren` 방식으로 수정할지 인스펙터 수동 연결로 할지 사용자에게 확인 후(수동 연결 대신 `GetComponentInChildren` 선택) 반영함.
