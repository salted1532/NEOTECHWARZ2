# 0013. Docs 폴더 세션 기록 규칙 수립

## 날짜
2026-07-07

## 요청
아니 다음 세션부터 가지고 있어야하는 규칙은 내가 한 질문 혹은 명령에 대한 내용을 Docs폴더 안에 md파일로 1~ 해서 차례대로 정리해서 나열해놔 그리고 답변 혹은 변경사항들도 문서로 같이 정리해서 저장해줘 만약 다음 세션부터라서 질문 혹은 명령에 대한 순서를 알수 없으면 Docs폴더를 확인해서 순서를 확인하고 작성하면 돼 지금 오늘 이 세션을 통해서 질문, 명령한거랑 답변, 변경사항들을 정리해서 Docs폴더에 md파일로 작성해줘 이제부터 이 규칙을 계속 지켜줘

## 답변 / 변경사항
- 오늘 세션에서 있었던 모든 질문/명령을 순서대로 `Docs/0001-...md` ~ `Docs/0013-...md`로 정리해서 작성 (본 파일이 그 13번째 항목).
- 파일명 규칙: `Docs/NNNN-<영문-슬러그>.md` (4자리 숫자 접두사, 시간순).
- 각 파일 구성: 날짜, 요청/질문 원문, 답변 및 변경사항 요약, 변경된 파일 목록.
- 앞으로 매 세션마다 사용자의 질문/명령과 그에 대한 답변·변경사항을 이 형식으로 `Docs` 폴더에 계속 이어서 기록하기로 함. 번호는 세션이 바뀌어도 이어지며, 이어서 작성할 때는 `Docs` 폴더를 확인해 마지막 번호 다음부터 계속 사용.
- 이 규칙 자체를 향후 세션에서도 자동으로 지키기 위해 memory(`feedback` 타입)에도 별도로 저장함.

## 변경 파일
- `Docs/0001-claude-version-question.md` (신규)
- `Docs/0002-minimap-click-teleport.md` (신규)
- `Docs/0003-enemy-resource-selection.md` (신규)
- `Docs/0004-resource-gather-marker-flash.md` (신규)
- `Docs/0005-enemy-attack-chase-and-attack-move.md` (신규)
- `Docs/0006-lost-sight-search-mechanism-requested.md` (신규)
- `Docs/0007-revert-search-mechanism.md` (신규)
- `Docs/0008-friendly-fire-attack.md` (신규)
- `Docs/0009-bugfix-missing-reference-exception.md` (신규)
- `Docs/0010-bugfix-focus-fire-ignores-other-enemies.md` (신규)
- `Docs/0011-bugfix-far-target-loses-focus.md` (신규)
- `Docs/0012-conversation-history-question.md` (신규)
- `Docs/0013-docs-session-logging-rule.md` (신규, 본 파일)
