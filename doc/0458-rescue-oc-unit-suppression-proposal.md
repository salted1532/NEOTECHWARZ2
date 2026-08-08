# 0458. 3스테이지 "생존한 OC 병사 구조" - 조종 억제 스크립트 제안

**날짜:** 2026-08-08

## 요청 내용
> 미션3에서 OC를 구조하는걸 구현하려고하는데 이걸 그냥 플레이어의 유닛NTA로 스테이지에 배치하고
> 조종을 억제하는 스크립트를 둬서 비콘에 구조 비콘에 유닛을 가져가면 해당 스크립트가 비활성화
> 되면서 그 유닛을 조종할수 있도록 하려고 그렇게하면 그 사실은 플레이어유닛인데 머티리얼만
> OC스킨을 씌워서 OC유닛인줄 알았는데 비콘을 통해서 그 유닛들도 조종할수 있도록 하게

후속 확인: "이동 방식"을 물었더니 - 유닛은 처음부터 구조 비콘 근처에 배치돼 있고, 다른 곳에서
옮겨올 필요 없음(캐리/에스코트 불필요). 플레이어 유닛이 비콘 근처에 오면(기존 Stage3Objectives의
"생존자 구조" 서브목표 판정) 그 즉시 억제가 풀리는 구조.

## 조사 내용

### 이미 있는 것 - `Stage3Objectives.cs`의 "생존자 구조" 서브목표

지금도 "구조 비콘 rescueRadius 안에 아무 아군 유닛이나 들어오면 완료 처리"하는 로직이 이미 있음
(`IsAnyUnitWithinRadius`). 지금은 완료 판정만 하고 끝 - 실제로 "구조할 대상 유닛"이 따로 없음. 이번
요청은 여기에 "그 판정이 완료되는 순간, 근처에 미리 배치해둔 위장 OC(사실은 NTA) 유닛의 억제를
푼다"는 동작을 붙이는 것.

### `UnitController.Start()`가 언제 실행되는지 - "억제"의 핵심 메커니즘

`UnitController.Start()`(`rtsController.UnitList.Add(this)`로 자기 자신을 등록하고, 스탯 적용,
마커 초기화 등을 하는 곳)는 **컴포넌트가 `enabled`일 때만** 실행됨 - `Awake()`는 비활성 상태에서도
돌지만 `Start()`는 처음 활성화되는 시점까지 미뤄짐. 즉 **`UnitController.enabled = false`로 둔 채
배치하면 `Start()`가 통째로 안 도니까**:

- `UnitList`에 등록이 안 돼서 드래그(박스) 선택 후보에서 자동으로 빠짐(`UserControl.SelectObject()`가
  `rtsUnitController.UnitList`를 순회함).
- `Update()`(이동/전투 로직)도 안 돎.
- 스탯 적용(`ApplyUnitData`)도 안 됨.

다만 **좌클릭 단일 선택은 이걸로 안 막힘** - `UserControl.HandleLeftClick()`의 "1. 유닛 클릭" 분기는
`unitHit.transform.GetComponent<UnitController>()`로 직접 찾기 때문에, `enabled=false`인
컴포넌트도 그냥 찾아내서 선택/명령을 시도해버림(초기화가 안 된 상태라 오작동 위험). 그래서 **Layer도
같이 바꿔서** `layerUnit` 레이캐스트 자체에 안 걸리게 해야 완전히 막힘 - 아군 OC(doc/0447)/미션
오브젝트(doc/0455)에서 이미 썼던 "전용 Layer로 일반 흐름에서 제외" 패턴을 그대로 재사용.

## 제안하는 변경

### 1) 새 Layer `Rescuable` 추가

`AllyOC`(13)/`MissionObject`(14)와 동일한 목적 - 억제된 동안 플레이어의 일반 유닛 클릭/드래그 흐름에서
완전히 빠지게 하는 전용 레이어(15번 슬롯).

### 2) 신규 컴포넌트 `Assets/Scripts/Unit/RescueSuppressor.cs`

