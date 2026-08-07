# 0448. OC 아군 전체 로스터 + 전용 AllyAttackRange 스크립트

**날짜:** 2026-08-07

## 요청 내용
> OC 유닛/건물 전부 아군버전을 만들어야하고 EnemyAttackRange이 아니라 새로운 스크립트를 짜도록하자
> 그리고 프리팹도 새로 만들거라 Tag나 Layer도 변경해서 중립으로 만들고 아군유닛이 아군OC를
> 공격하는일도 없게 만들고 새로운 팩션으로 만드는게 좋을거 같아

`doc/0447`에서 정한 방향(Prefab Variant, 외계종족과 자동교전)을 그대로 이어가되, 범위를 OC
로스터 전체(유닛 9종 + 건물 6종)로 넓히고, `EnemyAttackRange`를 직접 재설정해서 쓰지 않고 전용
클래스를 새로 만드는 것으로 확정.

## 설계

### 왜 `EnemyAttackRange`를 직접 안 쓰고 상속하는지

`doc/0447`에서 이미 `EnemyAttackRange.TargetTags`를 정적 배열 → 인스턴스 필드(`targetTags`)로
바꿔서 프리팹마다 다른 대상을 설정할 수 있게 해뒀음(이 작업 자체는 낭비가 아니라 이번 설계의
토대가 됨 - 그대로 재사용). 다만 "아군 OC 프리팹에 `EnemyAttackRange`라는 컴포넌트가 (설정값과
무관하게) 그대로 붙어있는 건" 이름만으로 혼동을 주므로, **`AllyAttackRange : EnemyAttackRange`**
로 상속만 받는 얇은 서브클래스를 새로 만듦:

```csharp
public class AllyAttackRange : EnemyAttackRange
{
    private void Reset() // 컴포넌트를 처음 추가하는 순간 기본값을 바로 맞춰줌
    {
        targetTags = new[] { "Enemy" };
    }
}
```

로직(추격/교전/거리 판정/도달 불가 판정 등 `EnemyAttackRange`의 나머지 전부)은 100% 그대로
상속받아 재사용 - 코드 중복 없음. `EnemyUnitController.Awake()`의
`GetComponentInChildren<EnemyAttackRange>()`는 다형성으로 `AllyAttackRange`도 그대로 찾아내므로
`EnemyUnitController.cs`는 전혀 건드릴 필요 없음.

### Tag/Layer

- **Layer**: 새 프리팹 루트를 `AllyOC` 레이어로(기존 OC 프리팹은 `Enemy` 레이어 8번 — 그대로 둠,
  아군 Variant만 다르게).
- **Tag**: `Untagged`로(기존 `Enemy` Tag 대신) — 플레이어 쪽 `AttackRange.OnTriggerEnter`의
  `CompareTag("Enemy")`가 자동으로 걸러줘서 자동공격 대상에서 빠짐.
- 자식 `AttackRange` 콜라이더(트리거, 외계종족 감지용)는 원래 Layer 그대로 둠 — 물리 충돌
  매트릭스는 안 건드림.

### Prefab Variant 15개

원본 OC 유닛/건물 프리팹을 상속하는 Variant를 `Assets/prefabs/OC/Ally/` 아래에 생성:

**유닛 9종** (`Unit/`) — 각 Variant는 루트 Layer/Tag 변경 + 자식 `AttackRange`의
`EnemyAttackRange` 컴포넌트를 `AllyAttackRange`로 교체(`UnitRange` 값은 그대로 이전):
Nanobot Repair, Cyborg Soldier, Railgunner, Striker, Brute Mech, Heavy Assault Tank, Ironhawk,
Raven, Strike Drone.

**건물 6종** (`Building/`) — 루트 Layer/Tag만 변경(건물은 원래 `EnemyAttackRange`가 없는 순수
껍데기라 자식 교체 작업 없음): Ally_MainBase, Ally_SupplyDepot, Ally_Tier1, Ally_Tier2,
Ally_Tier3, Ally_Lab. (`BaseStructure.prefab`은 건설 중 표시 전용 공용 placeholder라 제외.)

스탯(`enemyUnitID`/`enemyBuildingID`)은 원본 그대로 상속되므로 기존 "OC Unit/Building Data SO"를
그대로 계속 참조함 — 아군판이라고 스탯이 달라지는 게 아니라 배치 방식(아군)만 다른 것이므로
새 데이터 SO는 필요 없음.

## 결과

15개 Variant 전부 생성 성공(0 실패). 검증 결과 전부 `PrefabAssetType.Variant`, 루트 Layer=`AllyOC`,
루트 Tag=`Untagged`. 유닛 9종은 `AttackRange` 자식의 컴포넌트가 `EnemyAttackRange` → `AllyAttackRange`로
교체됐고 `UnitRange` 값도 원본 그대로 보존됨, `targetTags`는 `Reset()`으로 `["Enemy"]` 확인됨(별도
수정 불필요).

| Variant | UnitRange | targetTags |
|---|---|---|
| Nanobot Repair (Ally) | 4 | [Enemy] |
| Cyborg Soldier (Ally) | 12 | [Enemy] |
| Railgunner (Ally) | 20 | [Enemy] |
| Striker (Ally) | 14 | [Enemy] |
| Brute Mech (Ally) | 2 | [Enemy] |
| Heavy Assault Tank (Ally) | 20 | [Enemy] |
| Ironhawk (Ally) | 18 | [Enemy] |
| Raven (Ally) | 18 | [Enemy] |
| Strike Drone (Ally) | 20 | [Enemy] |
| Ally_MainBase / Ally_SupplyDepot / Ally_Tier1 / Ally_Tier2 / Ally_Tier3 / Ally_Lab | (건물 - AttackRange 없음) | — |

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 0`.
- Unity 콘솔 Error 로그 0건.

## 변경된 파일

- `Assets/Scripts/FogOfWar/Enemy/EnemyAttackRange.cs` — `targetTags` 필드를 `private` → `protected`
  (doc/0447에서 이미 정적→인스턴스 필드로 바꿔둔 것의 연장, 하위 클래스가 기본값을 오버라이드할 수
  있게)
- `Assets/Scripts/FogOfWar/Ally/AllyAttackRange.cs` (신규) — `EnemyAttackRange` 상속, `Reset()`으로
  기본 대상 Tag를 `["Enemy"]`로 설정
- Prefab Variant 15개(신규): `Assets/prefabs/OC/Ally/Unit/*.prefab` 9개,
  `Assets/prefabs/OC/Ally/Building/*.prefab` 6개

## 남은 작업

- 4스테이지 씬에 이 아군 OC Variant들을 실제로 배치하는 건 씬 작업이라 별도 요청 시 진행.
- ~~`UserControl.cs`의 `layerAllyOC` 필드 인스펙터 연결~~ — `GameManager.prefab`의 `UserControl`
  컴포넌트에 `layerAllyOC: m_Bits: 8192`(레이어 13번=`AllyOC`)로 직접 연결 완료. 재컴파일/콘솔 확인
  결과 이상 없음.
