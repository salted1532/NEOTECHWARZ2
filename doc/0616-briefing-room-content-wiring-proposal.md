# 0616 - 브리핑룸 텍스트/이미지 연결 제안

## 요청
사용자가 `Briefing_Room` 씬을 다음과 같이 구성함 (uloop get-hierarchy로 확인):
- `Canvas/speakerPortraitImage`, `speakerPortraitImage2`, `speakerPortraitImage3` (Image, 각각 자식 "Image")
- `Canvas/dialogueText` (자식 TMP "Text (TMP)")
- `Canvas/missionInfoPanel` (자식 TMP "Text (TMP)") - 미션 목표 표시
- `Canvas/mapImage` (Image)
- `Canvas/Go_Back` (Button)
- `Canvas/Start_Mission` (Button)

미션별로 알맞은 텍스트/이미지가 연결되도록 배선 요청.

## 조사
- `MissionSelectManager.cs`: 미션 클릭 시 `SceneManager.LoadScene(entry.sceneName)`로 미션 씬을 바로 로드. `MissionSelectEntry`(인스펙터 리스트)로 버튼↔씬 매핑을 코드 밖에서 관리하는 패턴.
- `LocalizationManager.GetText(key)` / `GetTextOrFallback(key, fallback)` - JSON 키-값 기반 로컬라이제이션. `Assets/Resources/Localization/ko.json`, `en.json`에 `missionselect.name.{n}`, `missionselect.planet.{n}` 등 미션 번호 인덱스 키 패턴 존재.
- 씬 전환 간 데이터를 들고 갈 persistent 매니저(DontDestroyOnLoad)나 GameSession류 static 클래스는 프로젝트에 없음 - 새로 필요.

## 제안
1. **선택 정보 전달**: 새 static 클래스(또는 `MissionSelectManager` 옆에 작은 static 필드 홀더)로 클릭된 미션의 `missionNumber`, `isSubMission`, `targetSceneName`을 담아둠. `MissionSelectManager.LoadMission()`이 대상 미션 씬 대신 `Briefing_Room`을 로드하도록 변경.
2. **BriefingRoomController.cs (신규)**: `Briefing_Room` 씬의 `Canvas`에 붙임. 인스펙터에 미션별 데이터 리스트(`MissionSelectEntry`와 동일한 패턴):
   ```csharp
   [System.Serializable]
   public class BriefingEntry
   {
       public int missionNumber;
       public bool isSubMission;
       public Sprite portrait1, portrait2, portrait3;
       public Sprite mapImage;
       // 로컬라이제이션 키, missionselect.name.{n}과 동일한 인덱스 컨벤션
   }
   ```
   `Awake()`에서 static 홀더의 missionNumber/isSubMission으로 리스트에서 항목을 찾아 `speakerPortraitImage(2/3)`, `mapImage`의 `Image.sprite`와 `dialogueText`/`missionInfoPanel`의 TMP 텍스트를 채움. 텍스트는 `briefing.dialogue.{n}` / `briefing.missioninfo.{n}` (서브미션은 `.sub{n}`) 로컬라이제이션 키로 `LocalizationManager.GetText`를 통해 가져옴.
3. **버튼 배선**: `Go_Back` → 미션 선택 씬(`MissionSelectManager.mainMenuSceneName` 컨벤션과 동일하게 씬 이름 필드로 노출) 로드. `Start_Mission` → static 홀더의 `targetSceneName`으로 `SceneManager.LoadScene`.
4. **로컬라이제이션 파일**: `en.json`/`ko.json`에 `briefing.dialogue.{n}`, `briefing.missioninfo.{n}` 키를 미션 수만큼(0~5, sub1~4) 추가 - 실제 텍스트 내용은 임시 placeholder로 넣고 추후 사용자가 교체.

## 범위 밖 (요청하지 않음, 안 만듦)
- 대사가 여러 줄로 넘어가는 진행형 다이얼로그(다음 버튼, 화자 전환 애니메이션 등) - 지금 씬에 "다음" 버튼이 없고 요청도 없어서 텍스트/이미지 정적 표시만 구현.
- 브리핑 중 사운드/애니메이션 연출 - 요청 범위 아님.

## 구현 완료
- `Assets/Scripts/UI/BriefingSelection.cs` (신규): 클릭된 미션 번호/서브미션 여부/목표 씬 이름을 담는 static 홀더.
- `Assets/Scripts/UI/BriefingRoomController.cs` (신규): `briefingEntries` 리스트에서 선택된 미션 항목을 찾아 초상화 3장/맵 이미지/대사·미션정보 텍스트를 채우고, `Go_Back`/`Start_Mission` 버튼을 배선.
- `MissionSelectManager.LoadMission()`: 미션 씬을 바로 로드하지 않고 `BriefingSelection`에 정보를 채운 뒤 `Briefing_Room` 씬을 로드하도록 변경.
- `Assets/Resources/Localization/en.json`, `ko.json`: `briefing.dialogue.{n}`, `briefing.missioninfo.{n}` (0~5, sub1~4) 임시 placeholder 텍스트 추가.
- `Briefing_Room.unity`: `Canvas`에 `BriefingRoomController` 컴포넌트를 붙이고 UI 참조(초상화 3개, 맵 이미지, 대사/미션정보 텍스트, 뒤로가기/시작 버튼) 전부 연결, `briefingEntries`에 미션 0~5·sub1~4 총 10개 항목 등록 완료.
- 컴파일 성공 (에러 0, 기존 프로젝트 warning만 존재).

## 남은 작업 (사용자 몫)
- `briefingEntries`의 `portrait1/2/3`, `mapImage` 스프라이트는 아직 프로젝트에 관련 이미지 에셋이 없어 비어있음 - 인스펙터에서 직접 연결 필요.
- placeholder 로컬라이제이션 텍스트("(임시) ...")를 실제 브리핑 대사/미션 설명으로 교체 필요.