```csharp
using UnityEngine;

// 겉보기엔 OC(머티리얼만 바꿔치기)지만 실제로는 플레이어 유닛(UnitController)인 "구조 대상" 유닛에
// 붙인다. 구조되기 전까지 UnitController를 꺼두고(Start()가 안 돌아 UnitList 미등록/이동·전투 로직
// 정지) Layer도 전용 레이어로 바꿔서 일반 유닛 클릭/드래그 선택에서 완전히 제외한다. Stage3Objectives가
// "생존자 구조" 서브목표를 완료 처리하는 순간 Rescue()를 호출해서 원래 상태로 되돌린다 (doc/0458).
[RequireComponent(typeof(UnitController))]
public class RescueSuppressor : MonoBehaviour
{
    [SerializeField] private int normalUnitLayer; // 구조 후 되돌릴 원래 Layer(보통 "Unit") - 직접 지정

    private UnitController unitController;
    private int suppressedLayer;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        suppressedLayer = gameObject.layer; // 배치 시점에 지정해둔 전용 Layer(Rescuable)를 그대로 기억

        unitController.enabled = false;
    }

    public void Rescue()
    {
        if (unitController.enabled)
            return; // 이미 구조됨 - 중복 호출 방지

        unitController.enabled = true;
        gameObject.layer = normalUnitLayer;
        Destroy(this); // 역할 끝 - 더 이상 필요 없음
    }
}
```

### 3) `Stage3Objectives.cs` - 구조 판정 완료 시 `Rescue()` 호출

```csharp
[Header("서브목표")]
[SerializeField] private Transform rescueBeacon;
[SerializeField] private float rescueRadius = 2f;
[SerializeField] private RescueSuppressor rescuedUnit; // 위장 OC(실제로는 NTA) 유닛 - 직접 연결 (doc/0458)
[SerializeField] private TextMeshProUGUI rescueSurvivorsText;

...

if (!survivorsRescued && rescueBeacon != null && rtsController != null)
{
    survivorsRescued = IsAnyUnitWithinRadius(rescueBeacon.position, rescueRadius);
    if (survivorsRescued)
        rescuedUnit?.Rescue();
}
```

`IsAnyUnitWithinRadius`는 `rtsController.UnitList`를 도는데, 구조 대상 유닛은 `RescueSuppressor`가
꺼둔 상태라 `UnitList`에 아예 등록이 안 돼 있음 - 그래서 "구조 대상 유닛 자기 자신이 비콘 근처에
있다"는 이유로 즉시 완료되는 오작동 걱정 없이, 실제로 플레이어의 다른 유닛이 접근했을 때만 완료됨
(코드 추가 변경 불필요, 억제 메커니즘 자체가 이미 이 문제를 해결함).

## 확인 필요 - 씬에 실제로 배치할 때 필요한 정보 (1차안, 아래 최종안으로 대체됨)

1. **어떤 NTA 유닛 종류**로 배치할까요? (예: Marine/Assault Trooper, Worker 등 - 기존 NTA 유닛
   프리팹 중 하나를 그대로 씀)
2. **OC 스킨 머티리얼**은 어떤 걸 쓸까요? 기존 OC 유닛이 쓰는 머티리얼 에셋 경로를 알려주시면 그대로
   가져다 씀 - 아니면 특정 색상/느낌으로 새로 하나 만들어드릴까요?
3. 구조 후에도 계속 OC 스킨으로 남을까요, 아니면 구조되는 순간 원래 NTA 외형으로 되돌릴까요?
4. Mission3 씬의 어느 위치(구조 비콘 근처 좌표)에 배치할까요? 이미 비콘이 배치돼 있다면 그 위치를
   그대로 참고해서 근처에 놓겠습니다.

사용자가 "NTA 껍데기 + OC 머티리얼 위장" 대신 "**실제 OC 유닛 프리팹**을 배치하고, 거기에
`UnitController`를 붙여서 조종 가능하게 만들되, 스탯은 OC 데이터를 그대로 가져오게" 하는 쪽으로
방향을 바꿈 - 아래 최종안 참고.

---

## 최종안 - 진짜 OC 프리팹 + 진짜 `UnitController`(플레이어 조종 클래스)

### 왜 이게 가능한지

- **조종 가능 여부는 순전히 클래스에서 나옴**: 선택/이동명령/공격명령 UI·로직은 전부
  `UnitController`에만 있음(`EnemyUnitController`/`AllyController`엔 없음, doc/0452에서 확인).
  그러니까 OC 프리팹의 컨트롤러를 `EnemyUnitController` → `UnitController`로 바꿔치기만 하면 그
  즉시 "조종 가능한 유닛"이 됨 - 외형(모델/메시)은 원본 OC 프리팹 그대로 유지됨(머티리얼 위장이
  아니라 진짜 OC 모델).
