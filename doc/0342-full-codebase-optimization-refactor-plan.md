# 0342 — 전체 코드베이스 최적화 리팩토링 계획

**날짜:** 2026-07-31

## 요청 내용

"코드 전체를 읽고 전체적인 리팩토링을 좀 진행해줘. 기능적인건 문제 없고 코드 최적화면에서만 집중해서, 리팩토링 이후 게임 기능상에 차이는 없어야해. 어떤순서로 진행했고 어떤 변경점들이 있었는지는 문서로도 남겨줘."

→ 기능/동작 변화 없이 순수 성능/클린업 관점의 최적화만 진행. 진행 순서와 변경점을 문서화.

## 조사 내용

`Assets/Scripts` 하위 82개 스크립트, 총 14,164줄을 5개 그룹(코어 유닛/전투, UI, 건물/배치, 적 AI·전장의 안개, 오디오·이펙트·기타)으로 나눠 전수 검토했다. AssetFolder/Plugins/TextMesh Pro 등 서드파티 에셋 코드는 제외(프로젝트 자체 코드가 아님).

찾은 항목은 "게임 동작/타이밍/결과에 영향을 주지 않는" 것만 추렸다 — 즉 다음 셋 중 하나:
- 동일한 비교를 더 싼 연산으로 바꾸는 것 (`Vector3.Distance` → `sqrMagnitude` 비교)
- 이미 캐싱된 값이 있는데 다시 조회하는 중복 제거 (`GetComponent`/`FindFirstObjectByType` 캐싱)
- 실제로 쓰이지 않는 코드 제거 (dead code, 미사용 using/필드, 디버그 로그 잔재)

AI 판정 로직, 데미지 계산, 타겟 우선순위 등 **결과가 달라질 수 있는 부분은 전부 제외**했다(예: Stage0Objectives의 매프레임 적 카운트 스캔, UserControl의 레이캐스트 우선순위 — 조사 중 발견했지만 "안전하게 기계적으로 고칠 수 없다"고 판단해 이번 계획에서 제외).

---

## 진행 순서 및 변경점

### Phase 1 — sqrMagnitude 치환 (가장 안전, 즉시 적용 가능)

`Vector3.Distance(a, b) <= range` 형태는 내부적으로 `sqrt`를 계산한다. `(a - b).sqrMagnitude <= range * range`로 바꾸면 **수학적으로 완전히 동일한 결과**를 내면서 sqrt 연산만 제거된다. 매 프레임 실행되는 전투 판정 코드라 체감 효과가 가장 크다.

**1-1. `Assets/Scripts/Unit/AttackRange.cs:79`** (모든 아군 유닛에 붙어있어 매 프레임 실행)

기존 코드:
```csharp
float distance = Vector3.Distance(transform.position, target.transform.position);
...
if (distance <= UnitRange)
```
변경 코드:
```csharp
float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;
...
if (sqrDistance <= UnitRange * UnitRange)
```

**1-2. `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs:51`** (`HasTargetInAttackRange` getter)

기존 코드:
```csharp
return Vector3.Distance(transform.position, target.transform.position) <= UnitRange;
```
변경 코드:
```csharp
return (transform.position - target.transform.position).sqrMagnitude <= UnitRange * UnitRange;
```

**1-3. `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs:107`** (`Update()`, 1-1과 동일 패턴)

기존 코드:
```csharp
float distance = Vector3.Distance(transform.position, target.transform.position);
...
if (distance <= UnitRange)
```
변경 코드:
```csharp
float sqrDistance = (transform.position - target.transform.position).sqrMagnitude;
...
if (sqrDistance <= UnitRange * UnitRange)
```

**1-4. `Assets/Scripts/Unit/Projectile.cs:36-37`** (비행 중인 투사체 전부, 매 프레임)

기존 코드:
```csharp
Vector3 toTarget = target.position - transform.position;
if (toTarget.magnitude <= hitDistance)
```
변경 코드:
```csharp
Vector3 toTarget = target.position - transform.position;
if (toTarget.sqrMagnitude <= hitDistance * hitDistance)
```
(바로 아래 `toTarget.normalized`에서 `magnitude`가 다시 필요하므로 그 줄은 그대로 둠 — `sqrMagnitude`는 비교에만 사용)

