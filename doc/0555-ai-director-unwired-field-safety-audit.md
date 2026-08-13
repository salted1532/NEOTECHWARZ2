# 0555 - EnemyAIDirector/AllyAIDirector 인스펙터 미연결 시 안전성 점검

## 날짜
2026-08-13

## 요청 내용
"아군AI나 적AI나 내가 사용할 곳에선 인스펙터를 연결해서 사용할거고 안쓰는곳은 그대로 둘건데 뭐
연결된게 없어서 작동할수 없는 상태이면 오류 발생없이 그냥 작동 안하도록 해줘"

즉: 씬에 여러 개의 `EnemyAIDirector`/`AllyAIDirector`를 배치할 예정인데, 그중 일부는 인스펙터 필드를
다 채워 쓰고 일부는 비워둔 채로 둘 것 - 비워둔 쪽이 그 기능을 못 쓰는 건 당연하지만, 그 상태에서
오류(NullReferenceException 등)가 나면 안 되고 조용히 아무 동작도 안 해야 한다는 요청.

## 조사 내용 - 코드 변경 없음, 기존 코드 전수 점검
`Assets/Scripts/System/EnemyAIDirector.cs`, `Assets/Scripts/System/AllyAIDirector.cs` 두 파일을
필드별로 전부 훑어서, 인스펙터에서 비워뒀을 때(리스트면 빈 리스트, 참조면 null) 실제로 예외 없이
"그냥 작동 안 함"이 되는지 확인함.

### 이미 안전한 것으로 확인된 지점들
- **웨이브 타이머(`waveTimes`)**: `Start()`에서 `if (waveTimes.Count > 0) StartCoroutine(AttackWaveRoutine())`
  로 감싸져 있어, 비워두면 코루틴 자체가 시작하지 않음(EnemyAIDirector.cs:306, AllyAIDirector.cs:125).
- **점령지 별동대(`raidTargets`, EnemyAIDirector 전용)**: 동일하게 `raidTargets.Count > 0`일 때만
  `RaidRoutine()` 시작(EnemyAIDirector.cs:308).
- **기지 방어(`homeBuildings`)**: `OnEnable()`에서 각 건물의 `GetHealthManager() != null`을 확인한
  것만 이벤트 구독 - 비워두면 구독 자체가 0건이라 `HandleBaseAttacked`가 아예 호출될 일이 없음
  (EnemyAIDirector.cs:252-266).
- **배치형 방어유닛(`defenseUnits`)**: null 항목은 건너뛰고 `defenseSlots`를 구성 - 비워두면
  `RespawnDeadDefenseUnits()`가 빈 리스트를 돌아 아무 일도 안 함(양쪽 파일 공통 패턴).
- **스폰 지점(`spawnPoints`)**: 생산 큐(`LeastLoadedQueue`/`NextSpawnPoint`)가 사용 가능한 지점이
  없으면 `null`을 반환하고, 호출부(`EnqueueProduction`/`SpawnUnit`)는 그 `null`을 확인하고 조용히
  리턴함 - 생산 자체가 무한정 안 이뤄질 뿐 예외 없음.
- **집결지(`rallyPoint`)**: 비워두면 `spawnPoints[0]` → 그마저 없으면 `transform.position`으로
  자동 대체(`DefaultRallyPosition()`).
- **`rtsController`**(씬에서 `FindFirstObjectByType`로 자동 탐색)**: 못 찾으면(null) 이걸 쓰는 모든
  지점(`IsPlayerDefeated`, `PickAttackTarget`, `EnqueueProduction`, `RespawnDeadDefenseUnits`,
  `SpawnUnit` 등)이 개별적으로 null 체크 후 조기 리턴하도록 이미 작성돼 있음.

## 결론 - 수정 사항 없음
두 파일 모두 "필드를 안 채우면 그 기능만 조용히 꺼짐" 원칙이 이미 전체 지점에 일관되게 적용돼 있어서,
지금 상태로 인스펙터 일부만 연결해 쓰고 나머지는 비워둬도 오류(NRE 등) 없이 안전함. 코드 변경 불필요.

혹시 실제로 특정 상황에서 콘솔에 에러가 뜨는 걸 보셨다면(어떤 필드를 비워뒀을 때 어떤 메시지인지)
알려주시면 그 지점만 정확히 짚어서 고치겠음.

## 영향받는 파일
- 변경 없음 (점검만 수행): `Assets/Scripts/System/EnemyAIDirector.cs`,
  `Assets/Scripts/System/AllyAIDirector.cs`
