# 0637 - 보급고 Info 패널 영어 하드코딩 수정

## 문의

> 보급고 건물 설명 한글 번역 안되어있음. 한글 번역에도 영어로 나오네 확인좀

## 원인 조사

보급고(Supply Depot)를 선택했을 때 Info 패널에 뜨는 설명 텍스트는 다른 건물과
다른 경로를 탄다.

- 다른 NTA 건물: `BuildingController.GetDescription()` → `infoDescription`
  필드. 이 필드는 SO에 비어있고, 로컬라이제이션 키(`building.nta.{ID}.info`)도
  없어서 그냥 빈 문자열을 반환한다 (표시할 게 없어서 비워둔 의도된 상태,
  doc/0479).
- **보급고만 예외**: `RTSUnitController.GetBuildingInfoDescription()`
  (`Assets/Scripts/System/RTSUnitController.cs:1731-1739`)이 정적 텍스트 대신
  현재 인구수를 매 프레임 계산해서 보여준다. 이때 텍스트가 로컬라이제이션
  시스템을 거치지 않고 **C# 코드에 영어로 하드코딩**되어 있다:

```csharp
return $"Current Population : {GetPopulation()}/{GetMaxPopulation()}\nPopulation Capacity Added : +{populationAdded}";
```

`LocalizationManager.GetText/GetTextOrFallback`을 전혀 호출하지 않으므로 언어
설정(한/영)과 무관하게 항상 영어로 나온다. 이게 사용자가 보고한 증상의
원인이다 — 보급고의 생산 버튼 툴팁(`building.nta.2.desc`)이나 이름
(`building.nta.2.name`)은 이미 en.json/ko.json에 정상 번역되어 있고 문제
없음. 문제는 오직 Info 패널의 인구수 설명 한 줄.

## 수정 제안

1. `en.json` / `ko.json`에 포맷 문자열 키 추가:

   ```json
   { "key": "building.nta.2.populationInfo", "value": "Current Population : {0}/{1}\nPopulation Capacity Added : +{2}" }
   ```
   ```json
   { "key": "building.nta.2.populationInfo", "value": "현재 인구 : {0}/{1}\n증가하는 인구 수용량 : +{2}" }
   ```

2. `RTSUnitController.cs:1738`을 아래로 교체 (기존 `LocalizationManager.GetText(key, args)` 오버로드가 이미 `string.Format`을 지원하므로 그대로 사용):

   ```csharp
   return LocalizationManager.GetText("building.nta.2.populationInfo", GetPopulation(), GetMaxPopulation(), populationAdded);
   ```

## 영향 범위

- `Assets/Resources/Localization/en.json`, `ko.json`: 키 1개씩 추가.
- `Assets/Scripts/System/RTSUnitController.cs`: 1738번 줄 한 줄 교체.
- 다른 건물 Info 패널 텍스트는 건드리지 않음 (보급고만 해당하는 특수 경로).

## 적용 결과

사용자 승인 후 위 3개 파일(en.json, ko.json, RTSUnitController.cs)을 제안대로
그대로 수정. `npx uloop-cli compile` 결과 `Success: true, ErrorCount: 0` 확인
(WarningCount 49는 전부 이 변경과 무관한 기존 경고, `FindFirstObjectByType`
obsolete 경고 등).
