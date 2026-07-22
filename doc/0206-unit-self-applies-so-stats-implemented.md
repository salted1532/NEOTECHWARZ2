## 2026-07-22

### 요청 내용

> 이대로 적용시켜줘 그리고 각 프리팹에 아이디값은 내가 스크립터블오브젝트의 아이디값을 잘 맞춰서 조정할게

`doc/0205` 승인. Sharpshooter/SkyLancer 등 프리팹의 `unitID`를 SO의 ID(8/9)와 맞추는 작업은 사용자가 직접 에디터에서 진행하기로 함.

### 코드 변경

`doc/0205`에서 제안한 대로 3개 파일 수정.

#### `Assets/Scripts/System/RTSUnitController.cs`

**변경 코드** (`DamageMultiplierTable` 프로퍼티 옆에 추가)
```csharp
    // unitID로 UnitData를 조회한다 (UnitController가 자기 자신의 스탯을 SO에서 가져올 때 사용).
    public UnitData GetUnitData(int unitID) => unitDatabase.unitData.Find(d => d.ID == unitID);
```

#### `Assets/Scripts/Unit/UnitController.cs`

**기존 코드**
```csharp
        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController.UnitList.Add(this);
    }
```

**변경 코드**
```csharp
        rtsController = FindFirstObjectByType<RTSUnitController>();
        rtsController.UnitList.Add(this);

        // 생산 큐를 거쳤든 씬에 직접 배치됐든, 어떤 경로로 만들어진 인스턴스든 항상 자기 unitID로
        // UnitDataSO를 조회해서 스스로 스탯을 적용한다 (UnitSpawner가 밖에서 push하던 방식 대체).
        ApplyUnitData(rtsController.GetUnitData(unitID));
    }
```

#### `Assets/Scripts/UnitSpawner/UnitSpawner.cs`

**기존 코드**
```csharp
        if (spawnunit.TryGetComponent<UnitController>(out var unitController))
        {
            unitController.ApplyUnitData(data); // 체력/공격력/사거리/아이콘/장갑타입/크기타입을 SO 값으로 덮어씀
            unitController.MoveTo(buildingController.GetRallyPos());
        }
```

**변경 코드**
```csharp
        // 스탯 적용은 이제 유닛 자신이 Start()에서 처리한다 (UnitController.ApplyUnitData 참고) - 여기선 이동만 지시.
        if (spawnunit.TryGetComponent<UnitController>(out var unitController))
        {
            unitController.MoveTo(buildingController.GetRallyPos());
        }
```

### 요약/영향받는 파일

- `Assets/Scripts/System/RTSUnitController.cs`, `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — `doc/0205` 그대로 적용
- `Docs/UnitBalanceReference.md` — 0장 설명을 `doc/0205` 기준(유닛 자가 적용 방식)으로 갱신

### 남은 작업

- Sharpshooter/SkyLancer 등 프리팹의 `unitID` 값을 SO ID와 맞추는 작업은 사용자가 직접 진행 예정.
- `armor`/`attackType`/고유보너스/`isAirUnit`은 여전히 SO에 필드가 없어 프리팹 값만 사용(변경 없음).
