# 건물 생산 대기열(Production Queue) UI 설계

작성일: 2026-07-01
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 목표

건물이 유닛을 생산할 때 `UnitSpawner`가 내부적으로 들고 있는 생산 대기열(`productionQueue`)을
Canvas UI에 **5개의 슬롯 버튼**으로 시각화한다.

- 슬롯에는 대기 중인 유닛의 아이콘이 표시된다.
- 슬롯 버튼을 누르면 해당 슬롯(index)에 대응하는 대기열 항목이 취소된다.
- 대기열이 비어있는 슬롯은 비활성화(숨김) 상태로 표시된다.

기존에 `UIController`가 관리하는 "커맨드 패널"(이동/공격/생산 버튼 등, 예: `ShowMainBasePanel`,
`ShowBarracksPanel`)과는 **역할이 다른 별도 패널**이다.

| 구분 | 커맨드 패널 (기존) | 생산 대기열 패널 (신규) |
|---|---|---|
| 표시 내용 | "무엇을 생산할지" 선택 버튼 (예: 마린 생산 버튼) | "현재 생산 중/대기 중"인 유닛 목록 |
| 클릭 시 동작 | `BuildingController.SpawnUnit(unitID)` 호출 → 큐에 추가 | 큐에서 해당 인덱스 항목 취소 |
| 데이터 소스 | `BuildingDataSO` / `UnitDataSO` (정적 데이터) | `UnitSpawner.productionQueue` (동적, 매 프레임 변함) |
| 갱신 주기 | 상태(State) 전환 시 1회 | 선택된 건물이 있는 동안 매 프레임 |

두 패널은 동시에 화면에 존재할 수 있다 (예: 배럭 선택 시 상단엔 대기열, 하단엔 마린/벌쳐 생산 버튼).

## 2. 현재 구조 파악 요약

- `UnitSpawner.cs`
  - `List<ProductionData> productionQueue` (private) — 이미 최대 5개로 제한(`if (productionQueue.Count >= 5) return;`).
  - `Enqueue(int unitID)` — 큐에 추가.
  - `Cancel(int index)` — 이미 인덱스 기반 취소 메서드가 구현되어 있음 (재사용 가능).
  - 큐 상태를 외부에서 읽을 수 있는 **public 접근자가 없음** → 추가 필요.
  - 큐 변경 시 알림(이벤트)이 없음 → 매 프레임 폴링 방식으로 처리 예정.

- `BuildingController.cs`
  - `UnitSpawner` 참조를 들고 있고 `SpawnUnit(unitID)`로 `Enqueue`를 위임함.
  - 큐 조회/취소를 위한 위임 메서드가 없음 → 추가 필요.

- `RTSUnitController.cs`
  - `Update()`에서 `BuildingSelectState`에 따라 `uIController.ShowXXXPanel(...)`을 매 프레임 호출 중.
  - 건물 선택 시 `selectedBuildingList`에 선택된 건물이 들어있음 (다중 선택 가능).

- `UIController.cs`
  - `ProductionSlot[] slots` 1세트만 존재 (커맨드 패널 전용).
  - `CommandButtonData(Sprite icon, Action callback, bool interactable)` 구조체로 슬롯 데이터 표현.
  - 유닛 아이콘 스프라이트 필드(`workerIcon`, `marineIcon`, ...)가 이미 존재 → 대기열 아이콘 매핑에 재사용 가능.

- `ProductionSlot.cs`
  - `Button` + `Image` + `Action callback` 구조로 이미 범용적으로 설계되어 있어, 대기열 슬롯 용도로도 **그대로 재사용 가능** (수정 불필요).

## 3. 데이터 흐름

```
[UnitSpawner]                [BuildingController]           [RTSUnitController]              [UIController]
productionQueue (private) ─▶ GetProductionQueue() ─▶ Update()에서 선택된 건물 조회 ─▶ ShowProductionQueue(queue, onCancel)
Cancel(index)         ◀── CancelProductionAt(index) ◀── 슬롯 클릭 콜백 바인딩 ◀── ProductionSlot.OnClick()
```

