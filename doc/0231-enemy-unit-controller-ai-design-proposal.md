# 0231 - EnemyUnitController(적 유닛 AI) 설계 제안

## 요청

적 유닛(현재 "Enemy" 태그로 존재하는 `EnemyController`)에 실제 전투 AI를 붙이고 싶음. 플레이어 유닛
(`UnitController`)보다 기능은 훨씬 적어도 되고, 지금 필요한 건 딱 3가지:

1. **공격 기능**: 사거리 안에 가까운 상대 유닛을 자동으로 추적/공격 (가만히 있다가 AttackRange에 닿으면 반응)
2. **이동 기능**: 특정 위치로 이동 (추후 미션 스크립트나 1:1 AI가 호출)
3. **공격-이동**: 플레이어의 "땅 공격(A + 클릭)"과 동일한 기능 - 이동 중 교전하고 끝나면 원래 목적지로 복귀

추후 이 유닛들을 지휘하는 "AI 관제소" 스크립트가 따로 생겨서 이 3개 함수를 호출하는 구조.

## 현재 상태 확인

- `EnemyController.cs` (`Assets/Scripts/Enemy/EnemyController.cs`): 지금은 **AI가 전혀 없음**. 선택 마커,
  Info_panel용 아이콘/이름/공격력/방어력 getter, `Die()`만 있음. `TestEnemy.prefab`에 `NavMeshAgent`
  컴포넌트가 붙어있긴 하지만 어떤 스크립트도 이걸 사용하지 않음(장식용으로 남아있는 상태).
- `AttackRange.cs`: 플레이어 유닛(`UnitController`) 전용. 자식 트리거 콜라이더로 `"Enemy"` 태그만 감지하고,
  `unitController.IsAttack()/IsIdle()/Attack()/ChaseTarget()` 등 `UnitController`에 강하게 결합되어 있어
  그대로 재사용 불가 (타입도 반대 방향).
- 태그 체계 확인(`ProjectSettings/TagManager.asset`): 플레이어 유닛은 `Worker`(일꾼) 또는
  `AttackUnit`(전투유닛) 태그, 적 유닛은 `Enemy` 태그. 건물은 `MainBase/Tier1/Tier2/Tier3/SupplyDepot/Lab`.
  → 이 프로젝트는 "플레이어 vs 적" 단일 방향 태그 구조라, OC를 대칭 진영으로 만들더라도 지금 당장은
  "Enemy 쪽 AI가 Worker/AttackUnit 태그를 감지"하는 방향 하나만 구현하면 됨.

## 제안 설계

### 새 컴포넌트 2개 (기존 파일은 건드리지 않음)

**`EnemyUnitController.cs`** (신규, `EnemyController`와 같은 오브젝트에 나란히 부착)
- `EnemyController`가 이미 들고 있는 스탯(`attackDamage`, `armor`, `armorType`, `sizeType`)을 그대로 참조해서
  쓰고, 이동/전투 상태만 새로 담당 (스탯 중복 선언 안 함)
- 상태: `Idle`, `Move`, `Attack` (UnitController의 상태머신을 그대로 축소 이식)
- `NavMeshAgent` 기반 이동만 지원 (공중 유닛은 이번 범위에서 제외 - 아래 질문 참고)
- 공개 API:
  - `MoveTo(Vector3 destination)` — 그 자리로 이동, 도착하면 Idle
  - `AttackMoveTo(Vector3 destination)` — 이동 중 사거리에 적이 들어오면 교전, 교전 끝나면 원래 목적지로 복귀
    (UnitController.AttackMoveTo + AttackOrderTick과 동일한 패턴, 지정 대상/아군 강제공격 개념은 뺌)
  - `ChaseTarget(Vector3 pos)` — Idle 상태에서 감지된 적이 사거리 밖일 때 그 쪽으로 다가감
  - `Attack(Vector3 pos, GameObject target)` — 정지하고 대상 조준 후 데미지 적용 (쿨다운은 `timeBetweenAttacks`)
  - `IsIdle()` / `IsAttack()` — 아래 EnemyAttackRange가 상태를 물어보는 용도

