# 0158. 줌 범위/현재 줌을 지형 단(Layer1/Layer2 태그) 기준으로 이동

## 날짜
2026-07-17

## 요청
"현재 인스펙터 값은 건들이지 말고 지금은 최대줌이 15인데 만약 화면 중앙 레이가 언덕에 닿으면 15->20으로 제한도 잠깐 늘고 카메라 줌도 20으로 변경되도록해줘 만약 다시 언덕 아래로 카메라가 가면 다시 20 -> 15로 제한도 내려가고 줌도 15로 내려가고 만약 내가 확대를 해서 8까지 확대를 했을때 언덕에 닿으면 13까지 늘어나도록 이걸 늘어나는값이 5로 보고 해줘 언덕 위인건 알수 있는 방법은 Mission1 오브젝트 안에 있는 Layer에서 Layer1 ,2 ,3 으로 1이 지상, 2언덕, 3언덕위언덕 이런식으로 일단 3가지 높이로만 만들었는데 레이어로 판단하도록 해줘"

`doc/0157`에서 만든 "화면 중앙 지형의 실제(연속) 높이를 레이캐스트로 재서 줌 범위에 더하는" 방식을 대체하는 요청 — 연속 높이 대신 **고정된 단(tier) 개념 + 고정 증가폭(5)**으로, 그리고 범위뿐 아니라 **현재 줌 위치 자체도** 같이 밀어 올리도록 확장.

## 조사
사용자가 말한 "Layer1/2/3"은 실제로는 Unity **Layer**가 아니라 **Tag**였다. `Assets/prefabs/Maps/Mission1.prefab`을 확인:
- GameObject `layer1`(fileID `4876573636215074505`): `m_TagString: Layer1`
- GameObject `layer2`(fileID `4876573636871858164`): `m_TagString: Layer2`
- ("Layer3"에 해당하는 세 번째 오브젝트는 아직 없음 — "일단 3가지 높이로만 만들었는데"라는 말대로 언덕/언덕위언덕 2단만 존재, 지상은 태그 없는 기본 지형)

두 오브젝트 다 `Transform` 컴포넌트만 가지고 있고, 실제 메시/콜라이더는 그 밑에 자식으로 붙은 수십~수백 개의 타일 프리팹 인스턴스에 있다(YuME 타일맵 방식). `TestScene.unity`의 `Mission1` 프리팹 인스턴스 오버라이드를 보면 이 하위 오브젝트들의 `m_Layer`가 전부 `7`(`Ground`)로 덮어써져 있어, 실제 레이캐스트는 기존에 이미 연결해둔 `groundLayer`(`Ground`)로 맞힐 수 있다. 다만 맞는 콜라이더 자체는 "layer1"/"layer2" 태그가 붙은 부모가 아니라 그 자식(타일)일 가능성이 높아, 태그 판정은 **맞은 콜라이더에서 부모 방향으로 올라가며** 찾아야 한다.

또한 `TagManager.asset`에는 "Layer1"/"Layer2" 태그가 아직 등록되어 있지 않았다(프리팹 YAML에는 문자열로 이미 박혀 있었지만) — 등록 안 된 태그 문자열은 에디터/런타임에서 제대로 인식 안 될 수 있어 `tags:` 목록에 추가.

## 코드 변경

### 1) `ProjectSettings/TagManager.asset`
**기존**: `tags:` 목록에 `Layer1`/`Layer2`/`Layer3` 없음
**변경**: 목록 끝에 `Layer1`, `Layer2`, `Layer3` 추가 (Layer3는 아직 쓰는 오브젝트가 없지만 사용자가 "3가지 높이" 개념을 언급해서 미리 등록)

### 2) `Assets/Scripts/Camera/CameraControl.cs`

**기존 코드** (`doc/0157`에서 추가한 연속 높이 방식)
```csharp
[Header("Zoom")]
[SerializeField] private float zoomSpeed = 25f;
[SerializeField] private float minZoom = 8f;
[SerializeField] private float maxZoom = 35f;
[SerializeField] private LayerMask groundLayer;
```
```csharp
private void Update()
{
    HandleMovement();
    HandleZoom();
    HandleRotate();
}
```
```csharp
private void HandleZoom()
{
    ...
    float groundHeight = GetGroundHeightAtScreenCenter();
    if (nextY < minZoom + groundHeight || nextY > maxZoom + groundHeight)
        return;
    ...
}

private float GetGroundHeightAtScreenCenter()
{
    if (groundLayer == 0) return 0f;
    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    if (Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
        return hit.point.y;
    return 0f;
}
```

