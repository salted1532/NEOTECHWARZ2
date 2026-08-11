# 0516. 건물 조기 건설(체력 1) 원인 조사

**날짜:** 2026-08-11

## 요청 내용

> 건물 건설시 체력1 단 상태에서 조기 건설된 경우가 발생하는걸 확인했어 이유나 원인은 모르겠는데
> 해당 건물이 공격을 받지 않았는데도 체력이 1만 달았다는게 이상하네 이거 원인좀 확인해줘

## 결론부터

**적에게 공격받은 게 아니라, 아군(플레이어 자신)의 SkyLancer 액티브 스킬 "지상 폭격(Ground Bombardment)"의
범위 피해에 건설 중이던 건물(BaseStructure)이 휘말린 것**으로 보인다. 이 스킬은 "아군/적/시전자 본인 구분 없이
범위 안의 전부에게" 피해를 주도록 **의도적으로** 설계돼 있고(`doc/0323`), 그 판정이 "유닛"만이 아니라
`HealthManager`가 붙은 모든 콜라이더를 대상으로 하기 때문에 건설 중인 건물도 그대로 맞는다.

## 조사 과정

1. **건설 중 체력이 낮게 끝나는 구조 자체를 먼저 확인** (`Assets/Scripts/Building/BaseStructure.cs`)
   - `Initialize()`가 건설 중 체력을 완공될 건물과 같은 척도(0~`finalMaxHealth`)로 맞춰두고, `Update()`에서
     매 프레임 `healthPerSecond`(`= finalMaxHealth / buildTime`)만큼 서서히 채운다.
   - `remainingBuildTime`(건설 완료까지 남은 시간)과 체력 채우기가 **같은 `Time.deltaTime`으로 같은 프레임에서
     같이 진행**되므로, 정상적으로는 건설이 끝나는 시점에 체력도 함께 거의 다 차 있어야 한다(부동소수점 나머지로
     인한 오차는 최대 1 미만 - 이것만으로는 "1"까지 떨어질 수 없음).
   - 즉 체력이 눈에 띄게 낮게 끝나려면 **건설 중간에 실제로 데미지를 입은 경우**뿐이다. 이는 `doc/0053`에서
     이미 의도된 동작으로 명시돼 있다: "건설 중 체력이 0까지 떨어지면 `Die()`가 호출되어(→`CancelConstruction()`)
     애초에 완공까지 못 감. 막판에 데미지를 입어 회복이 못 따라잡은 채로 `remainingBuildTime`이 0이 되면
     완공된 건물이 최대체력보다 낮은 상태로 스폰될 수 있음(의도된 동작)."
   - 그래서 "건설 중 데미지를 입힐 수 있는 경로가 실제로 있는가"를 찾는 쪽으로 조사 방향을 바꿈.

2. **`HealthManager.GetDamage()`를 호출하는 모든 지점**을 전수 조사 (`grep -rn "\.GetDamage("`):
   - `UnitController.cs` / `EnemyUnitController.cs` / `AllyController.cs` (일반 공격), `Projectile.cs`,
     `DamageOverTimeEffect.cs`, `SharpshooterSkill.cs`(단일 대상 스킬), **`SkyLancerSkill.cs`**(범위 스킬)
   - `BaseStructure.Die()`의 기존 주석을 보면 "현재는 BaseStructure를 실제로 공격하는 경로가 없어 이론상의
     대비"라고 적혀 있었음 - 즉 **적의 일반 공격(단일 대상 타겟팅)은 건설 중인 건물을 대상으로 삼지 않는다**
     (`EnemyUnitController`에 별도의 범위 검색/타겟팅 코드가 없음 - 확인 완료). 여기까지는 주석이 맞음.
   - 하지만 **`SkyLancerSkill.Activate()`(지상 폭격)만은 예외**다:
     ```csharp
     Collider[] hits = Physics.OverlapSphere(context.groundPoint, traitData.areaRadius);
     ...
     foreach (Collider hit in hits)
     {
         if (!hit.TryGetComponent(out HealthManager health) || !alreadyHit.Add(health))
             continue;
         // 아군/적/시전자 본인 구분 없이 전부 적용 (doc/0323 확정 사항)
         health.GetDamage(bombardDamage, unit.transform.position, AttackEffectType.Explosive, isEnemyAttacker: false);
     }
     ```
     범위 안의 콜라이더 중 `HealthManager`가 붙은 것이면 무엇이든(태그/타입 구분 없이) 피해를 준다.
   - `BaseStructure` 프리팹(`Assets/prefabs/NTA/Building/BaseStructure.prefab`)을 확인해보니
     `BoxCollider` + `HealthManager`를 둘 다 갖고 있음 → `OverlapSphere` 대상에 그대로 걸림.

3. **정황이 맞아떨어짐**: SkyLancer의 "지상 폭격"은 사거리 안의 지점을 지정해 반경 5칸에 20 데미지를 주는
   범위 스킬로, 근처에 있는 적을 노리다가 반경 안에 마침 짓고 있던 아군 건물이 걸리면 그 건물도 20 데미지를
   그대로 맞는다. 플레이어 입장에서는 "내가 쓴 스킬"이라 "공격당했다"고 인지하지 못하고, 로그상으로도
   `isEnemyAttacker: false`라 "적에게 공격받음" 경고음도 울리지 않는다(`doc/0292` 참고) - 신고하신 "공격을
   받지 않았는데도"라는 인상과 정확히 일치한다.
   - 만약 이 폭격이 건설 완료 직전(`remainingBuildTime`이 거의 0에 가까운 시점)에 건물을 스치면, 이후 남은
     시간 동안 `healthPerSecond`가 다시 채울 시간이 부족해 체력이 낮은 채로(운이 나쁘면 1까지) 완공되어
     버린다.

## 결론

- **버그가 아니라 두 기존 기능(doc/0053의 "건설 중 피해 반영" + doc/0323의 "지상 폭격은 아군도 맞음")이
  겹쳐서 나타난 부작용**으로 보인다. 각 기능은 개별적으로는 의도한 대로 동작하고 있음.
- 재현 조건 추정: SkyLancer "지상 폭격"을 자기 진영 건설 현장 반경 5칸 이내에 사용 + 마침 그 건물이 완공
  직전(체력 회복이 데미지를 못 따라잡을 정도로 얼마 안 남은 시점)일 때.

## 다음 단계 (원하시면 진행)

원인만 확인해달라고 하셔서 아직 코드는 건드리지 않았습니다. 고치고 싶다면 방향이 여러 개라 먼저 골라주세요:

1. **`SkyLancerSkill.Activate()`에서 건설 중인 건물(`BaseStructure`)만 범위 피해 대상에서 제외** - "아군
   유닛도 맞는다"는 `doc/0323`의 의도는 그대로 유지하면서, 아직 짓고 있는 건물만 예외 처리.
2. **모든 건물(완공 여부 상관없이)을 범위 피해 대상에서 제외** - 지상 폭격을 순수 대인용 스킬로 한정.
3. **현재 동작 유지** - "아군 오사가 건설 중인 건물까지 늦게라도 부순다"를 의도된 리스크로 보고 그대로 둠.

어느 쪽으로 할지 알려주시면 그때 구현하겠습니다.
