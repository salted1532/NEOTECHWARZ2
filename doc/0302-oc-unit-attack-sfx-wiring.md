# 0302 - OC 유닛 공격 SFX 클립을 사운드 뱅크에 연결

날짜: 2026-07-30

## 요청 내용

"이제 적 유닛들의 공격 sfx 클립들을 각 폴더에 넣었거든 이걸 각 사운드 뱅크에 연결해줘"

## 조사 내용

`Assets/Sound/OC/Unit/<유닛명>/SFX/`에 각 유닛당 파일이 1개씩 들어와 있고, `.meta`도 이미 생성되어 있어 guid를 바로 읽을 수 있었다:

| 유닛 | 클립 파일 | guid |
|---|---|---|
| Nanobot Repair | `laser_attack2.wav` | `1d3b5d28c4a19a04698e8a956bbdfebf` |
| Cyborg Soldier | `Rifle_attack.wav` | `1569c5fa873d84246be2b2aecec413fe` |
| Striker | `Marksman_attack.mp3` | `681c339bc68b267448b07d326490387a` |
| Railgunner | `laser_attack.wav` | `dd38db717367a1940a1961f520396431` |
| Brute Mech | `radio-select.mp3` | `b64babbd3ac7cc945b8cb0d21a6d612e` |
| Heavy Assault Tank | `Explosion_attack.wav` | `83fd65637d3fa8447b87ea563c2bce65` |
| Ironhawk | `rocket-attack2.mp3` | `c709664a7b3bac04e9af460a1eea5b86` |
| Raven | `laser_attack2.wav` | `ec9fc02b7e6a61e4faa76deef847354f` |
| Strike Drone | `Explosion_attack.wav` | `dbc488c56a8b2fe4d98bac3b9cfbc102` |

**주의**: Brute Mech 폴더의 파일명만 `radio-select.mp3`로, 다른 8개(`*_attack*`)와 이름 패턴이 다르다 - "라디오 선택음"류 이름이라 공격 사운드가 아니라 다른 용도로 잘못 들어갔을 가능성이 있어 보인다. 확인 필요.

각 유닛의 `UnitSoundBankSO` 에셋(`Assets/Scripts/ScriptableObject/Sound/OC/Unit/<유닛명> Unit Sound Bank SO.asset`)의 `attackSFX.clips`는 현재 전부 빈 배열(`[]`)이다.

## 확인 결과

"9개 전부 연결 (추천)" 선택 → Brute Mech의 `radio-select.mp3`도 그대로 포함해 9개 전부 적용.

## 코드 변경 (적용 완료)

각 `<유닛명> Unit Sound Bank SO.asset`에서, 예: Nanobot Repair -

기존 코드:

```yaml
  <attackSFX>k__BackingField:
    <clips>k__BackingField: []
```

변경 코드:

```yaml
  <attackSFX>k__BackingField:
    <clips>k__BackingField:
    - {fileID: 8300000, guid: 1d3b5d28c4a19a04698e8a956bbdfebf, type: 3}
```

나머지 8개 유닛도 각자의 attackSFX에 표의 guid로 동일하게 클립 1개씩 추가.

## 요약

9개 유닛 전부 attackSFX에 클립 1개씩 연결 완료. Brute Mech의 `radio-select.mp3`는 파일명 패턴이 달라 공격음이 맞는지 확인이 필요하다고 안내했으나, 사용자가 그대로 연결하기로 결정.

## 변경된 파일

- `Assets/Scripts/ScriptableObject/Sound/OC/Unit/*.asset` (9개 수정 - attackSFX.clips 채움)
- `doc/0302-oc-unit-attack-sfx-wiring.md` (이 파일, 신규)
