# 0464. 스테이지 2 임무 완료 시 성공 SFX (Global Voice)

**날짜:** 2026-08-08

## 요청 내용
> 스테이지 2에서 유물 + 데이터를 비콘으로 가져가면 아이템은 없어지고 임무는 완료되는데 이때
> 성공 SFX를 Global Voice에서 나오도록 추가해주고 작동하도록 만들어줘

## 조사

- `SoundManager`/`GlobalVoiceBankSO` 조합이 이미 존재(자원/인구 부족, 피격 경고, 업그레이드 완료 등
  유닛/건물에 안 묶이는 나레이션 전용, doc/0255). 같은 패턴으로 "임무 성공" 카테고리를 추가하는 게
  일관됨.
- `Stage2Objectives.Update()`에서 `artifactDelivered`(유물 반납 완료, 주목표)가 켜지면
  `StageManager.Instance.ReportVictory()`를 호출 - 이게 곧 "임무 완료" 시점(연구 데이터는 서브목표라
  승리 조건이 아님, 기존 로직 그대로 둠).
- `ReportVictory()`는 `StageManager` 내부에서 `Result != InProgress`면 무시하는 가드가 있지만, 이
  가드는 `Stage2Objectives.Update()`가 매 프레임 `ReportVictory()`를 호출하는 것 자체는 막지 않음
  - 사운드 재생 호출을 `if (artifactDelivered)` 블록에 그대로 넣으면 승리 이후에도 매 프레임
    호출되어 버림. `PlayGlobalVoice`가 "재생 중이면 무시"는 하지만 "이미 다 재생 끝났으면" 막을
    장치가 없어서, 방치하면 재생이 끝나자마자 바로 다음 프레임에 다시 울리는 문제가 생김 → 별도의
    1회성 플래그로 최초 1회만 호출되게 함.
- 프로젝트에 "임무 성공" 전용 SFX 파일이 아직 없음(사용자에게 확인 후, 빈 슬롯으로 배선만 하고
  실제 클립은 나중에 직접 연결하기로 함).

## 적용

- `GlobalVoiceBankSO.cs`: `missionSuccess`(`SoundClipSet`) 필드 추가.
- `SoundManager.cs`: `PlayMissionSuccessVoice()` 래퍼 추가(`PlayUpgradeCompleteVoice()`와 동일한
  패턴 - `globalVoiceBank` null 체크 후 `PlayGlobalVoice(globalVoiceBank.missionSuccess)`).
- `Stage2Objectives.cs`: `missionSuccessSfxPlayed`(bool) 필드 추가. `artifactDelivered`가 켜져서
  `ReportVictory()`를 호출하는 지점에서, 아직 안 울렸으면 플래그를 켜고
  `SoundManager.Instance?.PlayMissionSuccessVoice()`를 딱 1회만 호출.
- `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`의 `missionSuccess` 슬롯은
  아직 비어있음(클립 없으면 `SoundClipSet.HasClips`가 false라 `PlayGlobalVoice`가 조용히
  스킵) - 실제 성공 SFX 오디오 파일을 이 슬롯에 연결하면 그 즉시 작동함.

## 검증 (Play Mode, Mission2)

- 리플렉션으로 `Stage2Objectives.artifactDelivered`를 강제로 `true`로 설정한 뒤 한 프레임 대기 후
  확인: `StageManager.Instance.Result=Victory`, `Stage2Objectives.missionSuccessSfxPlayed=True`
  (1회 호출 확인 - 클립이 없어서 소리 자체는 안 나지만, 호출 경로와 1회성 가드는 정상 동작 확인).
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- `git status`: 의도한 3개 스크립트 파일만 변경됨, 애셋 노이즈 없음.

## 변경된 파일

- `Assets/Scripts/ScriptableObject/GlobalVoiceBankSO.cs` (`missionSuccess` 필드 추가)
- `Assets/Scripts/Audio/SoundManager.cs` (`PlayMissionSuccessVoice()` 추가)
- `Assets/Scripts/System/Stage2Objectives.cs` (1회성 성공 SFX 호출 추가)

## 남은 작업 (사용자)

- `Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`의 `Mission Success` 슬롯에
  실제로 재생할 오디오 클립을 연결해야 소리가 남.
