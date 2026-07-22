## 2026-07-22

### 요청 내용

> 이제 수치조정은 전부 다 했어 유닛스크립터블오브젝트의 수치를 각 프리팹의 인스펙터 값에다가 그대로 적용시켜줄래? 그냥 동기화 해주는 느낌으로

실제 게임 동작은 `doc/0205`부터 이미 SO 값이 authoritative(스폰 시 `ApplyUnitData`가 덮어씀)이라 바뀔 게 없지만, **프리팹 인스펙터에 남아있는 미리보기용 값이 SO와 달라서 헷갈리는 상태**라, 순수하게 "프리팹 인스펙터 = SO" 상태로 맞춰달라는 요청. 코드/동작 변경 없음, 데이터만 동기화.

### 조사 내용

`ApplyUnitData`가 실제로 덮어쓰는 6개 필드(`icon`, `attackDamage`, `armorType`, `sizeType`, `timeBetweenAttacks`, `AttackRange.UnitRange`, `HealthManager.maxHealth`) 기준으로 9개 프리팹의 현재 인스펙터 값과 SO 값을 비교함. `armor`/`attackType`/`bonusVersusArmorType`/`Percent`/`isAirUnit`/`unitID`는 SO에 대응 필드가 없어 동기화 대상이 아님(이번 요청과 무관, 그대로 둠).

| 유닛 (SO ID) | 필드 | 프리팹 현재값 | SO 값 (변경 후) |
|---|---|---|---|
| Worker Drone (1) | 체력 | 50 | **40** |
| | (나머지 5개 필드는 이미 SO와 동일) | | |
| Assault Trooper (2) | 사거리 | 8 | **12** |
| | 공격력 | 6 | **5** |
| Scout Drone (3) | 사거리 | 10 | **14** |
| | 공격력 | 20 | **6** |
| | 체력 | 80 | **75** |
| Sharpshooter (4) | 사거리 | 8 | **20** |
| | 공격력 | 6 | **10** |
| | 체력 | 40 | **45** |
| | 공격주기 | 0.6 | **1** |
| Ranger IFV (5) | 사거리 | 12 | **14** |
| | 공격력 | 12 | **6** |
| | 장갑 | Light | **Heavy** |
| | 체력 | 125 | **150** |
| Pulasr Tank (6) | 사거리 | 14 | **20** |
| | 공격력 | 30 | **20** |
| | 크기 | Medium | **Large** |
| SkyLancer (7) | 사거리 | 12 | **18** |
| | 공격력 | 12 | **8** |
| | 장갑 | Light | **Heavy** |
| | 아이콘 | (Ranger IFV 아이콘) | **(Worker Drone 아이콘 — SO에 그렇게 등록돼 있음)** |
| Firehawk (8) | 사거리 | 14 | **18** |
| | 체력 | 120 | **150** |
| Guardian Drone (9) | 사거리 | 12 | **20** |
| | 체력 | 500 | **400** |

Sharpshooter/SkyLancer는 여전히 각각 Assault Trooper/Ranger IFV와 거의 동일한 값(중복 프리팹 상태)이라 이번 동기화로 처음으로 실제 자기 자신의 수치를 갖게 됩니다(단, `unitID` 자체는 이번 작업 범위 밖이라 안 건드림 — 별도로 맞추고 계신 작업).

### 코드 변경

없음. 9개 유닛 프리팹의 `UnitController`(`icon`/`attackDamage`/`armorType`/`sizeType`/`timeBetweenAttacks`), 자식 `AttackRange`(`UnitRange`), `HealthManager`(`maxHealth`) 인스펙터 값을 위 표대로 SO와 동일하게 덮어씀.

### 요약/영향받는 파일

- `Assets/prefabs/NTA/Unit/MainBase/Worker Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Scout Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Sharpshooter.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Pulsar Tank.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/SkyLancer.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`

---

**적용 완료** (2026-07-22) — 위 표대로 9개 프리팹 전부 적용, grep으로 최종 값이 SO와 일치하는지 재확인함.
