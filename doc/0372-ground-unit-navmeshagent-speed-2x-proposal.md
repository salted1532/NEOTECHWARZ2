# 0372 - 지상 유닛 NavMeshAgent 이동속도 2배

**날짜:** 2026-08-03

**승인 후 구현 완료.** 열린 질문(테스트 프리팹 포함 여부)은 "전체 16개 포함"으로 확정.

## 요청 내용

> 지상유닛의 이동속도 navmeshagent의 speed를 각 현재 값에서 2배로 고쳐줘

## 조사 결과

- `UnitController.cs`/`EnemyUnitController.cs` 모두 `navMeshAgent.speed`를 코드에서 직접 대입하는 곳은
  없음 - 지상 유닛의 이동속도는 순수하게 각 프리팹에 붙은 `NavMeshAgent` 컴포넌트의 Inspector 값
  (`m_Speed`)에서 온다. 따라서 이번 변경은 코드가 아니라 프리팹 애셋(`.prefab` YAML)의 `m_Speed`
  필드를 직접 고치는 작업.
- 지상/공중 구분은 `isAirUnit` 직렬화 필드(`UnitController`/`EnemyUnitController` 공통)로 갈린다.
  `Assets/prefabs` 내 유닛 프리팹 전수 조사 결과:
  - **공중 유닛(`isAirUnit: 1`, 제외 대상):** `NTA/Unit/Tier3/Firehawk`, `NTA/Unit/Tier3/Guardian Drone`,
    `OC/Unit/Tier3/Raven`, `OC/Unit/Tier3/Strike Drone`, `Test/TestAirUnit`
  - **지상 유닛(`isAirUnit: 0`, 이번 변경 대상):** 아래 16개 전부
- `SkyLancer`, `Ironhawk`처럼 이름은 "비행체" 느낌이지만 실제 `isAirUnit` 값은 0으로 확인되어 지상
  유닛으로 분류함 (땅 위를 달리는 호버/차량형 유닛으로 취급되는 것으로 보임 - 이름만으로 판단하지 않고
  실제 필드 값 기준으로 목록을 확정).

## 코드 변경 (제안)

각 프리팹의 `NavMeshAgent` 컴포넌트 `m_Speed` 값을 2배로:

| 프리팹 | 현재 `m_Speed` | 변경 후 |
|---|---|---|
| `NTA/Unit/MainBase/Worker Drone.prefab` | 3.5 | 7 |
| `NTA/Unit/Tier1/Assault Trooper.prefab` | 3.5 | 7 |
| `NTA/Unit/Tier1/Scout Drone.prefab` | 6 | 12 |
| `NTA/Unit/Tier1/Sharpshooter.prefab` | 3.5 | 7 |
| `NTA/Unit/Tier2/Pulsar Tank.prefab` | 3 | 6 |
| `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab` | 3.5 | 7 |
| `NTA/Unit/Tier2/SkyLancer.prefab` | 3.5 | 7 |
| `OC/Unit/Mainbase/Nanobot Repair.prefab` | 3.5 | 7 |
| `OC/Unit/Tier1/Cyborg Soldier .prefab` | 3.5 | 7 |
| `OC/Unit/Tier1/Railgunner.prefab` | 3.5 | 7 |
| `OC/Unit/Tier1/Striker.prefab` | 3.5 | 7 |
| `OC/Unit/Tier2/Brute Mech.prefab` | 3.5 | 7 |
| `OC/Unit/Tier2/Heavy Assault Tank.prefab` | 3 | 6 |
| `OC/Unit/Tier2/Ironhawk.prefab` | 3 | 6 |
| `Test/TestUnit.prefab` | 3.5 | 7 |
| `Test/TestEnemy.prefab` | 3.5 | 7 |

각 파일에서 아래 형태의 한 줄만 바뀜 (예: Worker Drone):

기존:
```yaml
  m_Speed: 3.5
```

변경:
```yaml
  m_Speed: 7
```

## 검증

- 위 표대로 16개 프리팹 모두 `m_Speed`가 정확히 2배(예: 3.5→7, 3→6, 6→12)로 바뀐 것을 재확인.

## 영향받는 파일

- 위 표의 프리팹 16개 (`Assets/prefabs/...`)
