# 0196 - 안개에 가려진 적/중립 자원 클릭 선택 차단 - 수정 제안

## 요청

"지금 fogwar로 가려진 적들이나 뭐 중립 광물등이 클릭으로 선택하는게 불가능했으면 좋겠어 생성된
plane이 클릭을 막는 기능을 추가시켜줄래" — 안개(fog of war)에 가려져 화면에 안 보이는 적 유닛이나
중립 자원(광물/가스)을, 실제로는 안 보이는데도 클릭하면 선택되는 문제가 있어서, 안 보이면 클릭
선택도 안 되게 해달라는 요청. 사용자는 "fog plane이 클릭을 막게" 해달라고 구체적으로 제안함.

## 조사 내용

클릭/선택은 `Assets/Scripts/UserControl/UserControl.cs`의 `HandleLeftClick()`에서
`Physics.Raycast(ray, out hit, Mathf.Infinity, layerEnemy)` / `layerOre` / `layerGas`처럼
**대상 레이어별로 각각 독립된 raycast**를 쏴서 처리한다. fog plane의 콜라이더는 이 raycast들과
전혀 무관한 별개의 오브젝트라서, 사용자가 제안한 "fog plane 콜라이더를 켜서 막기" 방식은 실제로는
동작하지 않는다:

- fog plane 콜라이더를 켜도, `layerEnemy`/`layerOre`/`layerGas` 전용 raycast는애초에 그 레이어만
  보도록 필터링되어 있어서 fog plane(다른 레이어)에 막히지 않는다. fog plane을 그 레이어들에 포함시켜
  물리적으로 막으면, 안개가 걷혀서 **보이는** 적/자원까지 전부 막혀버려 요청과 반대로 동작한다.
- 애초에 fog plane은 지도 전체를 덮는 "한 장짜리 높이"라 타일별로 지금 밝혀졌는지/안 밝혀졌는지
  정보를 전혀 갖고 있지 않다(그건 `csFogWar` 내부의 `shadowcaster.fogField` 텍스처 데이터에만 있음).
  즉 물리 충돌만으로는 "밝혀진 곳은 뚫리고 안 밝혀진 곳만 막힌다"를 표현할 수 없다.

그래서 실제로 필요한 방식은, `csFogWar`가 이미 제공하는 공개 API
`CheckVisibility(Vector3 worldCoordinates, int additionalRadius)` (해당 좌표가 **지금 실제로
보이는지**를 반환)를 클릭 처리 코드에서 직접 확인해서, 안 보이는 대상은 클릭이 맞았어도 무시하는
것이다. 이 방식은 이미 `Assets/Scripts/FogOfWar/FogRevealerAgent.cs`,
`Assets/Scripts/FogOfWar/TerritoryFogReveal.cs`가 `FindFirstObjectByType<csFogWar>()`로 참조를
얻어 쓰는 것과 동일한 패턴.

## 범위

- **포함**: 좌클릭 선택(`HandleLeftClick()`의 "2. 적 클릭", "5. 광물 클릭", "5. 가스 클릭" 분기) +
  마우스 호버 커서(`GetHoveredTarget()`) — 커서도 같이 막아야 하는 이유: 안 그러면 클릭은 안 먹혀도
  마우스를 올렸을 때 "적/중립" 색 커서로 바뀌면서 안 보이는 대상이 거기 있다는 정보가 새어나감.
- **제외(이번엔 안 건드림)**: 우클릭 명령(적 공격 추격, 자원 채취)은 그대로 둠. 필요하면 나중에 별도
  요청으로 확장 가능.

## 계획된 코드 변경

`Assets/Scripts/UserControl/UserControl.cs`

상단 using 추가:
```csharp
using FischlWorks_FogWar;
```

필드 추가 (Before/After):
```csharp
    private RTSUnitController rtsUnitController;
```
→
```csharp
    private RTSUnitController rtsUnitController;
    private csFogWar fogWar;
```

`Awake()`:

