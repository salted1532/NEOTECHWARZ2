# 0481 - 로컬라이제이션(다국어) 시스템 도입 (제안)

## 요청 내용
"현재 텍스트로 출력되는 모든 텍스트들을 뽑아내서 json파일로 밖으로 빼줘 ... 어떤 언어로
설정되어있는지에 따라 해당하는 json파일을 읽어와 텍스트로 뿌려주는거로 변경해줘 현재 한글로
작성되어있는건(미션 오브젝트) 영어로 바꿔주고 영어랑 한글 2가지 json파일을 일단 생성해주고
그걸 변경 할수 있도록만 만들어줘 내가 메인화면에 연결해서 작동하도록 하는 테스트 버튼을 일단
만들어둘게" + 후속: "그냥 버튼들(스크립트 연결로 출력되는거 아닌)이나 그냥 텍스트들도 거기있는거
가져와서 번역으로 바꾸도록도 해야해"

## 조사 내용 (전수 조사 완료)
코드에 하드코딩된 문자열과, 스크립트를 안 거치고 씬/프리팹에 직접 타이핑된 정적 텍스트를 전부
훑었음. 총 **약 85개** 문자열 확인. 아래 "제외 항목" 외엔 전부 이번에 로컬라이즈 대상.

### 제외 항목 (이번 범위 밖 - 별도 판단 필요)
- **`UnitData`/`BuildingData` SO의 `unitName`/`description`/`infoDescription`** (36개 항목 × 필드) -
  Info Panel/툴팁에 표시되는 유닛·건물 이름/설명. 코드가 아니라 6개 SO 에셋에 데이터로 박혀있어서,
  JSON 로컬라이제이션에 넣으려면 "SO ID → 로컬라이즈 키" 매핑이 추가로 필요함. 이번 1차 작업 범위엔
  포함 안 함 - 필요하면 2차로 이어서 진행 가능.
- **`VersionText`("Ver 0.0.1")**, **`VersionComment`**("08-03 미니맵에 자원 표시 추가" 같은 개발용
  변경사항 메모) - 버전 넘버는 번역 대상이 아니고, 변경사항 메모는 빌드마다 바뀌는 개발자 전용 문구라
  JSON에 박아두면 오히려 관리가 안 됨. 그대로 둠.
- **`Unlock ALL Mission` 버튼** - doc/0472에 "정식 버전 출시 전 제거" 명시된 개발자 전용 버튼. 어차피
  삭제될 거라 번역 대상에서 제외.
- **KR/EN 버튼 자체의 캡션("KR"/"EN")** - 언어 코드라 번역이 의미 없음. 그대로 둠. (이 두 버튼은
  사용자가 직접 만들어둔 테스트용 언어 전환 버튼으로 보임 - 아래 "언어 전환 연결 방법" 참고)

## 설계

### 왜 JsonUtility + List 래퍼인가
프로젝트에 Newtonsoft.Json 같은 별도 JSON 패키지가 없고 유니티 내장 `JsonUtility`만 씀
(`Packages/manifest.json` 확인). `JsonUtility`는 `Dictionary<string,string>`을 직렬화 못 하는
알려진 제약이 있어서, `{"entries":[{"key":"...", "value":"..."}]}` 형태의 리스트 래퍼로 감싸서 읽음
(추가 패키지 설치 없이 되는 가장 간단한 방법).

### `Assets/Scripts/Localization/LocalizationManager.cs` (신규)
`SoundManager.Instance`/`TooltipUI.Instance`와 동일한 싱글턴 패턴(DontDestroyOnLoad 없음 - 이
프로젝트에 그 패턴 자체가 없음, 대신 SoundManager처럼 PlayerPrefs로 씬 넘어가도 설정이 이어짐).

```csharp
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }
    public event System.Action OnLanguageChanged;
    public string CurrentLanguage { get; private set; } = "en";

    private void Awake() { Instance = this; LoadLanguage(PlayerPrefs.GetString("Language", "en")); }

    public void SetLanguage(string languageCode) { ... PlayerPrefs.SetString("Language", ...); OnLanguageChanged?.Invoke(); }
    public string Get(string key) => strings.TryGetValue(key, out var v) ? v : key; // 키 없으면 키 자체를 보여줌(번역 누락 바로 눈에 띄게)
    public string Get(string key, params object[] args) => string.Format(Get(key), args);
}
```

