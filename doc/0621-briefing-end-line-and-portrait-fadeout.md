# 0621 - 브리핑 종료 문구 + 인물 이미지 페이드아웃

## 요청
각 미션 브리핑 대사 끝에 "브리핑 끝." 같은 문구가 나오고, 그 다음 인물 이미지가 꺼지도록. (사용자 추가 지시: 대사 텍스트 로그 자체는 지우지 말고 남겨둘 것.)

## 구현
- `BriefingRoomController.PlayDialogue()`: `entry.lines` 재생이 끝나면 `briefing.end` 키("브리핑 끝." / "End of briefing.")를 같은 타이프라이터 방식(로그에 이어 붙임, 색상 없음)으로 출력. 다 끝나면 `pauseBetweenLines` 만큼 대기 후 `FadeOutAllPortraits()` 호출.
- `FadeOutAllPortraits()`: 3개 초상화 슬롯 전부(비어있는 슬롯은 이미 alpha 0이라 사실상 no-op) `FadeTo(image, 0f, portraitFadeDuration)`로 페이드아웃.
- 기존 `FadeIn`을 `FadeTo(image, targetAlpha, duration)`로 일반화해서 등장(0→1)/퇴장(1→0) 양쪽에 재사용 (`RevealPortraitIfNeeded`도 이걸 씀).
- `briefing.end` 로컬라이제이션 키를 ko.json/en.json에 추가 (짧은 고정 문구라 placeholder 없이 바로 번역).
- 대사 텍스트(`dialogueText`)는 건드리지 않아 로그 그대로 남음 - 페이드아웃 대상은 인물 이미지뿐.

## 검증
컴파일 성공. Play Mode로 미션0 재생 - 3줄 대사 후 "브리핑 끝." 타이핑되고, 이어서 아드리안/부관 초상화가 페이드아웃되는 것을 스크린샷으로 확인. 텍스트 로그는 유지됨.

## 상태
완료.