1. `RTSUnitController.Update()`가 매 프레임 `selectedBuildingList`의 첫 번째 건물을 확인한다.
2. 그 건물이 생산 가능 타입(MainBase/Tier1/Tier2/Tier3)이면
   `BuildingController.GetProductionQueue()`로 현재 큐 스냅샷을 받아온다.
3. `uIController.ShowProductionQueue(queue, i => selectedBuilding.CancelProductionAt(i))`를 호출한다.
4. `UIController`는 큐 항목 수만큼 `queueSlots[i]`에 아이콘을 채우고, 초과분은 `Clear()`한다.
5. 사용자가 슬롯 버튼 클릭 → `ProductionSlot.OnClick()` → 바인딩된 콜백 실행
   → `BuildingController.CancelProductionAt(i)` → `UnitSpawner.Cancel(i)` → 리스트에서 제거.
6. 다음 프레임에 큐가 갱신된 상태로 다시 그려진다 (별도 이벤트 불필요, 폴링으로 충분).

건물이 여러 개 선택된 경우(다중 선택)에는 **첫 번째 선택된 건물의 큐만 표시**한다.
(스타크래프트 원작 기준으로도 생산 대기열은 대표 1개 건물 기준으로 표시되는 것이 자연스러움.)

## 4. Canvas / UI 구조 설계

```
Canvas
└─ CommandPanelRoot (기존, panelRoot)
│   └─ Slots[0..4]           (ProductionSlot, 기존 slots 배열)
│
└─ ProductionQueuePanelRoot (신규)
    └─ QueueSlots[0..4]      (ProductionSlot 컴포넌트 재사용, 신규 GameObject 5개)
```

- `ProductionQueuePanelRoot`는 건물이 선택되고, 큐를 가진 생산 건물(MainBase/Tier1/Tier2/Tier3)일 때만
  활성화된다. 그 외 상태(유닛 선택, 건물 미선택, SupplyDepot/Lab 선택 등)에서는 비활성화.
- 슬롯 하나당 `Button` + 자식 `Image`(아이콘) 구조는 기존 `ProductionSlot` 프리팹 구조를 그대로 복제해서 사용.
- 슬롯 개수는 `UnitSpawner`의 큐 최대치(5)와 1:1로 고정 대응.

## 5. 각 클래스별 변경 지점 (설계만, 실제 코드는 아직 미적용)

### 5.1 `UnitSpawner`
- 추가: 큐 스냅샷을 읽기 전용으로 노출하는 접근자.
  ```csharp
  public IReadOnlyList<ProductionData> GetQueueSnapshot() => productionQueue;
  ```
- `Cancel(int index)`는 기존 구현 그대로 사용 (수정 불필요).
- `ProductionData`에 이미 `UnitID`, `RemainTime`이 있어 아이콘 매핑과 (선택적) 진행률 표시에 바로 사용 가능.

### 5.2 `BuildingController`
- 추가: `UnitSpawner`로의 위임 메서드 2개.
  ```csharp
  public IReadOnlyList<ProductionData> GetProductionQueue() => UnitSpawner.GetQueueSnapshot();
  public void CancelProductionAt(int index) => UnitSpawner.Cancel(index);
  ```
- 생산 큐가 없는 건물(SupplyDepot, Lab 등)에도 `UnitSpawner`가 없을 수 있으므로, null 체크 후
  빈 리스트(`Array.Empty<ProductionData>()`) 반환하도록 방어 처리.

### 5.3 `RTSUnitController`
- `Update()`의 `BuildingSelect` case 내부, 기존 `uIController.ShowXXXPanel(...)` 호출 직후에
  대기열 갱신 로직 추가.
  ```csharp
  if (selectedBuildingList.Count > 0)
  {
      BuildingController target = selectedBuildingList[0];
      var queue = target.GetProductionQueue();
      uIController.ShowProductionQueue(queue, i => target.CancelProductionAt(i));
  }
  else
  {
      uIController.ClearProductionQueue();
  }
  ```
