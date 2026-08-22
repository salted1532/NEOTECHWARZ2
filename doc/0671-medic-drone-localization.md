# 0671 - 메딕 드론 이름/설명 번역 추가

## 요청
> 메딕 드론 설명이랑 이름도 번역 해줘

## 원인
`LocalizationManager`는 `Resources/Localization/{en,ko}.json`에서 `unit.nta.{ID}.name` /
`unit.nta.{ID}.desc`(생산 버튼 툴팁) / `unit.nta.{ID}.info`(정보 패널 설명) 키를 찾고, 키가 없으면
`ScriptableObject`(`NTA Unit Data SO.asset`)에 적힌 원문(영어)을 그대로 보여준다
(`GetTextOrFallback`, doc/0487). 메딕 드론(`ID: 10`)은 다른 유닛(0~9)과 달리 이 세 키가 두 JSON
파일 모두에 아예 없어서, 언어를 한국어로 바꿔도 항상 SO 원문(영어)이 표시되고 있었다.

## 수정
`unit.nta.9.*`(가디언 드론) 바로 뒤에 `unit.nta.10.*` 3줄을 형식 그대로 추가 - 소스는 SO의
`unitName`("Medic Drone "), `infoDescription`("Unarmed repair drone. Heals nearby allied units at
range."), 생산 버튼 문구는 형제 유닛들과 동일한 "Train {이름}.\nshortcut key [...]" 관례에 맞춰
정리(SO의 `description` 필드 자체가 소문자 "train"/이중 줄바꿈 등 형제 유닛과 살짝 다르게
적혀있었는데, 키가 생기면 SO 원문은 더 이상 안 쓰이므로 다른 유닛과 같은 형식으로 새로 작성).

`en.json`:
```json
{ "key": "unit.nta.10.name", "value": "Medic Drone" },
{ "key": "unit.nta.10.desc", "value": "Train Medic Drone.\nshortcut key [<color=yellow>M</color>]" },
{ "key": "unit.nta.10.info", "value": "Unarmed repair drone. Heals nearby allied units at range." },
```

`ko.json`:
```json
{ "key": "unit.nta.10.name", "value": "메딕 드론" },
{ "key": "unit.nta.10.desc", "value": "메딕 드론 생산.\n단축키 [<color=yellow>M</color>]" },
{ "key": "unit.nta.10.info", "value": "비무장 수리 드론. 사거리 내 아군 유닛을 치유합니다." },
```

## 결과
- 생산 버튼 툴팁, 정보 패널 유닛명/설명 전부 언어 설정에 따라 정상적으로 한국어/영어로 표시됨.
- 코드 변경 없음(데이터 파일만 추가) - `node -e "JSON.parse(...)"`로 두 파일 모두 문법 검증 완료.
