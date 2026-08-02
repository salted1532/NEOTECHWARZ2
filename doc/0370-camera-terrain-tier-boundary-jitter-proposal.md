# 0370. 언덕 경계에서 카메라 지형 단(tier) 판정이 -5/+5로 덜컹거리는 문제

- 날짜: 2026-08-02
- 상태: 구현 완료 (컴파일 확인, 에러 0 / 기존 경고 33개는 이번 변경과 무관)

## 요청 내용

> 카메라가 언덕위 와 언덕아래를 움직이면서 y값 +5되는 부분에서 언덕과 언덕 사이에 카메라를 가만히 두면 -5되었다가 +5되었다가 덜컹거리는데 이유가 -5만큼 내려가면서 화면 가운데가 언덕위에 닿게되면서 +5가 되고 그러면 다시 언덕아래를 가운대로 가리키기 떄문에 또 -5되면서 계속 덜컹 거리는데 이걸 어떤식으로 해결하는게 좋을까?

## 조사 내용

관련 코드: `Assets/Scripts/Camera/CameraControl.cs`의 `HandleTerrainTier()`, `SampleTerrainTier()` (Line 164-207).

### 이미 있는 방어 장치와 그게 왜 안 통하는지

`tierChangeDebounce`(기본 0.15초)가 이미 있다. 판정된 단(`pendingTerrainTier`)이 확정 단(`currentTerrainTier`)과 다를 때, 그 상태가 `tierChangeDebounce` 동안 유지돼야만 실제로 `targetPosition.y`를 옮긴다. 주석에도 "경계면 근처 지형 판정 자체가 프레임마다 왔다갔다함"을 막기 위한 장치라고 적혀 있다.

이건 **매 프레임 무작위로 흔들리는 노이즈**에는 잘 듣는다. 하지만 사용자가 겪는 증상은 노이즈가 아니라 **결정론적(deterministic) 피드백 루프**라서, 매번 한쪽 상태가 디바운스 시간 이상 안정적으로 유지된 뒤 "확정"되고, 확정되자마자 반대 방향으로 또 안정적으로 넘어가서 다시 확정되는 식이다. 그래서 유저 눈에는 "잠깐 멈췄다가 덜컹, 잠깐 멈췄다가 덜컹"으로 보인다 — 디바운스가 루프의 주기를 늘렸을 뿐 루프 자체를 끊지는 못한다.

### 루프가 왜 생기는가

```csharp
private int SampleTerrainTier()
{
    ...
    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
        return 0;
    ...
}
```

이 레이는 **카메라의 실제 현재 위치(`transform.position`)** 에서 화면 정중앙 방향(아래를 내려다보는 각도)으로 나간다. 카메라가 아래를 보는 각도로 고정돼 있으므로, 카메라 Y가 바뀌면 이 레이가 지면에 닿는 지점(전/후 방향 거리)도 같이 바뀐다.

언덕과 언덕 아래의 경계에 카메라가 멈춰 있을 때:

1. 0단 높이에서 쏜 레이가 언덕 위(Layer1)에 닿는다 → 디바운스 통과 → `HandleTerrainTier()`가 `targetPosition.y += tierZoomStep`(+5).
2. 카메라가 5만큼 높아졌다. **같은 화면 좌표, 같은 각도**로 쏜 레이는 더 높은 곳에서 출발하므로 지면에 닿는 지점이 더 멀리(언덕을 넘어) 이동해서 이번엔 언덕 아래(태그 없음)에 닿는다 → 0단 판정 → 디바운스 통과 → `targetPosition.y -= tierZoomStep`(-5).
3. 다시 1번 상태로 돌아가서 무한 반복.

즉 **"카메라 Y가 판정 결과를 바꾸고, 판정 결과가 다시 카메라 Y를 바꾸는" 순환 참조**가 근본 원인이다. 판정 지점 자체가 카메라 Y에 종속돼 있는 한, 디바운스 시간을 아무리 늘려도 루프의 주기만 늘어날 뿐 근본적으로는 안 끊긴다.

## 제안하는 수정 (구현 전 — 확인 필요)

지형 단 판정용 레이를 카메라 Y와 무관하게 만든다. 화면 중앙을 향한 원근 레이 대신, **카메라의 XZ 위치 바로 위 고정 높이에서 수직 아래(`Vector3.down`)로** 쏜다. 이러면 판정 결과가 카메라 Y가 몇이든 항상 같은 지점(카메라 바로 아래 지형)을 가리키므로 순환 참조 자체가 성립하지 않는다.

### 기존 코드

```csharp
private int SampleTerrainTier()
{
    if (groundLayer == 0)
        return 0;

    Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
    if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer))
        return 0;

    for (Transform t = hit.transform; t != null; t = t.parent)
    {
        if (t.CompareTag("Layer2"))
            return 2;
        if (t.CompareTag("Layer1"))
            return 1;
    }

    return 0;
}
```

### 변경 코드 (제안)