**1-5. `Assets/Scripts/BuildSystem/PlacementSystem.cs:447`** (`IsTooCloseToResource`, 그리드 셀 변경/배치 클릭 시)

기존 코드:
```csharp
float distance = Mathf.Sqrt(dx * dx + dz * dz);
if (distance < minDistanceFromResource)
```
변경 코드:
```csharp
float sqrDistance = dx * dx + dz * dz;
if (sqrDistance < minDistanceFromResource * minDistanceFromResource)
```

**1-6. `Assets/Scripts/Unit/UnitController.cs`** — 같은 패턴이 4곳 더 있음 (`FriendlyAttackTick` L651, `BuildTick` L767, `DistanceToTarget` L1453-1458 내부, `SkillOrderTick` L1641/1657). 같은 파일의 `AttackOrderTick`/`GetClosestEnemy`는 이미 sqrMagnitude 방식으로 되어 있어, 이 4곳만 스타일이 다르다 — 동일 패턴으로 통일.

**1-7. `Assets/Scripts/Building/BuildingController.cs:125, 179`** (`UpdateLiftedMovement`, 건물 이착륙 중 매 프레임) — 동일 패턴 (`Vector3.Distance(...) < 0.05f` → `sqrMagnitude < 0.05f*0.05f`).

---

### Phase 2 — 중복 조회 제거 (캐싱)

이미 캐싱된 필드가 있는데도 `GetComponent`/`FindFirstObjectByType`를 다시 호출하는 경우. 결과값은 100% 동일 — 조회 경로만 짧아짐.

**2-1. `Assets/Scripts/Unit/UnitController.cs:1488` — `Die()`** (가장 명확한 낭비: 캐싱된 필드가 바로 옆에 있는데 씬 전체를 다시 검색)

기존 코드:
```csharp
RTSUnitController controller = FindFirstObjectByType<RTSUnitController>();
controller?.UnitList.Remove(this);
```
변경 코드:
```csharp
rtsController?.UnitList.Remove(this);
```
(`rtsController` 필드는 `Start()`에서 이미 캐싱되어 있음 — L278-279)

**2-2. `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs:521` — `ApplyUnitData()`**

기존 코드:
```csharp
GetComponent<HealthManager>()?.InitializeHealth(...)
```
변경 코드:
```csharp
healthManager?.InitializeHealth(...)
```
(`healthManager` 필드는 `Awake()`에서 이미 캐싱됨 — L79)

**2-3. `Assets/Scripts/Unit/UnitController.cs` — `Awake()`에서 캐싱 추가**
공격/명령 시마다 `GetComponent<UnitEffects>()` (L547), `GetComponent<UnitAudio>()`/`GetComponent<LaserBeamAttack>()` (L948-950), `TryGetComponent<ProjectileAttack>()` (L943, L1527)를 매번 새로 조회한다. 이미 같은 파일에서 `attackRange`/`turretController`를 `Awake()`에 캐싱해둔 패턴이 있으므로 동일하게 필드로 승격.

**2-4. `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` — 동일 패턴**
`Attack()` 내부 `GetComponent<UnitEffects>()`/`GetComponent<UnitAudio>()`/`GetComponent<LaserBeamAttack>()`(L303-306), `TryGetComponent<ProjectileAttack>()`(L298, `GetShotCount()`L494-496), `MoveTo()`의 `GetComponent<UnitEffects>()`(L193) → `Awake()`에서 캐싱.

**2-5. `Assets/Scripts/Unit/UnitController.cs:981-1031` — `GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`/`IsTargetAirborne`**
공격 한 번에 이 4개 메서드가 모두 같은 `target`에 대해 각자 `TryGetComponent<UnitController>` → `TryGetComponent<EnemyUnitController>` 순으로 조회한다(최대 8회 중복). 데미지 계산 진입부에서 한 번만 분류하고 그 결과를 네 메서드에 넘기도록 변경(반환값은 동일).

