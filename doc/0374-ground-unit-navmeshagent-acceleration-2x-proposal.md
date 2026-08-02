# 0374 - 지상 유닛 NavMeshAgent 가속도(Acceleration) 2배

**날짜:** 2026-08-03

**승인 후 구현 완료.** 표대로 16개 프리팹 `m_Acceleration` 2배 적용, 값 재확인 완료.

## 요청 내용

> Acceleration을 원래값에서 2배씩 올려줘

직전 대화([[0372]], [[0373]] - NavMeshAgent Speed 조정)와 같은 맥락, 같은 지상 유닛 16개
프리팹(`isAirUnit: 0`) 대상으로 해석함. 공중 유닛(Tier3 4개 + TestAirUnit)은 애초에 `NavMeshAgent`
컴포넌트 자체가 없음(공중 이동은 좌표 직접 보간 방식이라 - [[0372]] 조사 내용 참고) - 대상에서 자동 제외.

## 조사 결과

- `NavMeshAgent.m_Acceleration` 현재 값: `Worker Drone`만 16이고 나머지 15개는 전부 8.

## 코드 변경 (제안)

각 프리팹의 `NavMeshAgent` 컴포넌트 `m_Acceleration` 값을 2배로:

| 프리팹 | 현재 `m_Acceleration` | 변경 후 |
|---|---|---|
| `NTA/Unit/MainBase/Worker Drone.prefab` | 16 | 32 |
| `NTA/Unit/Tier1/Assault Trooper.prefab` | 8 | 16 |
| `NTA/Unit/Tier1/Scout Drone.prefab` | 8 | 16 |
| `NTA/Unit/Tier1/Sharpshooter.prefab` | 8 | 16 |
| `NTA/Unit/Tier2/Pulsar Tank.prefab` | 8 | 16 |
| `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab` | 8 | 16 |
| `NTA/Unit/Tier2/SkyLancer.prefab` | 8 | 16 |
| `OC/Unit/Mainbase/Nanobot Repair.prefab` | 8 | 16 |
| `OC/Unit/Tier1/Cyborg Soldier .prefab` | 8 | 16 |
| `OC/Unit/Tier1/Railgunner.prefab` | 8 | 16 |
| `OC/Unit/Tier1/Striker.prefab` | 8 | 16 |
| `OC/Unit/Tier2/Brute Mech.prefab` | 8 | 16 |
| `OC/Unit/Tier2/Heavy Assault Tank.prefab` | 8 | 16 |
| `OC/Unit/Tier2/Ironhawk.prefab` | 8 | 16 |
| `Test/TestUnit.prefab` | 8 | 16 |
| `Test/TestEnemy.prefab` | 8 | 16 |

각 파일에서 `m_Acceleration` 한 줄만 값 교체.

## 영향받는 파일 (예정)

- doc/0372과 동일한 프리팹 16개 (`Assets/prefabs/...`)