```csharp
private int SampleTerrainTier()
{
    if (groundLayer == 0)
        return 0;

    // 화면 중앙을 향한 원근 레이 대신 카메라 XZ 바로 위에서 수직으로 쏜다.
    // 원근 레이는 카메라 Y가 바뀌면 착지 지점도 같이 밀리기 때문에,
    // 언덕 경계에서 "Y가 판정을 바꾸고 판정이 다시 Y를 바꾸는" 루프가 생겨 -5/+5가 반복된다.
    // 수직 레이는 판정 지점이 카메라 XZ에만 묶여 있어 Y가 바뀌어도 결과가 그대로다.
    //
    // X/Z를 맵 경계로 클램프하는 이유: HandleRotate()의 Q/E 궤도 회전은 targetPosition을
    // minX/maxX/minZ/maxZ로 재클램프하지 않아서, 화면 가장자리에서 회전하면 카메라가 잠깐
    // 맵 밖으로 나갈 수 있다. 원근 레이는 그래도 안쪽 지형을 맞힐 여지가 있었지만, 수직 레이는
    // 카메라가 지형 콜라이더 바깥에 있으면 그대로 허공을 가리켜 판정에 실패한다(→ tier 0으로
    // 튐). 맵 안쪽 가장 가까운 지점으로 클램프해서 이 경우에도 항상 지형을 맞히게 한다.
    float sampleX = Mathf.Clamp(targetPosition.x, minX, maxX);
    float sampleZ = Mathf.Clamp(targetPosition.z, minZ, maxZ);
    Vector3 origin = new Vector3(sampleX, 1000f, sampleZ);

    if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f, groundLayer))
        return 0;

    for (Transform t = hit.transform; t != null; t = t.parent)
    {
        if (t.CompareTag("Layer2"))
            return 2;
        if (t.CompareTag("Layer1"))
            return 1;
    }

    return 0;
}
```

이렇게 하면:
- 지형 단 판정이 카메라 Y와 완전히 분리되므로, 경계에서 멈춰 있어도 루프가 성립하지 않는다.
- `tierChangeDebounce`는 그대로 두되(원래 목적인 프레임 단위 노이즈 필터링 용도로 계속 유효), 이번 근본 원인만 별도로 제거하는 형태.
- 판정 원점 X/Z를 기존 맵 경계 필드(`minX/maxX/minZ/maxZ`)로 클램프해서, `HandleRotate()`의 회전으로 카메라가 잠깐 맵 밖으로 나가도 지형을 놓치지 않는다(진짜 지형 구멍으로 레이가 실패하는 경우는 기존처럼 tier 0으로 안전하게 폴백).
- 트레이드오프: "화면 정중앙이 보는 지형"이 아니라 "카메라 바로 아래(XZ, 맵 안쪽으로 클램프됨) 지형" 기준으로 살짝 의미가 바뀐다. 탑다운 RTS 카메라는 피치 각도가 고정돼 있어 둘 사이 체감 차이는 크지 않다.
- `GetScreenCenterGroundPoint()`(Q/E 회전 피벗용)는 이 버그와 무관하므로 그대로 둔다.

### 범위 밖(참고용): HandleRotate의 클램프 누락 자체

`HandleRotate()`가 회전 후 `targetPosition`을 맵 경계로 재클램프하지 않는 것은 이번 지형단 버그와 별개인 기존 동작이다. 위 수정으로 지형 판정 쪽은 이 문제에 영향받지 않게 되지만, 카메라가 시각적으로 맵 밖까지 회전해 나가는 것 자체는 남아있다. 이번 수정 범위에는 포함하지 않고, 원하면 별도 요청으로 처리.

## 요약 / 남은 작업

- 근본 원인은 "지형 단 판정 레이가 카메라 Y에서 출발해서, Y가 바뀌면 판정도 바뀌고 판정이 다시 Y를 바꾸는" 순환 참조. 기존 `tierChangeDebounce`는 노이즈성 흔들림용이라 이 루프는 못 막는다.
- 맵 밖 대처: 판정 원점 X/Z를 기존 `minX/maxX/minZ/maxZ`로 클램프해서, 카메라가 회전 등으로 잠깐 경계 밖에 있어도 지형을 놓치지 않게 한다. `HandleRotate()` 자체의 클램프 누락은 별개 문제로 이번 범위에서 제외.
- 위 수정(판정 레이를 카메라 XZ 기준, 맵 경계로 클램프한 수직 레이로 변경)을 적용할지 확인 필요. 확인되면 `CameraControl.cs`의 `SampleTerrainTier()`를 실제로 수정.

## 추가 요청: 레이가 지형을 못 맞히면 0단이 아니라 직전 단 유지

> 맵 밖으로 나가더라도 layer1 과 같은 판정으로 그대로 있었으면 좋겠어

X/Z를 맵 경계로 클램프해도, 클램프된 지점에 실제 지형 콜라이더가 없으면(맵 경계와 지형 메시 범위가 정확히 안 맞는 경우 등) 레이가 여전히 실패할 수 있다. 기존엔 이때 `return 0`이라 Layer1/2 위에 있다가 갑자기 지상(0단) 취급돼 `targetPosition.y`가 튀었다.

### 변경 코드

```csharp
// 레이가 지형을 못 맞히면(맵 밖 등 지형이 없는 지점) 0단(지상)으로 되돌리지 않고
// 직전까지 확정돼 있던 단을 그대로 유지한다 - 지형이 없다고 갑자기 지상 취급해서
// Y가 튀는 것을 막기 위함.
if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2000f, groundLayer))
    return currentTerrainTier;
```

`currentTerrainTier`(마지막으로 확정된 단)를 그대로 반환해서, 레이 실패가 곧바로 `pendingTerrainTier`와 같아지므로 `HandleTerrainTier()`에서 아무 변화도 일으키지 않는다.

## 변경된 파일

- `Assets/Scripts/Camera/CameraControl.cs`: `SampleTerrainTier()`를 위 "변경 코드 (제안)"대로 수정(화면 중앙 원근 레이 → 카메라 XZ(맵 경계로 클램프) 기준 수직 아래 레이), 레이 실패 시 0단 대신 `currentTerrainTier` 유지로 수정.
