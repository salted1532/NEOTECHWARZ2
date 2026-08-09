# 0491 - 유물/연구 데이터 번역(doc/0490) 검증 재개

## 요청 내용
"유물 + 연구데이터 번역 작업이랑 해당 설명 관련된거 중단되었던거 다시 진행해줘"

## 조사 내용
`git status`/`git diff`로 확인한 결과, doc/0490에서 제안했던 코드 변경(`MissionItem.cs`의
`itemID`/`description`/`GetDescription()` 추가, `RTSUnitController.cs:2087`의 `ShowInfoPanel` 호출에
설명 인자 추가, `Artifact.prefab`/`Database.prefab`의 `itemID`+`description` 인스펙터 값, `ko.json`/
`en.json`의 `missionitem.*` 키 4개)는 이미 작업 디렉토리에 doc/0490 제안 그대로 전부 적용되어 있었음
(커밋 전 unstaged 상태). 즉 코드 변경 자체는 중단된 게 아니라 완료돼 있었고, doc/0490 마지막 "확인
예정" 절의 검증만 안 된 채로 남아있던 상태.

## 진행한 검증
1. **JSON 키 일치성** (Node 스크립트로 ko.json/en.json 파싱): 양쪽 177개 키, 중복 없음, 키셋 완전
   일치 (`missionitem.artifact.name/.desc`, `missionitem.researchdata.name/.desc` 4개 모두 양쪽에
   존재).
2. **컴파일** (`uloop-cli compile`): `Success: true`, `ErrorCount: 0`, `WarningCount: 0`.
3. **런타임 동작 확인** (`uloop-cli execute-dynamic-code`로 Editor 내에서 `LocalizationManager`를
   직접 생성해 `Awake`/`LoadLanguage`를 리플렉션 호출한 뒤, `Artifact.prefab`/`Database.prefab`의
   `MissionItem` 컴포넌트에서 `GetItemName()`/`GetDescription()` 직접 호출):
   - EN: `Artifact` / "A mysterious artifact believed to be an alien race's energy source,
     radiating an otherworldly aura." / `Research Database` / "Research data on a new weapon the
     OC is developing."
   - KO: `외계 유물` / "외계종족의 에너지원으로 추정되는 신비한 유물이다. 정체를 알 수 없는 기운이
     감돈다." / `OC 연구 데이터` / "OC가 연구 중인 신형 무기에 대한 연구 데이터다."
   - 안전장치: 존재하지 않는 키(`missionitem.doesnotexist.name`)로 `GetTextOrFallback` 호출 시
     전달한 폴백 문자열이 그대로 반환됨 확인 (doc/0487 안전장치 패턴 정상 동작).

## 코드 변경
없음 (doc/0490에서 이미 적용된 변경을 검증만 함).

## 요약
doc/0490 제안 내용이 이미 프로젝트에 반영돼 있었고, 이번 세션에서 JSON 키 정합성·컴파일·런타임
번역/폴백 동작을 모두 확인해 doc/0490의 "확인 예정" 항목을 마무리함. 남은 건 실제 Play Mode에서
미션 오브젝트를 클릭해 Info Panel에 이름+설명이 시각적으로 잘 뜨는지 눈으로 보는 것 정도(선택 사항,
로직 자체는 이번 검증으로 확인됨).

## 영향받는 파일
없음 (검증만 수행, 파일 변경 없음)
