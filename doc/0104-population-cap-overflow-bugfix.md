# 0104 - 인구수 한도 200 캡 초과분이 보급고 파괴 시 사라지는 버그 (제안)

**날짜:** 2026-07-13

## 요청 내용

> 현재 보급고나 메인기지를 통해서 인구수 최대값이 늘어나는데 최대제한이 200으로 되어있는데 만약 200을 넘겨서 보급고를 지었는데 만약 200넘긴 만큼 보급고가 있는데 보급고가 부서지면 그냥 200 -8 로 되어버리네 만약 240만큼 보급고를 지었는데 -8 씩 달면 232이라 200남아있어야 하는거 아닌가?

인구수 한도(보급)를 늘려주는 건물(사령부/보급고 등)을 지어서 실제 누적치가 200을 넘게(예: 240) 지었어도, 화면상 한도는 200으로 고정된다. 이 상태에서 보급고 하나(8 제공)가 파괴되면 기대값은 240-8=232 → 200 캡에 걸려 여전히 200이어야 하는데, 실제로는 200-8=192로 줄어든다.

## 조사 내용

`Assets/Scripts/Resource/ResourceManager.cs`:

```csharp
[SerializeField] private int maxPopulationCap = 200; // 인구수 한도가 아무리 늘어도 이 값을 넘지 않음

private int maxPopulation;       // 인구수 한도 (보급고 등 건물로 증가)

private void Awake()
{
    ...
    maxPopulation = startMaxPopulation;
}

public int GetMaxPopulation() => maxPopulation;

public void AddMaxPopulation(int amount)
{
    if (amount <= 0) return;
    maxPopulation = Mathf.Min(maxPopulationCap, maxPopulation + amount);
    OnResourceChanged?.Invoke();
}

public void RemoveMaxPopulation(int amount)
{
    if (amount <= 0) return;
    maxPopulation = Mathf.Max(0, maxPopulation - amount);
    OnResourceChanged?.Invoke();
}
```

- `maxPopulation` 자체가 **캡이 적용된 값**으로 저장된다. `AddMaxPopulation()`이 매번 `Mathf.Min(200, ...)`으로 누른 값을 그대로 필드에 덮어쓰기 때문에, 240만큼 지어도 실제로는 200에서 더 이상 올라가지 않고 "누적으로 240만큼 지었다"는 사실 자체가 소실된다.
- 이후 보급고가 파괴되면 `RemoveMaxPopulation(8)`이 이미 캡 걸린 200에서 그대로 8을 빼서 192가 된다. "이미 캡을 초과한 상태였으니 200을 유지해야 한다"는 정보가 없어진 상태이므로 이렇게 될 수밖에 없다.
- 호출부(`Assets/Scripts/Building/BaseStructure.cs:189`, `Assets/Scripts/BuildSystem/PlacementSystem.cs:96`, `Assets/Scripts/System/RTSUnitController.cs:806,813`)는 모두 `AddMaxPopulation`/`RemoveMaxPopulation`을 그대로 호출할 뿐이라 문제 없음 — 버그는 `ResourceManager` 내부의 캡 적용 시점에 있다.

## 계획한 코드 변경

캡이 적용되지 않은 "실제 누적 인구 한도" 값을 별도로 들고 있다가, 외부에 노출할 때만(`GetMaxPopulation()`, `CanAfford()`) 200 캡을 씌우는 방식으로 변경한다. 이렇게 하면 240을 지어도 내부적으로는 240이 유지되고, 8이 파괴되면 232가 되며, 표시값은 `Mathf.Min(200, 232) = 200`으로 그대로 유지된다.

### `Assets/Scripts/Resource/ResourceManager.cs`

기존 코드:
```csharp
    private int currentOre;
    private int currentGas;
    private int currentPopulation;   // 현재 사용 중인 인구수
    private int maxPopulation;       // 인구수 한도 (보급고 등 건물로 증가)

    // 자원/인구가 변경될 때마다 발생하는 이벤트 (UI 갱신용)
    public event System.Action OnResourceChanged;

    private void Awake()
    {
        currentOre = startOre;
        currentGas = startGas;
        maxPopulation = startMaxPopulation;
    }

    // ===== Get =====
    public int GetOre() => currentOre;
    public int GetGas() => currentGas;
    public int GetPopulation() => currentPopulation;
    public int GetMaxPopulation() => maxPopulation;
```

