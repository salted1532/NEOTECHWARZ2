## 날짜
2026-07-29

## 요청 내용
README.md와 Docs/ 폴더(스크립트별 상세 문서)를 최신 상태로 갱신 — 새로 생긴 스크립트는 Docs/에 문서를 추가하고 README에 기존 방식대로 연결, 진행상황(TODO)도 갱신. 사용자가 프로젝트 전체 TODO 리스트를 첨부(대부분 이미 완료 체크 표시).

## 조사 내용
`Assets/Scripts/**/*.cs` 전체와 기존 `Docs/*.md` 목록을 대조한 결과, doc/0255~0293 세션(사운드 시스템 신설, 투사체 공격, 아군사격 경고음 분리, 정보패널 스탯 확장)에서 만든 스크립트 다수가 `Docs/`에 문서가 없었음: `SoundManager`/`SoundClipSet`/`UnitAudio`/`BuildingAudio`/`UnitSoundBankSO`/`BuildingSoundBankSO`/`GlobalVoiceBankSO`/`SoundSettingsPanel`(사운드 시스템 전체), `LaserBeamAttack`(이전 세션이지만 누락), `ProjectileAttack`(doc/0290), `DamageTypes`(enum 파일, 누락). 기존 `Docs/UnitController.md`/`HealthManager.md`/`UIController.md`는 이번에 바뀐 필드/메소드(공격 전달 방식, `isEnemyAttacker`, Info Panel 툴팁 확장)가 반영 안 돼 있어 같이 갱신.

## 변경 사항

### Docs/ 신규 작성 (11개)
`Docs/SoundManager.md`, `Docs/SoundClipSet.md`, `Docs/UnitAudio.md`, `Docs/BuildingAudio.md`, `Docs/UnitSoundBankSO.md`, `Docs/BuildingSoundBankSO.md`, `Docs/GlobalVoiceBankSO.md`, `Docs/SoundSettingsPanel.md`, `Docs/LaserBeamAttack.md`, `Docs/ProjectileAttack.md`, `Docs/DamageTypes.md` — 기존 `Docs/*.md` 포맷(개요/주요 필드 표/메소드 표/연관 컴포넌트)을 그대로 따름.

### Docs/ 기존 문서 갱신
- `Docs/HealthManager.md`: `OnDamaged`/`GetDamage`에 `isEnemyAttacker` 매개변수 반영
- `Docs/UnitController.md`: `attackType`/`armorType`/`sizeType`/`attackDelivery` 필드, `Attack()`의 Projectile 분기, `GetShotCount()` 추가
- `Docs/UIController.md`: `ShowInfoPanel` 시그니처(공격타입/방어타입/크기/투사체개수 추가), 확장된 툴팁 텍스트 반영

### README.md
- 프로젝트 구조에 `Assets/Scripts/Audio/` 추가
- 핵심 스크립트 표에 `DamageTypes`/`LaserBeamAttack`/`ProjectileAttack`/사운드 8종 문서 링크 추가
- "전투" 항목에 Hitscan/Projectile 선택 가능 문구 추가, "UI" 항목의 Info Panel 툴팁 설명 갱신, 새 "사운드" 불릿 추가
- 구현 완료 기능에 "### 사운드" 섹션 신설(8개 체크 항목: BGM/SoundBank/3D 감쇠/단일채널/스팸방지/아군사격 경고음 제외/설정 UI 로직만 완료 등), 유닛 섹션의 Info Panel 툴팁 텍스트와 공격 전달 방식 체크 항목 추가, UI 섹션의 Info Panel 설명 갱신
- 로드맵에서 "유닛/건물 사운드, 사운드 매니저" 항목 제거(완료됨), "건물 고유 스킬 추가"/"볼륨 설정 UI 실제 배치" 항목 신규 추가, "Enemy AI" 항목의 `EnemyController` → `EnemyUnitController`로 표기 수정

## 요약/남은 작업
적용 완료. TODO 원문 리스트 전체를 한 줄씩 대조하지는 않았고(대부분 이미 README에 반영돼 있었음), 사운드/투사체 공격/정보패널 확장처럼 확실히 새로 반영해야 하는 항목 위주로 처리함. `UIController.md`의 구식 유닛 이름(마린/벌처 등) 같은 기존 문서 노후화는 이번 범위 밖이라 손대지 않음.

## 변경된 파일
- `Docs/SoundManager.md` (신규)
- `Docs/SoundClipSet.md` (신규)
- `Docs/UnitAudio.md` (신규)
- `Docs/BuildingAudio.md` (신규)
- `Docs/UnitSoundBankSO.md` (신규)
- `Docs/BuildingSoundBankSO.md` (신규)
- `Docs/GlobalVoiceBankSO.md` (신규)
- `Docs/SoundSettingsPanel.md` (신규)
- `Docs/LaserBeamAttack.md` (신규)
- `Docs/ProjectileAttack.md` (신규)
- `Docs/DamageTypes.md` (신규)
- `Docs/HealthManager.md`
- `Docs/UnitController.md`
- `Docs/UIController.md`
- `README.md`