`Assets/Resources/Localization/en.json`, `.../ko.json`로 저장 → `Resources.Load<TextAsset>("Localization/en")`로 읽음.

### `Assets/Scripts/Localization/LocalizedText.cs` (신규, 정적 라벨용)
스크립트가 안 건드리는 버튼 캡션/패널 헤더에 붙이는 작은 컴포넌트. `key`만 인스펙터에 채우면
`OnEnable`에서 즉시 반영 + `LocalizationManager.OnLanguageChanged` 구독으로 언어 전환 시 즉시 갱신.

```csharp
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI target;
    [SerializeField] private string key;
    private void OnEnable() { Apply(); if (Instance != null) Instance.OnLanguageChanged += Apply; }
    private void OnDisable() { if (Instance != null) Instance.OnLanguageChanged -= Apply; }
    private void Apply() { if (target != null) target.text = LocalizationManager.Instance.Get(key); }
}
```

이 컴포넌트를 아래 "정적 라벨" 목록의 각 오브젝트에 자동으로 붙이는 건 손으로 YAML을 고치는 것보다
유니티 에디터 스크립트(AddComponent)로 하는 게 훨씬 안전함 - 구현 단계에서 그렇게 처리.

### 언어 전환 연결 방법
사용자가 만들어둔 KR/EN 버튼의 OnClick()에 `LocalizationManager.Instance.SetLanguage("ko")` /
`SetLanguage("en")`을 인스펙터에서 직접 연결하면 됨(public 메서드라 유니티 Button OnClick UI에서
바로 선택 가능, 문자열 파라미터도 지원됨). 코드 쪽엔 손 안 대고 인스펙터 연결만 하면 되도록
`SetLanguage(string)`를 public으로 열어둠.

### 실시간 갱신 범위
- 커맨드 패널 버튼(Move/Attack/Stop 등)과 연구 버튼은 `RTSUnitController.UpdateUI()`가 매 프레임
  다시 그리므로, 언어를 바꾸면 다음 프레임에 자동으로 새 언어로 바뀜 - 별도 이벤트 구독 불필요.
- `LocalizedText`가 붙은 정적 라벨은 `OnLanguageChanged` 이벤트로 즉시 갱신.
- 미션 목표 텍스트(`ObjectiveTextUtil`)는 목표가 갱신되는 시점(상태 변화)에만 다시 그려지므로,
  미션 진행 중에 언어를 바꾸면 이미 떠 있는 목표 문구는 다음 상태 변화 전까진 안 바뀔 수 있음 -
  이번 범위에서 별도 리프레시 로직은 안 넣음(메인화면 테스트 버튼 용도로는 불필요).

## 로컬라이즈 대상 키 목록 (~85개)

