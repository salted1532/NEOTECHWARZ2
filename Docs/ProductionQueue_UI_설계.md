# SelectInfo 패널 - 생산 대기열(Production Queue) & 상태정보 UI 설계

> 목적: `Canvas/SelectInfo` 아래에 만들어둔 `productionQueue_Panel`(슬롯 5개)과 아이콘/체력 표시 UI를
> `UIController`가 `OrderButtons`처럼 실제로 관리·갱신하도록 만들기 위한 작업 정리 문서.
> 코드는 아직 수정하지 않았고, 아래 내용은 설계/작업 목록이다.

---

## 1. 현재 씬 구조 (SampleScene.unity 기준 실측)

```
Canvas
├─ SelectInfo                 (Image, 배경 스프라이트만 있음)
│   ├─ InfoPanel              (Image alpha 0, 자식 없음 ← Icon_image/HealthText가 들어갈 자리)
│   └─ productionQueue_Panel  (Image alpha 0, 자식 없음 ← Slot0~4가 들어갈 자리)
└─ OrderButtons
    └─ Panel
        ├─ Slot0 ~ Slot8      (ProductionSlot 컴포넌트, 9개)
```

- `InfoPanel`, `productionQueue_Panel`은 **컨테이너만 만들어져 있고 자식(Icon_image, HealthText, Slot0~4)은
  아직 씬에 생성되어 있지 않다.** (RectTransform의 `m_Children: []` 확인됨)
- 실제로 존재하는 슬롯 9개(Slot0~Slot8)는 전부 `OrderButtons/Panel` 밑에 있고,
  `UIController.slots` 배열이 이 9개만 참조하고 있다. `ShowWorkerPanel`, `ShowBuildPanel` 등
  기존 메소드들은 전부 이 배열을 통해 커맨드 버튼(이동/공격/건설 등)을 그린다.
- 즉 "production패널의 slot0~4"는 이름만 준비된 상태이고, `productionQueue_Panel`을 채우는
  코드/게임오브젝트가 UIController와 완전히 분리되어 있어서 아무것도 출력되지 않는 것이 맞다.

### 관련 기존 스크립트

| 스크립트 | 역할 | 비고 |
|---|---|---|
| `UIController.cs` | `OrderButtons/Panel`의 슬롯 9개(`slots`)만 관리. `ShowXXXPanel()`류 메소드로 커맨드 버튼 세팅 | SelectInfo 관련 필드/메소드 없음 |
| `ProductionSlot.cs` | 버튼 1개 + 아이콘 1개. 클릭 시 `Action` 콜백 실행 | 진행률(%)이나 잔여시간 표시 기능 없음 |
| `UnitSpawner.cs` | 건물 하나당 붙어서 `List<ProductionData>`(유닛ID, 잔여시간)로 실제 생산 큐를 들고 있음. `Enqueue/Cancel/Produce` 구현됨 | **외부에서 큐 상태를 읽어갈 수 있는 API가 없음** (UI가 구독 불가) |
| `BuildingController.cs` | `UnitSpawner.Enqueue`를 `SpawnUnit(unitID)`로 감싸서 노출 | 큐 조회/취소용 메소드 없음 |
| `RTSUnitController.cs` | `Update()`에서 선택 상태에 따라 `uIController.ShowXXXPanel(...)` 호출 | SelectInfo/큐 갱신 호출 없음 |

### 체력(HP) 관련 확인 사항 (막힘 포인트)

- `UnitData.hp`, `BuildingData`에는 "최대 체력" 개념조차 없음(`BuildingData`는 hp 필드 자체가 없음).
- `UnitController`, `BuildingController` 어디에도 `currentHP` 필드가 없음 — 즉 **현재 데미지 처리/체력 시스템이 아직 없다.**
- `HealthText`를 실제 값으로 채우려면 이 문서 범위를 넘어서는 "체력 시스템"이 최소한 아래 형태로 선행되어야 함:
  - `UnitController`/`BuildingController`에 `currentHP`, `maxHP` (또는 공통 `IDamageable` 인터페이스) 추가
  - 데미지를 주는 쪽(`UnitController.Attack` 등)이 실제로 그 값을 깎도록 연결
- 이 문서에서는 **HealthText를 채워주는 UIController API까지만** 설계하고, 체력 시스템 자체는 별도 작업으로 분리했다.

---

