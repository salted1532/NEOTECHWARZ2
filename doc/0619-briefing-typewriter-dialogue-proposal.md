# 0619 - 브리핑 대사 타이프라이터/스크롤/인물 순차 등장 (제안)

## 요청
1. Campaign.md의 실제 브리핑 대사를 게임에 반영
2. 대사에 타이프라이터 효과 (인물 이름은 즉시 출력, 대사 내용은 한 글자씩 출력)
3. 텍스트가 다 차면 위로 스크롤되며 최신 대사가 보이도록
4. 첫 화자 이미지는 처음부터 표시, 다음 대사에서 다른 인물이 등장하면 텍스트 타이핑과 함께 그 인물 이미지도 로딩(표시)

## 현재 상태
- `BriefingEntry`: 미션당 `speaker1/2/3Key`(고정 3인) + 통짜 `dialogueText` 로컬라이제이션 키 1개 (doc/0616, doc/0617).
- 씬의 `dialogueText`는 배경 패널 + TMP 텍스트 1개, 스크롤 관련 컴포넌트 없음.
- `speakerPortraitImage/2/3`는 항상 3개 다 즉시 표시, 개별 on/off·페이드 로직 없음.

## 제안 설계

### 1. 데이터 구조 (`BriefingRoomController.cs`)
```csharp
[System.Serializable]
public class BriefingLine
{
    public int speakerSlot;       // 1, 2, 3 - speaker1/2/3Key 중 어느 슬롯인지
    public string speakerLabelKey; // 예: "briefing.speaker.adrian" (7개 고정 키, 재사용)
    public string textKey;         // 그 줄의 대사, 예: "briefing.line.1.0"
}
```
`BriefingEntry`에 `List<BriefingLine> lines` 추가. 기존 `briefing.dialogue.{n}` 단일 키는 더 이상 안 쓰고 제거.

인물 이름 표시용 키 7개 (재사용, 새로 안 늘어남):
`briefing.speaker.adrian/selena/adjutant/scout/detachment_leader/rescue_leader/defense_leader`

### 2. 타이프라이터 + 자동 진행
- 미션 진입 시 코루틴이 `lines`를 순서대로 재생.
- 각 줄: `"{speakerLabel}: "` 즉시 출력 → 대사 내용은 `charsPerSecond`(인스펙터 노출, 기본 30자/초) 속도로 한 글자씩 출력.
- 줄이 끝나면 `pauseBetweenLines`(기본 0.6초) 대기 후 다음 줄 자동 시작.
- 현재 씬에 "다음" 버튼이 없어서 자동 진행으로 구현 (필요하면 나중에 클릭으로 스킵/진행하는 버튼 추가 가능).

### 3. 스크롤 로그
`dialogueText` 배경은 그대로 두고, 내부에 `Viewport`(RectMask2D로 클리핑) + `Content`(TMP 텍스트, ContentSizeFitter로 세로 자동 확장) + `ScrollRect` 구조를 새로 만듦. 줄이 쌓여 세로로 넘치면 `scrollRect.verticalNormalizedPosition`을 0으로 맞춰 항상 최신 줄이 보이게 자동 스크롤.

### 4. 인물 이미지 순차 등장
미션 진입 시 slot1(첫 화자) 이미지 alpha=1(바로 표시), slot2/3은 alpha=0(숨김). 각 슬롯이 처음 등장하는 줄이 시작될 때 그 슬롯의 이미지를 0→1로 짧게 페이드인(예: 0.25초)하면서 동시에 타이핑 시작.

### 5. 대사 콘텐츠 반영
Campaign.md 10개 미션(본편 0~5, 서브 1~4)의 대사를 doc/0617 화자-슬롯 매핑 기준으로 옮겨 담음 (미션3은 이미 합의된 병사→정찰병 병합 버전 사용). ko.json에 실제 한글 대사(총 약 46줄) 입력, en.json은 기존 방식대로 "(TODO) translate: ..." placeholder로 채움. 기존 `briefing.dialogue.{n}` / `.sub{n}` 키는 미사용이 되므로 정리(삭제).

## 범위 밖
- `missionInfoText`(미션 목표 요약)는 지금처럼 즉시 표시 유지, 타이프라이터 미적용 - 요청이 "인물이 말하는 대사"에 한정됨.
- 클릭으로 다음 줄 넘기기/스킵 버튼 - 지금 씬에 그런 버튼이 없어서 범위 밖, 필요하면 별도 요청.
- 대사 완료 후 상호작용(예: 완료 표시) - 요청에 없음.

