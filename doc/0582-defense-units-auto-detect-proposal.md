## 날짜
2026-08-15

## 요청 내용
"적AI로 스포어 브루드 종족이 정해져있을때 스포어 브루드의 방어 유닛 세팅이 안되는거 같음"

후속 대화로 확인/구체화된 요구사항:
- 방어 유닛(배치형 방어 유닛)이 인스펙터에 표시되고, 죽으면 다시 생산해서 보내줘야 하는데 인스펙터에
  표시가 안 되고 다시 생성되지도 않는 것 같다.
- "게임 시작하고 자동으로 되어야하는데" → 인스펙터에 수동으로 드래그해 넣는 게 아니라 자동 인식 원함.
- (최종 확정) "배치형 방어 유닛은 씬의 미리 세워둔 고정 수비 유닛인데 이것들은 그냥 Enemy리스트의 있는
  모든 유닛을 넣어주면 되고 죽으면 빼주면 돼. 죽으면 그때 같은 유닛을 생산하고 유닛이 있던 위치로
  가도록 해줬으면 좋겠어 그러면 1회만 재생산되고 또 죽으면 그땐 방어유닛 판정이 아니라 그냥 죽고
  끝일거 같네"

## 조사 내용
`Assets/Scripts/System/EnemyAIDirector.cs`의 `defenseUnits`(배치형 방어 유닛)는 처음부터 완전 수동
방식이었다 - 씬에 세워둔 유닛을 인스펙터에 하나하나 드래그해 넣어야 `Start()`에서 `defenseSlots`로
등록됐다. Unity Editor 라이브 확인 + Mission1~5 전수 조사 결과 **OC 포함 그 어떤 미션에도 설정된 적이
없었다**(전부 빈 리스트) - 스포어 브루드만 차별적으로 안 되는 버그가 아니라 기능 자체가 한 번도 실제
쓰인 적이 없었던 것.

또한 기존 `RespawnDeadDefenseUnits()`는 생산 대기열을 거치지 않고 `Instantiate()`로 그 자리에 즉시
재생산했는데, 이번 요청은 "생산하고 그 자리로 이동"이라 기존 스폰 지점 생산 대기열(`EnqueueProduction`/
`SpawnQueue`/`Update()`)을 그대로 재사용해 생산 시간(`data.productionTime`)이 걸리고 완성되면 원래
위치로 `MoveTo()` 이동하도록 바꾼다 - garrison/raidGarrison과 동일한 생산 파이프라인, 목적지만
"풀에 추가+집결지 이동" 대신 "슬롯에 등록+원래 위치 이동"으로 분기.

## 계획된 코드 변경

**파일:** `Assets/Scripts/System/EnemyAIDirector.cs`

### 1. `defenseUnits` 필드 (57-58행) - 입력용 수동 리스트 → 출력용 디버그 리스트

#### 기존 코드
```csharp
    [Header("<공통> 배치형 방어 유닛 (씬에 미리 세워둔 고정 수비 유닛 - 죽으면 같은 자리에 같은 종류로 1회 재생산, 그 대체 유닛까지 죽으면 더 생산 안 함. 건물 재건은 범위 밖, doc/0552, doc/0558)")]
    [SerializeField] private List<EnemyUnitController> defenseUnits;
```

#### 변경 코드
```csharp
    [Header("<디버그> 배치형 방어 유닛 현재 상태 (자동 감지 - Start() 시점에 씬에 이미 있던 모든 적 유닛을 방어 슬롯으로 등록. 죽으면 생산 큐에 주문해 완성되는 대로 원래 있던 위치로 보내 1회만 재생산, 그 대체 유닛까지 죽으면 더 생산 안 함. 건물 재건은 범위 밖, doc/0552, doc/0558, doc/0582)")]
    [SerializeField] private List<EnemyUnitController> defenseUnits = new List<EnemyUnitController>();
```

