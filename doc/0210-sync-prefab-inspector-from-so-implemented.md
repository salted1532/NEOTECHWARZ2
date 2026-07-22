## 2026-07-22

### 요청 내용

> 적용시켜줘 (`doc/0209` 승인)

### 변경 내용

`doc/0209`의 표대로 9개 유닛 프리팹의 `UnitController`(`attackDamage`/`armorType`/`sizeType`/`timeBetweenAttacks`/`icon`), 자식 `AttackRange`(`UnitRange`), `HealthManager`(`maxHealth`) 인스펙터 값을 `New Unit Data SO.asset`과 동일하게 맞춤. 코드 변경 없음, 데이터만 동기화.

적용 후 9개 프리팹 전체를 다시 grep해서 SO 값과 정확히 일치하는지 재확인 완료.

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

### 남은 이슈 (변경 없음, 참고용)

- Sharpshooter/SkyLancer의 `unitID`는 이번 작업 범위 밖 — 사용자가 별도로 맞추는 중.
- `armor`/`attackType`/고유보너스/`isAirUnit`은 SO에 필드가 없어 계속 프리팹 값만 사용.
