# 0495 - 스포어 브루드(외계 종족) doc/0441 → Docs/ 정식 등록

## 요청 내용
"외계 종족에 대한 0441 문서를 확인해서 Docs 문서에 정식으로 등록해줘"

## 조사 내용
- `doc/0441`(외계 몬스터 종족 3유닛/3건물 디자인 제안, "스포어 브루드(Spore Brood)")이 실제로
  구현됐는지 SO 에셋을 확인함:
  - `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset` - 립팽(ID 10)/스피터(ID
    11)/스키터윙(ID 12) 스탯이 doc/0441 제안값과 완전히 일치.
  - `Assets/Scripts/ScriptableObject/Data/Spore Brood Building Data SO.asset` - 하이브 코어(ID
    7)/산란구덩이(ID 8)/바이오리액터(ID 9)도 마찬가지로 제안값과 완전히 일치.
  - 프리팹(`Assets/prefabs/Spore_Brood/Unit`, `.../Building`)과 로컬라이제이션 키(`unit.oc.10~12`,
    `building.oc.7~9`, ko/en 둘 다)도 이미 존재 - 즉 doc/0441은 제안에 그치지 않고 실제로 게임에
    반영된 상태였음.
  - `Docs/Campaign.md` 확인 결과 "OC"는 오메가 코퍼레이션(인간 진영)이고, 2~5막에서 NTA·OC 양쪽을
    공격하는 "외계종족"이 곧 스포어 브루드 - 로컬라이제이션/SO의 `oc` 네임스페이스는 오메가
    코퍼레이션과 외계종족(적 진영 전체)을 함께 묶어 쓰고 있음(기존 `EnemyUnitDataSO`/
    `EnemyBuildingDataSO` 재사용 구조).
  - doc/0441이 "남은 작업"으로 남겼던 항목 중 **하이브 코어 자연 재생**, **바이오리액터 파괴
    페널티**는 코드베이스 전체에서 관련 로직(`regen` 등)이 검색되지 않아 여전히 미구현. **스테이지
    배치 여부**도 `Assets/Scripts/System/Stage*.cs`에 참조가 없어 확인되지 않음.

## 변경 사항
- **신규**: `Docs/SporeBrood.md` - 스포어 브루드 컨셉 + 유닛 3종/건물 3종 실측 스탯(SO 에셋 기준)을
  기존 `Docs/EnemyUnitAndBuildingStats.md`와 동일한 양식으로 정리한 정식 레퍼런스 문서. 구현
  상태(완료/미구현) 절을 추가해 doc/0441의 "남은 작업" 중 아직 안 끝난 항목이 뭔지도 명시.
- **수정**: `README.md`의 "유닛/건물 수치 문서" 목록에 `Docs/SporeBrood.md` 링크 추가.

## 요약
doc/0441의 스포어 브루드 설계가 이미 SO 에셋·프리팹·로컬라이제이션까지 전부 구현되어 있음을 확인하고,
그 결과를 `Docs/SporeBrood.md`로 정식 등록함. doc/0441(세션 로그, 요청 시점의 제안 과정)과
Docs/SporeBrood.md(현재 구현 상태 기준 영구 레퍼런스)의 역할이 겹치지 않게 분리했고, 자연 재생/파괴
페널티/스테이지 배치처럼 아직 안 끝난 부분은 문서에 명시해 다음 작업으로 넘길 수 있게 함.

## 변경된 파일
- `Docs/SporeBrood.md` (신규)
- `README.md` (Docs 목록에 링크 추가)
