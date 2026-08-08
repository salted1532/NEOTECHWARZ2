# 0479 - Info Panel 설명 다듬기 (건물 대기열 예외, 유닛 설명 축약, 보급고 인구수 표시)

## 요청 내용
"대기열이랑 같이 켜지는 건물에 경우는 설명 비워두어도 되고 유닛에 경우 설명을 좀더 간략하게
적어줬으면 좋겠고 보급고 설명에는 현재인구수: 숫자 보급고가 늘려주는 인구수: 8 뭐 이런식으로
인구수 관련된 정보가 표시되도록해줘"

## 변경 내용

### 1. 대기열이 같이 뜨는 NTA 건물은 infoDescription을 비움
`RTSUnitController.UpdateUI()`의 `BuildingSelect` 분기를 보면 CommandCenter/Barracks(Tier1)/
Factory(Tier2)/Spaceport(Tier3)는 `ShowProductionUI`(생산 대기열), Lab은 `ShowResearchUI`(연구
대기열)가 Info Panel과 함께 뜬다 — 이 5개는 `NTA Building Data SO.asset`의 `infoDescription`을
빈 문자열로 비움. SupplyDepot만 대기열이 없어서 아래 3번 항목으로 대체.
(OC/스포어 브루드 건물은 애초에 대기열 UI 자체가 없어서 이번 변경 대상 아님 - 기존 설명 그대로 둠)

### 2. 유닛 설명 축약
NTA/OC/스포어 브루드 유닛 21개 전부 `infoDescription`을 한 문장 → 짧은 구절로 줄임. 예:
"Versatile drone that gathers minerals and gas and constructs new buildings." → "Gathers minerals
and gas; builds structures."

### 3. 보급고 설명은 실시간 인구수로 대체
`RTSUnitController.cs`에 `GetBuildingInfoDescription(BuildingController building)` 추가 - 선택된
건물이 SupplyDepot(`BuildingID.SupplyDepot`)이면 SO의 정적 설명 대신
`"Current Population : {현재}/{최대}\nPopulation Capacity Added : +{이 건물이 늘려주는 인구수(8)}"`를
매번 새로 계산해서 보여줌. `UpdateUI()`가 매 프레임 도는 루프 안에서 호출되므로 인구수가 바뀌면
자동으로 갱신됨. SupplyDepot 외 건물은 기존처럼 `building.GetDescription()`(SO의 infoDescription,
대기열 있는 건물은 비어있음) 그대로 사용.

컴파일 확인 완료(에러 0).
