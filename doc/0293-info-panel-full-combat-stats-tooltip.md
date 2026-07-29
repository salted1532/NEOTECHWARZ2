## 날짜
2026-07-29

## 요청 내용
유닛 정보 패널에서 공격력/방어력 아이콘 호버 시 지금은 숫자만 나오는데, 공격타입/공격력(SkyLancer처럼 투사체를 동시에 여러 발 쏘는 유닛은 "x2"로 명시)/방어력/장갑타입/유닛 크기까지 보여주도록 코드 수정.

## 조사 내용
`UIController.ShowInfoPanel(icon, unitName, health, attackDamage, armor)`가 `infoAttackDamage`/`infoArmor`만 저장하고, `SetupInfoStatHoverTooltips()`가 각각 `"Attack Damge : N"`/`"Armor : N"`만 툴팁으로 보여주고 있었음. `UnitController`/`EnemyUnitController`엔 이미 `GetAttackType()`/`GetArmorType()`/`GetSizeType()`가 있었지만(`EnemyUnitController`엔 `GetAttackType()`만 없었음), 정보 패널로 전달되지 않고 있었음.

"x2" 표기는 doc/0291에서 만든 `ProjectileAttack.firePoints`(다연장 투사체)에서 가져와야 함 - `ProjectileAttack`에 발사 지점 개수를 물어볼 공개 메서드가 없어서 하나 추가.

## 코드 변경 (적용 완료)

### Assets/Scripts/Unit/ProjectileAttack.cs
`GetFirePointCount()` 추가 - `firePoints`가 비어있으면 1, 아니면 그 개수를 반환.

### Assets/Scripts/Unit/UnitController.cs / Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs
`GetShotCount()` 추가 - `attackDelivery == Projectile`이고 `ProjectileAttack`이 붙어있으면 `GetFirePointCount()`, 아니면 1(Hitscan은 항상 1발). `EnemyUnitController`엔 `GetAttackType()`도 없어서 같이 추가.

### Assets/Scripts/UI/UIController.cs
- `infoAttackType`/`infoArmorType`/`infoSizeType`/`infoShotCount` 필드 추가.
- `ShowInfoPanel(...)`에 `AttackEffectType attackType, ArmorType armorType, SizeType sizeType, int shotCount` 매개변수 추가(건물용 3-인자 오버로드는 기본값 전달 후 `SetCombatStatsVisible(false)`로 어차피 숨김).
- `SetupInfoStatHoverTooltips()`:
  - 공격 아이콘: `"Attack Type : {타입}\nAttack Damage : {값}{(2발 이상이면 " (x2)")}"`
  - 방어 아이콘: `"Armor : {값}\nArmor Type : {타입}\nSize : {크기}"`

### Assets/Scripts/System/RTSUnitController.cs
아군/적 유닛 단일 선택 시 `ShowInfoPanel` 호출에 `GetAttackType()/GetArmorType()/GetSizeType()/GetShotCount()`를 추가로 전달하도록 두 곳(아군 `SelectState`, 적 `EnemySelect`) 수정.

## 요약/남은 작업
적용 완료. 유닛 선택 후 공격력/방어력 아이콘에 마우스를 올려서 공격타입/공격력(투사체 여러 발 유닛은 xN 표기)/방어력/장갑타입/크기가 전부 보이는지 확인 필요. SkyLancer에 `ProjectileAttack`을 아직 안 붙였다면(doc/0291 남은 수동 작업) 지금은 항상 1발(x2 표기 없음)로 보일 것 - firePoints를 2개 연결하면 "x2"가 뜸.

## 변경된 파일
- `Assets/Scripts/Unit/ProjectileAttack.cs`
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/UI/UIController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
