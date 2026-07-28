# 0261 - NTA 유닛 SoundBank의 Voice 클립 일괄 연결

**날짜:** 2026-07-28

## 요청 내용

> 내가 Sound 폴더에 NTA의 Building과 Unit 두개의 폴더를 만들었고 건물 같은경우는 나레이션 부관의
> 목소리만 나오면 되는거라서 하나의 건물사운드뱅크를 사용할 예정이고 유닛에 경우 각 사운드 뱅크를
> 유닛스크립터블 오브젝트에 연결해뒀고 NTA->Unit폴더 안에 각 유닛의 이름별로 폴더를 만들고 생성,
> 선택, 명령(이동,순찰,스킬 등), 공격, 사망 과 같은 소리들을 유닛명_select, order, attack 등으로
> 정리해뒀거든 그걸 스크립터블오브젝트의 NTA->Unit폴더 안에 각 유닛의 스크립터블 오브젝트의 voice
> 부분으로만 맞게 판단하여 적용시켜줘. 일꾼은 건설 완료, 건설 실패(가로막힌경우) 2개가 더있고 스킬
> 사용할때 나는 소리도 order 클립들로 넣어주면돼.

## 조사 내용

`Assets/Sound/NTA/Unit/<유닛이름>/Voice/` 밑에 이미 만들어진 mp3 파일들을 전수 조사(Glob)하고, 각
`.meta`에서 guid를 grep으로 뽑아 다음과 같이 확인했다:

- 파일명 규칙: `<유닛명>_spawnvoice`(1개), `_select1~5`, `_order1~5`(유닛별로 4~7개까지 다양), `_attack1~7`
  (유닛별로 4~7개), 워커만 추가로 `_buildcomplete1~3`, `_buildfail1~3`.
- **`death`(사망) 카테고리 파일은 9개 유닛 전부 존재하지 않음** → `deathVoice`는 이번에 비워둔다.
- `Assets/Sound/NTA/Building/`은 아직 파일이 하나도 없음(사용자가 폴더만 만들어둔 상태) → 건물
  SoundBank는 이번 작업 범위에서 제외.
- `Assets/Scripts/ScriptableObject/Sound/NTA/Unit/*.asset` 9개(WorkerDrone/Assault Trooper/
  Scout Drone/Sharpshooter/Pulasr Tank/Ranger IFV/SkyLancer/Firehawk/Guardian Drone) 전부 doc/0255
  구현 당시 만든 `UnitSoundBankSO` 그대로, 아직 `clips`가 전부 `[]`인 빈 상태였다.

## 적용한 매핑 규칙

| 파일명 접미사 | 연결한 필드 | 비고 |
|---|---|---|
| `_select<N>` | `selectVoice` | |
| `_order<N>` | `moveVoice` | 요청대로 이동/순찰/스킬 사용 음성을 전부 이 필드 하나로 통합 |
| `_attack<N>` | `attackOrderVoice` | |
| `_spawnvoice` | `spawnVoice` | 1개뿐이라 리스트에 1개만 |
| `_buildcomplete<N>` (워커 전용) | `buildCompleteVoice` | |
| `_buildfail<N>` (워커 전용) | `buildFailVoice` | |
| (없음) | `deathVoice` | 소스 파일이 없어 빈 채로 둠 |

`.meta`의 `guid`를 이용해 `{fileID: 8300000, guid: <guid>, type: 3}` 형식(유니티가 AudioClip을
참조할 때 쓰는 고정 fileID)으로 각 `SoundClipSet.clips` 리스트에 직접 채워 넣었다.

## 결과 (유닛별 카운트)

| 유닛 | select | order(=move) | attack | spawn | buildComplete/Fail |
|---|---|---|---|---|---|
| WorkerDrone | 5 | 5 | 0 (공격 없음) | 1 | 3 / 3 |
| Assault Trooper | 5 | 5 | 5 | 1 | - |
| Scout Drone | 5 | 5 | 5 | 1 | - |
| Sharpshooter | 5 | 5 | 5 | 1 | - |
| Pulasr Tank | 5 | 5 | 7 | 1 | - |
| Ranger IFV | 5 | 5 | 4 | 1 | - |
| SkyLancer | 4 | 4 | 4 | 1 | - |
| Firehawk | 5 | 5 | 7 | 1 | - |
| Guardian Drone | 5 | 5 | 5 | 1 | - |

## 변경된 파일

`Assets/Scripts/ScriptableObject/Sound/NTA/Unit/` 아래 9개 `UnitSoundBankSO` 에셋 전부:
`WorkerDrone Unit Sound Bank SO.asset`, `Assault Trooper Unit Sound Bank SO.asset`,
`Scout Drone Unit Sound Bank SO.asset`, `Sharpshooter Unit Sound Bank SO.asset`,
`Pulasr Tank Unit Sound Bank SO.asset`, `Ranger IFV Unit Sound Bank SO.asset`,
`SkyLancer Unit Sound Bank SO.asset`, `Firehawk Unit Sound Bank SO.asset`,
`Guardian Drone Unit Sound Bank SO.asset` — 각각 `selectVoice`/`moveVoice`/`attackOrderVoice`/
`spawnVoice`(+워커만 `buildCompleteVoice`/`buildFailVoice`) 필드에 해당 클립 참조를 채워 넣음.
`attackSFX`/`spawnSFX`/`deathSFX`/`skillSFX`/`gatherSFX`/`deathVoice`는 이번 요청 범위(voice만) 밖이라
그대로 빈 채로 뒀다.

## 남은 작업

- `Assets/Sound/NTA/Building/`에 건물용 나레이션 클립을 넣고 `NTA Building Sound Bank SO.asset`에
  연결하는 작업은 아직 안 함(파일이 아직 없어서).
- 사망(death) 음성 클립이 준비되면 각 유닛 SoundBank의 `deathVoice`에 추가로 채워야 함.
- SFX(공격음/생성음/사망음/스킬음/채취음)는 이번 요청이 "voice 부분만"이라고 명시해 손대지 않음 -
  나중에 SFX 클립도 준비되면 동일한 방식으로 채우면 됨.
