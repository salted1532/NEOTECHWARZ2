# 0474 - NTA/OC/스포어 브루드 유닛·건물 아이콘 연결

## 결과
사용자 확인 후 아래 매핑대로 6개 SO 에셋(총 36개 항목, 스피터는 립팽 이미지 임시 공유) 전부 적용함.

**적용 중 발견한 문제**: 처음에 Single Sprite 관례인 `fileID: 21300000`으로 연결했더니 유니티에서
전부 `Icon == null`로 나왔음. 원인은 새로 추가된 이미지들이 전부 Sprite Mode **Multiple**로
임포트되어 있어서(슬라이스가 1개뿐이라도), 실제 서브에셋 fileID가 21300000이 아니라 각 텍스처
`.meta`의 `spriteSheet.nameFileIdTable`에 있는 해시값이었기 때문. 각 이미지의 진짜 fileID를
다시 읽어와 36개 항목 전부 재수정했고, `AssetDatabase.Refresh()` 후 36/36 `Icon` non-null +
이름 스팟체크로 최종 확인함.

## 요청 내용
"`Assets/images`의 Building/Unit 아래에 진영별로 새로 추가한 유닛/건물 이미지들을, 이걸 쓰는 모든
인스펙터에 연결해줘. 한글 이름이라 헷갈리면 외계종족 doc/0441, `Docs/EnemyUnitAndBuildingStats.md`
(OC진영)를 참고."

## 조사 내용

### 어디에 연결해야 하나
`UnitData.Icon`/`BuildingData.Icon` (`UnitDataSO.cs`/`BuildingDataSO.cs`)는 `Sprite` 필드이고,
`UIController`/`ProductionSlot`/`UnitController`/`EnemyUnitController`/`EnemyBuildingController`/
`AllyController`가 전부 이 값을 런타임에 그대로 읽어다 쓴다(`.Icon` 사용처 확인). 즉 아이콘을 연결해야
할 "인스펙터"는 프리팹이 아니라 다음 6개 데이터 SO 에셋의 `Icon` 필드뿐이다:

- `NTA Unit Data SO.asset` (9개), `NTA Building Data SO.asset` (6개)
- `OC Unit Data SO.asset` (9개), `OC Building Data SO.asset` (6개)
- `Spore Brood Unit Data SO.asset` (3개), `Spore Brood Building Data SO.asset` (3개)

(고급유닛 특성 아이콘 `traitA/traitB.icon`은 Sharpshooter의 Snipe/Cloak을 확인해보니 이미
`Assets/images/Unit/Skill/저격.png`, `은신.png`에 정확히 연결되어 있음 — 별도 작업이라 이번
범위에서 제외.)

### 현재 상태 (문제)
- **NTA 건물**: `Icon`이 전부 `{fileID: 0}` (비어있음, 미연결).
- **NTA 유닛**: `Assets/images/Unit/*.png`(진영 구분 없는 구버전 평면 폴더)를 가리키는데, 이 중 일부는
  아예 잘못 연결되어 있었음 — Sharpshooter가 Assault Trooper 아이콘(`어썰트 트루퍼.png`)을,
  SkyLancer가 Worker Drone 아이콘(`일꾼드론.png`)을 재사용 중.
- **OC 유닛/건물**: 전부 NTA 아이콘을 그대로 재사용 중 (예: Cyborg Soldier → NTA Assault Trooper
  아이콘). SO의 `description`에도 "NTA OOO 대응"이라고 명시돼 있어 의도된 임시 placeholder였음.
- **스포어 브루드 유닛/건물**: `Icon` guid가 전부 프로젝트 내 어떤 `.meta`와도 매치되지 않는 깨진 참조
  (원본 텍스처가 이미 삭제된 상태).

### 새로 추가된 이미지와의 매칭
파일명(한글)과 `doc/0441`(스포어 브루드 설계) / `Docs/EnemyUnitAndBuildingStats.md`(OC 스탯)의
영문 유닛명을 대조해서 1:1 매칭을 확인함. 전부 이름이 명확히 대응되어 모호한 항목은 없었음 —
**단, 스포어 브루드 "스피터(Spitter)" 유닛만 새 이미지가 없어서 이번엔 연결 못 함** (Ripfang/
Skitterwing 2개만 파일 존재).

