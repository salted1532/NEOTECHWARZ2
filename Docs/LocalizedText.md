# LocalizedText

`Assets/Scripts/Localization/LocalizedText.cs`

## 개요

스크립트가 텍스트를 직접 갱신하지 않는 정적 UI 라벨(버튼 캡션, 패널 헤더 등)에 붙여서 현재 언어로 표시한다. `LocalizationManager.OnLanguageChanged`를 구독해 언어가 바뀌면 즉시 다시 그린다(doc/0481). `TextMeshProUGUI`/레거시 `UI.Text` 둘 다 지원한다 — OptionPanel처럼 레거시 Text를 쓰는 라벨도 있기 때문(doc/0482). OptionPanel 설정 라벨은 `settings.<카테고리>.<항목>` 키 컨벤션을 따른다(doc/0483).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `target` | `TextMeshProUGUI` 대상 (있으면 우선 사용) |
| `legacyTarget` | 레거시 `UI.Text` 대상 |
| `key` | 조회할 로컬라이제이션 키 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Reset()` | 에디터에서 컴포넌트 추가 시 같은 오브젝트의 `TextMeshProUGUI`/`Text`를 자동으로 연결 |
| `OnEnable()` | `OnLanguageChanged` 구독 + 즉시 `Apply()` |
| `Start()` | 씬 로드 시점 최초 적용을 한 번 더 수행 — `OnEnable`이 `LocalizationManager.Awake`보다 먼저 실행될 수 있어(순서 불보장) 그 시점엔 `Instance`가 null이라 `Apply()`가 조용히 무시될 수 있다. `Start()`는 모든 `Awake`가 끝난 뒤 보장되므로 이 시점엔 항상 준비됨(doc/0485) |
| `OnDisable()` | 구독 해제 |
| `Apply()` (private) | `LocalizationManager.Instance.Get(key)`로 조회한 텍스트를 `target`/`legacyTarget`에 반영 |

## 연관 컴포넌트

- **LocalizationManager**: `OnLanguageChanged` 이벤트 발행처, `Get(key)` 조회 대상
