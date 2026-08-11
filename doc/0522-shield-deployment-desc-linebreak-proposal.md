# 0522. 가디언 드론 "실드 전개(Shield Deployment)" 스킬 설명 줄바꿈 추가 - 제안

**날짜:** 2026-08-11

## 요청 내용

> 쉴드 전개 스킬(가디언드론) 의 스킬 설명에 줄바꿈 하나정도는 넣어줘 너무 한문장으로 기네

## 현재 값

`Assets/Resources/Localization/en.json`/`ko.json`의 `trait.nta.9.b.desc` (doc/0516에서 추가한 키):
- ko: `"에너지 실드를 전개하여 150의 추가 체력을 부여합니다."`
- en: `"Deploys an energy shield, granting 150 bonus health."`

다른 트레이트 설명들(같은 doc/0516)과 달리 이 문장은 원래 짧아서 `\n` 없이 1줄로 넣었었는데, 실제
툴팁 폭 기준으로 보니 한 줄로는 길다는 피드백.

## 변경 제안

같은 파일의 다른 트레이트 설명과 동일한 스타일로, 절 경계에서 한 번만 끊음:
- ko: `"에너지 실드를 전개하여\n150의 추가 체력을 부여합니다."`
- en: `"Deploys an energy shield,\ngranting 150 bonus health."`

## 변경 예정 파일
- `Assets/Resources/Localization/en.json`, `ko.json` (`trait.nta.9.b.desc` 값만 수정)

이대로 진행할까요?

---

## 적용 (사용자 승인 후)

> 이대로 진행시켜줘

제안대로 `en.json`/`ko.json`의 `trait.nta.9.b.desc` 값을 수정함.

## 변경된 파일
- `Assets/Resources/Localization/en.json`, `ko.json`
