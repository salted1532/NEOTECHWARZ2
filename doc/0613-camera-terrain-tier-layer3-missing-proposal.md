# 0613. Layer3 태그가 지형 단 판정에서 빠져서 카메라가 내려가는 문제

- 날짜: 2026-08-19
- 상태: 구현 완료 (컴파일 확인, 에러 0 / 기존 경고 49개는 이번 변경과 무관)

## 요청 내용

> layer3은 카메라가 올라가는게 아니라 내려가는거 같은데 layer3은 layer2에서 +5된 값으로 올라가도록 해줘

## 조사 내용

관련 코드: `Assets/Scripts/Camera/CameraControl.cs`의 `SampleTerrainTier()` (Line 189-222).

`ProjectSettings/TagManager.asset`에는 `Layer1`, `Layer2`, `Layer3` 세 태그가 모두 정의돼 있다. 하지만 `SampleTerrainTier()`는 `Layer2`와 `Layer1` 태그만 검사한다:

```csharp
for (Transform t = hit.transform; t != null; t = t.parent)
{
    if (t.CompareTag("Layer2"))
        return 2;
    if (t.CompareTag("Layer1"))
        return 1;
}

return 0;
```

`Layer3` 태그가 붙은 지형에 레이가 닿아도 이 루프의 어느 조건에도 걸리지 않아 결국 `return 0`(지상)으로 떨어진다. 그래서 카메라가 Layer3 지형 위로 올라가면 코드상으로는 "0단(지상)"으로 판정되어, 직전 단(예: Layer2 = 2단)보다 낮은 단으로 확정되고 `HandleTerrainTier()`가 `targetPosition.y`를 오히려 내린다 — 이게 "올라가야 하는데 내려간다"는 증상의 원인이다.

`tierOffset`/`targetPosition.y` 보정은 `tier * tierZoomStep`(기본 `tierZoomStep = 5`) 식으로 이미 일반화돼 있으므로(Line 151, 182), Layer3을 3단으로만 판정하게 해주면 "Layer2(2단, +10) 대비 +5 = 3단(+15)"이 자동으로 성립한다. 별도 산식 추가는 필요 없다.

## 제안하는 수정 (구현 전 — 확인 필요)

`SampleTerrainTier()`에 `Layer3` 검사를 추가한다. 기존 주석의 "Layer2(언덕 위 언덕) > Layer1(언덕) > 태그 없음(지상)" 우선순위 그대로, `Layer3`를 가장 높은 단으로 취급해 맨 앞에 검사한다.

### 기존 코드

```csharp
for (Transform t = hit.transform; t != null; t = t.parent)
{
    if (t.CompareTag("Layer2"))
        return 2;
    if (t.CompareTag("Layer1"))
        return 1;
}

return 0;
```

### 변경 코드 (제안)

```csharp
for (Transform t = hit.transform; t != null; t = t.parent)
{
    if (t.CompareTag("Layer3"))
        return 3;
    if (t.CompareTag("Layer2"))
        return 2;
    if (t.CompareTag("Layer1"))
        return 1;
}

return 0;
```

`currentTerrainTier`/`pendingTerrainTier` 관련 주석(Line 38, 186-188)도 "0=지상, 1=Layer1, 2=Layer2, 3=Layer3"로 같이 갱신.

이렇게 하면:
- Layer3 지형 위에서는 3단으로 확정되어 `targetPosition.y`가 `tierZoomStep`(5) 만큼 더 올라간다 — Layer2(+10) 대비 Layer3(+15)로 정확히 +5 차이.
- 줌 범위(`HandleZoom()`의 `tierOffset`)도 같은 공식을 쓰므로 자동으로 3단 기준 범위로 같이 올라간다.
- 레이가 지형을 못 맞히면 직전 단 유지(`return currentTerrainTier`) 로직은 그대로라 영향 없음.

## 변경된 파일 (예정)

- `Assets/Scripts/Camera/CameraControl.cs`: `SampleTerrainTier()`에 `Layer3` → 3단 분기 추가, 관련 주석 갱신.