## 2. 추가해야 할 씬 계층 구조

```
SelectInfo
├─ InfoPanel
│   ├─ Icon_image      (Image)              ← 선택된 유닛/건물 아이콘
│   └─ HealthText      (TextMeshProUGUI)    ← "현재HP / 최대HP" 표기
└─ productionQueue_Panel
    ├─ QueueSlot0
    ├─ QueueSlot1
    ├─ QueueSlot2
    ├─ QueueSlot3
    └─ QueueSlot4       (각 슬롯: Image(아이콘) + 진행률 표시 + 취소용 Button)
```

- Order 버튼용 `ProductionSlot`은 "아이콘 + 클릭 콜백"만 있으면 되지만, 생산 대기열 슬롯은
  **잔여시간/진행률 표시 + 취소(클릭 시 해당 인덱스 취소)** 기능이 필요해서 요구사항이 다르다.
  → 기존 `ProductionSlot`을 억지로 재사용하기보다 **`ProductionQueueSlot`을 새 스크립트로 분리**하는 것을 권장.

### 신규 스크립트: `ProductionQueueSlot.cs` (Assets/Scripts/UI)

```csharp
public class ProductionQueueSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image progressFill;   // Image.Type = Filled, 선택
    [SerializeField] private Button cancelButton;

    private Action onCancel;

    public void SetData(Sprite icon, float progress01, Action onCancel);
    public void Clear();
    private void OnClickCancel() => onCancel?.Invoke();
}
```

---

## 3. UIController.cs에 추가할 것

### 3-1. 필드

```csharp
[Header("Select Info")]
[SerializeField] private GameObject selectInfoRoot;      // SelectInfo
[SerializeField] private Image selectedIconImage;        // Icon_image
[SerializeField] private TMP_Text healthText;             // HealthText

[Header("Production Queue")]
[SerializeField] private ProductionQueueSlot[] queueSlots; // productionQueue_Panel/QueueSlot0~4
```

### 3-2. 데이터 구조체 (`CommandButtonData`와 동급으로 추가)

```csharp
public struct ProductionQueueData
{
    public Sprite Icon { get; }
    public float Progress01 { get; }   // 0~1, 진행률
    public Action OnCancel { get; }

    public ProductionQueueData(Sprite icon, float progress01, Action onCancel)
    {
        Icon = icon;
        Progress01 = progress01;
        OnCancel = onCancel;
    }
}
```

### 3-3. 메소드

| 메소드 | 역할 | 호출 시점 |
|---|---|---|
| `ShowSelectInfo(Sprite icon, int currentHp, int maxHp)` | `selectInfoRoot` 활성화, `selectedIconImage.sprite` 세팅, `healthText.text = $"{currentHp}/{maxHp}"` | 유닛/건물 선택 직후 (기존 `ShowWorkerPanel` 등과 같은 타이밍) |
| `UpdateHealth(int currentHp, int maxHp)` | 텍스트만 갱신 (전투 중 실시간 반영용) | 매 프레임 or 데미지 이벤트 발생 시 |
| `UpdateProductionQueue(ProductionQueueData[] queue)` | `queueSlots` 5개에 `SetData`/`Clear` 적용 (기존 `SetCommands`와 동일 패턴) | 건물 선택 중 매 프레임, 혹은 큐 변경 이벤트 발생 시 |
| `ClearSelectInfo()` | `selectInfoRoot.SetActive(false)`, `queueSlots` 전체 `Clear()` | `ClearPanel()`에서 같이 호출 + 선택 해제 시 |

- 기존 `ClearPanel()`은 `panelRoot`(OrderButtons)만 끄고 있으므로, **`ClearSelectInfo()`를 `ClearPanel()` 안에서도 호출**하도록
  같이 수정해야 SelectInfo와 OrderButtons가 선택 해제 시 항상 같이 사라진다.
- `SetCommands`가 하던 것처럼, `UpdateProductionQueue`도 `queue.Length`가 5보다 적으면 나머지 슬롯은 `Clear()` 처리.

---

## 4. 큐 데이터를 UI까지 끌고 오는 흐름 (UnitSpawner → BuildingController → RTSUnitController → UIController)

현재 `UnitSpawner`는 큐를 들고만 있고 외부에 안 알려준다. 최소 변경으로 아래를 추가.

### 4-1. `UnitSpawner.cs`

