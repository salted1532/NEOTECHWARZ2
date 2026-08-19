# 0626 - 브리핑룸 맵 이미지 연결

## 요청
`Assets/images/Mission/`에 준비해둔 미션0~5, 서브미션1~4 지도 이미지를 Briefing_Room의 해당 인스펙터(`BriefingEntry.mapImage`)에 연결.

## 구현
파일명 규칙(`미션{n}지도.png`, `서브미션{n}지도.png`)으로 10개 전부 매칭해서 `BriefingRoomController.briefingEntries[*].mapImage`에 연결. 텍스처가 Sprite Mode = Multiple로 임포트되어 있어 `AssetDatabase.LoadAssetAtPath<Sprite>`로는 못 찾고, `LoadAllAssetsAtPath`에서 `Sprite` 타입만 골라내는 방식으로 로드.

## 검증
Play Mode로 미션0 재생 - 맵 이미지 패널에 지도가 정상 표시됨을 스크린샷으로 확인.

## 남은 작업 (참고)
같은 폴더(`Assets/images/Mission/`)에 인물 초상화 4장(`아드리안.png`, `셀레나이미지.png`, `부관이미지.png`, `병사이미지.png`)도 이미 있음 - `characterRoster`의 4개 항목(adrian/selena/adjutant/soldier)과 이름이 정확히 대응됨. 이번 요청은 맵 이미지에 한정되어 초상화는 연결하지 않음 - 필요하면 요청.

## 상태
완료.
