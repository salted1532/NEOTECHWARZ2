# 0485 - 로컬라이제이션 즉시 갱신 / 씬 전환 유지 / Pretendard 폰트 전면 적용

## 요청 내용
"En, ko 버튼을 누르는 동시에 텍스트들이 다 바뀌었으면 좋겠어 오브젝트가 꺼졌다가 다시 켜져야
갱신이 되는게 아니라 그리고 현재 textmeshpro에서 사용하는 폰트를 모두 Pretendard 폰트로
변경해줘 현재 미션 선택 씬에서 사용하는 텍스트중에 한글폰트가 깨지는게 있어서 그리고 씬을
넘어 다녀도 현재 선택된 언어에 맞춰서 갱신된 상태가 계속 유지되었으면 좋겠어 현재 메인 ->
미션선택 -> 메인으로 돌아오면 다시 영어로 표시되는게 확인되었어"

## 1) EN/KR 버튼 클릭 즉시 갱신 안 됨 + 2) 씬 복귀 시 언어가 영어로 리셋되는 것처럼 보임
두 증상 다 **같은 원인**이었음.

**원인**: `LocalizedText.OnEnable()`이 `LocalizationManager.Instance.OnLanguageChanged += Apply`로
구독했는데, 이건 그 시점에 `Instance`가 이미 세팅돼 있어야만 성립함. 근데 Unity는 같은 씬 안의
서로 다른 오브젝트 간 `Awake`/`OnEnable` 호출 순서를 보장하지 않음 - `LocalizedText`가 붙은
오브젝트의 `OnEnable`이 `LocalizationManager`의 `Awake`(여기서 `Instance`가 세팅됨)보다 먼저
실행되면, 그 시점엔 `Instance == null`이라 구독 자체가 조용히 스킵됨. 이후 EN/KR 버튼을 눌러
`OnLanguageChanged`가 발행돼도 아무도 구독하고 있지 않으니 반응이 없음(증상 1) - 오브젝트를
껐다 켜서 `OnEnable`을 나중에(이땐 `Instance`가 이미 준비된 뒤이므로) 다시 태워야만 그제서야
정상 구독되고 갱신됨.

또한 `OnEnable()`의 최초 `Apply()` 호출도 같은 이유로 조용히 no-op이 될 수 있어서, 씬을 다시
불러올 때(메인→미션선택→메인) `LocalizedText`가 실제로는 PlayerPrefs에 저장된 언어를 못
읽어오고, 에디터에 애초에 박혀있던 원본 텍스트("Play"/"Option"/"Exit" 같은 영문 placeholder)가
그대로 남아있어서 "다시 영어로 표시된다"처럼 보였던 것 - `LocalizationManager` 자체는 정상적으로
PlayerPrefs에서 저장된 언어를 읽어오고 있었음(재현 시 언어 자체가 리셋된 게 아니라 표시만
갱신 안 됐던 것).

**수정**:
- `LocalizationManager.OnLanguageChanged`를 인스턴스 이벤트 → **static 이벤트**로 변경.
  static이면 구독 시점에 `Instance` 존재 여부와 무관하게 항상 구독에 성공하므로, 순서
  문제로 구독이 스킵되는 경우가 아예 없어짐.
- `LocalizedText`에 `Start()`를 추가해서 최초 `Apply()`를 한 번 더 호출. `Start()`는 Unity가
  "씬의 모든 오브젝트의 `Awake`가 끝난 뒤에만 호출됨"을 보장하는 콜백이라, 이 시점엔
  `LocalizationManager.Instance`가 반드시 준비돼 있음 - 순서에 기대지 않는 안전한 초기 적용.
  (`OnEnable()`의 `Apply()` 호출은 이후 오브젝트가 껐다 켜질 때 - 예: 패널 토글 - 즉시 반영을
  위해 그대로 유지)

## 3) 미션 선택 씬 한글 폰트 깨짐 + 전체 TMP 폰트 Pretendard 통일

**원인**: 프로젝트의 실제 게임 씬/프리팹(`MainScene.unity`, `MissionSelect.unity`,
`GameManager.prefab`) 중 일부 TMP 텍스트가 여전히 TextMeshPro 기본 폰트인
`LiberationSans SDF`를 쓰고 있었음 - 이 폰트엔 한글 글리프가 없어서 한글 텍스트가 깨져(네모
박스) 보임. `Pretendard-Black SDF.asset`(한글 포함, 프로젝트에 이미 존재)은 이미 일부
오브젝트(GameManager.prefab의 툴팁 등)에 적용돼 있었지만 전부는 아니었음.

**수정**: `LiberationSans SDF` → `Pretendard-Black SDF`로 폰트/머티리얼 참조를 실제 게임에
쓰이는 파일에서만 일괄 교체.
- `Assets/prefabs/Game/GameManager.prefab` (1곳)
- `Assets/Scenes/MainScene/MainScene.unity` (3곳)
- `Assets/Scenes/Missions/MissionSelect.unity` (3곳)
- `Assets/TextMesh Pro/Resources/TMP Settings.asset`의 프로젝트 전역 기본 폰트도 함께 변경
  (앞으로 새로 만들 TMP 오브젝트가 폰트를 명시적으로 지정 안 해도 기본이 Pretendard가 되도록)

`Assets/TextMesh Pro/Examples & Extras/`(TMP 패키지가 같이 배포하는 데모 씬, 빌드에 포함 안 됨)와
써드파티 에셋의 데모 씬/프리팹(Character Selection Scene Demo, Cinematic Explosions FREE Demo)에
남아있는 Anton/Bangers/Roboto-Bold/Unity SDF/Electronic Highway Sign 폰트는 실제 게임(빌드
씬 목록: MainScene/MissionSelect/Mission0~5)에서 전혀 참조되지 않는 데모 콘텐츠라 건드리지
않음.

**참고**: Pretendard SDF 폰트 애셋은 Black 웨이트 하나만 이미 생성돼 있었음(다른 웨이트는
.ttf 원본만 있고 TMP SDF 애셋으로 아직 안 구워짐) - "모두 Pretendard로"라는 요청 범위 안에서
지금 존재하는 유일한 Pretendard SDF 애셋을 그대로 사용. 다른 웨이트가 필요해지면 Font Asset
Creator로 추가 생성 후 같은 방식으로 교체하면 됨.

## 확인
컴파일 확인 완료(에러 0).
