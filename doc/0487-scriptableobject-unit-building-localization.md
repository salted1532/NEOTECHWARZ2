# 0487 - 유닛/건물 ScriptableObject 이름·설명 로컬라이제이션 + 안전장치

## 요청 내용
"이제 스크립터블오브젝트의 텍스트들을 번역 작업을 진행했으면 좋겠어 이름, 설명 등을 영어, 한글
버전으로 하고 만약 오류가 발생할수 있으니깐 오류일때는 그냥 원래 스크립터블 오브젝트 내용이
출력 되도록 안전장치까지 두면 좋을거 같네" - doc/0481 진행 당시 제안했던 SO 로컬라이제이션
확장을 실제로 진행. 추가 요구사항: 번역 조회가 실패해도(키 없음/매니저 없음/예외) 항상 SO에
원래 적혀있던 내용이 그대로 출력되도록 안전장치.

## 안전장치: `LocalizationManager.GetTextOrFallback(key, fallback)`
기존 `GetText(key)`는 키가 없으면 "키 문자열"을 그대로 보여줘서 번역 누락을 눈에 띄게 하는
용도였는데(정적 UI 라벨용), SO 데이터는 그 대신 **원본 SO 값**이 나와야 하므로 별도 메서드 추가:

```csharp
public string GetOrFallback(string key, string fallback) =>
    strings.TryGetValue(key, out string value) ? value : fallback;

public static string GetTextOrFallback(string key, string fallback)
{
    try { return Instance != null ? Instance.GetOrFallback(key, fallback) : fallback; }
    catch { return fallback; }
}
```
`Instance`가 없거나(매니저 없는 씬), 키가 JSON에 없거나, 조회 중 예외가 나는 모든 경우에 `fallback`
(호출부에서 넘긴 원래 `data.xxx` 값)을 그대로 반환 - 어떤 상황에서도 화면에 빈 값이나 키 문자열이
아니라 항상 뭔가 읽을 수 있는 텍스트가 뜬다.

## 키 스킴
`unit.<faction>.<ID>.name` / `.desc` / `.info`, `building.<faction>.<ID>.name` / `.desc` / `.info`
(faction = `nta` 또는 `oc`). `desc`=생산/건설 버튼 툴팁 설명, `info`=Info Panel 설명.

NTA(플레이어)와 OC(적) 양쪽 다 같은 `UnitData`/`BuildingData` 클래스를 재사용하므로(진영별로 SO
에셋만 다름, doc/0230) ID가 진영마다 독립적으로 겹칠 수 있어 faction을 키에 포함시켰다.
Spore Brood(스포어 브루드)는 `EnemyUnitDataSO`/`EnemyBuildingDataSO`를 OC와 동일 타입으로 쓰고,
`RTSUnitController.GetEnemyUnitData/GetEnemyBuildingData`가 OC 테이블에 없으면 자동으로 Spore
Brood 테이블에서 찾아오도록 이미 합쳐져 있어서(doc/0444) 호출부는 어느 쪽 데이터인지 구분할 수
없다 - 그래서 Spore Brood도 그냥 `oc` 네임스페이스를 같이 쓴다(ID 범위가 겹치지 않아 충돌 없음).

**OC/Spore Brood의 `.desc`(생산 버튼 설명)는 만들지 않음** - `description` 필드를 실제로 읽는
곳은 `RTSUnitController.UnitButtonAction`/`BuildingButtonAction`뿐인데 이건 항상 NTA(플레이어가
직접 생산 가능한) 데이터베이스만 조회하므로, OC/Spore Brood 쪽 `description`은 애초에 화면에
출력된 적이 없는 죽은 데이터임 - 번역 대상에서 제외.

## 코드 변경 (조회 지점에 안전장치 적용)
- `RTSUnitController.GetUnitName`/`GetBuildingName` - NTA 이름 조회(Info Panel 등에서 사용).
- `RTSUnitController.UnitButtonAction`/`BuildingButtonAction` - 생산/건설 버튼 제목 + 설명
  (설명이 비어있지 않을 때 원문을 그대로 쓰던 부분을 번역 조회로 교체).
- `UIController.GetUnitDisplayName` - Squad panel 유닛 이름.
- `BuildingController` (NTA, Info Panel `infoDescription`).
- `EnemyBuildingController.ApplyBuildingData` (OC, `infoDescription`/`buildingName`).
- `EnemyUnitController.ApplyUnitData` (OC, `infoDescription`/`enemyName`).
- `AllyController.ApplyUnitData` (구조된 아군 OC 유닛, `infoDescription`/`enemyName`).
- `UnitController.ApplyUnitData(UnitData data, string faction = "nta")` - NTA/OC 둘 다 처리하는
  유일한 메서드라 `faction` 매개변수 추가. 호출부(`Awake()`)에서 `enemyDataUnitID > 0`이면
  `"oc"`를 넘긴다(doc/0458의 "구조 가능한 OC 유닛" 경로). 같은 곳의 `heroName` OC 이름 폴백도
  번역 대상에 포함.

## 번역 데이터
6개 SO 에셋(`NTA Unit/Building Data SO`, `OC Unit/Building Data SO`,
`Spore Brood Unit/Building Data SO`)의 실제 내용을 스크립트로 전부 덤프해서 확인 후 번역:

- NTA 유닛 9종 - 이름/설명/Info 이미 영문이었음(일부 trailing space 정리). 한글 번역 추가.
  설명은 "Train {이름}.\n단축키 [{키}]" 패턴(`unit.trainfallback`과 동일한 스타일)로 통일,
  `Pulasr Tank`는 `Pulsar Tank` 오타 교정.
- NTA 건물 6종 - 이름/설명 동일한 패턴. `infoDescription`은 전부 비어있음(대기열 패널이 같이
  뜨는 건물이라 doc/0479 정책대로 의도적으로 빈 값) - 번역 키 추가 안 함(빈 문자열은 그대로 안전).
- OC 유닛 9종 - 이름/Info는 이미 영문이었음, 한글 번역 추가.
- Spore Brood 유닛 3종 - 이름 필드가 "립팽 (Ripfang)"처럼 한/영이 한 문자열에 섞여 있던 것을
  분리: EN="Ripfang", KO="립팽" 식으로 각 언어에 맞는 이름만 나오도록 정리. Info는 원래 영문.
- OC/Spore Brood 건물 9종 - 유닛과 동일한 패턴.

en.json/ko.json에 총 81개 키 추가(각 파일 167개), Node 스크립트로 일괄 삽입 후 키 집합 일치·중복
없음 확인.

## 확인
컴파일 확인 완료(에러 0). `GetTextOrFallback`은 아직 별도 자동 테스트는 없음 - 다음에 Play Mode
직접 확인 시 EN/KR 전환하며 유닛/건물 이름·설명이 바뀌는지, 그리고 일부러
`Resources/Localization/ko.json`을 잠깐 지워서 안전장치(원본 SO 텍스트 표시)가 실제로 동작하는지
확인해볼 수 있음.