변경 코드:
```csharp
    private int currentOre;
    private int currentGas;
    private int currentPopulation;   // 현재 사용 중인 인구수
    private int rawMaxPopulation;    // 인구수 한도 누적치 (200 캡 적용 전 실제 합계)

    // 자원/인구가 변경될 때마다 발생하는 이벤트 (UI 갱신용)
    public event System.Action OnResourceChanged;

    private void Awake()
    {
        currentOre = startOre;
        currentGas = startGas;
        rawMaxPopulation = startMaxPopulation;
    }

    // ===== Get =====
    public int GetOre() => currentOre;
    public int GetGas() => currentGas;
    public int GetPopulation() => currentPopulation;
    // 표시/판정용 한도는 항상 여기서 캡을 씌운다. 누적치(rawMaxPopulation) 자체는 캡을 넘어도 그대로 보존.
    public int GetMaxPopulation() => Mathf.Min(maxPopulationCap, rawMaxPopulation);
```

기존 코드:
```csharp
    // ===== 인구수 한도 변경 (보급고 건설/파괴 시) =====
    public void AddMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        maxPopulation = Mathf.Min(maxPopulationCap, maxPopulation + amount);
        OnResourceChanged?.Invoke();
    }

    public void RemoveMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        maxPopulation = Mathf.Max(0, maxPopulation - amount);
        OnResourceChanged?.Invoke();
    }
```

변경 코드:
```csharp
    // ===== 인구수 한도 변경 (보급고 건설/파괴 시) =====
    // 누적치(rawMaxPopulation)에는 캡을 씌우지 않는다 — 캡을 넘겨 지은 뒤 일부가 파괴돼도
    // 남은 누적치가 여전히 캡을 넘으면 표시 한도(GetMaxPopulation)는 캡값 그대로 유지되어야 하기 때문.
    public void AddMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        rawMaxPopulation += amount;
        OnResourceChanged?.Invoke();
    }

    public void RemoveMaxPopulation(int amount)
    {
        if (amount <= 0) return;
        rawMaxPopulation = Mathf.Max(0, rawMaxPopulation - amount);
        OnResourceChanged?.Invoke();
    }
```

기존 코드 (`CanAfford`, 필드 직접 참조):
```csharp
    public bool CanAfford(int oreCost, int gasCost, int populationCost = 0)
    {
        if (currentOre < oreCost) return false;
        if (currentGas < gasCost) return false;
        if (currentPopulation + populationCost > maxPopulation) return false;

        return true;
    }
```

변경 코드:
```csharp
    public bool CanAfford(int oreCost, int gasCost, int populationCost = 0)
    {
        if (currentOre < oreCost) return false;
        if (currentGas < gasCost) return false;
        if (currentPopulation + populationCost > GetMaxPopulation()) return false;

        return true;
    }
```

## 영향받는 파일

- `Assets/Scripts/Resource/ResourceManager.cs` (`maxPopulation` 필드를 캡 미적용 누적치 `rawMaxPopulation`으로 교체, 노출/판정 시점에만 캡 적용)

## 예시로 검증

- 240만큼 보급고를 지음 → `rawMaxPopulation = 240`, `GetMaxPopulation() = Min(200, 240) = 200` (기존과 동일하게 화면엔 200)
- 보급고 하나(8) 파괴 → `rawMaxPopulation = 232`, `GetMaxPopulation() = Min(200, 232) = 200` (요청하신 대로 200 유지)
- 추가로 계속 파괴돼서 `rawMaxPopulation`이 200 밑으로 내려가면(예: 192) 그때부터 `GetMaxPopulation()`도 192로 정상적으로 줄어듦.

## 참고

- 다른 스크립트는 전부 `GetMaxPopulation()`/`AddMaxPopulation()`/`RemoveMaxPopulation()`을 통해서만 접근하므로 이 파일 하나만 고치면 됨.
- 아직 프로젝트 파일에는 반영하지 않음 — 승인 시 위 변경 그대로 적용 예정.

## 적용 결과 (2026-07-13)

사용자 승인 받아 `Assets/Scripts/Resource/ResourceManager.cs`에 위 계획대로 그대로 반영함. 코드 diff는 계획한 코드 변경 섹션과 100% 동일.