**2-6. `Assets/Scripts/CaptureSystem/TerritoryZone.cs`** (영향 범위가 가장 넓음 — `BaseStructure`/`ResearchQueue`/`UnitController`/`UnitSpawner`/`FogRevealerAgent` 등 5곳 이상에서 매 프레임 호출)

기존 코드 (`GetPolygonXZ`, L78-87):
```csharp
public Vector2[] GetPolygonXZ()
{
    var result = new Vector2[pinPoints.Count];
    for (int i = 0; i < pinPoints.Count; i++) { ... }
    return result;
}
```
이 배열이 `Update()`(L51-55)에서 매 프레임 `RefreshOutline()`을 거쳐 재생성되고, 소유권(owner)이 바뀌지 않는 한 값도 동일하다. 핀 위치가 런타임에 거의 바뀌지 않는 점을 이용해, 결과 배열을 캐싱하고 핀이 실제로 움직였을 때만 재계산하도록 변경(반환값은 항상 동일 — dirty-flag 방식).

**2-7. `Assets/Scripts/Building/ResearchQueue.cs:68-69` — `IsQueued`**

기존 코드:
```csharp
private bool IsQueued(ResearchType type) =>
    researchQueue.Exists(r => r.Type == type);
```
변경 코드:
```csharp
private bool IsQueued(ResearchType type)
{
    foreach (var r in researchQueue)
        if (r.Type == type) return true;
    return false;
}
```
(람다 캡처가 없어 GC 할당 자체는 없지만, `Exists`가 만드는 델리게이트 오버헤드 제거 — 결과는 100% 동일)

**2-8. `Assets/Scripts/Effects/RadiusIndicator.cs:46` — `Shader.Find` 캐싱**
`Show()`/`CreateFollowing()`마다(스킬 사용 시) `Shader.Find("Sprites/Default")` 문자열 조회가 반복됨 → `private static readonly Shader` 필드로 1회만 조회.

**2-9. `Assets/Scripts/Building/BaseStructure.cs:65, 177`**
`Initialize()`/`CompleteConstruction()` 양쪽에 동일한 `buildingDatabase.buildingData.Find(d => d.ID == buildingID)`가 중복 — 헬퍼 메서드로 추출(호출 빈도 낮아 성능 영향은 미미, 순수 클린업).

---

### Phase 3 — Dead code / 디버그 로그 정리

**3-1. `Assets/Scripts/Unit/UnitController.cs:2, 5`** — `using TMPro;`, `using UnityEngine.Audio;` 미사용 (grep 확인) → 제거.

**3-2. `Assets/Scripts/System/RTSUnitController.cs:3`** — `using System.Linq;` 미사용 (파일 내 LINQ 연산자 없음, `Find`/`FindAll`은 `List<T>` 자체 메서드) → 제거.

**3-3. `Assets/Scripts/BuildSystem/PlacementSystem.cs:1`** — `using NUnit.Framework;` 프로덕션 코드에 테스트 프레임워크 참조가 들어가 있음 → 제거.

**3-4. `Assets/Scripts/Unit/UnitController.cs:97`** — `stuckTimer` 필드가 선언·직렬화만 되고 어디서도 읽거나 쓰이지 않음 (grep 확인) → 제거.

**3-5. `Assets/Scripts/UnitSpawner/UnitSpawner.cs:57, 62-67`** — 디버그 로그 잔재. 특히 `Enqueue()`의 `FindIndex` 조건절 안에 `Debug.Log`가 들어가 있고, 바로 아래 줄에 로그 없는 버전이 주석으로 남아있음(교체 흔적).

기존 코드:
```csharp
Debug.Log($"unitData : {database.unitData}");
...
int index = database.unitData.FindIndex(d =>
{
    Debug.Log($"Compare: {d.ID} == {unitID}");
    return d.ID == unitID;
});
//int index = database.unitData.FindIndex(d => d.ID == unitID);
```
변경 코드:
```csharp
int index = database.unitData.FindIndex(d => d.ID == unitID);
```

**3-6. `Assets/Scripts/BuildSystem/PlacementSystem.cs:414-416`** — `IsBlocked()`에서 막힌 콜라이더마다 `Debug.Log` 출력(배치 커서가 막힌 칸 위에 있는 동안 계속 호출됨) → 제거.

