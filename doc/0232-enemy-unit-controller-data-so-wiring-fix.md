# 0232 - EnemyUnitController가 OC Unit Data SO 값을 못 읽어오던 문제 수정

## 요청

적 유닛 프리팹 모델링을 마무리하고 `OC Unit Data SO`에 각 유닛의 프리팹을 연결했는데, 정작
`EnemyUnitController`가 그 정보(체력/공격력/이름 등)를 프리팹에 반영하지 못해서 플레이어가 적 유닛을
선택해도 Info_panel에 제대로 된 정보가 안 보임. 스크립터블오브젝트 → 프리팹으로 정보가 전달되도록 수정
요청.

## 원인

[[0231]]에서 `EnemyUnitController.ApplyUnitData(UnitData)` 메서드 자체는 만들어뒀지만, **아무도 이
메서드를 호출하지 않았음.** 플레이어 쪽 `UnitController.Start()`는 `unitID` 필드로 `RTSUnitController`의
`unitDatabase`(UnitDataSO)를 조회해서 자동으로 `ApplyUnitData()`를 호출하는데, `EnemyUnitController`에는
그 대응 짝(자기 ID + DB 참조)이 아예 없었다. 그래서 인스펙터에 남아있는 프리팹 기본값(대부분 비어있거나
0)이 그대로 표시되고 있었던 것.

## 수정 내용

`UnitController`/`RTSUnitController`의 기존 패턴을 그대로 따라서 짝을 채움:

- **`EnemyUnitController.cs`**: `enemyUnitID` 필드 추가 (`OC Unit Data SO`의 `UnitData.ID`와 매칭).
  `Start()`에서 `rtsController.GetEnemyUnitData(enemyUnitID)`로 조회한 값을 `ApplyUnitData()`에 넘겨서
  스스로 스탯(아이콘/이름/공격력/장갑타입/크기타입/공격속도/지상·대공 공격가능여부/사거리/체력)을 덮어쓰게
  함. `GetEnemyUnitID()` 접근자도 추가.
- **`RTSUnitController.cs`**: `unitDatabase`/`buildingDatabase` 옆에 `enemyUnitDatabase`(`EnemyUnitDataSO`)
  필드를 추가하고, `GetUnitData(int)`와 동일한 패턴의 `GetEnemyUnitData(int enemyUnitID)`를 추가.

Info_panel은 원래부터 `enemy.GetIcon()/GetEnemyName()/GetAttackDamage()/GetArmor()`와
`enemy.GetComponent<HealthManager>()`를 그대로 읽어서 보여주고 있었으므로(`RTSUnitController.UpdateUI()`의
`EnemySelect` 케이스), 이 값들이 `ApplyUnitData()`로 정확히 채워지기만 하면 별도 UI 코드 수정 없이 바로
정상 표시된다.

## 에디터에서 직접 해야 하는 작업 (코드만으로는 안 되는 부분)

1. 씬의 `RTSUnitController` 컴포넌트 인스펙터에서 새로 생긴 **`Enemy Unit Database`** 필드에
   `OC Unit Data SO` 에셋을 드래그해서 연결.
2. OC 유닛 프리팹마다(`EnemyUnitController`의 **`Enemy Unit ID`** 필드) `OC Unit Data SO`에서의 ID를
   맞춰서 지정:

   | 프리팹 | Enemy Unit ID |
   |---|---|
   | Nanobot Repair | 1 |
   | Cyborg Soldier | 2 |
   | Striker | 3 |
   | Railgunner | 4 |
   | Brute Mech | 5 |
   | Heavy Assault Tank | 6 |
   | Ironhawk | 7 |
   | Raven | 8 |
   | Strike Drone | 9 |

이 두 가지가 안 돼 있으면 `GetEnemyUnitData()`가 `null`을 반환해서 `ApplyUnitData()`가 조용히 아무 것도
안 하고 넘어간다 (기존 인스펙터 기본값 그대로 유지) - 에러는 안 나지만 정보가 여전히 안 보이는 것처럼
보일 수 있으니, 이 두 단계를 먼저 확인해볼 것.

## 변경 파일

- `Assets/Scripts/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`

## 추가 작업: 기존 프리팹 9종 값 직접 동기화

사용자가 모델링을 마치고 9종 프리팹을 만들어 `OC Unit Data SO`에 전부 연결해뒀는데, 실제 프리팹의
`EnemyUnitController`/`HealthManager`/`EnemyAttackRange` 값은 전부 `TestEnemy`를 복제한 시점의 낡은 값
(대부분 `enemyName: Cyborg Soldier`, `maxHealth: 100`, `UnitRange: 0` 등 플레이스홀더) 그대로 남아있었음.
런타임에 `ApplyUnitData()`가 자동으로 덮어쓰긴 하지만(위 항목), 에디터 인스펙터/씬 프리뷰 등에서 당장
정확한 값이 보이도록 SO에 적힌 값을 프리팹 에셋 자체에도 직접 반영해달라는 요청을 받아 9개 프리팹 전부
`enemyName`/`Icon`/`attackDamage`/`armorType`/`sizeType`/`canAttackGround`/`canAttackAir`/
`timeBetweenAttacks`(attackSpeed)/`HealthManager.maxHealth`(hp)/`EnemyAttackRange.UnitRange`(attackRange)를
SO 값과 동일하게 맞춤.

| 프리팹 | 경로 |
|---|---|
| Nanobot Repair | `Assets/prefabs/OC/Unit/Mainbase/Nanobot Repair.prefab` |
| Cyborg Soldier | `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab` (파일명에 공백 있음, 기존 상태 유지) |
| Striker | `Assets/prefabs/OC/Unit/Tier1/Striker.prefab` |
| Railgunner | `Assets/prefabs/OC/Unit/Tier1/Railgunner.prefab` |
| Brute Mech | `Assets/prefabs/OC/Unit/Tier2/Brute Mech.prefab` |
| Heavy Assault Tank | `Assets/prefabs/OC/Unit/Tier2/Heavy Assault Tank.prefab` |
| Ironhawk | `Assets/prefabs/OC/Unit/Tier2/Ironhawk.prefab` |
| Raven | `Assets/prefabs/OC/Unit/Tier3/Raven.prefab` |
| Strike Drone | `Assets/prefabs/OC/Unit/Tier3/Strike Drone.prefab` |

**동기화하지 않고 그대로 둔 필드**: `armor`(방어력), `attackType`(피격 이펙트 종류), `isAirUnit` 등은
`UnitData`에 아예 없는 필드라 SO에 원본 값이 없음 - 프리팹에 이미 입력돼 있던 값을 그대로 유지함
(`isAirUnit`은 Raven/Strike Drone이 이미 1로, 나머지는 0으로 올바르게 설정돼 있었음 - Ironhawk는
"워커형 차량"이라 지상에 있는 게 맞아서 0 유지가 맞음).

`enemyUnitID`는 이미 사용자가 전부 올바르게(1~9) 지정해둔 상태라 손댈 필요 없었음.