- **스탯도 OC 데이터에서 그대로 가져올 수 있음**: `UnitController.ApplyUnitData(UnitData data)`는
  데이터가 NTA 테이블에서 왔는지 OC 테이블에서 왔는지 신경 안 씀(공용 구조체). 지금
  `UnitController.Start()`는 `rtsController.GetUnitData(unitID)`(NTA 테이블)만 조회하는데,
  이 유닛만 `GetEnemyUnitData(...)`(OC 테이블)를 조회하도록 필드 하나만 추가하면 됨. 이미
  `UnitController`엔 "영웅 유닛" 전용으로 `unitID`를 0으로 둬서 자동 스탯 조회를 건너뛰는 관례가
  있어서(주석 참고), 그와 같은 패턴(0=기본 동작, 값 있으면 대체 동작)으로 자연스럽게 얹을 수 있음.

### 1) `UnitController.cs` - OC 데이터 소스로 스탯을 가져오는 옵션 추가

```csharp
// 0(기본값)이면 지금처럼 unitID로 NTA Unit Data SO를 조회한다. 0이 아니면 이 값으로 OC Unit Data SO를
// 대신 조회해서 스탯(공격력/방어력/체력 등)을 그 값으로 적용한다 - "겉모습은 OC 프리팹 그대로, 스탯도
// OC 그대로, 조종만 플레이어가 가능한" 구조(RescueSuppressor)에 사용 (doc/0458).
[SerializeField]
private int enemyDataUnitID;
```

`Start()`:
```csharp
// Before
ApplyUnitData(rtsController.GetUnitData(unitID));

// After
UnitData data = enemyDataUnitID > 0
    ? rtsController.GetEnemyUnitData(enemyDataUnitID)
    : rtsController.GetUnitData(unitID);
ApplyUnitData(data);
```

### 2) `RescueSuppressor.cs` - 1차안과 동일한 메커니즘, Tag까지 함께 처리

1차안에서 다룬 "Layer로 일반 유닛 클릭/드래그에서 제외 + `UnitController.enabled=false`로 `Start()`
자체를 막기"는 그대로 유효함. 여기에 **Tag도 같이 갈아끼워야 함** - OC 프리팹은 기본 Tag가
`"Enemy"`인데, 그대로 두면:
- 구조 전: 플레이어의 다른 유닛이 이 위장 OC를 자동공격해버림(`AttackRange`가 Tag `"Enemy"`를 자동
  감지 - `doc/0231`).
- 구조 후에도 Tag가 `"Enemy"`로 남으면 똑같이 계속 아군 오사(誤射)당함.

그래서 배치 시점엔 Tag를 `Untagged`(자동공격 대상에서 빠짐, doc/0447과 동일한 이유)로 두고, 구조
완료 시 `RescueSuppressor.Rescue()`가 `"AttackUnit"`(일반 NTA 전투유닛과 동일 Tag)으로 바꿔준다.

```csharp
using UnityEngine;

[RequireComponent(typeof(UnitController))]
public class RescueSuppressor : MonoBehaviour
{
    [SerializeField] private int normalUnitLayer; // 구조 후 되돌릴 Layer("Unit") - 직접 지정
    private const string RescuedTag = "AttackUnit"; // 구조 후 적용할 Tag - 일반 NTA 전투유닛과 동일

    private UnitController unitController;

    private void Awake()
    {
        unitController = GetComponent<UnitController>();
        unitController.enabled = false;
    }

    public void Rescue()
    {
        if (unitController.enabled)
            return; // 이미 구조됨 - 중복 호출 방지

        unitController.enabled = true;
        gameObject.layer = normalUnitLayer;
        gameObject.tag = RescuedTag;
        Destroy(this);
    }
}
```

### 3) `Stage3Objectives.cs` - 1차안과 동일

구조 판정 완료 시 `rescuedUnit?.Rescue()` 호출 (1차안 그대로).

### 4) 프리팹 - "구조 가능 OC" Variant 신설

기존 OC 유닛 프리팹(예: `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab`) 하나를 골라 Prefab
Variant를 만듦(`Assets/prefabs/OC/Rescuable/` 아래, 아군 OC Variant를 만들 때와 동일한 방식,
doc/0448):

