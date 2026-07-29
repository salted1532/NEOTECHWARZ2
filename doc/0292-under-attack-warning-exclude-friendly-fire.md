## 날짜
2026-07-29

## 요청 내용
"적에게 공격받았습니다" 경고음이 아군사격(doc/0008~0051)으로도 울리는데, 실제로 적 유닛에게 공격받았을 때만 작동하도록 바꿔달라는 요청.

## 조사 내용
`UnitAudio.HandleDamaged`/`BuildingAudio.HandleDamaged`가 `HealthManager.OnDamaged` 이벤트를 구독해서, 화면 밖에서 데미지를 받으면 무조건 `PlayUnitUnderAttackWarning`/`PlayBuildingUnderAttackWarning`을 호출하고 있었음 - 공격자가 아군인지 적인지 구분하는 정보가 `OnDamaged`/`GetDamage()`에 아예 없었음(데미지량/공격자 위치/공격 타입만 전달).

`GetDamage()` 호출부는 3곳: `UnitController.Attack()`(아군이 공격 - 대상이 아군이면 아군사격, 적이면 정상 전투), `EnemyUnitController.Attack()`(적 AI가 공격 - 대상은 항상 플레이어 진영), `ProjectileAttack.cs`(doc/0290 - 위 둘 중 누가 발사했는지에 따라 명중 시점에 전달해야 함).

`OnDamaged` 구독자는 5곳: `UnitAudio`/`BuildingAudio`(경고음 - 이번에 구분 필요), `UnitEffects`/`BuildingEffects`/`ConstructionEffects`(피격 이펙트 - 아군사격이든 적 공격이든 똑같이 재생돼야 하므로 구분 불필요).

## 코드 변경 (적용 완료)

`HealthManager`의 `OnDamaged` 이벤트와 `GetDamage()`에 `bool isEnemyAttacker` 매개변수를 추가하고, 호출부마다 공격자 진영에 맞는 값을 넘기도록 전체 체인을 수정.

### Assets/Scripts/Unit/HealthManager.cs
- `OnDamaged` 이벤트 시그니처: `Action<int, Vector3, AttackEffectType>` → `Action<int, Vector3, AttackEffectType, bool>`
- `GetDamage(int, Vector3, AttackEffectType)` → `GetDamage(int, Vector3, AttackEffectType, bool isEnemyAttacker)`

### Assets/Scripts/Unit/UnitController.cs
`Attack()`의 `GetDamage`/`ProjectileAttack.Fire` 호출에 `isEnemyAttacker: false` 전달 - 공격자가 항상 아군(`UnitController`)이므로.

### Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs
`Attack()`의 `GetDamage`/`ProjectileAttack.Fire` 호출에 `isEnemyAttacker: true` 전달 - 공격자가 항상 적 AI이므로.

### Assets/Scripts/Unit/ProjectileAttack.cs
`Fire()`/`FireFromPoint()`/`FlyRoutine()`에 `isEnemyAttacker` 매개변수를 추가해 발사자 쪽(`UnitController`/`EnemyUnitController`)에서 받은 값을 명중 시점의 `GetDamage()` 호출까지 그대로 전달.

### Assets/Scripts/Audio/UnitAudio.cs, Assets/Scripts/Audio/BuildingAudio.cs
`HandleDamaged`에 `isEnemyAttacker` 매개변수 추가, `isEnemyAttacker`가 `false`(아군사격)면 경고음을 재생하지 않도록 조건 추가:
```csharp
if (isEnemyAttacker && !SoundManager.IsWorldPositionOnScreen(transform.position))
    SoundManager.Instance?.PlayUnitUnderAttackWarning();
```

### Assets/Scripts/Effects/UnitEffects.cs, BuildingEffects.cs, ConstructionEffects.cs
`HealthManager.OnDamaged` 델리게이트 시그니처를 맞추기 위해 `HandleDamaged`에 `isEnemyAttacker` 매개변수만 추가 - 실제 동작(피격 이펙트 재생)은 변경 없음, 아군사격이든 적 공격이든 똑같이 재생됨.

## 요약/남은 작업
적용 완료. 실제 플레이로 아군끼리 공격(친화력 사격)할 때 경고음이 안 울리고, 적 유닛/AI에게 공격받을 때만 경고음이 울리는지 확인 필요.

## 변경된 파일
- `Assets/Scripts/Unit/HealthManager.cs`
- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`
- `Assets/Scripts/Unit/ProjectileAttack.cs`
- `Assets/Scripts/Audio/UnitAudio.cs`
- `Assets/Scripts/Audio/BuildingAudio.cs`
- `Assets/Scripts/Effects/UnitEffects.cs`
- `Assets/Scripts/Effects/BuildingEffects.cs`
- `Assets/Scripts/Effects/ConstructionEffects.cs`