### 커맨드 패널 (RTSUnitController.cs)
| 키 | English | 한글 |
|---|---|---|
| cmd.move.title / .desc | Move / "Move to a location. \nshortcut key [{0}]" | 이동 / "지정한 위치로 이동합니다. \n단축키 [{0}]" |
| cmd.attack.title / .desc | Attack / "Attack a target or location. \nshortcut key [{0}]" | 공격 / "대상이나 위치를 공격합니다. \n단축키 [{0}]" |
| cmd.stop.title / .desc | Stop / "Stop the current action. \nshortcut key [{0}]" | 정지 / "현재 행동을 멈춥니다. \n단축키 [{0}]" |
| cmd.patrol.title / .desc | Patrol / "Patrol along a path. \nshortcut key [{0}]" | 순찰 / "경로를 따라 순찰합니다. \n단축키 [{0}]" |
| cmd.hold.title / .desc | Hold / "Hold the current position. \nshortcut key [{0}]" | 대기 / "현재 위치를 지킵니다. \n단축키 [{0}]" |
| cmd.returncargo.title / .desc | Return Cargo / "Return gathered resources to base. \nshortcut key [{0}]" | 자원반납 / "채취한 자원을 본진에 반납합니다. \n단축키 [{0}]" |
| cmd.build.title / .desc | Build / "Enter build mode. \nshortcut key [{0}]" | 건설 / "건설 모드로 진입합니다. \n단축키 [{0}]" |
| cmd.rally.title / .desc | Rally / "Set the rally point for newly produced units. \nshortcut key [{0}]" | 집결지 / "새로 생산될 유닛의 집결지를 지정합니다. \n단축키 [{0}]" |
| cmd.land.title / .desc | Land / "Choose a landing site. \nshortcut key [{0}]" | 착륙 / "착륙 위치를 지정합니다. \n단축키 [{0}]" |
| cmd.liftoff.title / .desc | Lift Off / "Lift the building into the air. \nshortcut key [{0}]" | 이륙 / "건물을 공중으로 띄웁니다. \n단축키 [{0}]" |
| cmd.moveairborne.desc | "Move to a location while airborne. \nshortcut key [{0}]" | "공중에서 지정한 위치로 이동합니다. \n단축키 [{0}]" |
| cmd.cancel.title | Cancel | 취소 |
| cmd.cancelconstruction.desc | "Cancel construction and refund resources. \nshortcut key [{0}]" | "건설을 취소하고 자원을 환불합니다. \n단축키 [{0}]" |
| cmd.cancelbuildmode.desc | "Exit build mode. \nshortcut key [{0}]" | "건설 모드를 나갑니다. \n단축키 [{0}]" |
| research.attack.name / research.armor.name | Attack Upgrade / Armor Upgrade | 공격력 연구 / 방어력 연구 |
| research.title.level / .maxed | "{0} Lv.{1}" / "{0} (MAX)" | "{0} Lv.{1}" / "{0} (최대)" |
| research.desc.maxed | "{0} fully researched." | "{0} 연구 완료." |
| research.desc.attack | "Research increased attack damage for all units. (Lv.{0} → Lv.{1})" | "모든 유닛의 공격력 증가를 연구합니다. (Lv.{0} → Lv.{1})" |
| research.desc.armor | "Research increased armor for all units. (Lv.{0} → Lv.{1})" | "모든 유닛의 방어력 증가를 연구합니다. (Lv.{0} → Lv.{1})" |
| unit.trainfallback | "Train {0}." | "{0} 생산." |
| building.constructfallback | "Construct {0}." | "{0} 건설." |
| trait.cooldownsuffix | "\nRemain time: {0:F1}" | "\n남은 시간: {0:F1}" |

### Info Panel / Squad Panel (UIController.cs)
| 키 | English | 한글 |
|---|---|---|
| infopanel.attacktooltip | "Attack Type : {0}\nAttack Damage : {1}{2}\nAttack Target : {3}" | "공격 타입 : {0}\n공격력 : {1}{2}\n공격 대상 : {3}" |
| infopanel.armortooltip | "Armor : {0}\nArmor Type : {1}\nSize : {2}" | "방어력 : {0}\n장갑 타입 : {1}\n크기 : {2}" |
| infopanel.attacktarget.groundair / .ground / .air / .none | Ground/Air / Ground / Air / None | 지상/공중 / 지상 / 공중 / 없음 |
| squad.unittooltip | "Click: Select Unit\nShift+Click: Deselect Unit\nCtrl+Click: Select Unit Type" | "클릭: 유닛 선택\nShift+클릭: 선택 해제\nCtrl+클릭: 같은 종류 전체 선택" |
| squad.unitfallback | Unit | 유닛 |
| squad.buildingtitle | Building | 건물 |
| squad.buildingtooltip | "Click: Select Building\nShift+Click: Deselect Building\nCtrl+Click: Select Building Type" | "클릭: 건물 선택\nShift+클릭: 선택 해제\nCtrl+클릭: 같은 종류 전체 선택" |
| warning.resource | Gather more resources. | 자원을 더 채취하세요. |
| warning.population | Build a Supply Depot. | 보급고를 건설하세요. |
| warning.constructionfail | Build somewhere else. | 다른 곳에 건설하세요. |
| missionselect.tooltip.subtitle | "Mission {0}" | "미션 {0}" |