**`EnemyAttackRange.cs`** (신규, `AttackRange.cs`를 적 쪽으로 뒤집은 축소판)
- 자식 트리거 콜라이더에 부착 (기존 AttackRange와 동일한 배치 패턴)
- `"Worker"` 또는 `"AttackUnit"` 태그를 가진 콜라이더만 감지 (플레이어 유닛)
- 매 프레임 감지 목록 중 가장 가까운 대상을 찾아서, `EnemyUnitController.IsIdle()`나 `IsAttack()`이면
  사거리 안이면 `Attack()`, 밖인데 Idle이면 `ChaseTarget()` 호출 (AttackRange.Update()와 동일한 로직)

### 이번 범위에서 뺀 것 (플레이어 UnitController 대비 단순화 - 확인 필요)

1. **지정 공격 대상(우클릭으로 특정 유닛 찍어서 추격)은 없음.** 자동 교전(사거리 감지) + 공격-이동만 지원.
   나중에 "AI 관제소"가 필요하면 그때 추가.
2. **공중 유닛 이동 미지원.** 지금 존재하는 적 프리팹(TestEnemy)이 지상 유닛이라 NavMeshAgent 기반만
   우선 구현. Raven/Ironhawk/Strike Drone처럼 공중 OC 유닛은 나중에 UnitController의 공중 이동 로직을
   참고해서 추가.
3. **데미지 계산 단순화.** `DamageMultiplierTableSO`(공격타입×크기 배율), 장갑타입 보너스 데미지,
   포탑/레이저 연동 없이 `최종 데미지 = max(1, attackDamage - 대상 방어력)`만 적용. 피격 이펙트 종류
   (`AttackEffectType`)는 항상 `Bullet` 기본값 사용.
4. **유닛만 공격 대상.** 사거리 안에 있어도 플레이어 건물(`MainBase` 등 태그)은 감지하지 않음 - "상대
   유닛"만 대상으로 명시하셨어서. 건물 공격은 나중에 필요해지면 태그를 추가하는 정도로 확장 가능.

## 확인 결과 (설계안에서 변경됨)

1. **컴포넌트 분리 대신 병합.** `EnemyController` = `EnemyUnitController`로 판단하셔서, 새 컴포넌트를
   따로 만들지 않고 기존 `EnemyController.cs`를 `EnemyUnitController.cs`로 이름을 바꾸고 그 안에
   선택/스탯/Die + 이동/전투 AI를 전부 합침.
2. **지상+공중 동시 구현.** `UnitController`의 공중 이동 로직(목표 좌표/고도 추적, 지면 높이 샘플링)을
   그대로 이식해서 `isAirUnit` 플래그로 분기. (공중 유닛끼리 겹침 분리는 이번 범위에서 제외 - 아래 참고)
3. **데미지 공식을 플레이어와 동일하게.** `DamageMultiplierTableSO`(공격타입×크기 배율) + 장갑타입 고유
   보너스까지 `UnitController.CalculateFinalDamage`와 동일하게 적용. 단, 연구소(Lab) 전역 공격/방어
   보너스는 적용 안 함 - OC 쪽 연구 시스템이 아직 없어서 (필요해지면 나중에 추가).
4. **건물도 공격 대상에 포함.** `MainBase/Tier1/Tier2/Tier3/SupplyDepot/Lab` 태그도 감지.

## 구현 완료 내용

### `EnemyController.cs` → `EnemyUnitController.cs` (이름 변경 + 기능 병합)

기존 파일을 지우고 같은 스크립트 GUID(`705a7d03ca178994088c2896e78ce1ce`)를 유지한 채 새 파일로 교체해서,
기존 프리팹(`TestEnemy.prefab`)의 컴포넌트 참조가 그대로 유지되도록 함. 기존 기능(선택 마커, 아이콘/이름
getter, `Die()`)은 그대로 두고 다음을 추가:

- **상태머신** `Idle/Move/Attack` (UnitController를 축소 이식)
- **`MoveTo(Vector3)`** — 지정 위치로 이동
- **`AttackMoveTo(Vector3)`** — 공격-이동 (교전 후 원래 목적지로 자동 복귀, `AttackMoveTick()`)
- **`ChaseTarget(Vector3)`** — Idle 상태에서 사거리 밖 감지 대상에게 접근 (`EnemyAttackRange`가 호출)
- **`Attack(Vector3, GameObject)`** — 데미지 적용. `UnitController.CalculateFinalDamage`와 동일한 공식
  (`DamageMultiplierTableSO` + 장갑타입 보너스), 대상이 `UnitController`면 그쪽 방어력/크기/장갑타입을
  조회하고 건물 등은 기본값(방어력 0/Medium/Light) 사용
- **지상(`NavMeshAgent`) + 공중 이동** 둘 다 지원 (`isAirUnit` 플래그로 분기, `UnitController`의 공중
  이동 로직 이식 - 단 공중 유닛끼리 겹침 분리(`SeparateFromOverlappingAirUnits`)는 이식 안 함)
- **`ApplyUnitData(UnitData)`** — [[0230]]에서 만든 `OC Unit Data SO`의 데이터를 나중에 스포너/AI
  관제소가 흘려보낼 수 있도록 `UnitController.ApplyUnitData`와 동일한 패턴으로 추가

### `EnemyAttackRange.cs` (신규)

`AttackRange.cs`를 반대 방향으로 뒤집은 버전. 자식 트리거 콜라이더에 부착해서 `Worker`/`AttackUnit`
(플레이어 유닛) + `MainBase`/`Tier1`/`Tier2`/`Tier3`/`SupplyDepot`/`Lab`(플레이어 건물) 태그를 감지하고,
매 프레임 가장 가까운 대상을 찾아 사거리 안이면 `Attack()`, 밖인데 Idle이면 `ChaseTarget()`을 호출.
지정 대상 강제 추격 개념은 없음 (요청 범위 밖).

### 기존 코드 전역 리네이밍

`EnemyController` 타입을 참조하던 모든 곳을 `EnemyUnitController`로 교체함 (동작 변경 없음, 타입 이름만
변경):
- `RTSUnitController.cs` (selectedEnemyList, ClickSelectEnemy, AttackSelectedUnits 등)
- `UnitController.cs` (orderedTarget, GetOrderedTarget, AttackUnitTarget, GetTargetArmor/SizeType/ArmorType)
  — 겸사겸사 `IsTargetAirborne()`에 `EnemyUnitController.IsAirUnit()` 체크를 추가함 (이제 적도 공중 개념이
  생겼으므로, 플레이어 유닛이 공중 적을 공격할 때 `canAttackAir` 도메인 제한이 정확히 적용되도록)
- `AttackRange.cs`, `CaptureSystem.cs`, `UserControl.cs`, `FogRevealerAgent.cs`(주석)
- `TestEnemy.prefab`의 `m_EditorClassIdentifier` 표기도 갱신 (스크립트는 GUID로 바인딩되므로 기능엔
  영향 없었지만 표기를 맞춰둠)

## 남은 작업 (에디터에서 직접 해야 함)

`TestEnemy.prefab`은 지금 트리거 콜라이더가 루트 오브젝트 자체에 하나만 있고, `EnemyAttackRange`가
붙을 자식 오브젝트(플레이어 쪽 `AttackRange`처럼 별도 트리거 콜라이더를 가진 자식)가 없음. 이건 Transform
계층 구조를 새로 만드는 작업이라 YAML을 직접 손으로 편집하기보다 유니티 에디터에서 직접 자식 오브젝트를
만들어 `EnemyAttackRange` 컴포넌트 + 트리거 콜라이더를 붙이고 `UnitRange` 값을 지정하는 게 안전함.
그 전까지는 `EnemyUnitController`가 붙어있어도 실제로 자동 교전은 일어나지 않음 (이동 API는 스크립트로
직접 호출하면 바로 동작함).

다른 OC 유닛 프리팹(Cyborg Soldier 등)도 만들어지면 동일하게 `EnemyAttackRange` 자식 오브젝트가 필요함.