```csharp
public IReadOnlyList<ProductionData> GetQueueSnapshot() => productionQueue;
```

- `ProductionData`에 `ProductionTime`(총 생산시간, 진행률 계산용) 필드가 없다 — 현재는 `RemainTime`만 있고
  카운트다운되며 원래 총 시간을 잃어버린다. 진행률(`progress01`)을 보여주려면
  `ProductionData`에 `TotalTime`을 같이 저장해두는 편이 좋다 (생성 시점의 `data.productionTime` 그대로 보존).

### 4-2. `BuildingController.cs`

```csharp
public IReadOnlyList<ProductionData> GetProductionQueue() => UnitSpawner.GetQueueSnapshot();
public void CancelProduction(int index) => UnitSpawner.Cancel(index);
```

### 4-3. `RTSUnitController.cs`

- `Update()`의 `SelectState.BuildingSelect` 분기(건물 선택 중)에서, 기존 `ShowXXXPanel(...)` 호출과 나란히
  선택된 건물 1개(`selectedBuildingList[0]`)의 큐를 읽어 `uIController.UpdateProductionQueue(...)`를 호출.
- 유닛ID → 아이콘 스프라이트 변환이 필요한데, 이 매핑은 지금 `UIController`가 `marineIcon`, `tankIcon` 등
  필드로만 들고 있고 "ID로 아이콘 찾기" 헬퍼가 없다. `UIController`에 아래 같은 private 매핑 함수를 추가해서
  재사용하는 것을 권장 (`ShowBarracksPanel` 등에서 하드코딩된 아이콘 대응 관계와 동일하게 `RTSUnitController.UnitID`
  상수 기준으로 매핑).

```csharp
private Sprite GetUnitIcon(int unitId) => unitId switch
{
    RTSUnitController.UnitID.Worker  => workerIcon,
    RTSUnitController.UnitID.Marine  => marineIcon,
    RTSUnitController.UnitID.Vulture => vultureIcon,
    RTSUnitController.UnitID.Goliath => goliathIcon,
    RTSUnitController.UnitID.Tank    => tankIcon,
    RTSUnitController.UnitID.Wraith  => wraithIcon,
    RTSUnitController.UnitID.Guardian=> guardianIcon,
    _ => null
};
```

- 유닛(건물이 아니라 유닛을 선택했을 때)의 경우 생산 큐 개념이 없으므로 `SelectState.UnitSelect` 분기에서는
  `UpdateProductionQueue`를 호출하지 않거나 빈 배열로 `Clear`만 하면 됨. `ShowSelectInfo`는 유닛/건물 공통으로 호출.

---

## 5. 작업 체크리스트

1. [씬] `InfoPanel` 밑에 `Icon_image`(Image), `HealthText`(TMP_Text) 오브젝트 생성
2. [씬] `productionQueue_Panel` 밑에 `QueueSlot0~4` 생성, 각각 `ProductionQueueSlot` 컴포넌트 부착
3. [스크립트] `ProductionQueueSlot.cs` 신규 작성 (Assets/Scripts/UI)
4. [스크립트] `UIController.cs`
   - `selectInfoRoot`, `selectedIconImage`, `healthText`, `queueSlots` 필드 추가 + 인스펙터 연결
   - `ProductionQueueData` 구조체 추가
   - `ShowSelectInfo`, `UpdateHealth`, `UpdateProductionQueue`, `ClearSelectInfo` 메소드 추가
   - `ClearPanel()`에서 `ClearSelectInfo()`도 같이 호출하도록 수정
5. [스크립트] `UnitSpawner.cs`: `GetQueueSnapshot()` 추가, `ProductionData`에 `TotalTime` 필드 추가해서 진행률 계산 가능하게 보강
6. [스크립트] `BuildingController.cs`: `GetProductionQueue()`, `CancelProduction(int)` 추가
7. [스크립트] `RTSUnitController.cs`: `Update()`의 건물/유닛 선택 분기에서 `ShowSelectInfo` + (건물인 경우) `UpdateProductionQueue` 호출 추가, 유닛ID→아이콘 매핑 헬퍼 추가
8. [별도 작업, 선행 필요] 체력 시스템: `UnitController`/`BuildingController`에 `currentHP`/`maxHP` 추가하지 않으면 `HealthText`는 하드코딩 값 이상을 보여줄 수 없음
