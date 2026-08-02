# 0373 - 지상 유닛 NavMeshAgent 이동속도, 2배 대신 1.5배로 정정

**날짜:** 2026-08-03

**승인 후 구현 완료.** 표대로 16개 프리팹 `m_Speed`를 원본×1.5로 정정.

## 요청 내용

> 원래 속도를 기준으로 1.5배만 빠르게 변경해줄래

[[0372]]에서 2배로 적용한 걸, 원래(2배 적용 전) 값 기준 1.5배로 다시 계산해서 덮어쓰는 정정 요청.

## 코드 변경 (제안)

원본 값(doc/0372 기준) × 1.5로 재계산. 현재(2배 적용된) 값 → 정정 값:

| 프리팹 | 원본 `m_Speed` | 현재(2배) | 정정 후(1.5배) |
|---|---|---|---|
| `NTA/Unit/MainBase/Worker Drone.prefab` | 3.5 | 7 | 5.25 |
| `NTA/Unit/Tier1/Assault Trooper.prefab` | 3.5 | 7 | 5.25 |
| `NTA/Unit/Tier1/Scout Drone.prefab` | 6 | 12 | 9 |
| `NTA/Unit/Tier1/Sharpshooter.prefab` | 3.5 | 7 | 5.25 |
| `NTA/Unit/Tier2/Pulsar Tank.prefab` | 3 | 6 | 4.5 |
| `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab` | 3.5 | 7 | 5.25 |
| `NTA/Unit/Tier2/SkyLancer.prefab` | 3.5 | 7 | 5.25 |
| `OC/Unit/Mainbase/Nanobot Repair.prefab` | 3.5 | 7 | 5.25 |
| `OC/Unit/Tier1/Cyborg Soldier .prefab` | 3.5 | 7 | 5.25 |
| `OC/Unit/Tier1/Railgunner.prefab` | 3.5 | 7 | 5.25 |
| `OC/Unit/Tier1/Striker.prefab` | 3.5 | 7 | 5.25 |
| `OC/Unit/Tier2/Brute Mech.prefab` | 3.5 | 7 | 5.25 |
| `OC/Unit/Tier2/Heavy Assault Tank.prefab` | 3 | 6 | 4.5 |
| `OC/Unit/Tier2/Ironhawk.prefab` | 3 | 6 | 4.5 |
| `Test/TestUnit.prefab` | 3.5 | 7 | 5.25 |
| `Test/TestEnemy.prefab` | 3.5 | 7 | 5.25 |

각 파일에서 `m_Speed` 한 줄만 값 교체 (예: Worker Drone `m_Speed: 7` → `m_Speed: 5.25`).

## 영향받는 파일 (예정)

- doc/0372과 동일한 프리팹 16개 (`Assets/prefabs/...`)
