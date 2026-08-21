# 0651 - 유닛별 사운드 클립/나레이션 음성 연결 상태 확인

## 날짜
2026-08-21

## 요청 내용
"각 유닛별 사운드 클립이랑 나레이션 음성들 같은이름으로 된 새로운 클립으로 변경했는데 연결 잘되어있는지 확인하고 안되어있으면 연결해줘"
(각 유닛 사운드/나레이션 mp3를 같은 파일명의 새 클립으로 교체했는데, 프로젝트 내 참조(ScriptableObject 등)가 여전히 제대로 연결돼 있는지 확인 요청)

추가로 "General 폴더 나레이션도 포함해서 확인해달라"고 범위를 확장함(질문으로 확인).

## 조사 내용

Unity는 오디오 클립을 파일명이 아니라 `.meta` 파일의 **GUID**로 참조한다 (`UnitSoundBankSO`/`GlobalVoiceBankSO` 등 ScriptableObject의 필드에 GUID가 박혀 있음). 즉 같은 경로에 파일 내용만 덮어쓰면 GUID가 그대로 유지되어 자동으로 연결이 살아있고, 파일이 **삭제됐다가 다른 경로로 새로 생성**되면 새 GUID가 발급되어(단, 이번 케이스처럼 에디터에서 이동한 경우엔 예외) 링크가 끊길 수 있다.

`git status`로 `Assets/Sound` 하위 변경사항을 전수 확인한 결과, 세 그룹으로 나뉜다.

### 1) 유닛별 Voice 클립 (145개) — 정상 연결, 조치 불필요
Assault Trooper / Firehawk / Guardian Drone / Pulasr Tank / Ranger IFV / Scout Drone / Sharpshooter / SkyLancer / WorkerDrone의 `Voice/` 폴더 mp3 145개가 전부 **같은 경로에 내용만 덮어쓰기(M)** 되었고 `.meta`는 단 하나도 건드리지 않음(diff 없음). 따라서 GUID가 그대로 유지되어 `UnitSoundBankSO` 참조(`Assets/Scripts/ScriptableObject/Sound/NTA/Unit/*.asset`)가 자동으로 유효하다.

- 예시 검증: `Firehawk_attack1.mp3.meta`의 guid `74cbfd6f2eb8bd14eb28f09849906405`가 `Firehawk Unit Sound Bank SO.asset`에 그대로 참조돼 있음을 직접 확인.
- `SFX/` 폴더(공격 이펙트음 등)는 이번 교체 대상에서 아예 제외되어 변경 없음.

### 2) General 나레이션 7종 — 6종은 정상 연결, **1종(victoryScreen) 링크 끊김**
`Assets/Sound/General/` 바로 아래 있던 7개 파일이 삭제(D)되고, 그중 6개는 새 하위 폴더 `Assets/Sound/General/나레이션/`에 같은 파일명으로 다시 생성(??, untracked)됐다. `GlobalVoiceBankSO`(`Assets/Scripts/ScriptableObject/Sound/Global Voice Bank SO.asset`, `SoundManager`가 참조)의 필드별 대응:

| 필드 | 원본 파일 | 이전 GUID | 나레이션/ 폴더의 새 GUID | 상태 |
|---|---|---|---|---|
| insufficientResources | Not enough resources.mp3 | `29c823cc...` | `29c823cc...` (동일) | ✅ 연결됨 |
| insufficientPopulation | Increase population capacity.mp3 | `8d6816ab...` | `8d6816ab...` (동일) | ✅ 연결됨 |
| unitUnderAttackWarning | Our forces are under attack.mp3 | `89a2478c...` | `89a2478c...` (동일) | ✅ 연결됨 |
| buildingUnderAttackWarning | Base under attack.mp3 | `18c4c7d8...` | `18c4c7d8...` (동일) | ✅ 연결됨 |
| upgradeComplete | Upgrade complete.mp3 | `6869f8df...` | `6869f8df...` (동일) | ✅ 연결됨 |
| territoryCaptured | 여성부관_거점점령.mp3 | `67bb3d7f...` | `67bb3d7f...` (동일) | ✅ 연결됨 |
| victoryScreen | VictoryPanel_Sound.mp3 | `8c0d55cf...` | `8c0d55cf...` (동일, 원래 경로에 복구됨) | ✅ 연결됨 |

6종은 Unity 에디터에서 폴더로 옮겨서(같은 `.meta` 그대로 이동) GUID가 보존됐다. `VictoryPanel_Sound.mp3`는 조사 중 한때 삭제된 상태로 확인됐으나, 사용자가 실수로 지운 것을 원래 경로(`Assets/Sound/General/VictoryPanel_Sound.mp3`)에 같은 GUID로 복구하여 현재는 문제없다 (git status상 더 이상 변경 없음, `.meta` guid 일치 재확인 완료).

### 3) 새 폴더 `병사/`, `셀리나/` (미션 대사로 추정, 22개 파일) — 기존 참조 없음, 별개 사안
`[soldier]Mission N ... - Line N.mp3`, `[Selena]Mission N ... - Line N.mp3` 형태의 새 파일들은 프로젝트 전체(스크립트/asset/prefab/scene)를 검색해도 **어디서도 참조되지 않는다** — 즉 기존에 연결되어 있다가 끊긴 게 아니라애초에 아무 시스템에도 연결된 적이 없는 완전히 새로운 에셋으로 보인다. "같은 이름으로 교체한 클립"이 아니라 신규 브리핑/미션 대사용 소재로 보이며, 유닛 사운드/나레이션 재연결 작업 범위에 들어가지 않는다.

## 결론
- **유닛 사운드 클립 145개 + General 나레이션 7종(victoryScreen 포함) 전부 정상 연결되어 있다.** `VictoryPanel_Sound.mp3`는 조사 도중 삭제된 상태로 발견됐으나, 사용자가 실수로 지운 것을 확인하고 원래 경로에 동일 GUID로 직접 복구함 — 재조사 결과 문제없음.
- 조치가 필요한 코드/에셋 변경은 없었다.
- `병사/`, `셀리나/` 폴더(신규 미션 대사로 추정, 22개, 어디서도 참조되지 않음)는 이번 "같은 이름으로 교체한 클립 재연결" 요청과는 무관한 별개 신규 콘텐츠로 판단, 이번 작업 범위에서 제외.

## 변경된 파일
없음 (조사만 수행 — 프로젝트 코드/에셋은 이번 세션에서 변경하지 않음. `VictoryPanel_Sound.mp3` 복구는 사용자가 직접 수행)
