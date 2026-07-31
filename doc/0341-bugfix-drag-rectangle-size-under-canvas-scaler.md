# 0341. 해상도 변경 후 드래그 선택 박스가 실제 드래그 범위보다 크게/왜곡되어 보이는 문제 수정

**날짜:** 2026-07-31

## 요청

> 내가 QHD라서 빌드해서 테스트해봤는데 그 DragRectangle 이것도 해상드 크기에 맞춰서 크기가 커지다
> 보니깐 내가 클릭한 부분부터 놓는 위치까지가 지정되어야하는데 더 크게 나타나거나 좀 일그러져서
> 나타나내 이거 확인하고 수정해줘

## 원인

바로 이전 세션([[0340]])에서 캔버스를 `Constant Pixel Size` → `Scale With Screen Size`(기준
해상도 1920×1080)로 바꾼 게 원인. `UserControl.DrawDragRectangle()`은:

```csharp
dragRectangle.position = (start + end) * 0.5f;
dragRectangle.sizeDelta = new Vector2(Mathf.Abs(start.x - end.x), Mathf.Abs(start.y - end.y));
```

`start`/`end`는 `Input.mousePosition`(실제 화면 픽셀 좌표). Overlay 캔버스에서 `RectTransform.position`
(월드 좌표)은 캔버스 배율과 무관하게 항상 실제 화면 픽셀과 1:1로 맞도록 유니티가 알아서 보정해주지만,
**`sizeDelta`(로컬/디자인 단위)는 그렇지 않다** — 렌더링될 때 `CanvasScaler`가 계산한 배율만큼
그대로 곱해져서 화면에 그려진다.

`Scale With Screen Size`를 켜기 전(배율이 항상 1로 고정)에는 "화면 픽셀 좌표 차이 = sizeDelta"가
우연히 맞아떨어졌지만, 이제는 실행 해상도가 기준 해상도(1920×1080)보다 큰 화면(QHD 2560×1440 등)에서
배율이 1보다 커지므로, `sizeDelta`에 그대로 넣은 "화면 픽셀 차이"가 배율만큼 한 번 더 곱해져서
실제 드래그한 범위보다 박스가 더 크게(배율만큼) 그려짐 — 사용자가 보고한 증상과 정확히 일치.

## 수정

`UserControl.cs`:
- `dragRectangleCanvas` 필드 추가, `Awake()`에서 `dragRectangle.GetComponentInParent<Canvas>()`로
  캐싱.
- `DrawDragRectangle()`에서 `sizeDelta` 계산 시 `dragRectangleCanvas.scaleFactor`로 나눠서, 렌더링
  시 다시 곱해지는 배율을 상쇄 — 최종적으로 실제 마우스 드래그 픽셀 거리와 화면에 그려지는 박스 크기가
  해상도/배율과 무관하게 항상 일치하도록 함.

## 추가 점검

같은 문제(원시 `Input.mousePosition` 픽셀 값을 캔버스 로컬 단위인 `sizeDelta`/`anchoredPosition`에
직접 대입)가 다른 곳에도 있는지 `sizeDelta`/`anchoredPosition`을 쓰는 나머지 파일(`UIController.cs`,
`TooltipUI.cs`, `TooltipContentFitter.cs`, `MinimapViewIndicator.cs`)을 전부 확인함 — 전부
`RectTransformUtility.ScreenPointToLocalPointInRectangle`로 이미 올바르게 변환하고 있거나
(`UIController`/`TooltipUI`), 애초에 화면 픽셀을 전혀 안 쓰고 다른 RectTransform의 `rect` 기준으로만
계산해서(`MinimapViewIndicator`) 문제 없음. `DragRectangle`이 유일한 예외였음.

`npx uloop-cli compile`/`get-logs`: 에러 0개 확인.

## 영향받는 파일

- `Assets/Scripts/UserControl/UserControl.cs`
