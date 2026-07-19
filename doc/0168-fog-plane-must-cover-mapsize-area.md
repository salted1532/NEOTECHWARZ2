# 0168 - 안개 Plane이 맵 전체를 덮어야 하는지 질문

## 질문

"plane을 그럼 맵 전체를 덮어야 하나?" — [[fogofwar-eye-scripts-implementation]](0167)에서 생성한
`FogOfWarManager`용 3D 안개 Plane을 씬에 설치할 때, 지형 전체를 덮어야 하는지에 대한 질문.

## 답변

정확히는 "지형 전체"가 아니라 `FogOfWarManager.mapOrigin`/`mapSize`로 정의한 사각형 영역과
**정확히 일치**해야 한다고 답했다.

근거: 기본 Unity `Plane`의 UV는 표면 전체에 0~1로 깔리고, `FogOfWarManager.WorldToCell()`(FogOfWarManager.cs)도
월드 좌표를 같은 `mapOrigin`/`mapSize` 기준 0~1 비율로 변환해 텍스처 픽셀에 매핑한다. 두 쪽이 같은
사각형을 기준으로 삼지 않으면(Plane이 더 작거나 위치가 어긋나면) 화면에 보이는 안개와 실제 시야 판정
(`IsVisible`) 좌표가 어긋나는 불일치가 생긴다.

- 지형 메시 자체가 `mapSize`보다 넓어도 무방(카메라가 못 가는 배경 지형 등) — Plane은 `mapOrigin`~
  `mapOrigin+mapSize` 사각형만 덮으면 된다.
- 위치/스케일 공식(0166 문서 7절과 동일): 중심 `(mapOrigin.x + mapSize.x/2, 0.3, mapOrigin.y + mapSize.y/2)`,
  `localScale.x = mapSize.x/10`, `localScale.z = mapSize.y/10`(기본 Plane이 10×10 유닛 기준).
- 현재 `mapOrigin=(-130,-130)`/`mapSize=(170,170)` 기본값은 `CameraControl`의 이동 제한값을 가져다 쓴
  추정치라, 실제 지면 크기와 맞는지 씬에서 확인 후 조정 필요.

## 변경 사항

없음(질문 답변만, 코드/씬 변경 없음).
