# 0126. 점령 시스템(CaptureSystem) MVP

## 날짜
2026-07-16

## 요청
큰 그림(설계 구상):
- 거점 포인트(비콘)가 맵에 있고, 그 위에 아군 병력이 있으면 일정 시간 후 점령 완료
- 거점 3가지 상태: 아군점령(초록) / 중립(흰색) / 적점령(빨강)
- 해당 영토 안에서만 건설 가능하도록 PlacementSystem 제한
- 영토 밖 자원은 채취 불가
- 영토를 상실하면 그 안의 건물은 건설/생산/연구/체력회복 불가(비활성화)

이번에 실제로 요청한 범위(MVP):
- `Assets/Scripts/CaptureSystem` 폴더에 `CaptureSystem` 스크립트 작성
- 아군 유닛이 콜리전(트리거) 안에 있으면 점령 시간 30초 진행
- 30초가 차서 점령이 완료되면 "점령이 되었다"는 내용의 `Debug.log` 출력
- 점령 상태(초록/빨강/흰색)에 따라 재생할 이펙트를 인스펙터에서 지정할 수 있는 필드 추가 (유저가 만든 이펙트 에셋을 끼워 넣을 용도)

PlacementSystem 연동/자원 채취 제한/건물 비활성화 등 나머지 설계는 이번 스크립트 범위에 포함하지 않음 — 상태(현재 누구 소유인지)를 다른 시스템이 나중에 읽어갈 수 있도록 최소한의 골격만 마련.

## 조사한 기존 코드 관례
- 아군/적 구분은 태그가 아니라 **컴포넌트 타입**으로 한다: 아군 유닛 = `UnitController`, 적 유닛 = `EnemyController` (서로 다른 독립 클래스, `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/Enemy/EnemyController.cs`). `Enemy` 태그는 `EnemyController` 쪽 오브젝트에 붙어있고 `AttackRange.cs`가 이 태그로 적만 감지한다(`Assets/Scripts/Unit/AttackRange.cs:38`).
- 트리거 범위 내 오브젝트 추적은 `AttackRange.cs`처럼 `OnTriggerEnter`/`OnTriggerExit`으로 리스트를 관리하고, `Update()`에서 `RemoveAll(x => x == null)`로 파괴된 항목을 정리하는 패턴을 그대로 따름.
- 기존 `Assets/prefabs/Capture_Point/Capture_Point.prefab`에 이미 `SphereCollider`(반지름 10)가 있는데 **`m_IsTrigger: 0`(트리거 아님)** 상태다. `OnTriggerEnter`가 동작하려면 이 콜라이더의 Is Trigger를 켜야 한다 — 스크립트만으로는 못 고치는 부분이라 별도로 안내함(프리팹 수정은 이번 변경에 포함 안 함).
- `Assets/prefabs/UI/Capture_point GREEN.prefab` / `RED` / `White` 프리팹이 이미 존재 — 각각 루트에 `ParticleSystem`이 붙은 이펙트. 이걸 그대로 CaptureSystem의 상태별 이펙트 필드에 인스펙터에서 끼워 넣을 수 있게 `GameObject` 타입으로 필드를 잡음(파티클이든 다른 연출이든 `SetActive`로 껐다 켤 수 있게 범용으로).

## 설계
### 상태
```csharp
public enum CaptureOwner { Neutral, Ally, Enemy }
```

### 핵심 필드
- `public float captureDuration = 30f;` — 점령에 필요한 시간(초)
- `public CaptureOwner CurrentOwner { get; private set; }` — 현재 소유 상태 (다른 시스템이 나중에 읽어갈 값)
- `public GameObject neutralEffect / allyEffect / enemyEffect;` — 상태별로 재생할 이펙트 오브젝트 (인스펙터에서 GREEN/RED/White 프리팹 연결)

### 동작
1. 트리거 콜라이더에 `UnitController`가 들어오면 `alliesInRange`에 추가, 나가면 제거 (아군 전용 — 요청 범위에 적 점령까지는 없지만 향후 `EnemyController` 목록도 같은 패턴으로 추가 가능하도록 구조는 대칭적으로 잡음).
2. 매 프레임, 이미 아군이 점령한 상태(`CurrentOwner == Ally`)가 아니고 `alliesInRange`가 비어있지 않으면 `captureTimer`를 `Time.deltaTime`만큼 누적.
   - 범위 안이 비면 타이머는 그 자리에서 멈춤(리셋하지 않음) — 다시 아군이 들어오면 이어서 진행.
3. `captureTimer >= captureDuration`이 되면 `CurrentOwner = Ally`로 전환, `Debug.Log("점령이 되었다")` 출력, 상태별 이펙트 갱신(`allyEffect` 활성화, 나머지 비활성화).
4. `ApplyEffect(CaptureOwner)` 헬퍼로 3개 이펙트 오브젝트 중 현재 상태에 해당하는 것만 `SetActive(true)`, 나머지는 `false`. `Awake()`에서 초기 상태(중립=흰색)로 한 번 호출.

### 아군/적 판별 방식
태그가 아니라 `GetComponent<UnitController>()`로 판별하기로 결정. 이유:
- 기존 코드 관례와 일치 (`AttackRange`는 `"Enemy"` 태그만 쓰고, 아군 판별은 항상 `UnitController`/`BuildingController` 타입 체크).
- 아군용 태그(`"Player"`/`"Ally"`)가 프로젝트에 아예 없어서, 태그 방식을 쓰려면 TagManager에 태그를 새로 추가하고 모든 아군 유닛 프리팹에 일일이 붙여야 함 — 새 유닛 프리팹을 만들 때 빠뜨리면 조용히 점령이 안 되는 버그로 이어질 위험.
- `GetComponent`는 트리거 진입/이탈 시점에만 호출되고 매 프레임 도는 게 아니라서 성능 부담도 없음.

## 변경/추가 파일
- `Assets/Scripts/CaptureSystem/CaptureSystem.cs` (신규)

## 비고
- 프리팹 쪽 `Capture_Point`의 `SphereCollider`가 `Is Trigger`로 꺼져있어, 씬에서 직접 켜줘야 스크립트가 실제로 동작함(에디터 작업 필요, 코드로 강제하지 않음).
- 적 점령(빨강)·중립 전환·PlacementSystem/자원채취/건물 비활성화 연동은 이번 범위 밖 — 후속 작업으로 별도 문서에서 다룰 예정.
