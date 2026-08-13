# 0535 - EnemyAIDirector 기지 방어(homeBuilding 리스트화 + 건물 근처 유닛 탐색) 설계안 → 구현 완료

## 날짜
2026-08-13

## 요청 내용
"기지 방어의 경우 HomeBuilding의 경우 메인기지 혹은 [적] 미션오브젝트인 건물 같이 중요한 건물을 내가
집어넣을수 있도록 리스트 형식으로 바꿔주고 추가로 기지 방어는 적 AI의 건물 리스트중 공격을 받게 되면
해당 건물 근처에 있는 유닛이 방어하러 간다는건데 내가 맵에 프리팹 형식으로 이미 배치한 유닛들이 있는데
해당 유닛들중 공격받은 건물 근처의 있는걸 어떻게 파악할지 + 해당 유닛이 그 위치로 공격 명령을 받아서
방어하러 가야해"

정리하면 두 가지:
1. `homeBuilding`(단일 필드) → 여러 개 넣을 수 있는 **리스트**로.
2. 리스트 중 하나가 공격받으면, **그 건물 근처**에 있는 적 유닛(이 director가 스폰한 유닛뿐 아니라
   **미션 씬에 미리 프리팹으로 배치해둔 유닛도 포함**)을 찾아서 공격받은 위치로 방어를 보내야 함 -
   "근처인지 어떻게 파악할지"가 핵심 질문.

이 문서는 제안일 뿐, 아직 코드 수정 안 함.

## 기존 코드 조사
지금 `HandleBaseAttacked()`(`EnemyAIDirector.cs:205-215`)는 `homeBuilding` 딱 하나만 구독하고, 반응
대상도 이 director의 `garrison`(자기가 `SpawnUnit()`으로 생성한 유닛)뿐이다 - 미션 제작자가 씬에
손으로 미리 배치해둔 `EnemyUnitController`는 애초에 `garrison`에 들어간 적이 없어서 전혀 반응하지 않는다.

`EnemyUnitController`엔 "씬에 존재하는 모든 인스턴스" 같은 전역 레지스트리가 없다(`EnemyBuildingController.ActiveBuildings`
같은 static 리스트가 유닛 쪽엔 없음). 대신 유닛들은 전투/선택 판정을 위해 이미 `Collider`를 갖고 있다
(`UnitController.cs:1945` `target.TryGetComponent<Collider>()` 참고, `EnemyAttackRange`/`CaptureSystem`도
전부 트리거 콜라이더 기반 감지를 씀) - 그래서 director가 "이 유닛이 내가 스폰했는지"를 몰라도, 물리
쿼리(`Physics.OverlapSphere`)로 "이 좌표 반경 안에 어떤 유닛이 있는지"는 바로 알아낼 수 있다. **새 static
레지스트리를 만들 필요 없이 기존 콜라이더를 그대로 활용** - `EnemyUnitController.cs` 자체는 손 안 댐.

## 설계안

### `homeBuilding` → `List<EnemyBuildingController> homeBuildings`
필드 타입만 리스트로 바뀜(원소 타입은 그대로 `EnemyBuildingController` - "미션 오브젝트인 건물"도
결국 `HealthManager`를 가진 적 건물이면 이 리스트에 그대로 넣으면 됨).

구독 방식이 조금 까다로워진다 - `HealthManager.OnDamaged` 이벤트엔 "어느 건물이 맞았는지"가 안 실려오므로
(`int damage, Vector3 attackerPosition, AttackEffectType, bool isEnemyAttacker`뿐), 건물별로 클로저를
만들어 구독하고 나중에 정확히 그 델리게이트로 해지해야 한다(`Dictionary<EnemyBuildingController, Action<...>>`로
보관).