**3-7. `Assets/Scripts/Unit/HealthManager.cs:77`** — 데미지를 받을 때마다 `Debug.Log`(대규모 전투 시 로그 폭주) → 제거.

**3-8. `Assets/Scripts/Resource/ResourceController.cs`** — 파일 전체가 빈 `Start()`/`Update()`만 있는 스텁. 씬/프리팹에 붙어있다면 아무 일도 안 하면서 매 프레임 Unity 메시지 디스패치 비용만 발생 → 빈 `Update()` 제거(컴포넌트 자체 삭제는 씬/프리팹 참조 확인이 필요해 별도 확인 후 진행).

---

### Phase 4 — UI 매프레임 갱신 → 변경 시에만 갱신 (Dirty-flag)

이 구간은 Phase 1-3보다 코드 변경 폭이 크다(상태 캐싱 필드 추가 필요). 최종 화면 출력 결과는 동일하지만 "매 프레임 무조건 다시 씀" → "값이 바뀔 때만 씀"으로 바뀌는 것이라, 별도 승인을 받고 진행하고 싶다.

- **`Assets/Scripts/UI/UIController.cs:295-303`** (`UpdateResourceUI`) — 광물/가스/인구 텍스트가 값이 같아도 매 프레임 `.text` 재할당(TMP 메시 재생성 비용) → 마지막 값과 비교 후 다를 때만 갱신.
- **`Assets/Scripts/UI/UIController.cs:772-865, 912-941`** (`RefreshSquadSlots`/`RefreshSquadBuildingSlots`) — 스쿼드 패널이 열려있는 동안 매 프레임 슬롯 버튼의 델리게이트를 새로 할당. 이미 계산해둔 `SquadUnitsEqual` 비교 결과가 있으니 그 결과로 게이팅.
- **`Assets/Scripts/UI/ProductionSlot.cs:137-147`** / **`Assets/Scripts/UI/Tooltip/TooltipUI.cs:146`** — 버튼에 마우스를 올리고 있는 동안 매 프레임 툴팁 텍스트 재계산 + 레이아웃 강제 리빌드 + `Vector3[4]` 배열 재할당 → 마지막 표시 내용과 동일하면 스킵, 배열은 필드로 재사용.
- **`Assets/Scripts/System/RTSUnitController.cs:1129-1141`** (`ShowUnitTierPanel`) — 생산 패널이 열려있는 동안 매 프레임 `unitDatabase.unitData.FindAll(...)`로 배열 재생성 → 선택 상태가 바뀔 때만 재생성.
- **`Assets/Scripts/System/RTSUnitController.cs`** — `GetRepresentativeBuilding()`이 프레임당 여러 메서드(`GetProductionQueue`/`GetProductionProgress`/`CancelProduction`/`TryResearch` 등)에서 각각 다시 호출됨(L967/973/982/1202/1228/1235, 내부적으로 `selectedBuildingList.Find()` 최대 6회 스캔) → `UpdateUI()`에서 한 번만 계산해 전달.

---

## 요약 / 영향받는 파일

**Phase 1-3은 결과가 수학적으로/논리적으로 완전히 동일함이 명확한 기계적 치환**이라 리스크가 낮음. Phase 4는 "매 프레임 재적용 → 변경 시에만 적용"으로 바뀌는 만큼, 캐시 무효화 조건을 놓치면 화면이 안 갱신되는 버그가 생길 수 있어 파일별로 좀 더 신중히 다뤄야 함.

| Phase | 파일 수 | 항목 수 | 리스크 |
|---|---|---|---|
| 1. sqrMagnitude 치환 | 5 | 7곳 | 매우 낮음 |
| 2. 중복 조회 캐싱 | 7 | 9곳 | 낮음 |
| 3. Dead code 정리 | 6 | 8곳 | 매우 낮음 |
| 4. UI dirty-flag | 4 | 5곳 | 중간 (신중히) |

