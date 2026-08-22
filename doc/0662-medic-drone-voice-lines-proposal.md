# 0662 - 메딕 드론(Medic Drone) 음성 대사 작성 (텍스트 초안, 코드/에셋 미변경)

## 요청
> 메딕 드론의 음성대사들을 작성해줘 각 명령에 맞춰서 필요한 음성대사를 만들어줘
> (후속) 영어로 된 대사집이어야해 - 대사 자체를 영어로 다시 작성

## 배경 조사
- 메딕 드론은 doc/0661에서 설계/구현된 NTA Tier1 지원 유닛(ID 10). 무공격
  (`attackDamage/attackRange/attackSpeed = 0`), 사거리 안 다친 아군을 나노 재생 빔으로
  자동 치유. `NTA Unit Data SO.asset`에 항목은 있지만 `soundBank`는 아직 fileID 0(빈 값) -
  실제 음성 클립/에셋이 없는 상태.
- `UnitSoundBankSO.cs`의 Voice 카테고리는 5개: `selectVoice`(선택, 3~4개 권장),
  `orderVoice`(이동/순찰, 3~4개 권장), `attackOrderVoice`(공격 명령, 1~2개 권장),
  `spawnVoice`(생산완료), `deathVoice`(사망). `buildCompleteVoice`/`buildFailVoice`는 워커
  전용이라 메딕 드론은 해당 없음.
- 메딕 드론은 무공격 유닛이라 평소 `attackOrderVoice`가 쓰일 일은 거의 없지만, 아군 강제공격
  (A모드) 등 커맨드 자체는 유닛 종류와 무관하게 걸릴 수 있어 필드가 비어있지 않게 채워두는 게
  안전 - 스타크래프트 메딕류의 "난 전투 요원이 아니다" 뉘앙스로 채움.
- 치유 틱 자체는 `healTickSFX`(효과음)만 있고 전용 보이스 슬롯이 없음 - 요청 범위 밖이라 코드
  변경(새 필드 추가)은 하지 않고, 참고용 "보너스" 대사만 아래 별도로 적어둠(나중에
  `healVoice` 필드를 추가하고 싶어지면 재사용 가능).

## 대사 초안 (영어, 전부 텍스트 스크립트 - 실제 오디오 파일/클립 연결은 별도 작업)

### 1. 선택 (`selectVoice`, 4개)
1. "Medic Drone, standing by."
2. "Report casualty location."
3. "Nano-regen systems, nominal."
4. "In need of treatment?"

### 2. 이동/순찰 명령 (`orderVoice`, 4개)
1. "Moving out."
2. "Heading to position."
3. "Relocating to the front."
4. "Adjusting support position."

### 3. 공격 명령 (`attackOrderVoice`, 2개 - 무공격 유닛용 거절 뉘앙스)
1. "I'm... not a combatant."
2. "No offensive systems installed."

### 4. 생산 완료 (`spawnVoice`, 2개)
1. "Medic Drone, deployed."
2. "Medical support, ready."

### 5. 사망 (`deathVoice`, 2개)
1. "Regen systems... offline..."
2. "Requesting... support..."

### 보너스 - 치유 중 대사 (현재 전용 필드 없음, 참고용)
- "Regen beam, engaged."
- "Restoring vitals."
- "Bleeding suppressed."

## 다음 단계 (사용자 작업 - 승인 시)
1. 위 텍스트로 실제 성우 녹음 또는 TTS 생성.
2. 녹음된 클립을 `NTA Unit Data SO.asset`(ID 10)용 `UnitSoundBankSO` 에셋을 새로 만들어
   각 카테고리에 연결(다른 NTA 유닛과 동일한 방식, `WorkerDrone Unit Sound Bank SO.asset` 참고).
3. `NTA Unit Data SO.asset`의 메딕 드론 항목 `soundBank` 필드에 그 에셋 연결.

코드/에셋 변경 없음 - 대사 텍스트만 작성.