### 건물 근처 유닛 탐색: `Physics.OverlapSphere`
```
List<EnemyUnitController> FindNearbyEnemyUnits(Vector3 center) {
    var found = new List<EnemyUnitController>();
    foreach (Collider hit in Physics.OverlapSphere(center, defenseRadius))
        if (hit.TryGetComponent<EnemyUnitController>(out var unit) && !found.Contains(unit))
            found.Add(unit);
    return found;
}
```
`defenseRadius`(신규 인스펙터 필드)는 **공격받은 건물의 위치** 기준 반경 - "그 건물 근처에 있는 유닛"이
요청 내용 그대로. 이 방식이면 director가 스폰했는지 여부와 무관하게, 미션 제작자가 씬에 프리팹으로 미리
박아둔 유닛도 똑같이 잡힌다(콜라이더만 있으면 됨 - 전투 유닛은 이미 다 갖고 있음).

### `HandleBaseAttacked()` 재작성
```
void HandleBaseAttacked(EnemyBuildingController building, Vector3 attackerPosition, bool isEnemyAttacker) {
    if (isEnemyAttacker) return;

    foreach (var unit in FindNearbyEnemyUnits(building.transform.position))
        if (unit != null && !deployed.Contains(unit) && unit.IsIdle())
            unit.AttackMoveTo(attackerPosition);
}
```
- 탐색 중심은 **공격받은 건물의 위치**, 실제로 보내는 목적지는 **공격이 들어온 위치(`attackerPosition`)**
  - "해당 유닛이 그 위치로 공격 명령을 받아서 방어" 요청을 "공격자 쪽으로 달려가서 반격"으로 해석함
  (기존 doc/0532 항목 3과 동일한 목적지 - 건물 앞에 멀뚱히 서 있는 게 아니라 실제 공격자를 향해 감).
- `!deployed.Contains(unit)`은 이 director가 이미 다른 임무(웨이브/별동대)로 내보낸 유닛만 걸러낸다 -
  다른 director 소속이거나 애초에 director 없이 배치된 유닛은 이 체크에 안 걸리므로 항상 방어 후보가
  된다(원래 그런 유닛은 "누구 것도 아니므로" 항상 자유로운 게 맞음).
- `unit.IsIdle()`은 기존과 동일한 한계가 있음 - `AttackMoveTo` 중인 유닛도 내부적으로 `Idle` 상태라
  걸러지지 않는다(doc/0532 구현 노트 참고). 즉 마침 그 근처에서 다른 곳으로 이동 중이던 유닛도 방어에
  끌려올 수 있음 - "건물 근처에 있으면 무조건 반응"이라는 요청 취지엔 오히려 부합한다고 보고 별도 처리
  안 함(필요해지면 나중에 조정).

## 결정 사항 (2026-08-13, 사용자 확인 완료)
1. **`defenseRadius` 기본값**: 15로 확정.
2. **목적지**: 공격자 위치(`attackerPosition`)로 확정 - 설계안 그대로.

## 영향받는 파일 (구현 완료, 2026-08-13)
- 변경: `Assets\Scripts\System\EnemyAIDirector.cs`
- 변경 없음: `EnemyUnitController.cs`, `EnemyBuildingController.cs`, `HealthManager.cs` - 전부 기존
  콜라이더/이벤트만으로 충분했음

## 코드 변경

### 기존 코드
```csharp
[Header("기지 방어")]
[SerializeField] private EnemyBuildingController homeBuilding;
```
```csharp
private void OnEnable()
{
    if (homeBuilding != null && homeBuilding.GetHealthManager() != null)
        homeBuilding.GetHealthManager().OnDamaged += HandleBaseAttacked;
}

private void OnDisable()
{
    if (homeBuilding != null && homeBuilding.GetHealthManager() != null)
        homeBuilding.GetHealthManager().OnDamaged -= HandleBaseAttacked;
}
```
```csharp
private void HandleBaseAttacked(int damage, Vector3 attackerPosition, AttackEffectType type, bool isEnemyAttacker)
{
    if (isEnemyAttacker)
        return; // 플레이어에게 맞았을 때만 반응

    garrison.RemoveAll(u => u == null);

    foreach (EnemyUnitController unit in garrison)
        if (!deployed.Contains(unit) && unit.IsIdle())
            unit.AttackMoveTo(attackerPosition);
}
```