- `SupplyDepot`, `Lab`, `None` 케이스에서는 `uIController.ClearProductionQueue()` 호출로 패널 숨김.

### 5.4 `UIController`
- 추가 필드:
  ```csharp
  [Header("Production Queue Panel")]
  [SerializeField] private GameObject productionQueuePanelRoot;
  [SerializeField] private ProductionSlot[] queueSlots; // 길이 5
  ```
- 유닛 ID → 아이콘 매핑 헬퍼 (기존 스프라이트 필드 재사용):
  ```csharp
  private Sprite GetUnitIcon(int unitID) => unitID switch
  {
      RTSUnitController.UnitID.Worker   => workerIcon,
      RTSUnitController.UnitID.Marine   => marineIcon,
      RTSUnitController.UnitID.Vulture  => vultureIcon,
      RTSUnitController.UnitID.Goliath  => goliathIcon,
      RTSUnitController.UnitID.Tank     => tankIcon,
      RTSUnitController.UnitID.Wraith   => wraithIcon,
      RTSUnitController.UnitID.Guardian => guardianIcon,
      _ => null
  };
  ```
- 신규 표시/초기화 메서드:
  ```csharp
  public void ShowProductionQueue(IReadOnlyList<ProductionData> queue, Action<int> onCancel)
  {
      bool hasQueue = queue != null && queue.Count > 0;
      productionQueuePanelRoot.SetActive(hasQueue);

      for (int i = 0; i < queueSlots.Length; i++)
      {
          if (queue != null && i < queue.Count)
          {
              int capturedIndex = i; // 클로저 캡처 주의
              queueSlots[i].SetData(new CommandButtonData(
                  GetUnitIcon(queue[i].UnitID),
                  () => onCancel(capturedIndex)));
          }
          else
          {
              queueSlots[i].Clear();
          }
      }
  }

  public void ClearProductionQueue()
  {
      productionQueuePanelRoot.SetActive(false);
      foreach (var slot in queueSlots)
          slot.Clear();
  }
  ```

### 5.5 `ProductionSlot`
- 변경 없음. `Button` + `Image` + `Action` 콜백 구조가 대기열 취소 용도에도 그대로 부합.

## 6. 클로저 캡처 주의사항

`ShowProductionQueue`는 매 프레임 호출되므로 `for` 루프 변수 `i`를 람다에 직접 캡처하면
안 되고, 반드시 `int capturedIndex = i;`로 복사한 변수를 캡처해야 모든 슬롯이 올바른
인덱스로 취소 요청을 보낸다. (기존 `AddCancelCommand` 등에서도 동일 패턴 유지 필요.)

## 7. 확장 여지 (이번 구현 범위 밖)

- 진행률 표시: `ProductionData.RemainTime` / `UnitData.productionTime` 비율로 슬롯에 프로그레스 바나
  radial fill 추가 가능 (`ProductionSlot`에 `Image.fillAmount` 필드 확장 필요).
- 다중 건물 선택 시 큐 병합 표시 (현재는 첫 번째 건물만 표시).
- 큐 변경 이벤트화(`UnitSpawner.OnQueueChanged`) — 현재는 매 프레임 폴링이라 5개 슬롯 규모에서는
  성능 문제 없음. 유닛 수가 크게 늘어나면 이벤트 기반으로 전환 고려.

## 8. 요약 체크리스트 (구현 시 순서 제안)

1. `UnitSpawner`에 `GetQueueSnapshot()` 추가.
2. `BuildingController`에 `GetProductionQueue()` / `CancelProductionAt(int)` 추가.
3. `UIController`에 `productionQueuePanelRoot`, `queueSlots` 필드 + `ShowProductionQueue` /
   `ClearProductionQueue` 메서드 추가.
4. `RTSUnitController.Update()`의 `BuildingSelect` 분기에 큐 갱신 호출 삽입.
5. Canvas에 `ProductionQueuePanelRoot` + 슬롯 5개(기존 `ProductionSlot` 프리팹 복제) 배치 후
   인스펙터에 배열 연결.