## 구현 완료
- `BriefingRoomController.cs`: `BriefingLine`(speakerSlot/speakerLabelKey/textKey) 추가, `BriefingEntry.lines` 리스트로 대사 순서 관리. `PlayDialogue()` 코루틴이 줄마다 이름 즉시 출력 + 내용 글자단위 타이핑, `RevealPortraitIfNeeded()`가 슬롯별 최초 등장 시 0→1 알파 페이드(0.25초)로 이미지 로딩, `SetDialogueText()`가 매 글자마다 `ScrollRect.verticalNormalizedPosition = 0`으로 최신 줄 노출. 컴파일 성공.
- 로컬라이제이션: `briefing.speaker.*` 7개(재사용) + `briefing.line.{missionKey}.{idx}` 46개를 ko.json(실제 Campaign.md 대사)/en.json("(TODO) Translate: ..." placeholder)에 반영, 기존 `briefing.dialogue.*` 10개는 제거. JSON 문법 검증 통과.
- 씬(`Briefing_Room.unity`): `dialogueText` 내부에 `Viewport`(RectMask2D) + 기존 `Text (TMP)`를 Content로 재배치(ContentSizeFitter 세로 자동확장) + `ScrollRect` 구성. `BriefingRoomController`의 `dialogueText`/`dialogueScrollRect` 참조 재배선. `briefingEntries` 10개에 대사 라인(총 46줄) 매핑 완료.
- 버그 수정: `Briefing_Room` 씬에 `LocalizationManager` 인스턴스가 없어서 모든 텍스트가 로컬라이제이션 키 원문으로 표시되던 문제 발견 - 다른 미션 씬들과 동일하게 `LocalizationManager` GameObject를 씬에 추가해서 해결. 텍스트가 왼쪽 경계에서 살짝 잘리던 것도 TMP margin(10,6,10,6)으로 보정.
- Play Mode 스모크 테스트(미션0)로 타이핑 진행/화자 전환 시 이미지 페이드인/로그 누적을 스크린샷으로 확인, 콘솔 에러 없음.

## 남은 작업 (사용자 몫)
- `characterRoster`의 `soldier` 포함 인물 초상화(아드리안/셀레나는 이미 등록된 것으로 확인됨, 부관/soldier는 스크린샷상 미확인 - 비어있으면 추가 필요)와 `mapImage` 스프라이트.
- en.json의 대사/미션정보 TODO 플레이스홀더를 실제 영문 번역으로 교체.
- 타이핑 속도(`charsPerSecond` 기본 30), 줄 간 대기(`pauseBetweenLines` 기본 0.6초), 페이드 시간(`portraitFadeDuration` 기본 0.25초)은 인스펙터에서 튜닝 가능.

## 추가 반영 - 미션 목표 텍스트도 즉시 타이프라이터
`missionInfoText`(미션 목표)도 브리핑 시작과 동시에(대사와 병렬로) 같은 속도(`charsPerSecond`)로 타이핑되도록 변경. 공용 `TypeText(TextMeshProUGUI, string)` 코루틴을 추가해 `Awake()`에서 `PlayDialogue()`와 별도로 동시에 시작. Play Mode로 재확인 - 대사와 미션정보가 동시에 타이핑되는 것 스크린샷으로 확인.

## 추가 수정 - 미션 목표 실제 내용 표시 + 버튼 텍스트 로컬라이제이션
- **미션정보 미기입 문제**: `briefing.missioninfo.{n}` placeholder를 채워 넣는 대신, 실제 인게임 목표 표시에 쓰이는 `objective.stage{n}/substage{n}.main{i}/sub{i}` 키(이미 "(주목표)"/"(서브)" 접두어가 붙은 실제 목표 문구, ko/en 모두 기존에 존재)를 그대로 재사용하도록 `BuildMissionInfoText()`를 추가. 브리핑과 인게임 목표 문구가 항상 같은 소스를 쓰게 되어 이중 관리가 없어짐. 미사용된 `briefing.missioninfo.*` placeholder 20개(ko+en)는 제거.
- **시작/돌아가기 버튼 텍스트**: 새 코드를 안 만들고 프로젝트에 이미 있던 `LocalizedText` 컴포넌트(정적 UI 라벨용, `LocalizationManager.OnLanguageChanged` 구독)를 `Start_Mission`/`Go_Back` 버튼의 `Text (TMP)`에 붙여서 `briefing.button.start`("미션 시작"/"Start Mission")·`briefing.button.back`("돌아가기"/"Back") 키로 연결.
- **버그**: 로컬라이제이션 JSON을 컴파일 없이 파일로만 수정하면 Unity가 바로 반영 안 함 - 새 키(`briefing.button.*`)가 키 원문으로 표시되는 것 발견, `AssetDatabase.Refresh()`로 해결.
- Play Mode 재확인: 목표 5줄("(주목표) 거점 1개 점령하기" 등)과 버튼 라벨("돌아가기"/"미션 시작") 정상 표시 스크린샷으로 확인.

## 상태
완료.