**변경 코드**
```csharp
[Header("Zoom")]
[SerializeField] private float zoomSpeed = 25f;
[SerializeField] private float minZoom = 8f;
[SerializeField] private float maxZoom = 35f;
[SerializeField] private LayerMask groundLayer;
[SerializeField] private float tierZoomStep = 5f; // 지형 단 하나당 줌 범위 + 현재 줌이 같이 움직이는 양
```
```csharp
private int currentTerrainTier = 0; // 0=지상, 1=언덕(Layer1), 2=언덕 위 언덕(Layer2)

private void Update()
{
    HandleMovement();
    HandleTerrainTier();
    HandleZoom();
    HandleRotate();
}
```
```csharp
private void HandleZoom()
{
    ...
    float tierOffset = currentTerrainTier * tierZoomStep;
    if (nextY < minZoom + tierOffset || nextY > maxZoom + tierOffset)
        return;
    ...
}

// 지형 단이 바뀌면 범위뿐 아니라 지금 카메라 높이 자체도 그 차이만큼 같이 밀어 올리거나 내림
private void HandleTerrainTier()
{
    int newTier = SampleTerrainTier();
    if (newTier == currentTerrainTier) return;

    targetPosition.y += (newTier - currentTerrainTier) * tierZoomStep;
    currentTerrainTier = newTier;
}

// 맞은 콜라이더에서 부모 방향으로 올라가며 Layer2/Layer1 태그를 찾아 지형 단을 판정
private int SampleTerrainTier()
{
    if (groundLayer == 0) return 0;

    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
        return 0;

    for (Transform t = hit.transform; t != null; t = t.parent)
    {
        if (t.CompareTag("Layer2")) return 2;
        if (t.CompareTag("Layer1")) return 1;
    }
    return 0;
}
```

`GetGroundHeightAtScreenCenter()`는 이 방식으로 대체되어 삭제.

## 동작 확인 (예시 검산)
- 지상(tier 0)에서 `maxZoom=15`일 때 최대로 줌인/줌아웃해서 화면 중앙이 언덕(tier 1)에 닿으면: `targetPosition.y += 1*5`, 범위도 `15+5=20`으로 같이 이동 → 사용자가 말한 "15->20, 줌도 20" 시나리오와 일치(현재 줌이 마침 상한에 붙어있던 경우).
- 지상에서 `8`까지 줌인한 상태로 언덕에 닿으면: `8 + 5 = 13` → 사용자가 말한 "8까지 확대했을 때 언덕에 닿으면 13" 시나리오와 정확히 일치.
- 언덕에서 다시 지상으로 화면 중앙이 내려가면 `tier 1→0`이라 `-5`만큼 반대로 내려감(범위/현재 값 둘 다).

## 요약 / 영향받는 파일
- `ProjectSettings/TagManager.asset` — `Layer1`/`Layer2`/`Layer3` 태그 등록
- `Assets/Scripts/Camera/CameraControl.cs` — `tierZoomStep` 필드 추가, `GetGroundHeightAtScreenCenter()` → `SampleTerrainTier()`/`HandleTerrainTier()`로 교체
- `Docs/CameraControl.md` — 갱신
- `Assets/Scenes/TestScene.unity` — 변경 없음(`minZoom`/`maxZoom`/`groundLayer`는 기존 값 그대로 유지, 사용자 요청대로 인스펙터 값 안 건드림). `tierZoomStep`은 씬에 별도로 안 써도 스크립트 기본값(5)이 그대로 적용됨

## 확인 필요 사항
- `SampleTerrainTier()`가 태그를 찾으려면 화면 중앙 레이가 실제로 `Ground` 레이어의 콜라이더에 맞아야 한다. "layer1"/"layer2" 밑에 실제로 콜라이더가 있는 타일 프리팹이 배치돼 있는지(그리고 그 레이어가 `Ground`로 덮어써져 있는지)는 씬의 방대한 중첩 프리팹 인스턴스 구조상 전수 확인은 못 했고, `TestScene.unity`의 override 목록 샘플과 `doc/0087` 기록을 근거로 추정한 것 — 에디터에서 언덕/언덕위언덕 위로 카메라를 이동시켜 줌이 실제로 5씩 튀는지 직접 확인 필요.
- `Layer3` 태그는 등록만 해뒀고 실제로 쓰는 세 번째 지형 오브젝트는 아직 없어서, `SampleTerrainTier()`도 아직 `Layer2`까지만 판정함 (나중에 세 번째 단이 생기면 `if (t.CompareTag("Layer3")) return 3;` 한 줄 추가하면 됨).

## 비고
[[confirm_before_implementing]] — 사용자가 매우 구체적으로 알고리즘까지 지정해서 요청했고, `doc/0157`에서 이미 승인된 같은 기능(언덕 줌 보정)의 직접적인 개선/치환 요청이라 별도 확인 절차 없이 바로 구현.