- 루트 컨트롤러 `EnemyUnitController` → `UnitController`로 교체(값 자동 복사는 필드 이름이 서로
  달라 안 되므로 직접 매핑: `enemyMarker` → `unitMarker`, `enemyUnitID` → `enemyDataUnitID`(신설
  필드) 등 필요한 값만 옮겨 적음). `unitID`는 0으로 둬서 NTA 테이블 조회를 건너뜀.
- 자식 `AttackRange`(`EnemyAttackRange`) → `AttackRange`(플레이어용 클래스)로 교체, `UnitRange` 값
  이전.
- 루트 Layer → 새 `Rescuable` Layer, Tag → `Untagged`(구조 전 임시 상태).
- 루트에 `RescueSuppressor` 추가(`normalUnitLayer` = `Unit` 레이어 지정).

### 확인하고 싶은 점

1. 이대로 진행해도 될까요? (`UnitController.cs`에 `enemyDataUnitID` 필드 추가, `RescueSuppressor.cs`
   신규, `Stage3Objectives.cs` 수정, 새 `Rescuable` Layer, OC Variant 프리팹 1개 신설)
2. **어떤 OC 유닛**을 이 "구조 대상"으로 쓸까요? (로스터: Nanobot Repair, Cyborg Soldier, Railgunner,
   Striker, Brute Mech, Heavy Assault Tank, Ironhawk, Raven, Strike Drone)
3. Mission3 씬의 구조 비콘 위치를 기준으로 근처에 배치하면 될까요, 아니면 원하는 정확한 좌표가
   있으신가요?

사용자가 "OC 유닛 선택 + Variant 프리팹 신설 + 씬 배치"는 직접 하겠다고 함("그건 내가 직접 배치하고
추가할게") - 재사용 가능한 인프라(스크립트 3개 + Layer)만 먼저 만들어둠.

## 구현 결과

인프라만 적용함(OC Variant 프리팹/씬 배치는 사용자가 직접 진행).

- `Assets/Scripts/Unit/UnitController.cs` - `enemyDataUnitID` 필드 추가(0=기본, NTA 테이블 조회),
  `Start()`가 0보다 크면 `GetEnemyUnitData(enemyDataUnitID)`로 OC 테이블에서 스탯을 대신 조회하도록 수정.
- `Assets/Scripts/Unit/RescueSuppressor.cs` (신규) - 제안 그대로.
- `Assets/Scripts/System/Stage3Objectives.cs` - `rescuedUnit`(`RescueSuppressor`) 필드 추가, "생존자
  구조" 서브목표 완료 판정 순간 `rescuedUnit?.Rescue()` 호출. 상단 주석도 새 동작에 맞게 갱신.
- `ProjectSettings/TagManager.asset` - Layer 15번에 `Rescuable` 추가.

### 씬에 실제로 배치할 때 필요한 설정 (사용자가 직접 진행 시 참고)

1. OC 유닛 프리팹을 골라 Prefab Variant 생성(`doc/0448`에서 아군 OC Variant 만들 때와 동일한 방식) -
   루트 컨트롤러 `EnemyUnitController` → `UnitController`, 자식 `AttackRange`(`EnemyAttackRange`) →
   `AttackRange`(플레이어용 클래스)로 교체.
2. 새 `UnitController`의 `unitID`는 `0`(NTA 테이블 조회 생략), `enemyDataUnitID`는 원본 OC 유닛의
   ID로 설정(`GetEnemyUnitData`가 그 값으로 OC Unit Data SO를 조회해서 스탯 적용).
3. `unitMarker` 필드는 필수값(null이면 `Start()`에서 NullReferenceException) - 기존
   `EnemyUnitController`가 쓰던 `enemyMarker`와 같은 자식 오브젝트(보통 "Marker")를 그대로 연결.
4. 루트에 `RescueSuppressor` 추가, `normalUnitLayer`는 `Unit` 레이어(6번) 지정.
5. 루트 Layer → 새 `Rescuable`(15번), Tag → `Untagged`(구조 전 임시 상태 - 플레이어/적 양쪽의 자동공격
   대상에서 빠짐). `RescueSuppressor.Rescue()`가 구조 시점에 Layer→`Unit`, Tag→`AttackUnit`으로 자동
   전환해줌.
6. `Mission3.unity`의 `Stage3Objectives` 컴포넌트에서 `rescuedUnit` 필드를 이 Variant 인스턴스로 연결.

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔 Error 0건.

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/Unit/RescueSuppressor.cs` (신규)
- `Assets/Scripts/System/Stage3Objectives.cs`
- `ProjectSettings/TagManager.asset`
