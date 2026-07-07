# 0018 - UnitController로 공격력 이동 + 방어력 필드 추가

**날짜:** 2026-07-07

## 요청 내용
Info_panel에서 유닛의 공격력/방어력을 보여줄 준비 작업으로, UnitDamage/UnitArmor 이미지 호버 시 각각 공격력/방어력을 표시할 예정. 이를 위한 선행 작업으로:
1. `UnitController`에 방어력(armor) 필드 추가
2. 기존 `AttackRange`에 있던 공격력(`AttackDamage`) 필드를 `UnitController`로 이동
3. 공격력/방어력 둘 다 `UnitController`가 관리

## 조사 내용
- `Assets/Scripts/Unit/AttackRange.cs`에 `public int UnitRange`, `public int AttackDamage` 두 필드가 있었고, `Update()`에서 사거리 판정 후 `unitController.Attack(pos, AttackDamage, target)` 형태로 호출.
- `Assets/Scripts/Unit/UnitController.cs`의 `FriendlyAttackTick()`(아군 강제공격)도 `attackRange.AttackDamage`를 직접 읽어 `Attack()`에 넘기고 있었음 — 이동 대상 사용처가 두 곳.
- `AttackDamage` 필드는 9개 유닛 프리팹(`Test/TestUnit`, `Test/TestAirUnit`, `NTA/Unit/MainBase/Worker Drone`, `NTA/Unit/Tier1/Assault Trooper`, `NTA/Unit/Tier1/Scout Drone`, `NTA/Unit/Tier2/Pulsar Tank`, `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle`, `NTA/Unit/Tier3/Guardian Drone`, `NTA/Unit/Tier3/Firehawk`)에 값이 직접 세팅되어 있었음 — 필드를 단순히 스크립트에서만 옮기면 프리팹에 저장된 값이 유실되므로, 각 프리팹의 YAML도 함께 수정해 값을 그대로 이전함.
- `UnitDataSO`(`Assets/Scripts/ScriptableObject/UnitDataSO.cs`)에도 `attackDamge`(오타) 필드가 있지만 생산 비용/스펙 표시용 데이터일 뿐 런타임 `AttackRange`/`UnitController`와는 연결되어 있지 않아 이번 작업 범위에서 제외.

## 변경 내용
### `Assets/Scripts/Unit/UnitController.cs`
- `unitID` 다음에 `[SerializeField] private int attackDamage;`, `[SerializeField] private int armor;` 필드 추가 (전투 스탯 섹션).
- `Attack(Vector3 end, int damage, GameObject enemy)` → `Attack(Vector3 end, GameObject enemy)`로 시그니처 단순화 — 이제 자기 자신의 `attackDamage` 필드를 직접 사용 (외부에서 damage를 넘겨받을 필요 없어짐).
- `FriendlyAttackTick()`에서 `Attack(friendlyTarget.transform.position, attackRange.AttackDamage, friendlyTarget.gameObject)` → `Attack(friendlyTarget.transform.position, friendlyTarget.gameObject)`로 변경.
- `GetAttackDamage()`, `GetArmor()` getter 추가 (기존 `GetIcon()`/`GetUnitID()`와 동일한 패턴, 추후 Info_panel 호버 표시에서 사용 예정).

### `Assets/Scripts/Unit/AttackRange.cs`
- `public int AttackDamage;` 필드 제거 (사거리 판정용 `UnitRange`만 남김 — 범위 감지는 여전히 이 컴포넌트의 책임).
- `Update()`의 `unitController.Attack(target.transform.position, AttackDamage, target)` → `unitController.Attack(target.transform.position, target)`로 변경.

### 프리팹 9종 (`Assets/prefabs/...`)
각 프리팹에서 `AttackRange` 컴포넌트의 `AttackDamage: N` 줄을 제거하고, 같은 값을 `UnitController` 컴포넌트에 `attackDamage: N` / `armor: 0`으로 이전:
- `Test/TestUnit.prefab` (1), `Test/TestAirUnit.prefab` (1)
- `NTA/Unit/MainBase/Worker Drone.prefab` (5)
- `NTA/Unit/Tier1/Assault Trooper.prefab` (6), `NTA/Unit/Tier1/Scout Drone.prefab` (20)
- `NTA/Unit/Tier2/Pulsar Tank.prefab` (30), `NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab` (12)
- `NTA/Unit/Tier3/Guardian Drone.prefab` (25), `NTA/Unit/Tier3/Firehawk.prefab` (8)

`armor`는 기존 데이터가 없었으므로 전부 기본값 0으로 채워둠 — 각 유닛의 실제 방어력 값은 에디터에서 인스펙터로 조정 필요.

## 남은 작업 (다음 요청에서 진행 예정)
- Info_panel에 UnitDamage/UnitArmor 이미지 호버 시 `GetAttackDamage()`/`GetArmor()` 값을 보여주는 UI 로직(툴팁 또는 텍스트 표시)은 아직 구현하지 않음 — 이번 세션은 데이터 이동/필드 추가까지만 범위.
- 각 유닛 프리팹의 `armor` 값(현재 전부 0)을 실제 밸런스에 맞게 인스펙터에서 채워야 함.

## 변경된 파일
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/AttackRange.cs`
- `Assets/prefabs/Test/TestUnit.prefab`
- `Assets/prefabs/Test/TestAirUnit.prefab`
- `Assets/prefabs/NTA/Unit/MainBase/Worker Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Scout Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Pulsar Tank.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`
