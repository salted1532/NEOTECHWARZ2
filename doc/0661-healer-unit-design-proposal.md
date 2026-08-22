# 0661 - 치유 유닛 컨셉 및 유닛 체력 회복 메커니즘 - 설계 (미구현)

## 요청
> 유닛 체력 회복. 치유 유닛 추가할 생각인데 치유 유닛의 컨셉이랑, 유닛 체력 회복 메커니즘을 설계해줘.
> 유닛 체력 회복은 원거리에서도 가능하도록 할거고, 치유할 때 건물 수리하는거처럼 실수(float)로
> 오르는 방식으로 할거고, 치료하고 있는 유닛과 레이저 빔으로 연결되어 회복되는거처럼 보이도록.

아직 코드는 건드리지 않았습니다 - 설계만. 기존 시스템(건물 수리 틱, 사거리 자동교전, 공격 레이저 빔)을
최대한 그대로 재사용하는 방향으로 짰습니다.

## 기존 시스템 조사 (이번 설계가 기대는 것들)

| 기존 것 | 위치 | 재사용할 부분 |
|---|---|---|
| 건물 수리 틱(실수 누적 → Heal) | `UnitController.RepairTick()`(`UnitController.cs:1142`) | "실수로 오르는" 요청 그대로 - `repairHealAccumulator`처럼 float를 매 프레임 누적하다가 정수가 되면 `HealthManager.Heal()` 호출 |
| 사거리 자동교전(트리거 콜라이더 + 매프레임 재탐색) | `AttackRange.cs` | "원거리 자동 회복"의 뼈대 - 적 대신 "다친 아군"을 감지하는 거울상 컴포넌트로 재사용 |
| 공격 레이저 빔(둘을 잇는 LineRenderer) | `LaserBeamAttack.cs` | 요청한 "레이저 빔으로 연결" 시각효과의 뼈대 - 다만 0.2초 버스트가 아니라 회복이 지속되는 동안 계속 이어져야 함(아래 변경점 참고) |
| 역할 플래그가 스탯이 아니라 **태그**로 결정됨 | `UnitController.cs:356` `isWorker = CompareTag("Worker")` | 치유 유닛도 스탯이 아니라 `"Healer"` 태그로 `isHealer`를 결정 - 기존 관례 그대로 |
| `HealthManager.Heal(int)` | `HealthManager.cs:114` | 이미 최대체력 클램프까지 구현돼 있음 - 그대로 호출만 하면 됨 |

## 컨셉

전투에 낀 다친 아군 유닛에게 붙어서(정확히는 사거리 안에만 들어오면) 레이저 빔을 지속적으로 쏘며
체력을 서서히 채워주는 원거리 지원 유닛. 건물을 고치는 일꾼(WorkerDrone/Nanobot Repair)의 "유닛판"
이지만, 걸어가서 옆에 붙어야 하는 건물 수리와 달리 **사거리 안이면 이동 없이도 회복**이 가능하다는
점이 다르다 - 전투 대형에서 뒤로 빠져서 안전 거리를 유지한 채 치유하는 서포터 포지션.

기존 전투 유닛의 "사거리 안 적을 자동 교전"(AttackRange)과 정확히 대칭되는 "사거리 안 다친 아군을
자동 치유"로 설계하면, 플레이어 입장에서도 배우기 쉽다(공격 유닛과 조작 감각이 똑같음 - 그냥 Idle로
두면 알아서 근처 다친 아군을 고쳐줌).

## 메커니즘 설계

### 1. 감지 - `HealRange` (신규, `AttackRange`의 거울상)
- `AttackRange`가 트리거 콜라이더로 `"Enemy"` 태그를 감지하듯, `HealRange`는 같은 트리거 방식으로
  **"Enemy" 태그가 아니면서 `HealthManager`가 있고 체력이 풀피가 아닌** 대상을 감지 목록에 담는다.
- 건물은 제외한다 - 건물 수리는 이미 일꾼 시스템이 전담하고 있고, 요청도 "유닛 체력 회복"으로
  명시했으므로, 감지 시 대상이 `BuildingController`면 건너뛴다(유닛만).
- 태그로만 거르면(`!= "Enemy"`) 플레이어(NTA) 유닛뿐 아니라 `AllyOC`(구조된 아군 OC) 유닛도 자동으로
  회복 대상에 들어온다 - `AttackRange`가 진영을 따로 안 가리고 태그 하나로 적/아군을 가르는 것과
  대칭. 자기 자신(치유 유닛 본인)이 다쳤을 때도 같은 필터로 자연스럽게 포함됨(제외할 이유 없음).
