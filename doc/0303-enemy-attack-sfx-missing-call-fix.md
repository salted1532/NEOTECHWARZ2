# 0303 - 적 유닛 공격 시 SFX가 안 나는 문제 원인 파악 및 수정

날짜: 2026-07-30

## 요청 내용

"연결된건 확인했는데 적 유닛이 공격할떄 소리가 안나는데 왜그런거야 확인조"

## 조사 내용

- 아군 유닛은 `UnitController.Attack()`(`Assets/Scripts/Unit/UnitController.cs:899`)에서 데미지 적용 직후 `GetComponent<UnitAudio>()?.PlayAttackSFX();`를 호출해 `UnitData.soundBank.attackSFX`를 재생한다.
- 반면 적 유닛의 공격 로직인 `EnemyUnitController.Attack()`(`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:230-282`)은 데미지 적용 후 `GetComponent<UnitEffects>()?.PlayAttack();`(시각 이펙트), `LaserBeamAttack`, `turretController.FireRecoil()`만 호출하고 **`UnitAudio.PlayAttackSFX()` 호출이 아예 없다** — 아군 쪽 패턴을 그대로 복사하면서 이 한 줄만 빠진 것으로 보인다.
- `UnitAudio` 컴포넌트 자체는 적 유닛 프리팹에도 정상적으로 붙어 있음(확인: `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab`에 `UnitAudio` 컴포넌트 존재) - 프리팹 설정 문제가 아니라 코드에서 호출을 안 하는 것.
- `UnitAudio.Awake()`(`Assets/Scripts/Audio/UnitAudio.cs:29-33`)는 `EnemyUnitController`가 붙어 있으면 `rtsController.GetEnemyUnitData(enemyUnitController.GetEnemyUnitID())`로 `soundBank`를 정상적으로 조회하도록 이미 구현되어 있다 - doc/0302에서 연결한 attackSFX 클립은 조회 자체는 문제없이 될 것으로 보임. 문제는 순전히 "호출이 안 됨" 하나.

## 확인 결과

"진행 (추천)" 선택 → 아래대로 적용 완료.

## 코드 변경 (적용 완료)

`Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs`

기존 코드 (Attack() 내부, 274~277행):

```csharp
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<LaserBeamAttack>()?.Fire(target.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
```

변경 코드:

```csharp
            GetComponent<UnitEffects>()?.PlayAttack();
            GetComponent<UnitAudio>()?.PlayAttackSFX();
            GetComponent<LaserBeamAttack>()?.Fire(target.transform); // 레이저 공격 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
            turretController?.FireRecoil(); // 포탑 유닛만 붙어있는 옵셔널 컴포넌트 (UnitController.Attack()과 동일한 훅 지점)
```

(`UnitController.Attack()`의 898~899행과 완전히 동일한 위치·패턴.)

## 요약

원인은 데이터 연결 문제가 아니라 **코드 누락** - `EnemyUnitController.Attack()`이 `UnitAudio.PlayAttackSFX()`를 호출하지 않아서, soundBank가 정상 연결돼 있어도 재생될 일이 없었다. 한 줄 추가로 아군과 동일한 지점에서 재생되도록 고치면 된다.

## 변경된 파일

- `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` (수정 - PlayAttackSFX 호출 1줄 추가)
- `doc/0303-enemy-attack-sfx-missing-call-fix.md` (이 파일, 신규)
