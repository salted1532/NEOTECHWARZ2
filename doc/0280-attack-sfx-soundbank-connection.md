# 0280 - 유닛별 공격 SFX SoundBank 연결

**날짜:** 2026-07-29

## 요청 내용

> 지금 sound폴더안에 유닛별 공격 sfx 사운드 클립을 넣어뒀는데 그걸 사운드 뱅크에 공격 sfc에
> 연결해줘

## 조사 내용

`Assets/Sound/NTA/Unit/<유닛명>/SFX/` 를 전수 조사한 결과, 공격 관련 클립은 파일명에
`_attack` 또는 `_attack<N>`이 붙어 있고, 각 유닛당 정확히 1개씩만 존재한다 (voice와 달리
여러 개 랜덤 재생용이 아님).

| 유닛 | SFX 폴더 안 파일 | `_attack` 클립 |
|---|---|---|
| Assault Trooper | `Rifle_attack.wav` | `Rifle_attack.wav` |
| Firehawk | `airplane_move.mp3`, `laser_attack2.wav` | `laser_attack2.wav` |
| Guardian Drone | `Explosion_attack.wav` | `Explosion_attack.wav` |
| Pulasr Tank | `Explosion_attack.wav` | `Explosion_attack.wav` |
| Ranger IFV | `Explosion_attack.wav` | `Explosion_attack.wav` |
| Scout Drone | `laser_attack2.wav` | `laser_attack2.wav` |
| Sharpshooter | `Marksman_attack.mp3` | `Marksman_attack.mp3` |
| SkyLancer | `Explosion_attack.wav`, `vehicle_move1.mp3` | `Explosion_attack.wav` |
| WorkerDrone | `laser_attack2.wav` | `laser_attack2.wav` (⚠️ 아래 참고) |

`Assets/Sound/NTA/Building/`, `Assets/Sound/OC/` 은 아직 파일이 없어 이번 범위에서 제외.

각 유닛의 `UnitSoundBankSO.attackSFX`(`Assets/Scripts/ScriptableObject/Sound/NTA/Unit/*.asset`)는
현재 전부 `clips: []` 빈 상태. `_attack` 클립의 `.meta` guid를 이용해
`{fileID: 8300000, guid: <guid>, type: 3}` 형식으로 `attackSFX.clips`에 1개씩 채워 넣을 예정
(doc/0261의 voice 연결과 동일한 방식).

## 확인한 점

- **WorkerDrone**: doc/0261 당시엔 attack voice 클립이 0개라 공격이 없는 유닛으로 판단했었으나,
  사용자 확인 결과 **워커도 공격 능력이 있음** → 다른 유닛과 동일하게 `attackSFX` 연결함.
- `laser_attack2.wav`(Firehawk/Scout Drone/WorkerDrone 3곳에 동일 파일, 크기 410702바이트로
  동일)는 있는 그대로 각 유닛에 연결(세 유닛이 같은 공격 사운드를 공유).

## 적용한 매핑 (9개 유닛 전체)

| 유닛 | 연결할 파일 | guid |
|---|---|---|
| Assault Trooper | `Rifle_attack.wav` | `eba2f54f254157b41bb635af719b3d59` |
| Firehawk | `laser_attack2.wav` | `97ffdb7bc19689245a15b98e41b70bb5` |
| Guardian Drone | `Explosion_attack.wav` | `8e83f00b740672246a8c60d07a9e0dc7` |
| Pulasr Tank | `Explosion_attack.wav` | `1cabc812a5bb89c44b76b05265efab92` |
| Ranger IFV | `Explosion_attack.wav` | `91ba571d553f8e543b845b3e9d055c8c` |
| Scout Drone | `laser_attack2.wav` | `992d0ff18110adf4dbed9c45740f3922` |
| Sharpshooter | `Marksman_attack.mp3` | `201664f7673df3c449f5a6a53adfda16` |
| SkyLancer | `Explosion_attack.wav` | `14556b4177cf32646b27cb3ad8a8743d` |
| WorkerDrone | `laser_attack2.wav` | `e8bf3ae1c04c8c64c937331a3ad51ae3` |

## 변경된 파일

`Assets/Scripts/ScriptableObject/Sound/NTA/Unit/` 아래 9개 `UnitSoundBankSO.asset` 전부:
`attackSFX.clips`에 위 표의 클립을 각 1개씩(`{fileID: 8300000, guid: <guid>, type: 3}`) 채워 넣음.
그 외 필드(`spawnSFX`/`deathSFX`/`skillSFX`/`gatherSFX`/`selectSFX`/`orderSFX`)는 이번 요청
범위(공격 SFX만) 밖이라 손대지 않음.

## 결과

9개 유닛 전부 `attackSFX`에 클립 1개씩 연결 완료.