- `AttackRange.GetPreferredTarget()`과 동일하게, 명시적으로 우클릭 지정한 대상(치유 명령)이 있으면
  그 대상만 우선하고, 없으면 사거리 안에서 가장 급한(체력비율이 가장 낮은, 혹은 가장 가까운) 아군을
  자동으로 고른다.

### 2. 회복 틱 - 기존 `RepairTick` 패턴 그대로 이식
`UnitController`에 아래 필드/메서드를 `isRepairing`/`RepairTick()`과 나란히 추가(완전히 같은 구조,
대상 타입만 `HealthManager`로 직접):
```csharp
private HealthManager healTarget;
private bool isHealing;
private float healTickTimer;
private float healAccumulator;      // repairHealAccumulator와 동일 패턴 - "실수로 오르는" 부분
[SerializeField] private float healTickInterval = 0.5f; // repairTickInterval과 동일값 재사용 권장
```
`HealTick()`은 `RepairTick()`에서 자원 정산(`TrySpendOre`) 블록만 통째로 빼고 나머지(실수 누적 →
정수 되면 `Heal()`)는 그대로:
```csharp
healAccumulator += healPerSecond * Time.deltaTime;
if (healAccumulator >= 1f)
{
    int wholeHeal = Mathf.FloorToInt(healAccumulator);
    healAccumulator -= wholeHeal;
    healTarget.Heal(wholeHeal);
}
```
자원 소모 없음(건물 수리는 광물이 드는데, 유닛 회복도 자원을 소모시킬지는 확인 필요 - 아래 "확인
필요" 참고. 기본값은 무료로 설계).

### 3. 사거리 안이면 정지하고 채널 - 기존 `Attack()`과 동일한 상태 처리
`AttackRange.Update()`가 사거리 안이면 `unitController.Attack(...)`을 부르듯, `HealRange.Update()`는
사거리 안이면 `unitController.BeginHeal(target)`을 부른다. `Attack()`이 `navMeshAgent.isStopped = true`로
정지하고 조준하듯, `BeginHeal()`도 이동을 멈추고 `FaceTransform(target)`으로 대상을 바라본다(수리 중
`FaceTransform(repairTarget.transform)`과 동일, `UnitController.cs:1163`). 사거리 밖이면
`AttackRange`의 `ChaseTarget`처럼 대상 쪽으로 접근(단, 완전히 붙을 필요는 없고 사거리 안으로만).

### 4. 시각효과 - 지속형 레이저 빔 (`LaserBeamAttack`을 신규 `HealBeam`으로 변형)
`LaserBeamAttack.Fire()`는 고정 `beamDuration`(0.2초) 뒤 자동으로 끄는 "버스트"라 그대로 못 쓴다.
같은 골격(풀링된 `LineRenderer`, 매 프레임 두 지점 갱신)에서 종료 조건만 바꾼 `HealBeam` 컴포넌트를
새로 만든다:
```csharp
public void StartBeam(Transform target) // BeginHeal()에서 호출
public void StopBeam()                  // 회복 종료(대상이 풀피/사망/사거리 이탈)될 때 호출
```
루프는 `target != null && isHealing`인 동안 계속 두 지점(발사 지점 ↔ 대상 콜라이더 표면)을 갱신 -
`BeamRoutine`의 `while (elapsed < beamDuration && target != null)` 조건에서 시간 조건만 빼면 됨.
색상은 공격 레이저(파랑, `Attack_Laser_Blue_3D`)와 구분되는 별도 프리팹/머티리얼 권장 - 기존
"아군에게 좋은 상태" 색으로 이미 `UnitSilhouette`가 `#19FF00`(초록)를 쓰고 있으므로(doc/0592) 통일감
있게 초록/청록 계열 추천.

### 5. 오디오 - 기존 `PlayRepairTick` 패턴 그대로
`UnitSoundBankSO`에 `healTickSFX`(또는 `healVoice`) 추가, `UnitAudio`에 `PlayHealTick()` 추가해서
`HealTick()`이 정산될 때마다 재생 - `BuildingAudio.PlayRepairTick()`과 동일 패턴.

### 6. 데이터 - `UnitDataSO`
- 역할 플래그: 스탯이 아니라 프리팹의 **`"Healer"` 태그**(신규, `"Worker"`와 나란히 `TagManager.asset`에
  추가)로 결정 - `isHealer = CompareTag("Healer")`.
- 전용 스탯 신규 추가(기존 `attackRange`/`attackDamage`와 의미가 다르므로 겹쳐쓰지 않고 분리):
  - `healRange` (int) - 원거리 회복 사거리.
  - `healPerSecond` (float) - 초당 회복량 (수리의 `repairSpeedMultiplier × maxHealth ÷ buildTime` 같은
    공식 대신, 힐러는 대상마다 최대체력이 제각각이라 그냥 고정값으로 스탯화하는 게 자연스러움).

## 확인 완료 / 남은 확인 사항

1. ~~소속 진영~~ - **확정: NTA(플레이어)**. `NTA Unit Data SO.asset`에 새 항목 추가, OC 로스터는 건드리지 않음.
2. ~~자동 감지 vs 수동 명령~~ - **확정: 자동 감지**. 위 설계(`HealRange`가 `AttackRange`처럼 Idle 상태에서
   사거리 안 다친 아군을 자동으로 치유, 우클릭 지정 시 그 대상 우선)를 그대로 진행.
3. ~~자원 비용~~ - **확정: 무료**. `HealTick()`에 `TrySpendOre` 같은 자원 정산 블록 자체를 넣지 않음
   (건물 수리의 `repairOreCostPerTick` 관련 코드는 전부 제외).
4. ~~레이저 빔 색상~~ - **확정: 사용자가 직접 조정**. 색을 코드/셰이더에 고정하지 않고, `HealBeam`
   컴포넌트에 `[SerializeField] private Color beamColor`(또는 머티리얼 `_Color`를 인스펙터에 노출)로
   빼서 프리팹 인스펙터에서 바로 바꿀 수 있게 한다 - `UnitSilhouette.mat`처럼 코드에 하드코딩하지 않음.
   프리팹 자체(LineRenderer + 머티리얼)는 `LaserBeamAttack`이 쓰는 `Attack_Laser_Blue_3D`를 복제해서
   만들고, 머티리얼 색만 인스펙터 노출 필드로 뺀 새 프리팹으로 시작.
5. **초당 회복량(`healPerSecond`) 기본값: 3** - `NTA Unit Data SO.asset`의 치유 유닛 항목 기본값으로
   반영. (참고: 회복 틱 간격 `healTickInterval = 0.5`s 기준이면 틱당 1.5, 누적 2틱마다 정수 3이 채워짐 -
   기존 `repairHealAccumulator`와 동일한 실수 누적 방식이라 자연스럽게 처리됨.)

## 변경 예정 파일 (구현 승인 시)
- `ProjectSettings/TagManager.asset` - `"Healer"` 태그 추가
- `Assets/Scripts/Unit/HealRange.cs` (신규, `AttackRange.cs` 거울상)
- `Assets/Scripts/Unit/HealBeam.cs` (신규, `LaserBeamAttack.cs` 변형)
- `Assets/Scripts/Unit/UnitController.cs` - `BeginHeal`/`HealTick`/`isHealer` 추가 (`Repair`/`RepairTick`과
  나란히)
- `Assets/Scripts/Audio/UnitAudio.cs` - `PlayHealTick()` 추가
- `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs` - `healTickSFX` 필드 추가
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` - `healRange`, `healPerSecond` 필드 추가
- 신규 치유 유닛 프리팹 + `NTA Unit Data SO.asset`(또는 OC) 항목 추가

## 구현 완료

설계대로 스크립트/데이터 계층은 전부 구현했다. 실제 "플레이 가능한 치유 유닛"이 되려면 3D
모델/아이콘/애니메이션/사운드 클립 같은 아트 에셋과 `NTA Unit Data SO.asset` 항목 추가, 프리팹
구성(콜라이더 자식에 `HealRange` 부착 등)이 필요한데, 이건 코드로 대신 만들어줄 수 없는 부분이라
이번엔 제외했다 - 아래는 그 전 단계, 재사용 가능한 시스템 자체.

- `ProjectSettings/TagManager.asset` - `"Healer"` 태그 추가. 다만 구현하다 보니 실제 게이팅은
  **컴포넌트 존재 여부**(프리팹에 `HealRange`가 자식으로 붙어있는지)만으로 충분해서, 코드에서
  `CompareTag("Healer")`를 읽는 곳은 없다(설계 문서의 `isHealer` 플래그는 뺐음, 안 쓰이는 필드를
  남겨두지 않으려고 - doc/0661 후속). 태그 자체는 `"AttackUnit"` 태그처럼 라벨/향후 UI 분기용으로
  남겨둠, 프리팹에 붙이는 건 선택사항.
- `Assets/Scripts/Unit/HealRange.cs`(신규) - `AttackRange.cs`의 거울상. 트리거로 "Enemy" 태그가
  아니면서 `HealthManager`가 있고(건물 제외) 덜 채워진 유닛을 감지, 매 프레임 가장 가까운 대상을
  골라 사거리 안이면 `UnitController.BeginHeal()`, 밖이면 `ChaseTarget()`으로 접근. `AttackRange`의
  "이미 물던 대상 유지(히스테리시스)" 로직은 가져오지 않음(치유는 대상이 자주 바뀌어도 전투처럼
  손해가 안 남 - 의도적 단순화, 필요해지면 나중에 추가).
- `Assets/Scripts/Unit/HealBeam.cs`(신규) - `LaserBeamAttack.cs`를 0.2초 버스트 대신 `StartBeam`/`StopBeam`으로
  직접 켜고 끄는 지속형으로 변형. `[SerializeField] Color beamColor`로 인스펙터에서 직접 색 조정 가능
  (기본값은 초록 계열이지만 코드에 고정된 게 아니라 프리팹에서 바로 바꿀 수 있음).
- `Assets/Scripts/Unit/UnitController.cs` - `BeginHeal()`/`HealTick()`/`StopHeal()` 추가.
  `RepairTick()`과 동일한 구조(실수 누적 → 정수 되면 `HealthManager.Heal()`)에서 자원 정산 블록만
  뺐다(무료로 확정) - `healTickInterval`(0.5초)은 HP 자체가 아니라 회복 틱 사운드 재생 주기로만 쓰임.
  `Update()` 루프에 `HealTick();` 한 줄 추가(`RepairTick()` 바로 다음).
- `Assets/Scripts/Audio/UnitAudio.cs` - `PlayHealTick()` 추가(`BuildingAudio.PlayRepairTick()`과 동일 패턴).
- `Assets/Scripts/ScriptableObject/UnitSoundBankSO.cs` - `healTickSFX` 필드 추가.
- `Assets/Scripts/ScriptableObject/UnitDataSO.cs` - `healRange`(int), `healPerSecond`(float) 필드 추가.
  치유 유닛이 아닌 기존 유닛들은 전부 0으로 남아 무효과 - 기존 유닛 데이터 에셋은 안 건드려도 됨.

컴파일 확인: 에러 0.

## 이름/컨셉 확정 및 데이터 등록

- **이름: Medic Drone** - 기존 "역할+Drone" 네이밍(Worker Drone/Scout Drone/Guardian Drone)을 그대로 이음.
  Worker Drone(건설/채집용 나노봇)을 의료용으로 개조한 버전이라는 설정.
- **역할**: 무공격 - `attackDamge/attackRange/attackSpeed = 0`, `canAttackGround/canAttackAir = false`.
  사거리 안 아군에게 나노 재생 빔으로 지속 회복만 함.
- **로스터 배치**: Tier 1(Barracks), Assault Trooper/Scout Drone/Sharpshooter와 동급 - 초반부터 뽑아
  본대에 붙일 수 있어야 서포터로서 의미가 있음(스타크래프트 메딕과 동일 포지션).
- `NTA Unit Data SO.asset`에 항목 추가 완료 (ID 10, tier 1):

  | 필드 | 값 |
  |---|---|
  | HP | 35 (Assault Trooper 40보다 낮게 - 무공격이라 그 자체로 잘 죽음) |
  | 비용 | 광물 50 / 가스 0, 인구 1 (Assault Trooper와 동일한 기본 보병 코스트) |
  | 생산시간 | 15초 |
  | healRange | 12 (Assault Trooper 사거리 12와 동일 - 안전거리에서 지원) |
  | healPerSecond | 3 (사용자 확정값) |
  | 단축키 | M(109) |
  | Icon/ProductionIcon/Prefab/soundBank | 전부 비워둠(fileID: 0) - 아트 에셋 없음, OC 로스터 추가 때(doc/0230)와 동일한 관례 |

  `uloop execute-dynamic-code`로 `UnitDataSO`를 실제 로드해서 10번 항목이 위 값 그대로 파싱되는지 확인함(hp=35, healRange=12, healPerSecond=3, shortcutKey=M 등 전부 일치). 컴파일 에러 0.

## 프리팹 검수 및 연결 (사용자가 프리팹/아이콘 제작 후)

사용자가 `Assets/prefabs/NTA/Unit/Tier1/Medic Drone.prefab`와 아이콘 2장(`Assets/images/Unit/NTA/메딕드론.png`,
`메딕드론_생산.png`)을 준비 - 검수해보니 Assault Trooper 프리팹을 복제해서 만든 것으로 보이는데, 그 과정에서
실제로 빠지거나 잘못 남은 부분이 있어서 아래 문제를 직접 고쳤다.

**발견된 문제 (전부 수정 완료)**
1. **`HealRange` 컴포넌트가 아예 없었음** - 이게 없으면 이 유닛은 치유든 공격이든 아무것도 자동으로
   못 함(자동 감지 자체가 안 됨). 복제 시 남아있던 `AttackRange` 자식 오브젝트(트리거 콜라이더 포함,
   반경 17 = UnitRange 12 + margin 5)를 그대로 재사용해서 스크립트만 `AttackRange` → `HealRange`로
   교체(콜라이더/반경 그대로 재사용 가능해서 새로 안 만듦).
2. **`unitID`가 2(Assault Trooper)로 남아있었음** → **10(Medic Drone)으로 수정**. 이게 제일 치명적인
   문제였음 - `UnitSpawner.Spawn()` → `ApplyUnitData()`가 이 ID로 `UnitDataSO`를 조회해서 스탯을
   덮어쓰는데, 2로 남아있으면 실제로 Assault Trooper 스탯(공격력 5, 사거리 12 공격 가능 등)이 그대로
   적용돼서 "공격능력 없음" 설계가 통째로 깨졌을 것.
3. `UnitController.icon`(Squad_panel 선택 UI용, `UnitData.Icon`과는 별개 필드)도 Assault Trooper
   아이콘이 그대로 남아있어서 새 메딕 아이콘으로 교체.
4. 인스펙터 기본값(`attackDamage: 5→0`, `canAttackGround/canAttackAir: 1→0`, `timeBetweenAttacks:
   0.6→0`, `HealthManager.maxHealth: 40→60`)도 Assault Trooper 값이 남아있어서 정리 - 어차피
   `ApplyUnitData()`가 스폰 시 `UnitData`(ID 10) 값으로 덮어써서 실제 게임플레이에는 영향 없었지만,
   프리팹을 열어봤을 때 혼동을 없애기 위해 정리.
5. `Prefab` 필드는 사용자가 이미 `NTA Unit Data SO.asset`에 직접 연결해둔 상태였음(확인만 함, 수정 불필요).

**아이콘/사운드뱅크 연결**
- `NTA Unit Data SO.asset`(ID 10)의 `Icon`/`ProductionIcon`을 각각 `메딕드론.png`/`메딕드론_생산.png`
  스프라이트로 연결.
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/Medic Drone Unit Sound Bank SO.asset` 신규 생성
  (기존 유닛들과 동일한 `UnitSoundBankSO` 구조, 클립은 전부 비워둠 - 다른 신규 필드 추가 때와 동일한
  관례) - `NTA Unit Data SO.asset`의 `soundBank` 필드에 연결.
- `Assets/Sound/NTA/Unit/Medic Drone/SFX`, `.../Voice` 빈 폴더 생성(다른 유닛들과 동일한 하위구조) -
  실제 클립은 준비되면 여기에 넣고 위 사운드뱅크 에셋에서 연결하면 됨.

**검증**: `uloop execute-dynamic-code`로 프리팹을 직접 로드해서 `unitID=10`, `tag=Healer`,
`HealRange present=True(UnitRange=12)`, `AttackRange present=False`, `HealBeam present=True`,
`HealthManager maxHealth=60`, `UnitData.Icon/ProductionIcon/Prefab/soundBank` 전부 연결됨을 확인.
컴파일 에러 0.

## 아직 남은 것 (에셋 필요 - 사용자 작업)
1. Medic Drone 3D 모델/프리팹 준비, 자식 오브젝트에 `HealRange`(트리거 `CapsuleCollider` 포함) +
   루트에 `HealBeam`(레이저 프리팹/firePoint 연결) 부착.
2. `NTA Unit Data SO.asset`(ID 10) 항목의 `Icon`/`ProductionIcon`/`Prefab`/`soundBank` 필드에
   실제 에셋 연결(현재 fileID 0 - 프리팹/아이콘/사운드뱅크가 준비되면 채우기).
3. (선택) 프리팹에 `"Healer"` 태그 부여 - 코드 동작엔 영향 없지만 라벨링 관례 유지.
4. (선택) `UnitSoundBankSO`에 `healTickSFX` 클립 연결.
5. `RTSUnitController`의 Barracks 생산 목록에 Medic Drone(ID 10)이 실제로 뽑히도록 연결 필요할 수
   있음(생산 버튼/커맨드 패널 쪽 - 다른 Tier1 유닛과 같은 방식이면 자동으로 딸려올 가능성 높음,
   프리팹 붙이고 나서 확인 필요).