### 2. `DefenseSlot` 클래스 (257-264행) - 생산 대기 여부 필드 추가

#### 기존 코드
```csharp
    private class DefenseSlot
    {
        public int unitID;
        public Vector3 position;
        public Quaternion rotation;
        public EnemyUnitController current;
        public bool respawned; // 원본이 죽어 이미 한 번 대체 생산됐는지 - true면 그 대체 유닛이 죽어도 더 생산하지 않음(doc/0558)
    }
```

#### 변경 코드
```csharp
    private class DefenseSlot
    {
        public int unitID;
        public Vector3 position;
        public Quaternion rotation;
        public EnemyUnitController current;
        public bool respawned; // 원본이 죽어 이미 한 번 대체 생산됐는지 - true면 그 대체 유닛이 죽어도 더 생산하지 않음(doc/0558)
        public bool pendingProduction; // 지금 생산 대기열에 이미 이 슬롯 몫 주문이 들어가 있는지(doc/0582) - 중복 주문 방지
    }
```

### 3. `EnemyProductionOrder` 클래스 (231-237행) - 목적지로 "슬롯"도 가능하게

#### 기존 코드
```csharp
    private class EnemyProductionOrder
    {
        public int unitID;
        public float remainTime;
        public float totalTime;
        public List<EnemyUnitController> destinationPool;
    }
```

#### 변경 코드
```csharp
    private class EnemyProductionOrder
    {
        public int unitID;
        public float remainTime;
        public float totalTime;
        public List<EnemyUnitController> destinationPool; // 웨이브/별동대용 - 완성되면 이 풀에 추가되고 집결지로 이동
        public DefenseSlot targetSlot; // 방어 유닛 대체 생산용(doc/0582) - 완성되면 이 슬롯에 등록되고 원래 위치로 이동. 둘 중 하나만 채워짐
    }
```

### 4. `Start()` (330-342행) - 수동 리스트 대신 씬에 있는 모든 적 유닛으로 슬롯 구성

#### 기존 코드
```csharp
        foreach (EnemyUnitController unit in defenseUnits)
        {
            if (unit == null)
                continue;

            defenseSlots.Add(new DefenseSlot
            {
                unitID = unit.GetEnemyUnitID(),
                position = unit.transform.position,
                rotation = unit.transform.rotation,
                current = unit,
            });
        }
```

#### 변경 코드
```csharp
        // Start() 시점(이 director가 아직 아무것도 생산하기 전)에 씬에 이미 존재하는 적 유닛은 전부
        // 레벨 디자이너가 미리 세워둔 고정 수비 유닛으로 간주한다(doc/0582) - 더 이상 인스펙터에
        // 수동으로 드래그해 넣을 필요 없음.
        // ponytail: 기지가 여러 개(EnemyAIDirector가 씬에 여럿)인 미션에서는 같은 유닛을 두 director가
        // 동시에 방어 슬롯으로 잡을 수 있음 - 지금은 미션당 기지 하나뿐이라 무시, 다중 기지 미션이
        // 생기면 유닛에 소속 기지 마커를 달아 구분할 것.
        foreach (EnemyUnitController unit in FindObjectsByType<EnemyUnitController>(FindObjectsInactive.Exclude))
        {
            defenseSlots.Add(new DefenseSlot
            {
                unitID = unit.GetEnemyUnitID(),
                position = unit.transform.position,
                rotation = unit.transform.rotation,
                current = unit,
            });
        }
        RefreshDefenseUnitsDebugList();
```

### 5. `RespawnDeadDefenseUnits()` (730-746행) → 생산 큐에 주문하는 방식으로 교체

#### 기존 코드
```csharp
    private void RespawnDeadDefenseUnits()
    {
        foreach (DefenseSlot slot in defenseSlots)
        {
            if (slot.current != null || slot.respawned)
                continue;

            UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(slot.unitID) : null;
            if (data == null || data.Prefab == null)
                continue;

            GameObject spawned = Instantiate(data.Prefab, slot.position, slot.rotation);
            if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
                slot.current = unit;
            slot.respawned = true;
        }
    }
```

