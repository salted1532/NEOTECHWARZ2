# 0234 - 적 유닛 레이저 공격 라인 재생

## 요청

레이저로 공격하는 적 유닛(예: Striker - `attackType: 2` Laser로 설정돼 있었음)에 `LaserBeamAttack`
스크립트를 이미 붙여뒀으니, 그걸 이용해서 firePoint와 대상을 잇는 빔 라인이 그려지도록 해달라는 요청.

## 확인

`LaserBeamAttack.cs`는 애초에 `UnitController`를 전혀 참조하지 않는 완전히 독립적인 컴포넌트였음
(`Fire(Transform target)`을 외부에서 호출해주기만 하면 됨 - firePoint↔대상을 잇는 LineRenderer를
`beamDuration`(기본 0.2초)만큼 재생). 플레이어 쪽에서도 `UnitController.Attack()`이 데미지 적용 직후
`GetComponent<LaserBeamAttack>()?.Fire(enemy.transform);`으로 호출해주는 것뿐, 컴포넌트 자체는 손대지
않음. 그래서 [[0233]]에서 `UnitEffects.PlayAttack()`을 이식한 것과 완전히 동일한 방식으로, 같은 지점에
한 줄만 추가하면 됨.

## 수정 내용

**`Assets/Scripts/Enemy/EnemyUnitController.cs`** - `Attack()`에서 데미지 적용 직후, 기존
`UnitEffects.PlayAttack()` 호출 바로 다음 줄에 추가:

```csharp
GetComponent<LaserBeamAttack>()?.Fire(target.transform);
```

`LaserBeamAttack.cs` 자체는 수정 없음 (컨트롤러 타입을 몰라도 되는 구조라 그대로 재사용됨). 레이저 프리팹이
안 붙어있는 유닛(대부분)은 `GetComponent<LaserBeamAttack>()`이 null이라 조용히 무시됨 - 기존 플레이어
유닛과 동일한 옵셔널 컴포넌트 패턴.

## 변경 파일

- `Assets/Scripts/Enemy/EnemyUnitController.cs`
