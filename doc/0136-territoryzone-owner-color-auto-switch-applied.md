# 0136. 소유권별 외곽선 색상 자동 전환 반영 (`doc/0135` 실제 적용)

## 날짜
2026-07-16

## 요청
`doc/0135` 제안대로 자동 연결(같은 오브젝트에서 `GetComponent`로 `TerritoryZone` 자동 탐색)하고 그대로 적용해달라.

## 변경 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (수정)
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (수정)

## 코드 변경

### `TerritoryZone.cs`
`doc/0135`의 제안 그대로 반영:
- `outlineColor` 단일 필드 → `outlineMaterial`(원본 참고용) + `neutralColor`/`allyColor`/`enemyColor` 3색 필드로 교체.
- `Awake()`에서 `outlineMaterial`(또는 URP Unlit/Sprites-Default 폴백 셰이더)을 복제한 `runtimeMaterial` 인스턴스를 만들어 `LineRenderer.material`에 지정 — 원본 에셋은 건드리지 않음.
- `Update()`가 매 프레임 `ApplyOutlineStyle()`을 호출해 `owner` 값에 맞는 색(`CurrentOwnerColor()`)을 `LineRenderer`와 `runtimeMaterial` 양쪽에 반영, 두께(`outlineWidth`)도 매 프레임 재적용(인스펙터 실시간 반영).
- `OnValidate()`에 `if (Application.isPlaying) return;` 가드 추가 — Play 모드 진입/종료 시점에 핀 동기화가 도는 문제 방지.

### `CaptureSystem.cs`
- `[SerializeField] private TerritoryZone territoryZone;` 필드 추가.
- `Awake()`에서 `if (territoryZone == null) territoryZone = GetComponent<TerritoryZone>();` — 같은 오브젝트에 붙어 있으면 자동 연결, 인스펙터에서 다른 오브젝트를 수동으로 넣어도 그걸 우선 사용.
- `ApplyEffect(CaptureOwner owner)` 끝에 `if (territoryZone != null) territoryZone.Owner = owner;` 한 줄 추가 — `Awake()`(중립 초기화)와 `CompleteCapture()`(점령 완료) 양쪽에서 이미 이 메서드를 호출하므로, 기존 이펙트 전환과 영토 외곽선 색 전환이 항상 같이 일어남.

## 요약
- `TerritoryZone`과 `CaptureSystem`을 같은 게임오브젝트에 붙여두면 별도 배선 없이 점령 상태(중립→아군, 추후 적)에 따라 다각형 외곽선이 흰색/초록/빨강으로 자동 전환된다.
- 색을 바꾸는 대상은 항상 런타임 복제 머티리얼이라 프로젝트의 공유 머티리얼 에셋은 안전함.
- `doc/0134`에서 지적됐던 문제(머티리얼 미지정으로 인게임 안 보임, 두께 실시간 반영 안 됨, Play 모드 핀 중복)도 이번 반영에 함께 포함됨.

## 확인/테스트 필요
- 유니티 에디터에서 실제로 점령 완료 시 색이 바뀌는지, Play 진입/종료 반복 시 핀이 더 이상 안 늘어나는지 직접 확인 필요(코드만 작성, 에디터 실행 확인 아직 안 함).
- 적 점령(`CaptureOwner.Enemy`) 전환 로직 자체는 여전히 코드에 없음(`doc/0135`에서 범위 밖으로 남겨둔 부분) — 나중에 추가되면 색상 매핑은 이미 준비돼 있어 별도 작업 없이 바로 동작함.

## 비고
[[confirm_before_implementing]], [[docs_session_logging_rule]] 참고. `doc/0134`(버그 수정 제안) → `doc/0135`(색상 자동전환 제안) → `doc/0136`(본 문서, 실제 적용) 순서.
