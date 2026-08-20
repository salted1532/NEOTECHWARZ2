# 0630 - 브리핑 대사별 음성 클립 재생 (제안)

## 요청
브리핑룸에서 각 대사가 타이핑될 때, 그 줄에 맞는 음성 클립(TTS로 미리 뽑아둔 파일)이 같이 재생되게 해달라.
클립은 [[0629-briefing-dialogue-script-by-character-en]]에서 정리한 `[인물] Mission N - Line i` 넘버링대로
준비 중이며, 현재는 부관(Adjutant) 클립만 우선 준비되고 있음 - 나머지 인물 클립은 나중에 채워짐.

## 현재 상태
- `BriefingLine`(`BriefingRoomController.cs:36`)은 `speakerSlot`/`speakerLabelKey`/`textKey`만 들고 있고 음성 필드가 없음.
- `PlayDialogue()`(`BriefingRoomController.cs:161`)가 `entry.lines`를 순서대로 돌며 타이프라이터로 텍스트만 출력, 오디오 재생 없음.
- 프로젝트에 이미 `SoundManager` 싱글턴(`Assets/Scripts/Audio/SoundManager.cs`)이 있지만, `PlayVoice()`는 `SoundClipSet`(랜덤 클립 풀 + 스팸 방지) 기반이라 "정확히 이 대사 줄 = 이 클립 1개" 같은 1:1 매핑에는 안 맞음 - 브리핑 대사는 매 순간 최대 1줄만 재생되니 스팸 방지/랜덤 선택 자체가 불필요.

## 제안 설계
1. `BriefingLine`에 `public AudioClip voiceClip;` 필드 하나 추가. 인스펙터에서 각 줄 항목에 클립을 직접 드래그해서 채움 (부관 줄만 채우고 나머지는 비워두면 됨).
2. `BriefingRoomController`에 `[SerializeField] private AudioSource voiceAudioSource;` 추가, 씬에 2D AudioSource 하나 배치해서 연결.
3. `PlayDialogue()`에서 각 줄을 시작할 때(`RevealPortraitIfNeeded` 직후) `line.voiceClip`이 있으면 `voiceAudioSource.Stop() → clip 교체 → volume = SoundManager.Instance.GetMasterVolume() * GetVoiceVolume() (뮤트면 0) → Play()`. 클립이 없으면(아직 준비 안 된 인물) 조용히 건너뜀 - 텍스트 타이핑은 지금처럼 그대로 진행.
4. 볼륨/뮤트는 새 시스템을 만들지 않고 `SoundManager`의 기존 Voice 볼륨/뮤트 설정(`GetMasterVolume`/`GetVoiceVolume`/`IsVoiceMuted`, 이미 공개 메서드로 있음)을 그대로 읽어서 적용 - 설정 화면의 "Voice" 슬라이더가 브리핑 음성에도 동일하게 적용됨.

## 범위 밖
- 타이핑 속도를 음성 길이에 맞춰 자동 조절 - 요청에 없음, 지금처럼 `charsPerSecond` 고정 속도 유지.
- 클립이 없는 줄에 임시 무음/플레이스홀더 톤 재생 - 그냥 무음 처리.
- 자막(현재도 텍스트 로그가 곧 자막 역할).

## 구현 완료
- `BriefingRoomController.cs`: `BriefingLine.voiceClip`(AudioClip) 필드 추가, `[SerializeField] private AudioSource voiceAudioSource` 추가, `PlayLineVoice()` 헬퍼로 줄 시작 시 클립 재생(없으면 스킵) + `SoundManager`의 기존 Voice 볼륨/뮤트 설정 반영. 컴파일 성공(에러 0, 기존과 무관한 경고 49개만).
- `Briefing_Room.unity`: `BriefingRoomController`가 붙은 `Canvas` 오브젝트에 `AudioSource`(playOnAwake=false, spatialBlend=0, loop=false) 추가하고 `voiceAudioSource` 필드에 연결, 씬 저장 완료.
- `BriefingLine.voiceClip`은 전부 비워둔 상태 - 각 줄의 인스펙터에서 [[0629-briefing-dialogue-script-by-character-en]]의 `[인물] Mission N - Line i` 클립을 직접 드래그해서 채우면 됨(부관 줄부터).

## 상태
완료 (클립 채워넣기는 사용자 몫).