#### 변경 코드
```csharp
    // 빈 슬롯(죽었고 아직 대체 생산 안 한)마다 생산 대기열에 주문을 넣는다 - 완성되면 Update()가
    // targetSlot을 보고 원래 위치로 보낸다(doc/0582). respawned는 주문 시점이 아니라 실제로 유닛이
    // 배치된 뒤(Update() 완성 처리)에 true로 바뀐다 - 그 전까지는 pendingProduction으로 중복 주문만 막는다.
    private void ReplenishDeadDefenseSlots()
    {
        foreach (DefenseSlot slot in defenseSlots)
        {
            if (slot.current != null || slot.respawned || slot.pendingProduction)
                continue;

            if (EnqueueDefenseProduction(slot))
                slot.pendingProduction = true;
        }

        RefreshDefenseUnitsDebugList();
    }

    // defenseSlots(내부 상태)를 인스펙터에서 실시간으로 볼 수 있는 defenseUnits 디버그 리스트에
    // 반영한다 - allEnemyUnits 등과 동일한 패턴(doc/0582). 죽어서 current가 null이 된 슬롯은 자동으로
    // 목록에서 빠진다.
    private void RefreshDefenseUnitsDebugList()
    {
        defenseUnits.Clear();
        foreach (DefenseSlot slot in defenseSlots)
            if (slot.current != null)
                defenseUnits.Add(slot.current);
    }
```

### 6. `ReinforceRoutine()` (691행대) - 호출부 이름만 교체

#### 기존 코드
```csharp
            RespawnDeadDefenseUnits();
```

#### 변경 코드
```csharp
            ReplenishDeadDefenseSlots();
```

### 7. `EnqueueProduction()` (788-808행) - 슬롯 대상 생산 주문 오버로드 추가

#### 기존 코드
```csharp
    private void EnqueueProduction(int unitID, List<EnemyUnitController> destinationPool)
    {
        if (rtsController == null)
            return;

        UnitData data = rtsController.GetEnemyUnitData(unitID);
        if (data == null || data.Prefab == null)
            return;

        SpawnQueue sq = LeastLoadedQueue();
        if (sq == null)
            return; // 쓸 수 있는 스폰 지점이 하나도 없음(전부 파괴됐거나 애초에 없음)

        sq.orders.Add(new EnemyProductionOrder
        {
            unitID = unitID,
            remainTime = data.productionTime,
            totalTime = data.productionTime,
            destinationPool = destinationPool,
        });
    }
```

#### 변경 코드
```csharp
    private void EnqueueProduction(int unitID, List<EnemyUnitController> destinationPool) =>
        EnqueueOrder(unitID, destinationPool, null);

    // 방어 슬롯 대체 생산 주문(doc/0582) - 쓸 수 있는 스폰 지점이 없으면 주문을 못 넣고 false를
    // 반환하므로, 호출부(ReplenishDeadDefenseSlots)가 pendingProduction을 세우지 않고 다음 주기에
    // 다시 시도한다.
    private bool EnqueueDefenseProduction(DefenseSlot slot) =>
        EnqueueOrder(slot.unitID, null, slot);

    private bool EnqueueOrder(int unitID, List<EnemyUnitController> destinationPool, DefenseSlot targetSlot)
    {
        if (rtsController == null)
            return false;

        UnitData data = rtsController.GetEnemyUnitData(unitID);
        if (data == null || data.Prefab == null)
            return false;

        SpawnQueue sq = LeastLoadedQueue();
        if (sq == null)
            return false; // 쓸 수 있는 스폰 지점이 하나도 없음(전부 파괴됐거나 애초에 없음)

        sq.orders.Add(new EnemyProductionOrder
        {
            unitID = unitID,
            remainTime = data.productionTime,
            totalTime = data.productionTime,
            destinationPool = destinationPool,
            targetSlot = targetSlot,
        });
        return true;
    }
```