### 변경 코드
```csharp
[Header("기지 방어")]
[SerializeField] private List<EnemyBuildingController> homeBuildings; // 메인기지/미션 오브젝트 등 방어 트리거로 삼을 건물들(doc/0535)
[SerializeField] private float defenseRadius = 15f; // 공격받은 건물 기준, 방어하러 부를 유닛을 찾는 반경(doc/0535)
```
```csharp
// OnDamaged 이벤트엔 "어느 건물이 맞았는지"가 안 실려오므로, 건물별로 그 건물을 캡처한 델리게이트를
// 만들어 구독하고 여기 보관해뒀다가 OnDisable에서 정확히 그 델리게이트로 해지한다(doc/0535).
private readonly Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>> baseDefenseHandlers
    = new Dictionary<EnemyBuildingController, System.Action<int, Vector3, AttackEffectType, bool>>();

private void OnEnable()
{
    foreach (EnemyBuildingController building in homeBuildings)
    {
        if (building == null || building.GetHealthManager() == null)
            continue;

        EnemyBuildingController capturedBuilding = building;
        System.Action<int, Vector3, AttackEffectType, bool> handler =
            (damage, attackerPosition, type, isEnemyAttacker) => HandleBaseAttacked(capturedBuilding, attackerPosition, isEnemyAttacker);

        baseDefenseHandlers[building] = handler;
        building.GetHealthManager().OnDamaged += handler;
    }
}

private void OnDisable()
{
    foreach (var pair in baseDefenseHandlers)
        if (pair.Key != null && pair.Key.GetHealthManager() != null)
            pair.Key.GetHealthManager().OnDamaged -= pair.Value;

    baseDefenseHandlers.Clear();
}
```
```csharp
// 탐색 중심은 "공격받은 건물의 위치"(this director가 스폰했는지와 무관하게 미션 씬에 미리 배치해둔
// 유닛도 포함), 실제로 보내는 목적지는 "공격이 들어온 위치"(attackerPosition) - 건물 앞에 서 있지
// 않고 공격자 쪽으로 달려가 반격한다(doc/0535).
private void HandleBaseAttacked(EnemyBuildingController building, Vector3 attackerPosition, bool isEnemyAttacker)
{
    if (isEnemyAttacker)
        return; // 플레이어에게 맞았을 때만 반응

    foreach (EnemyUnitController unit in FindNearbyEnemyUnits(building.transform.position))
        if (!deployed.Contains(unit) && unit.IsIdle())
            unit.AttackMoveTo(attackerPosition);
}

// center 반경 defenseRadius 안의 살아있는 적 유닛을 전부 찾는다. EnemyUnitController엔 전역
// 레지스트리가 없지만, 전투 유닛은 이미 콜라이더를 갖고 있어(선택/사거리 판정용) 물리 쿼리로 충분하다
// - 이 director가 스폰했는지 여부와 무관하게 미션 씬에 프리팹으로 미리 배치해둔 유닛도 잡힌다(doc/0535).
private List<EnemyUnitController> FindNearbyEnemyUnits(Vector3 center)
{
    List<EnemyUnitController> found = new List<EnemyUnitController>();

    foreach (Collider hit in Physics.OverlapSphere(center, defenseRadius))
        if (hit.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit) && !found.Contains(unit))
            found.Add(unit);

    return found;
}
```

## 컴파일 확인
`npx uloop-cli compile` 결과 에러 0개(경고는 기존과 동일한 39개 - 전부 프로젝트 전역의 기존
`FindFirstObjectByType` obsolete 경고).

## 알려진 한계 (구현 노트)
`unit.IsIdle()`은 doc/0532 구현 노트에서 이미 밝혔듯 `AttackMoveTo` 중인 유닛도 `true`를 반환한다 - 즉
근처를 지나가던 중이던 유닛(다른 곳으로 이동 중)도 방어에 끌려올 수 있음. "건물 근처면 무조건 반응"이라는
요청 취지엔 부합한다고 보고 그대로 둠(필요해지면 나중에 조정).
