# 0542 - EnemyAIDirector 인스펙터에 "적 전체" 유닛/건물 리스트로 교체 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
"현재 별동대 리스트 말고 그냥 적이 EnemyController, EnemyBuildingController 가진 모든 유닛,건물에
대한 리스트"

→ doc/0541에서 노출한 `garrison`/`raidGarrison`은 이 director가 관리하는 "병력 풀"(웨이브/별동대
차출용 내부 상태)이라 별동대로 나간 유닛까지 한 리스트에 섞여 있고, 이 director가 스폰한 유닛만
포함됨(미션 씬에 미리 배치해둔 유닛/건물은 안 잡힘). 요청은 그런 내부 풀이 아니라 그냥 현재 씬에
존재하는 `EnemyUnitController`/`EnemyBuildingController` 전체를 있는 그대로 보여달라는 것. 이 문서는
제안일 뿐, 아직 코드 수정 안 함.

## 설계안 - `FindObjectsByType`로 씬 전체를 긁어 별도 디버그 리스트에 채움
doc/0541에서 추가한 `garrison`/`raidGarrison`의 `[SerializeField]`/`[Header]`는 되돌리고(원래
`readonly` private로 복귀 - 내부 로직용으로만 사용), 대신 다음 필드를 새로 추가:

```csharp
[Header("<디버그> 현재 씬에 존재하는 적 전체 (런타임 전용, 주기적으로 갱신)")]
[SerializeField] private List<EnemyUnitController> allEnemyUnits = new List<EnemyUnitController>();
[SerializeField] private List<EnemyBuildingController> allEnemyBuildings = new List<EnemyBuildingController>();
```

갱신은 이미 주기적으로 도는 `ReinforceRoutine`(주기: `reinforceCheckInterval`)에 두 줄만 추가 -
새 코루틴/타이머를 따로 만들 필요 없음:
```csharp
allEnemyUnits.Clear();
allEnemyUnits.AddRange(FindObjectsByType<EnemyUnitController>(FindObjectsSortMode.None));
allEnemyBuildings.Clear();
allEnemyBuildings.AddRange(FindObjectsByType<EnemyBuildingController>(FindObjectsSortMode.None));
```

## 참고 - 이 리스트는 "이 director의 진영"이 아니라 "씬 전체"임
`EnemyUnitController`/`EnemyBuildingController`엔 어느 `EnemyAIDirector`(진영) 소속인지 구분하는
필드가 없음(그렙 확인 완료) - `faction` 구분은 `EnemyAIDirector` 인스펙터에만 있고 유닛/건물 자체엔
없음. 그래서 미션에 `EnemyAIDirector`가 여러 개(예: OC 기지 + Spore Brood 기지) 있으면, 각 director의
`All Enemy Units`/`All Enemy Buildings`엔 전부 똑같이 "씬에 있는 적 전체"가 뜸 - director별로 자기
진영 소속만 걸러서 보여주는 기능은 아님. 이 프로젝트에 그런 소속 구분 자체가 없어서, 지금 만들 수 있는
가장 간단한 형태가 이거임(진영별로 나누려면 유닛/건물에 소속 필드를 새로 추가해야 하는데, 요청 범위를
넘어서는 별도 작업).

## 컴파일/실행 확인 계획
`npx uloop-cli compile`로 에러 확인.

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. 설계안대로 교체(`garrison`/`raidGarrison`은 readonly로 원복, `allEnemyUnits`/`allEnemyBuildings` 신규 추가).
2. 갱신 주기는 `reinforceCheckInterval` 재사용(새 코루틴 없음).

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`

## 코드 변경

### 필드 (doc/0541에서 노출했던 것 원복 + 신규 추가)
```csharp
private readonly List<EnemyUnitController> garrison = new List<EnemyUnitController>();
private readonly List<EnemyUnitController> raidGarrison = new List<EnemyUnitController>();

[Header("<디버그> 현재 씬에 존재하는 적 전체 (런타임 전용, 주기적으로 갱신)")]
[SerializeField] private List<EnemyUnitController> allEnemyUnits = new List<EnemyUnitController>();
[SerializeField] private List<EnemyBuildingController> allEnemyBuildings = new List<EnemyBuildingController>();
```

### `ReinforceRoutine`에 갱신 두 줄 추가
```csharp
allEnemyUnits.Clear();
allEnemyUnits.AddRange(FindObjectsByType<EnemyUnitController>(FindObjectsInactive.Exclude));
allEnemyBuildings.Clear();
allEnemyBuildings.AddRange(FindObjectsByType<EnemyBuildingController>(FindObjectsInactive.Exclude));
```
`FindObjectsSortMode.None` 오버로드는 최신 Unity에서 obsolete라 경고가 추가로 뜨길래(프로젝트
기존 경고 39개 → 43개), 경고 없는 `FindObjectsInactive.Exclude` 오버로드로 바꿔서 기존 경고 개수
그대로 유지(39개).

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개, 경고 39개(기존과 동일 - 신규 경고 없음).

## 사용 방법
Play 모드에서 아무 `EnemyAIDirector`나 선택하면 "<디버그> 현재 씬에 존재하는 적 전체" 아래
`All Enemy Units`/`All Enemy Buildings`에 씬에 있는 `EnemyUnitController`/`EnemyBuildingController`
전체가 뜬다(최대 `reinforceCheckInterval` 지연으로 갱신, 기본 20초). 소속 진영 구분은 안 됨(설계안의
"참고" 항목 참고).
