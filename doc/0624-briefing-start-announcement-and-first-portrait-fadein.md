# 0624 - 브리핑 시작 안내문 + 첫 화자 페이드인

## 요청
브리핑 시작할 때(1초 대기 후) "브리핑 시작."이 먼저 나오고, 첫 대사 인물 이미지도 다른 인물들과 동일하게 페이드인 효과로 등장하도록.

## 구현
- `PlayDialogue()` 맨 앞에서 `TypeAnnouncement(log, "briefing.start")`를 호출해 "브리핑 시작."을 대사 로그에 먼저 타이핑 - "브리핑 끝."과 동일한 방식(화자 없음, 색상 없음).
- 기존 "브리핑 끝." 타이핑 코드를 `TypeAnnouncement(StringBuilder log, string localizationKey)` 공용 코루틴으로 뽑아서 시작/종료 문구 둘 다 재사용.
- `StartBriefingAfterDelay()`에서 첫 화자를 "대기 없이 즉시" 보여주던 특례 코드를 제거 - 이제 첫 화자도 `PlayDialogue()` 루프의 `RevealPortraitIfNeeded()`를 그대로 타면서 다른 인물과 동일하게 0→1 페이드인됨.
- `briefing.start` 로컬라이제이션 키 추가 (ko "브리핑 시작.", en "Briefing start.").

## 검증
컴파일 성공. Play Mode로 미션0 재생 - "브리핑 시작." 이 대사보다 먼저 출력되고, 아드리안 초상화도 페이드인으로 등장하는 것 스크린샷으로 확인.

## 상태
완료.
