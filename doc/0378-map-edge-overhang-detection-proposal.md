# 0378 - 같은 방식으로 맵 바깥쪽 난간(지형 없는 구간) 돌출도 감지

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개, 경고 33개(기존과 동일 - 새로 생긴 경고 없음).

## 요청 내용

> 언덕 벽면 건설도 막혔고 언덕난간에 건설하는것도 잘 막혔네. 맵 바깥쪽으로의 난간이라고 해야하나
> 맵 바깥에는 미세하게 뛰어나온 부분이 생기는데 이것도 이 방법대로 적용시킬수 있는지 확인해줘

## 조사 결과

- [[0377]]에서 만든 `IsFootprintTerrainFlat()`(`PlacementSystem.cs:501`)의 안쪽 레이캐스트 루프를
  보면, 어떤 표본 지점이 지형에 아예 안 맞으면(`Physics.Raycast`가 실패하면) `continue`로 그냥
  건너뛴다 - "지형이 없는 지점(맵 밖 등)은 다른 검사에서 이미 걸러짐"이라고 가정했었음.
- 그런데 언덕 절벽과 달리 **맵 바깥은 그 아래에 "다른 높이의 지형"이 있는 게 아니라 아예 지형 자체가
  없음(허공)**. 그래서 풋프린트 일부가 맵 밖으로 살짝 걸쳐도, 그 표본 지점들은 `continue`로 그냥
  무시되고 min/max 계산에 전혀 반영되지 않음 → 나머지 표본(맵 안쪽, 전부 같은 높이)만으로 높이차를
  계산하니 "평평하다"고 판정돼서 통과해버림. 사용자가 관찰한 "맵 바깥에 미세하게 뛰어나온 부분"이
  바로 이 케이스.
- 더 심한 부작용도 있음: 만약 풋프린트 표본이 **전부** 지형을 못 맞히면(건물이 완전히 맵 밖으로
  나가는 극단적인 경우) `min`은 초기값 `float.MaxValue`, `max`는 `float.MinValue`인 채로 남아서
  `(max - min)`이 거대한 음수가 되어 `<= maxFootprintHeightVariance` 조건을 항상 통과함 - 즉 이론상
  건물이 완전히 지형 밖에 떠 있어도 이 함수만 보면 "평평함(통과)"로 나오는 잠재 버그였음(다른 검사가
  우연히 막아줬을 수는 있지만 이 함수 자체의 논리는 틀려 있었음).

## 코드 변경 (제안)

같은 방법(레이캐스트 높이 스캔)을 그대로 쓰되, "표본 지점에서 지형을 못 맞힘" 자체를 절벽과 동일하게
"배치 불가 사유"로 취급하도록 한 줄만 바꾼다 - `continue`(무시) 대신 그 자리에서 바로 `false`를 반환.

기존 코드 (`PlacementSystem.cs:521~522`):
```csharp
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, inputManager.PlacementLayerMask))
                    continue; // 지형이 없는 지점(맵 밖 등)은 다른 검사에서 이미 걸러짐
```

변경 코드:
```csharp
                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 2000f, inputManager.PlacementLayerMask))
                    return false; // 풋프린트 안에 지형이 없는 지점(맵 바깥으로 걸침 등)이 있으면 그 자체로 배치 불가 (doc/0378)
```

주석도 함수 상단 설명에 맞춰 갱신 (선택):
```csharp
    // 건물 풋프린트 전체를 terrainSampleStep 간격의 격자로 촘촘히 스캔해(칸 경계와 무관), 지면 높이
    // 최고-최저 차이가 maxFootprintHeightVariance를 넘거나(절벽/벽면) 표본 지점에 지형 자체가 없으면
    // (맵 바깥 등, doc/0378) 배치를 막는다. 칸 중앙 1점만 재던 이전 방식(doc/0376)은 절벽이 칸 중앙을
    // 피해가면 놓치는 문제가 있어 (doc/0377) 교체.
```

## 영향받는 파일 (예정)

- `Assets/Scripts/BuildSystem/PlacementSystem.cs`