조사했지만 **이번 계획에서 제외한 항목** (안전하게 기계적으로 고칠 수 없어서):
- `Stage0Objectives.cs:44` 매프레임 `FindObjectsByType<EnemyUnitController>()` 전체 스캔 — 이벤트 기반으로 바꾸려면 스폰/사망 이벤트 배선이 필요한 구조 변경이라 제외.
- `UserControl.cs`의 `GetHoveredTarget`/`UpdatePointer` 매프레임 레이캐스트 4~6회 — 우선순위 순서가 커서 판정 로직과 얽혀있어 기계적 축소가 안전하지 않음.
- `UnitController.cs:382-415` 공중 유닛 분리 로직의 전체 유닛 리스트 순회 — 공중 유닛 전용 리스트 분리는 자료구조 변경(추가 유지보수 필요)이라 이번 "순수 최적화" 범위를 넘어선다고 판단, 제외.
- `UnitAudio.cs:25`/`BuildingAudio.cs:23`의 `FindFirstObjectByType<RTSUnitController>()` 반복 — `RTSUnitController`에 static 인스턴스를 추가해야 해서(새 공개 API 추가) 이번 범위에서 제외.

## 적용 결과 (2026-07-31)

사용자가 Phase 1~4 전체 적용을 승인해 실제 코드에 반영했다. `npx uloop-cli compile`로 컴파일 확인 — **에러 0개**, 경고 25개는 전부 이번 변경 이전부터 있던 것(주로 `FindFirstObjectByType` deprecated 경고, 서드파티 에셋 경고)이며 이번 리팩토링으로 새로 생긴 경고는 없다.

계획과 다르게 적용된 부분:

- **Phase 2-5 (`GetTargetArmor`/`GetTargetSizeType`/`GetTargetArmorType`/`IsTargetAirborne` 중복 조회 제거)**: 문서에는 "반환값 동일"로만 적었지만, 실제로는 네 메서드의 파라미터를 `GameObject target` → `UnitController targetFriendlyUnit, EnemyUnitController targetEnemyUnit`로 바꿔 `Attack()` 진입부에서 한 번만 분류(`TryGetComponent`)하고 그 결과를 그대로 넘기는 방식으로 구현했다(`CalculateFinalDamage`도 동일하게 시그니처 변경). 전부 `private` 메서드라 `UnitController.cs` 내부에서만 호출되는 것을 확인 후 진행 — 외부 호출부 영향 없음.
- **Phase 3-7 (`HealthManager.cs` 데미지 로그 제거)**: 계획대로 적용됨(실제 줄번호는 74번, 문서 작성 시 예상했던 77번과 약간 다름).
- **Phase 4 (`GetRepresentativeBuilding()` 중복 계산 제거) — 보류**: 계획에는 포함했지만 실제 구현 단계에서 재검토한 결과 보류했다. 이 메서드를 호출하는 `GetProductionQueue`/`CancelProduction`/`TryResearch`/`CanResearch` 등은 `UpdateUI()` 내부뿐 아니라 버튼 델리게이트(`CancelProduction`을 메서드 그룹으로 직접 바인딩하는 등)로 여러 곳에 노출된 공개 API라, 시그니처를 바꾸려면 호출부 전체를 건드려야 해서 "기능 차이 없음" 요구사항 대비 리스크가 컸다. 반면 `selectedBuildingList`(선택된 건물 수)는 보통 한두 개뿐이라 `GetRepresentativeBuilding()` 자체의 실제 비용은 미미하다 — 얻는 이득 대비 위험이 커서 스킵. (같은 파일의 `ShowUnitTierPanel` dirty-gating은 실제 GC 할당이 있던 부분이라 계획대로 적용함.)

최종 적용 요약: Phase 1(7곳) 전부 적용 / Phase 2(9곳 전부, 방식만 일부 조정) 적용 / Phase 3(8곳 전부) 적용 / Phase 4(5곳 중 4곳 적용, `GetRepresentativeBuilding` 1곳 보류).

## 다음 단계

없음 — 승인된 범위의 리팩토링은 모두 적용 완료. 보류한 `GetRepresentativeBuilding()` 중복 계산 제거는 필요해지면 별도 요청으로 진행.
