## 2026-07-22

### 요청 내용

> 그럼 현재 방식인 unitspawner에서 해당 수치 처리를 하는게 아니라 유닛이 직접 하는건 어떤거 같아 각 유닛 아이디 값을 통해서 유닛스크립터블오브젝트에 데이터를 가져와서 해당하는 유닛에 공격력이나 사거리(사거리는 AttackRange안에 있는걸 사용)하는 식으로 작동하는게 좋을거 같아 지금 테스트용으로 유닛스포너를 통해서 작동하는게 아닌 그냥 설치된 프리팹들은 해당 사항에서 예외가 되버리네

`doc/0203`에서는 `UnitSpawner.Spawn()`이 `Instantiate` 직후 `ApplyUnitData`를 "밖에서" 호출하는 방식(push)으로 구현했는데, 이러면 **생산 큐를 거쳐 스폰된 유닛만** SO 값을 적용받고, 테스트용으로 씬에 미리 배치해둔 프리팹 인스턴스나 향후 다른 경로(예: 시작 유닛 스폰, 다른 스폰 시스템)로 생성되는 유닛은 전부 예외가 됨. 유닛 자신이 자기 `unitID`로 SO를 조회해서 스스로 적용(pull)하면, **어떤 경로로 생성되든** 항상 적용됨.

동의합니다 — 이 방식이 더 견고합니다.

### 조사 내용

- `UnitController`는 이미 `Start()`(`Assets/Scripts/Unit/UnitController.cs:202`)에서 `rtsController = FindFirstObjectByType<RTSUnitController>();`로 씬의 `RTSUnitController`를 찾고 있음 — `attackRange`(자식 컴포넌트, `Awake()`에서 설정됨)와 마찬가지로 이미 존재하는 참조 획득 패턴을 그대로 재사용 가능.
- `RTSUnitController`는 `unitDatabase`(`UnitDataSO`)를 이미 들고 있지만 `private` — 외부(여기서는 `UnitController` 자신)에서 조회할 수 있는 public 메서드가 없음. `DamageMultiplierTable` 프로퍼티(`doc/0201`에서 추가)와 같은 패턴으로 조회용 메서드를 하나 추가하면 됨.
- **실행 순서 확인**: `HealthManager.Awake()`가 `currentHp = maxHealth`(프리팹 기본값)를 먼저 설정하므로, `UnitController`의 SO 적용 로직은 반드시 `Awake()`가 아니라 **`Start()`**에서 실행해야 함 — Unity는 같은 프레임 내 모든 오브젝트의 `Awake()`가 끝난 뒤에 `Start()`를 호출하는 걸 보장하므로, `Start()`에서 `InitializeHealth()`를 호출하면 `HealthManager.Awake()`가 이미 끝난 뒤라 덮어쓰기가 안전하게 적용됨.
- `doc/0203`에서 이미 만든 `UnitController.ApplyUnitData(UnitData data)` 메서드(체력/공격력/사거리/아이콘/장갑타입/크기타입 적용)는 그대로 재사용 — "누가, 언제 호출하는지"만 바뀜(`UnitSpawner` → 자기 자신의 `Start()`).
- SO에 등록 안 된 `unitID`(예: 테스트용 프리팹이 0이거나 미등록 ID인 경우)는 `GetUnitData`가 `null`을 반환하고, `ApplyUnitData`는 이미 `data == null`이면 그냥 리턴하도록 되어 있어 안전(프리팹 기본값 그대로 유지).

### 코드 변경 (예정)

#### 1) `Assets/Scripts/System/RTSUnitController.cs` — 유닛 데이터 조회용 public 메서드 추가

**변경 코드** (`DamageMultiplierTable` 프로퍼티 옆에 추가)
```csharp
    // unitID로 UnitData를 조회한다 (UnitController가 자기 자신의 스탯을 SO에서 가져올 때 사용).
    public UnitData GetUnitData(int unitID) => unitDatabase.unitData.Find(d => d.ID == unitID);
```

#### 2) `Assets/Scripts/Unit/UnitController.cs` — `Start()`에서 자가 적용

**기존 코드**
```csharp
    void Start()
    {
        unitMarker.SetActive(false);
        if (isWorker)
        {
            DepositOre.SetActive(false);
            DepositGas.SetActive(false);
        }

        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController.UnitList.Add(this);
    }
```

**변경 코드**
```csharp
    void Start()
    {
        unitMarker.SetActive(false);
        if (isWorker)
        {
            DepositOre.SetActive(false);
            DepositGas.SetActive(false);
        }

        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController.UnitList.Add(this);

        // 생산 큐를 거쳤든 씬에 직접 배치됐든, 어떤 경로로 만들어진 인스턴스든 항상 자기 unitID로
        // UnitDataSO를 조회해서 스스로 스탯을 적용한다 (UnitSpawner가 밖에서 push하던 방식 대체).
        ApplyUnitData(rtsController.GetUnitData(unitID));
    }
```

(`ApplyUnitData(UnitData data)` 메서드 자체는 `doc/0203`에서 만든 것 그대로 재사용 — 변경 없음)

#### 3) `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — 이제 필요 없어진 호출 제거

**기존 코드**
```csharp
        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        if (spawnunit.TryGetComponent<UnitController>(out var unitController))
        {
            unitController.ApplyUnitData(data); // 체력/공격력/사거리/아이콘/장갑타입/크기타입을 SO 값으로 덮어씀
            unitController.MoveTo(buildingController.GetRallyPos());
        }
```

**변경 코드**
```csharp
        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        // 스탯 적용은 이제 유닛 자신이 Start()에서 처리한다 (UnitController.ApplyUnitData 참고) - 여기선 이동만 지시.
        if (spawnunit.TryGetComponent<UnitController>(out var unitController))
        {
            unitController.MoveTo(buildingController.GetRallyPos());
        }
```

### 요약/영향받는 파일

- `Assets/Scripts/System/RTSUnitController.cs` — `GetUnitData(int)` 메서드 추가
- `Assets/Scripts/Unit/UnitController.cs` — `Start()`에서 자기 `unitID`로 `ApplyUnitData` 자가 호출
- `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — `ApplyUnitData` 호출 제거(이동 지시만 남김)

이제 생산 큐를 거치지 않고 씬에 직접 배치된 유닛 인스턴스(테스트용 등)도 `Start()` 시점에 자동으로 SO 값을 적용받습니다.

---

**적용 완료** (2026-07-22) — 사용자 확인 후 위 3개 파일 변경을 그대로 적용함. 각 프리팹의 `unitID` 값을 SO의 ID와 맞추는 작업은 사용자가 직접 진행하기로 함(Sharpshooter→8, SkyLancer→9 등).
