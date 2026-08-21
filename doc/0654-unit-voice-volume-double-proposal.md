# 0654. 유닛 음성 볼륨 2배 (제안)

## 요청
"유닛 음성들을 현재 소리에서 2배정도 크기 만들어줘"

## 조사
- 재생 경로: `UnitAudio.cs`가 `SoundManager.PlayVoice()`(스폰/사망/건설완료/건설실패)와
  `PlayOrderVoice()`(선택/이동·순찰명령/공격명령)를 호출. 둘 다 최종 볼륨을
  `EffectiveVolume(voiceVolume, voiceMuted) * set.volumeScale`로 계산함
  (`SoundManager.cs:257,287`). `voiceVolume`은 옵션창 슬라이더(0~1, 기본 1=최대)라 이미
  최대라서 못 올림 - 유닛 음성만 키우려면 `set.volumeScale`(클립셋 1개당 개별 배율, 기본 1)을
  쓰는 게 맞음. Unity `AudioSource.volume`은 1을 넘겨도 정상 재생됨(클리핑 위험은 있지만
  이미 이 값이 그대로 `AudioSource.volume`에 들어감).
- `volumeScale`은 유닛 사운드뱅크(`UnitSoundBankSO`) 애셋 21개
  (`Assets/Scripts/ScriptableObject/Sound/{NTA,OC,Spore_Brood}/Unit/*.asset`) 각각에
  필드별로 저장된 데이터 값 - 코드가 아니라 애셋 YAML 수정.
- "유닛 음성"에 해당하는 필드만 2로 올리고, SFX(공격/스폰엔진음/채취/스킬/선택확인음/명령확인음)는
  그대로 둘 계획:
  `spawnVoice`, `selectVoice`, `orderVoice`, `attackOrderVoice`, `deathVoice`,
  `buildCompleteVoice`, `buildFailVoice` (워커만 값 있음) - 필드당
  `<volumeScale>k__BackingField: 1` → `2`.
- 건물 음성(`BuildingSoundBankSO.selectVoice`, `BuildingAudio.cs`가 `PlayVoice()`로 재생)과
  전역 나레이션(`GlobalVoiceBankSO`, `PlayGlobalVoice()` - 자원부족/피격경고 등)은 "유닛"이
  아니므로 손대지 않을 계획.

## 상태
완료.

## 확인 결과
AskUserQuestion으로 범위 확인 → "유닛+나레이션까지" 선택. 건물 선택음성
(`BuildingSoundBankSO.selectVoice`)은 범위 밖으로 제외.

## 구현/검증
- 유닛 사운드뱅크 21종(`Assets/Scripts/ScriptableObject/Sound/{NTA,OC,Spore_Brood}/Unit/*.asset`)의
  `spawnVoice`/`selectVoice`/`orderVoice`/`attackOrderVoice`/`deathVoice`/
  `buildCompleteVoice`/`buildFailVoice` 7개 필드 `volumeScale` 1→2 (총 147개 필드,
  일회성 Node 스크립트로 필드명 기준으로만 골라 수정 후 삭제).
- `Global Voice Bank SO.asset`(나레이션: 자원부족/인구부족/유닛피격경고/건물피격경고/
  업그레이드완료/미션성공/행동실패/영토점령/승리화면) 8개 필드 전부 `volumeScale` 1→2.
- SFX 필드(공격/스폰엔진음/사망/스킬/채취/선택확인음/명령확인음)와 건물 음성은 손대지
  않음 - `git diff`로 대상 22개 파일에서 `volumeScale` 라인만 변경됐음을 확인.
- 코드 변경 없음(데이터만) - 컴파일 불필요.
