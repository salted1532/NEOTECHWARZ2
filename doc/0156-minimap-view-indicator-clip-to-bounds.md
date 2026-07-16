# 0156. 미니맵 시야 사각형을 미니맵 이미지 안으로 잘라내기

## 날짜
2026-07-17

## 요청
"이제 미니맵 이미지 안에서만 보이도록 해줄래 지금은 미니맵 이미지 밖으로도 나와서 보이는데 나오게 되면 그냥 짤려서 보이도록"

## 조사
`MinimapViewIndicator.Update()`(`Assets/Scripts/Camera/MinimapViewIndicator.cs`)가 계산한 시야 사각형의 바운딩 박스(`minX/maxX/minY/maxY`)를 그대로 `sizeDelta`/`anchoredPosition`에 반영하고 있어서, 메인 카메라가 맵 경계 근처로 이동하면 사각형이 `minimapRect`(=`MiniMap_image`) 범위를 넘어 미니맵 이미지 바깥까지 그려지고 있었다.

`Mask`/`RectMask2D` 같은 별도 마스킹 컴포넌트를 추가하는 대신, 이미 계산해둔 바운딩 박스를 `minimapRect.rect` 범위로 `Clamp`하는 방식을 택했다 — 엔진 내장 UI 컴포넌트를 씬 YAML에 직접 추가하려면 정확한 스크립트 GUID를 알아야 하는데 확신 없이 손으로 적으면 "Missing Script"로 깨질 위험이 있어서, 이미 검증된 코드 경로(스크립트 내 계산)만으로 해결.

## 코드 변경

**기존 코드** (`Assets/Scripts/Camera/MinimapViewIndicator.cs`)
```csharp
        viewIndicator.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        viewIndicator.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
```

**변경 코드**
```csharp
        // 미니맵 이미지 밖으로 나가는 부분은 그리지 않도록 사각형을 미니맵 rect 안으로 잘라낸다.
        minX = Mathf.Clamp(minX, rect.xMin, rect.xMax);
        maxX = Mathf.Clamp(maxX, rect.xMin, rect.xMax);
        minY = Mathf.Clamp(minY, rect.yMin, rect.yMax);
        maxY = Mathf.Clamp(maxY, rect.yMin, rect.yMax);

        viewIndicator.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        viewIndicator.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
```

시야 사각형이 미니맵 범위를 벗어나면 벗어난 쪽 경계가 `rect.xMin/xMax/yMin/yMax`로 눌려서, 미니맵 밖으로 나가는 부분 없이 잘린 채로 보인다.

## 요약 / 영향받는 파일
- `Assets/Scripts/Camera/MinimapViewIndicator.cs` (수정)

## 비고
[[confirm_before_implementing]] — `doc/0155`에서 이미 승인된 미니맵 시야 사각형 기능의 후속 보정(범위 클램프)으로, 별도 확인 없이 바로 적용.
