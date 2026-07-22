## 2026-07-22

### 요청 내용

> 이대로 진행시켜줘 (`doc/0203` 제안 승인)

### 코드 변경

`doc/0203`에서 제안한 대로 3개 파일 수정.

#### `Assets/Scripts/Unit/HealthManager.cs`

**기존 코드**
```csharp
    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = Mathf.Min(currentHp, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }
```

**변경 코드**
```csharp
    public void SetMaxHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = Mathf.Min(currentHp, maxHealth);
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }

    // 유닛이 생산되어 스폰되는 시점 전용: 최대 체력을 새로 지정하는 동시에 그만큼 꽉 채운다.
    public void InitializeHealth(int newMax)
    {
        maxHealth = Mathf.Max(0, newMax);
        currentHp = maxHealth;
        OnHealthChanged?.Invoke(currentHp, maxHealth);
    }
```

#### `Assets/Scripts/Unit/UnitController.cs`

**변경 코드** (신규 메서드 추가)
```csharp
    public void ApplyUnitData(UnitData data)
    {
        if (data == null)
            return;

        icon = data.Icon;
        attackDamage = data.attackDamge;
        armorType = data.armorType;
        sizeType = data.sizeType;

        if (attackRange != null)
            attackRange.UnitRange = data.attackRange;

        GetComponent<HealthManager>()?.InitializeHealth(data.hp);
    }
```

#### `Assets/Scripts/UnitSpawner/UnitSpawner.cs`

**기존 코드**
```csharp
        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        spawnunit.GetComponent<UnitController>().MoveTo(buildingController.GetRallyPos());
```

**변경 코드**
```csharp
        GameObject spawnunit = Instantiate(data.Prefab, spawnPos, Quaternion.identity);

        if (spawnunit.TryGetComponent<UnitController>(out var unitController))
        {
            unitController.ApplyUnitData(data);
            unitController.MoveTo(buildingController.GetRallyPos());
        }
```

### 요약/영향받는 파일

- `Assets/Scripts/Unit/HealthManager.cs`, `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/UnitSpawner/UnitSpawner.cs` — `doc/0203` 그대로 적용
- `Docs/UnitBalanceReference.md` — 이 변경으로 스폰 시 실제 적용 값이 대부분 SO 기준으로 바뀌어서(예: Scout Drone 공격력 20→5, Firehawk 체력 120→150 등) 전면 갱신함. 여전히 동기화 안 되는 필드(`unitID`/`armor`/`attackType`/고유보너스/`isAirUnit`)와 Sharpshooter/SkyLancer의 `unitID` 버그는 그대로 표시해둠.

### 남은 이슈 (문서에만 기록, 미조치)

- Sharpshooter/SkyLancer 프리팹의 `unitID`가 여전히 2/4라 인구수·이름 표시 등이 다른 유닛으로 오인식됨.
- `armor`/`attackType`/고유보너스/`isAirUnit`은 여전히 프리팹 값만 사용(SO에 대응 필드 없음).
- SO 안의 일부 수치(Sharpshooter 가격/공격력/사거리 등)가 최근 사용자가 준 설계 스펙과 다름 — 별도 확인 필요.
