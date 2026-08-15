# 0585. 적 유닛이 건설 중인 건물을 공격하지 않는 문제 - 제안

**날짜:** 2026-08-16

## 요청 내용

> 적 유닛들이 건설중인 건물은 공격 안하는거 같은데 확인좀

## 조사 내용

건설 중인 건물은 완공된 `BuildingController`가 아니라 임시 파운데이션 오브젝트
`BaseStructure`(`Assets/Scripts/Building/BaseStructure.cs`)로 존재한다. 이 클래스의 294번 줄 주석이
이미 원인을 암시하고 있었다:

> "현재는 BaseStructure를 실제로 공격하는 경로가 없어 이론상의 대비이지만..."

실제로 확인해보니, 적 유닛의 대상 감지는 `EnemyAttackRange.cs`의 `IsValidTarget()`이 대상의 Tag가
`targetTags` 목록(`Worker, AttackUnit, MainBase, Tier1, Tier2, Tier3, SupplyDepot, Lab, AllyOC`)에
있는지로 판정한다. 그런데 `BaseStructure` 프리팹(`Assets/prefabs/NTA/Building/BaseStructure.prefab`,
`Assets/prefabs/OC/Building/BaseStructure.prefab`) 루트 오브젝트의 Tag가 `Untagged`로 되어 있다 -
어떤 건물 종류(MainBase/Tier1/2/3/SupplyDepot/Lab)가 지어지는 중이든 이 파운데이션 프리팹 하나를
공유해서 쓰기 때문에, 완공 건물들처럼 종류별 Tag를 미리 박아둘 수 없었던 것으로 보인다.

결과적으로 트리거 콜라이더 자체(`BoxCollider, IsTrigger`)는 정상적으로 붙어있지만, Tag가 목록에 없어
`IsValidTarget()`이 항상 `false`를 반환 - 적 유닛이 건설 중인 건물을 아예 감지 후보로도 넣지 않는다.

`targetTags`는 `EnemyAttackRange`(및 이를 상속하는 `AllyAttackRange`)의 `[SerializeField]` 필드라서,
플레이어를 공격하는 각 적 유닛 프리팹(OC 비아군 유닛, Spore_Brood 유닛)마다 이미 배열 값이
개별적으로 굳어져(serialize) 저장되어 있다 - `EnemyAttackRange.cs`의 코드 기본값만 바꿔서는 기존
프리팹에 반영되지 않는다.

## 변경 계획

새 Tag `UnderConstruction`을 만들어 두 `BaseStructure` 프리팹에 붙이고, 플레이어(NTA)를 공격하는
적 유닛 프리팹들의 `targetTags`에 이 Tag를 추가한다.

### 1. `ProjectSettings/TagManager.asset`
`tags` 목록에 `UnderConstruction` 추가.

### 2. `Assets/prefabs/NTA/Building/BaseStructure.prefab`
루트 GameObject(`BaseStructure`, 콜라이더가 붙어있는 오브젝트)의 `m_TagString`을
`Untagged` → `UnderConstruction`으로 변경.

(`Assets/prefabs/OC/Building/BaseStructure.prefab`은 실제로 확인해보니 루트 태그가 이미 `Enemy`로
설정되어 있었다 - 처음엔 이 프리팹도 `Untagged`라고 잘못 판단했었다. `AllyAttackRange`가 `Enemy`
태그를 이미 기본 대상으로 삼고 있어서 OC 진영 건설 중인 건물은 원래부터 정상적으로 공격당하고
있었음 - 손대지 않았다.)

### 3. `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`
코드 기본값에도 반영(앞으로 새로 만드는 적 유닛 프리팹에 자동으로 포함되도록):
```diff
     protected string[] targetTags =
-        { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab", "AllyOC" };
+        { "Worker", "AttackUnit", "MainBase", "Tier1", "Tier2", "Tier3", "SupplyDepot", "Lab", "AllyOC", "UnderConstruction" };
```

### 4. 이미 굳어진 값을 가진 적 유닛 프리팹들 - 각 `targetTags` 배열에 `- UnderConstruction` 한 줄 추가
(전부 `EnemyAttackRange`를 그대로 쓰는, 플레이어(NTA)를 공격하는 쪽 - "(Ally)" 유닛들은
`AllyAttackRange`가 `targetTags`를 `["Enemy"]`로 덮어써서 대상이 아님, 손댈 필요 없음)

- `Assets/prefabs/OC/Unit/Mainbase/Nanobot Repair.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab`
- `Assets/prefabs/OC/Unit/Tier1/Railgunner.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Striker.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Brute Mech.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Heavy Assault Tank.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Ironhawk.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Raven.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Strike Drone.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Skitterwing.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Spitter.prefab`
- `Assets/prefabs/Test/TestEnemy.prefab`

## 범위 밖 (참고)

- `BaseStructure.Die()`는 이미 `CancelConstruction()`(건물값 환불 + 일꾼 해제)으로 구현돼 있어서,
  적이 건설 중인 건물을 파괴하면 자동으로 "건설 취소 + 환불" 처리된다 - 추가 구현 불필요.
- Spore_Brood(외계종족)는 건설 단계 없이 스폰되는 것으로 보여 "Ally OC가 Spore_Brood의 건설 중인
  건물을 공격"하는 대칭 케이스는 해당사항 없음 - `AllyAttackRange` 쪽은 손대지 않음.

## 변경 예정 파일
- `ProjectSettings/TagManager.asset`
- `Assets/prefabs/NTA/Building/BaseStructure.prefab`
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`
- 위 13개 적 유닛 프리팹

---

## 적용 (사용자 승인 후)

제안대로 적용함(`OC/Building/BaseStructure.prefab`은 조사 중 이미 `Enemy` 태그가 붙어있는 것을
확인해서 변경 계획에서 제외):

1. `ProjectSettings/TagManager.asset`에 `UnderConstruction` 태그 추가
2. `Assets/prefabs/NTA/Building/BaseStructure.prefab` 루트 태그를 `UnderConstruction`으로 변경
3. `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`의 `targetTags` 기본값에 `UnderConstruction` 추가
4. 위 13개 적 유닛 프리팹의 `targetTags` 배열에 `UnderConstruction` 추가

`npx uloop-cli compile` 성공 확인(Error 0개). 에셋/프리팹 파일은 에디터 밖에서 직접 수정했으므로
`AssetDatabase.Refresh()`로 재임포트를 강제한 뒤 Unity 에디터 API로 직접 검증함:
- 새 태그가 `InternalEditorUtility.tags`에 정상 등록됨
- `NTA/Building/BaseStructure.prefab` 루트 오브젝트의 태그가 `UnderConstruction`으로 반영됨
- `Striker.prefab`의 `EnemyAttackRange.targetTags`에 `UnderConstruction`이 포함됨

## 변경된 파일
- `ProjectSettings/TagManager.asset`
- `Assets/prefabs/NTA/Building/BaseStructure.prefab`
- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs`
- `Assets/prefabs/OC/Unit/Mainbase/Nanobot Repair.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab`
- `Assets/prefabs/OC/Unit/Tier1/Railgunner.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Striker.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Brute Mech.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Heavy Assault Tank.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Ironhawk.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Raven.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Strike Drone.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Skitterwing.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Spitter.prefab`
- `Assets/prefabs/Test/TestEnemy.prefab`
