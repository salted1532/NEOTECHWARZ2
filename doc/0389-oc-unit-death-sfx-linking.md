# 0389 - OC 유닛 SoundBank explosion_death 클립을 deathSFX에 연결

**날짜:** 2026-08-03

**승인 후 구현 완료.**

## 요청 내용

> Sound 폴더에 OC폴더 안에 유닛별 각 SFX폴더 안에 explosion_death라는 클립이 하나씩 들어있는데
> 각 클립을 해당 유닛의 사운드 클립의 사망소리에 연결해줘

## 조사 결과

- `Assets/Sound/OC/Unit/<유닛명>/SFX/explosion_death.mp3`가 9개 유닛 폴더 중 8개에 있음 -
  **Heavy Assault Tank**만 해당 클립이 없음(그 폴더 SFX엔 `Explosion_attack.wav`만 존재).
- 각 유닛의 `UnitSoundBankSO` 에셋(`Assets/Scripts/ScriptableObject/Sound/OC/Unit/*.asset`)의
  `<deathSFX>` 필드가 전부 빈 배열(`<clips>k__BackingField: []`)이었음. 사망 시 재생 로직은
  `UnitAudio.HandleDeath()`(`Assets/Scripts/Audio/UnitAudio.cs:141~148`)가
  `SoundManager.Instance?.PlaySFX(bank.deathSFX, transform.position)`로 이미 연결돼 있어서,
  에셋의 `deathSFX.clips`만 채우면 됨(코드 변경 불필요, 데이터 연결만).

## 변경 내용

각 유닛의 `.asset` 파일에서 `<deathSFX><clips>`에 해당 유닛 폴더의 `explosion_death.mp3` 1개씩 추가:

| 유닛 | 연결한 클립 guid |
|---|---|
| Brute Mech | `1ca67de45b7156047930a126117ea040` |
| Cyborg Soldier | `57ec14629553e6844b86a1c148b0faca` |
| Ironhawk | `d807249e1b266924089941e6fe90fd5e` |
| Nanobot Repair | `a412697b0cc062543b817104bc6139e3` |
| Railgunner | `0b6330b29f399cd4c96240f6534481f1` |
| Raven | `1f0878225b242674bb7a9efd1ff209b6` |
| Strike Drone | `0679a6ce37f49894785562a03abc4017` |
| Striker | `4d3000997fe7b9d439615819f55f2e6f` |
| Heavy Assault Tank | 연결 안 함 - 해당 SFX 폴더에 explosion_death 클립 자체가 없음 |

## 검증

- 8개 에셋 파일 모두 `grep`으로 `<deathSFX><clips>`에 정확히 1개 항목(`fileID: 8300000`, 각 유닛
  고유 guid, `type: 3`)이 들어갔는지 확인 완료. 코드 변경이 없어 `uloop-cli compile` 검증은 해당 없음.

## 영향받는 파일

- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Brute Mech Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Cyborg Soldier Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Ironhawk Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Nanobot Repair Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Railgunner Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Raven Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Strike Drone Unit Sound Bank SO.asset`
- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/Striker Unit Sound Bank SO.asset`