### 미션 목표 (Stage0~5Objectives.cs, ObjectiveTextUtil.cs) - 기존 한글 → 영어 신규 번역
| 키 | English (신규 번역) | 한글 (기존) |
|---|---|---|
| objective.fail.suffix | " (Failed)" | " (실패)" |
| objective.stage0.main1 | (Main) Capture 1 outpost | (주목표) 거점 1개 점령하기 |
| objective.stage0.main2 | (Main) Produce an Assault Trooper | (주목표) 어썰트 트루퍼 생산하기 |
| objective.stage0.main3 | (Main) Build a Barracks | (주목표) 병영 건설하기 |
| objective.stage0.sub1 | (Sub) Eliminate all nearby enemy units | (서브) 주변 적 유닛 모두 제거 |
| objective.stage0.sub2 | (Sub) Secure minerals | (서브) 광물 확보 |
| objective.stage1.main1 | (Main) Destroy the OC outpost (main base) | (주목표) OC 전초기지(메인기지) 파괴 |
| objective.stage1.sub1 | (Sub) Secure minerals | (서브) 광물 확보 |
| objective.stage1.sub2 | (Sub) Capture the radar base | (서브) 레이더 기지 점령 |
| objective.stage1.sub3 | (Sub) Destroy all enemy buildings | (서브) 적 건물 모두 파괴 |
| objective.stage2.main1 | (Main) Secure the alien artifact | (주목표) 외계 유물 확보 |
| objective.stage2.sub1 | (Sub) Secure OC research data | (서브) OC 연구 데이터 확보 |
| objective.stage3.main1 | (Main) Eliminate the alien outpost | (주목표) 외계 전초기지 제거 |
| objective.stage3.sub1 | (Sub) Rescue surviving OC soldiers | (서브) 생존한 OC 병사 구조 |
| objective.stage4.main1 | (Main) Destroy the alien command base | (주목표) 외계 사령기지 파괴 |
| objective.stage4.sub1 | (Sub) Keep the OC command post alive | (서브) OC 사령부 생존 |
| objective.stage5.main1 | (Main) Destroy the energy core | (주목표) 에너지 코어 파괴 |
| objective.stage5.main2 | (Main) Eliminate the alien command core | (주목표) 외계 지휘 코어 제거 |

### 정적 라벨 (`LocalizedText` 컴포넌트 부착 대상) - 기존 영어/한글 혼재 → 양쪽 다 채움
| 키 | English (기존/그대로) | 한글 (신규 번역) | 위치 |
|---|---|---|---|
| ui.play | Play | 플레이 | MainScene Play 버튼 |
| ui.option | Option | 옵션 | MainScene Option 버튼 + GameManager 내 Option 버튼 (동일 키 재사용) |
| ui.exit | Exit | 종료 | MainScene Exit 버튼 |
| ui.bgm | BGM | BGM | OptionPanel |
| ui.voice | Voice | 음성 | OptionPanel |
| ui.master | Master | 마스터 | OptionPanel |
| ui.sfx | SFX | 효과음 | OptionPanel |
| ui.goto | Go To | 이동 | GameManager 내 "GoTo"/"Go To" 3곳 (철자 불일치 → 한 키로 통일) |
| ui.backto | Back To | 뒤로 | GameManager 내 "Back To" 2곳 |
| ui.returnto | Return To | 돌아가기 | GameManager 내 "Return To" |
| ui.victory | Victory! | 승리! | GameManager `VictoryText` |
| missionselect.button.0~5 | Mission 0 ~ Mission 5 | 미션 0 ~ 미션 5 | MissionSelect 씬의 미션 버튼 캡션(툴팁 서브타이틀과 별개로 버튼 자체에 박힌 텍스트) |

## 변경 파일
- **신규**: `Assets/Scripts/Localization/LocalizationManager.cs`, `LocalizedText.cs`,
  `Assets/Resources/Localization/en.json`, `ko.json`
- **수정(코드)**: `RTSUnitController.cs`, `UIController.cs`, `MissionSelectManager.cs`,
  `ObjectiveTextUtil.cs`, `Stage0Objectives.cs`~`Stage5Objectives.cs` (6개)
- **수정(씬/프리팹, 에디터 스크립트로 컴포넌트 부착)**: `MainScene.unity`, `MissionSelect.unity`,
  `GameManager.prefab`, `OptionPanel.prefab`

이대로 진행할까요? (SO 데이터(유닛/건물 이름·설명) 로컬라이즈는 이번엔 빼고 필요시 2차로 진행
제안 - 괜찮으실지도 확인 부탁드립니다.)