## 변경 내용 (매핑 테이블)

### NTA Unit Data SO
| 유닛 (ID) | 새 아이콘 |
|---|---|
| Worker Drone (1) | `Unit/NTA/워커드론.png` |
| Assault Trooper (2) | `Unit/NTA/어썰트트루퍼.png` |
| Scout Drone (3) | `Unit/NTA/스카웃드론.png` |
| Sharpshooter (4) | `Unit/NTA/샤프슈터.png` ← 기존 오연결 수정 |
| Ranger IFV (5) | `Unit/NTA/IFV레인저.png` |
| Pulsar Tank (6) | `Unit/NTA/펄스탱크.png` |
| SkyLancer (7) | `Unit/NTA/스카이랜서.png` ← 기존 오연결 수정 |
| Firehawk (8) | `Unit/NTA/파이어호크.png` |
| Guardian Drone (9) | `Unit/NTA/가디언드론.png` |

### NTA Building Data SO
| 건물 (ID) | 새 아이콘 |
|---|---|
| CommandCenter (1) | `Building/NTA/커맨드센터 1.png` |
| SupplyDepot (2) | `Building/NTA/보급고 1.png` |
| Barracks (3) | `Building/NTA/병영.png` |
| Factory (4) | `Building/NTA/공장.png` |
| Spaceport (5) | `Building/NTA/공항.png` |
| Lab (6) | `Building/NTA/연구소 1.png` |

### OC Unit Data SO
| 유닛 (ID) | 새 아이콘 |
|---|---|
| Nanobot Repair (1) | `Unit/OC/나노로봇리페어.png` |
| Cyborg Soldier (2) | `Unit/OC/사이보그드론.png` |
| Striker (3) | `Unit/OC/스트라이커.png` |
| Railgunner (4) | `Unit/OC/레일거너.png` |
| Brute Mech (5) | `Unit/OC/브루트메카.png` |
| Heavy Assault Tank (6) | `Unit/OC/헤비어썰트탱크.png` |
| Ironhawk (7) | `Unit/OC/아이언호크.png` |
| Raven (8) | `Unit/OC/레이븐.png` |
| Strike Drone (9) | `Unit/OC/스트라이크드론.png` |

### OC Building Data SO
| 건물 (ID) | 새 아이콘 |
|---|---|
| Omega Core (1) | `Building/OC/오메가 코어.png` |
| Cargo Silo (2) | `Building/OC/카고 사일로.png` |
| Cyber Foundry (3) | `Building/OC/사이버 파운드리.png` |
| Mech Yard (4) | `Building/OC/메크 야드.png` |
| Drone Hangar (5) | `Building/OC/드론 행어.png` |
| Neural Lab (6) | `Building/OC/뉴럴 랩.png` |

### Spore Brood Unit Data SO
| 유닛 (ID) | 새 아이콘 |
|---|---|
| 립팽/Ripfang (10) | `Unit/Spore Brood/립팽.png` |
| 스피터/Spitter (11) | `Unit/Spore Brood/립팽.png` (전용 이미지 없어 립팽과 임시 공유, 사용자 확인) |
| 스키터윙/Skitterwing (12) | `Unit/Spore Brood/스키터윙.png` |

### Spore Brood Building Data SO
| 건물 (ID) | 새 아이콘 |
|---|---|
| 하이브 코어 (7) | `Building/Spore Brood/하이브 코어.png` |
| 산란구덩이 (8) | `Building/Spore Brood/산란구덩이.png` |
| 바이오리액터 (9) | `Building/Spore Brood/바이오리액터.png` |

## 적용 방법
각 `.asset` YAML 파일에서 해당 엔트리의 `<Icon>k__BackingField:` 줄을 다음 형식으로 교체
(모든 신규 이미지가 Single Sprite로 임포트되어 있어 fileID는 전부 `21300000`으로 동일):
```
<Icon>k__BackingField: {fileID: 21300000, guid: <위 표의 파일에 대응하는 .meta guid>, type: 3}
```

이대로 6개 SO 에셋에 적용할까요? (스피터는 이미지가 없어 그대로 둡니다.)
