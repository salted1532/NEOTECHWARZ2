# UI 자원 표시 (OreText / GasText / PopulationText) 갱신 설계

작성일: 2026-07-03
상태: 설계 문서 (소스 미수정, 구현 전 검토용)

## 1. 목표

`UIController`가 이미 들고 있는 `OreText`, `GasText`, `PopulationText`
(`UIController.cs:80-82`)에 [[resource-manager-design]]의 `ResourceManager`가 들고
있는 현재 Ore/Gas/Population 값을 매 프레임 반영한다.

- `UIController`는 `ResourceManager`를 직접 참조하지 않고, **`RTSUnitController`를
  거쳐서** 값을 받아온다 (요청하신 대로 — 지금도 `UIController`는 `UnitSpawner`나
  `ResourceManager`를 직접 몰라야 하고, 생산 대기열 조회도 이미
  `rtsUnitController.GetProductionQueue()`처럼 `RTSUnitController`를 거치는
  기존 관례([[health-manager-design]] 아님, 3절 참고 — `UIController.cs:224-232`)
  와 동일한 패턴).
- `UIController.Update()`에서 기존 `UpdateProductionProgress()`(`UIController.cs:104`)
  처럼 매 프레임 폴링해서 텍스트를 갱신한다.

## 2. RTSUnitController — ResourceManager 값을 그대로 전달만 함

`RTSUnitController`는 이미 `resourceManager` 참조를 들고 있다
(`RTSUnitController.cs:26-27`, [[resource-manager-design]] 4절에서 추가한 필드).
여기에 조회용 pass-through 메소드만 추가한다 (분기/가공 없이 그대로 전달).

```csharp
// RTSUnitController.cs 추가
public int GetOre() => resourceManager.GetOre();
public int GetGas() => resourceManager.GetGas();
public int GetPopulation() => resourceManager.GetPopulation();
public int GetMaxPopulation() => resourceManager.GetMaxPopulation();
```

`ResourceManager.GetOre()/GetGas()/GetPopulation()/GetMaxPopulation()`은
[[resource-manager-design]] 3절에서 이미 설계된 것을 그대로 사용한다 (추가 구현 불필요).

## 3. UIController — 매 프레임 텍스트 갱신

### 3-1. `rtsUnitController` 참조 확보

`UIController.cs:86`에 필드 `private RTSUnitController rtsUnitController;`는
이미 있지만 어디서도 값이 대입되지 않고 있다. `Start()`에서 찾아서 채운다.

```csharp
private void Start()
{
    rtsUnitController = FindFirstObjectByType<RTSUnitController>();

    ClearPanel();
    HideProductionUI();
}
```

### 3-2. `Update()`에 자원 텍스트 갱신 추가

```csharp
private void Update()
{
    UpdateProductionProgress();
    UpdateResourceUI();
}

private void UpdateResourceUI()
{
    if (rtsUnitController == null)
        return;

    OreText.text = rtsUnitController.GetOre().ToString();
    GasText.text = rtsUnitController.GetGas().ToString();
    PopulationText.text = $"{rtsUnitController.GetPopulation()}/{rtsUnitController.GetMaxPopulation()}";
}
```

`PopulationText`는 현재값 하나만 표시하기보다 `현재/최대`(예: `6/10`) 형태가
스타크래프트류 UI에서 흔한 표기라 이렇게 제안했다. 단순 숫자 하나만 원하면
`rtsUnitController.GetPopulation().ToString()`으로 바꾸면 됨.

## 4. 판단이 필요한 열린 질문

1. **매 프레임 폴링 vs 이벤트 구독.** `ResourceManager`엔 이미 `OnResourceChanged`
   이벤트(`ResourceManager.cs:14`)가 있어서, 값이 실제로 바뀔 때만 텍스트를
   갱신하는 이벤트 구독 방식도 가능하다 (매 프레임 `ToString()`/텍스트 갱신
   호출을 줄여서 약간 더 효율적). 다만 기존 `UpdateProductionProgress()`가
   이미 매 프레임 폴링 방식으로 짜여있어서, 이번에도 같은 스타일(폴링)로
   맞추는 안을 기본으로 제안했다. 이벤트 구독으로 바꾸려면
   `OnEnable`/`OnDisable`에서 `resourceManager.OnResourceChanged += UpdateResourceUI;`
   식으로 구독/해제하는 코드가 추가로 필요하고, `UIController`가
   `RTSUnitController`를 거치지 않고 `ResourceManager`의 이벤트에 직접
   구독해야 해서 "RTS를 거친다"는 이번 요청 조건과는 살짝 어긋난다
   (이벤트 구독까지 RTS를 거치게 하려면 `RTSUnitController`가 자신의 이벤트로
   한 번 더 감싸서 중계해야 함).
2. **`resourceManager`가 씬에 없을 때.** `RTSUnitController.GetOre()`
   등에서 `resourceManager`가 인스펙터에 연결 안 돼 있으면 NRE가 난다.
   지금 다른 pass-through 메소드들(`GetProductionQueue()` 등)도 별도
   null 체크 없이 그냥 호출하는 스타일이라 동일하게 맞췄는데, 필요하면
   `RTSUnitController` 쪽에 null 체크를 추가할 수도 있음.
