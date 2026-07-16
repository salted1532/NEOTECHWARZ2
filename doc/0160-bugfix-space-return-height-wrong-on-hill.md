# 0160. 버그: 언덕 위에서 Space로 본진 복귀 시 높이가 틀어짐

## 날짜
2026-07-17

## 요청
"현재 발견된 버그는 space바를 눌러서 원래 위치로 카메라 복귀하는거 왜 언덕위일때 현재 씬에선 y25일땐 10으로 돌아오고 그냥 지상일때 y15일땐 15로 잘돌아오는데 왜 언덕은 10으로 돌아오지?"

## 원인
`CameraControl.HandleMovement()`의 Space 처리(`Assets/Scripts/Camera/CameraControl.cs`)는 `targetPosition = mainBasePosition;`으로 위치(높이 포함)를 즉시 되돌리지만, `doc/0158`에서 추가한 `currentTerrainTier`는 그대로 남겨뒀다.

`mainBasePosition`은 `Start()`에서 지형 단 보정이 한 번도 적용되기 전(0단 기준) 값으로 저장됐는데, 언덕(1단) 위에서 Space를 누르면:
1. `targetPosition.y`는 15(0단 기준 본진 높이)로 즉시 리셋됨
2. 하지만 `currentTerrainTier`는 여전히 `1`(언덕)로 남아있음
3. 카메라가 본진으로 부드럽게 이동(Lerp)하는 동안, 화면 중앙 레이가 다시 지형을 훑으면서 `HandleTerrainTier()`가 "언덕(1) → 지상(0)으로 내려갔다"고 판정
4. `targetPosition.y += (0 - 1) * tierZoomStep`, 즉 `15 - 5 = 10`으로 또 한 번 깎임

→ 이미 0단 기준으로 세팅된 높이(15)에서 "1단→0단 하강 보정"이 중복으로 한 번 더 적용되어 10이 되는 것. `currentTerrainTier`가 실제 위치와 어긋난 채로(리셋 안 됨) `HandleTerrainTier()`의 델타 계산이 이어지는 게 근본 원인.

## 코드 변경

**기존 코드**
```csharp
        // Space: 본진(시작 위치)으로 즉시 복귀
        if (Input.GetKeyDown(KeyCode.Space))
        {
            targetPosition = mainBasePosition;
            return;
        }
```

**변경 코드**
```csharp
        // Space: 본진(시작 위치)으로 즉시 복귀
        if (Input.GetKeyDown(KeyCode.Space))
        {
            targetPosition = mainBasePosition;

            // mainBasePosition은 Start()에서 지형 단 보정이 아직 한 번도 적용되기 전(0단 기준) 높이로 저장된 값이다.
            // currentTerrainTier를 그대로 두면(예: 언덕 위에 있다가 Space를 누른 경우 1단으로 남아있음),
            // 복귀하는 도중 화면 중앙이 지형 단을 다시 지나가면서 HandleTerrainTier()가 "1→0으로 내려갔다"고
            // 착각해 이미 0단 기준으로 세팅된 targetPosition.y에서 또 한 번 tierZoomStep을 빼버려 높이가
            // 틀어진다(예: 15로 돌아와야 하는데 10으로 돌아옴). 본진 복귀는 항상 0단 기준이므로 같이 리셋한다.
            currentTerrainTier = 0;
            return;
        }
```

## 요약 / 영향받는 파일
- `Assets/Scripts/Camera/CameraControl.cs` — Space 처리에서 `currentTerrainTier`도 0으로 같이 리셋

## 확인 필요 사항
언덕(1단)/언덕 위 언덕(2단) 각각에서 Space를 눌러 본진 높이(15)로 정확히 돌아오는지 확인 부탁. 복귀 도중(카메라가 아직 언덕 위를 지나는 짧은 구간) 화면 중앙이 잠깐 언덕을 훑으면서 높이가 살짝 출렁일 수는 있으나(카메라가 실제로 그 지형 위를 지나가는 동안 감지되는 정상적인 동작), 최종적으로 멈췄을 때는 15로 안착해야 함.

## 비고
[[confirm_before_implementing]] — 명확한 버그 리포트라 별도 확인 없이 바로 수정.