### 8. `Update()` (357-391행) - 완성 처리 분기 + 취소 시 pendingProduction 해제

#### 기존 코드
```csharp
        foreach (SpawnQueue sq in spawnQueues)
        {
            if (!sq.IsAvailable)
            {
                sq.orders.Clear();
                continue;
            }

            if (sq.orders.Count == 0)
                continue;

            EnemyProductionOrder front = sq.orders[0];
            front.remainTime -= Time.deltaTime;
            if (front.remainTime > 0f)
                continue;

            sq.orders.RemoveAt(0);

            UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(front.unitID) : null;
            if (data == null || data.Prefab == null)
                continue;

            GameObject spawned = Instantiate(data.Prefab, sq.spawnPoint.point.position, sq.spawnPoint.point.rotation);
            if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
            {
                front.destinationPool.Add(unit);
                unit.MoveTo(DefaultRallyPosition()); // 생산되자마자 집결지로 - 웨이브/별동대 공통(doc/0545)
            }
        }
```

#### 변경 코드
```csharp
        foreach (SpawnQueue sq in spawnQueues)
        {
            if (!sq.IsAvailable)
            {
                // 생산 건물이 파괴돼 취소되는 주문 중 방어 슬롯용이 있으면 pendingProduction을 풀어줘야
                // ReplenishDeadDefenseSlots가 다음 주기에 다른 스폰 지점으로 다시 주문한다(doc/0582).
                foreach (EnemyProductionOrder order in sq.orders)
                    if (order.targetSlot != null)
                        order.targetSlot.pendingProduction = false;

                sq.orders.Clear();
                continue;
            }

            if (sq.orders.Count == 0)
                continue;

            EnemyProductionOrder front = sq.orders[0];
            front.remainTime -= Time.deltaTime;
            if (front.remainTime > 0f)
                continue;

            sq.orders.RemoveAt(0);

            UnitData data = rtsController != null ? rtsController.GetEnemyUnitData(front.unitID) : null;
            if (data == null || data.Prefab == null)
                continue;

            GameObject spawned = Instantiate(data.Prefab, sq.spawnPoint.point.position, sq.spawnPoint.point.rotation);
            if (spawned.TryGetComponent<EnemyUnitController>(out EnemyUnitController unit))
            {
                if (front.targetSlot != null) // 방어 슬롯용 - 원래 있던 위치로 이동(doc/0582)
                {
                    front.targetSlot.current = unit;
                    front.targetSlot.respawned = true;
                    front.targetSlot.pendingProduction = false;
                    unit.MoveTo(front.targetSlot.position);
                }
                else
                {
                    front.destinationPool.Add(unit);
                    unit.MoveTo(DefaultRallyPosition()); // 생산되자마자 집결지로 - 웨이브/별동대 공통(doc/0545)
                }
            }
        }
```

## 요약/영향받는 파일
- `Assets/Scripts/System/EnemyAIDirector.cs` 한 곳만 수정.
- `defenseUnits`는 이제 입력이 아니라 출력(디버그) - 기존에 값이 있던 미션이 없어 마이그레이션 이슈 없음.
- 방어 유닛 대체 생산도 웨이브/별동대와 같은 생산 대기열(스폰 지점 생산 시간)을 거치므로, 즉시 그
  자리에 나타나던 기존 동작과 달리 생산 시간만큼 걸린 뒤 원래 자리로 걸어온다.
- 기지가 여러 개인 미션(현재는 없음)에서 같은 유닛이 두 director에 동시에 잡힐 수 있는 한계는
  `ponytail:` 주석으로 남기고 지금은 대응하지 않음(YAGNI).

## 확인 필요
이대로 구현해도 될까?
