# LocalizationManager

`Assets/Scripts/Localization/LocalizationManager.cs`

## 개요

현재 언어(`PlayerPrefs` "Language", 기본 "en")에 맞는 JSON을 `Resources/Localization`에서 읽어와 텍스트 조회를 제공하는 싱글턴(doc/0481). `SoundManager`/`TooltipUI`와 동일한 패턴으로 `DontDestroyOnLoad` 없이 `PlayerPrefs`로 씬을 넘어가도 선택한 언어가 이어진다. `JsonUtility`는 Dictionary를 직접 못 읽으므로 `{"entries":[{key,value}]}` 리스트 래퍼로 감싸서 읽는다(Newtonsoft 같은 별도 JSON 패키지가 없음).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `Instance` | 정적 싱글턴 인스턴스 |
| `OnLanguageChanged` | 정적 이벤트 — `LocalizedText.OnEnable`이 매니저의 `Awake`보다 먼저 실행돼도(스크립트 실행 순서 무관하게) 항상 구독에 성공하도록 인스턴스 이벤트가 아니라 static으로 둠(doc/0485) |
| `CurrentLanguage` | 현재 적용된 언어 코드 |
| `strings` | 키→번역문자열 딕셔너리 (private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 싱글턴 등록, 저장된 언어(없으면 기본값)로 `LoadLanguage()` |
| `SetLanguage(languageCode)` | 언어 전환 — 메인화면 언어 전환 버튼(KR/EN)의 `OnClick()`에 인스펙터로 직접 연결. `PlayerPrefs`에 저장 후 `OnLanguageChanged` 발생 |
| `LoadLanguage(languageCode)` (private) | `Resources/Localization/{languageCode}` JSON을 읽어 `strings` 딕셔너리 재구성. 파일이 없으면 경고만 남기고 무시 |
| `Get(key)` / `Get(key, args)` | 키로 번역 문자열 조회, 키가 없으면 키 자체를 그대로 반환(번역 누락을 화면에서 바로 알아챌 수 있게) |
| `GetText(key)` / `GetText(key, args)` (static) | `Instance`가 없어도 매 호출부에서 null 체크를 반복하지 않는 정적 패스스루 |
| `GetOrFallback(key, fallback)` / `GetTextOrFallback(key, fallback)` (static) | 키가 없거나 매니저가 없거나 조회 중 예외가 나도 "키 문자열"이 아니라 지정한 원문(fallback)을 그대로 보여줘야 하는 곳(유닛/건물 SO 이름·설명 등)에서 사용(doc/0487) |

## 연관 컴포넌트

- **LocalizedText**: `OnLanguageChanged`를 구독해 정적 UI 라벨 텍스트를 갱신
- **UIController 등 다수**: `GetText`/`GetTextOrFallback` 정적 패스스루로 런타임 텍스트 조회