Before:
```csharp
    private void Awake()
    {
        mainCamera = Camera.main;
        rtsUnitController = GetComponent<RTSUnitController>();
```
After:
```csharp
    private void Awake()
    {
        mainCamera = Camera.main;
        rtsUnitController = GetComponent<RTSUnitController>();

        fogWar = FindFirstObjectByType<csFogWar>();

        if (fogWar == null)
            Debug.LogWarning($"{name}: csFogWar를 씬에서 찾지 못해 안개에 가려진 대상 클릭 차단 기능이 비활성화됩니다.", this);
```

새 헬퍼 메서드 추가 (클래스 아무 곳, `GetHoveredTarget()` 근처):
```csharp
    // 안개에 가려져(현재 시야 밖) 있는 대상은 클릭 선택/호버 대상에서 제외한다. fogWar가 없는 씬에서는 항상 보이는 것으로 취급.
    private bool IsRevealedByFog(Vector3 worldPosition)
    {
        if (fogWar == null)
            return true;

        return fogWar.CheckVisibility(worldPosition, 0);
    }
```

`HandleLeftClick()` - "2. 적 클릭" 분기:

Before:
```csharp
        if (clickedEnemy)
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null)
            {
```
After:
```csharp
        if (clickedEnemy)
        {
            EnemyController enemy = enemyHit.transform.GetComponent<EnemyController>();

            if (enemy != null && IsRevealedByFog(enemyHit.point))
            {
```

`HandleLeftClick()` - "5. 광물 클릭" 분기:

Before:
```csharp
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
```
After:
```csharp
        if (clickedOre)
        {
            ResourceNode node = OreHit.transform.GetComponent<ResourceNode>();

            if (node != null && IsRevealedByFog(OreHit.point))
            {
```

`HandleLeftClick()` - "5. 가스 클릭" 분기:

Before:
```csharp
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();

            if (node != null)
            {
```
After:
```csharp
        if (clickedGas)
        {
            ResourceNode node = GasHit.transform.GetComponent<ResourceNode>();

            if (node != null && IsRevealedByFog(GasHit.point))
            {
```

`GetHoveredTarget()`:

Before:
```csharp
    private CursorTarget GetHoveredTarget()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, Mathf.Infinity, layerEnemy))
            return CursorTarget.Enemy;

        if (Physics.Raycast(ray, Mathf.Infinity, layerUnit | layerBuilding))
            return CursorTarget.Ally;

        if (Physics.Raycast(ray, Mathf.Infinity, layerOre | layerGas))
            return CursorTarget.Neutral;

        return CursorTarget.None;
    }
```
After:
```csharp
    private CursorTarget GetHoveredTarget()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit enemyHit, Mathf.Infinity, layerEnemy) && IsRevealedByFog(enemyHit.point))
            return CursorTarget.Enemy;

        if (Physics.Raycast(ray, Mathf.Infinity, layerUnit | layerBuilding))
            return CursorTarget.Ally;

        if (Physics.Raycast(ray, out RaycastHit resourceHit, Mathf.Infinity, layerOre | layerGas) && IsRevealedByFog(resourceHit.point))
            return CursorTarget.Neutral;

        return CursorTarget.None;
    }
```

## 동작 변화

- 안개에 가려진(현재 시야 밖) 적/광물/가스는 좌클릭해도 선택되지 않는다. 대신 raycast 자체는 여전히
  성공하므로(예: 적 위에 클릭), 다른 분기(땅 클릭 등)로 자연스럽게 흘러간다 — 예를 들어 Attack
  대기 상태에서 안 보이는 적을 클릭하면 그 적을 강제로 조준하는 대신, 그 자리로 공격-이동 명령이 나감.
- 안개에 가려진 적/자원 위에 마우스를 올려도 커서가 "적/중립" 색으로 바뀌지 않는다(그대로 기본 커서).
- 우클릭(추격 공격/채취 명령)은 이번 변경으로 달라지지 않음.
- `fogWar`가 씬에 없는 경우(테스트 씬 등)는 전부 "항상 보임" 취급이라 기존과 동일하게 동작.

## 영향받는 파일

- `Assets/Scripts/UserControl/UserControl.cs`

## 결과

사용자가 우클릭 명령 처리 방식을 구체적으로 지시하는 것으로 답변(→ [[0197]])해서, 두 문서 내용을
한 번에 적용 완료.
