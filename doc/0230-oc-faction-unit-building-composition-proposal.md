# 0230 - OC(오메가 코퍼레이션) 진영 유닛/건물 구성 제안

## 요청

캠페인 스토리([[0229]])에 등장하는 적대 인간 진영 OC(Omega Corporation)를 실제로 구현하기 위해,
현재 NTA의 유닛/건물 구성과 비교해 OC 쪽 유닛/건물 구성안을 만들어달라는 요청.

사용자가 이전에 사용했던 "적(OC) 유닛 = NTA 대응 유닛" 이름 매핑:

| OC 유닛 (기존 명칭) | NTA 대응 유닛 |
|---|---|
| Nanobot Repair | Worker Drone |
| Cyborg Soldier | Assault Trooper |
| Striker | Scout Drone |
| Brute Mech (근접공격 유닛) | Ranger IFV |
| Heavy Assault Tank | Pulsar Tank |
| Raven | Firehawk |
| Strike Drone | Guardian Drone |

## 현재 NTA 구성 (코드/에셋 확인 결과)

### 건물 (`New Building Data SO.asset`, `RTSUnitController.BuildingID`)

| ID | 이름 | 크기 | 비용(광물/가스) | 선행조건 | 역할 |
|---|---|---|---|---|---|
| 1 | CommandCenter | 3x3 | 400/0 | - | 본진, 일꾼 생산, 인구 +10 |
| 2 | SupplyDepot | 2x2 | 100/0 | - | 인구 +8 |
| 3 | Barracks (Tier1) | 3x3 | 150/0 | - | 보병 생산 |
| 4 | Factory (Tier2) | 3x3 | 200/100 | Barracks | 차량 생산 |
| 5 | Spaceport (Tier3) | 3x3 | 150/100 | Factory | 공중유닛 생산 |
| 6 | Lab | 2x2 | 150/0 | - | 공격/방어 연구 |

### 유닛 (`New Unit Data SO.asset`)

| Tier | 이름 | HP | 공격력 | 사거리 | 공격속도 | 지상/공중 | 비용(광물/가스) | 인구 | 비고 |
|---|---|---|---|---|---|---|---|---|---|
| 0(본진) | Worker Drone | 40 | 5 | 4 | 0.6 | 지상만 | 50/0 | 1 | 일꾼 |
| 1(병영) | Assault Trooper | 40 | 5 | 12 | 0.6 | 지상+공중 | 50/0 | 1 | 기본 보병 |
| 1(병영) | Scout Drone | 75 | 6 | 14 | 1.2 | 지상만 | 75/0 | 2 | 정찰 |
| 1(병영) | Sharpshooter | 45 | 10 | 20 | 1.0 | 지상+공중 | 125/75 | 1 | 고급 저격수 |
| 2(공장) | Ranger IFV | 150 | 6 | 14 | 1.0 | 지상만 | 100/50 | 2 | 중장갑/저화력 |
| 2(공장) | Pulsar Tank | 150 | 20 | 20 | 1.5 | 지상만 | 150/100 | 2 | 중전차 |
| 2(공장) | SkyLancer | 125 | 16 | 18 | 1.0 | 공중전용(대공만) | 100/50 | 2 | 고급유닛(특성 2택1), 대공 전문 |
| 3(우주공항) | Firehawk | 150 | 8 | 18 | 1.2 | 지상+공중 | 150/100 | 2 | 경량 고속 공중 |
| 3(우주공항) | Guardian Drone | 400 | 25 | 20 | 1.2 | 지상+공중 | 400/300 | 6 | 최상위 공중 슈퍼유닛 |

정리하면 NTA는 **건물 6종 + 유닛 9종(일꾼1 + 병영3 + 공장3 + 우주공항2)** 구조. 이전 OC 매핑에는
**Sharpshooter**와 **SkyLancer**에 대응하는 이름이 빠져있음 (이 둘은 나중에 추가된 유닛으로 보임).

## 제안: OC 진영 구성 (NTA와 1:1 대응, 티어/역할 구조는 동일하게 유지)

OC는 스토리상 "군사 조직"이 아니라 "코퍼레이션(기업)"이므로, 건물명은 군사적 명칭(Command Center, Barracks) 대신
산업/기업 시설 느낌으로, 유닛은 이미 확정된 이름들의 톤(사이보그/메크/드론 - 인간을 기계로 강화한 기업형 사병)을 그대로 이어감.

### 건물 (OC)

| NTA 대응 | OC 이름(안) | 역할 |
|---|---|---|
| CommandCenter | **Omega Core** (오메가 코어) | 본진, Nanobot Repair 생산 |
| SupplyDepot | **Cargo Silo** (화물 사일로) | 인구 공급 |
| Barracks | **Cyber Foundry** (사이버 파운드리) | 보병(사이보그) 생산 |
| Factory | **Mech Yard** (메크 야드) | 차량/메크 생산 |
| Spaceport | **Drone Hangar** (드론 행거) | 공중 드론 생산 |
| Lab | **Neural Lab** (뉴럴 랩) | 연구 |

비용/크기/선행조건은 NTA와 동일하게(대칭 밸런스) 맞추는 것을 기본안으로 제안. 스토리상 대등한 세력이라
자원 채집·건물 값이 다르면 스커미시에서 밸런스가 깨지기 쉬움.

### 유닛 (OC)

| Tier | NTA 대응 | OC 이름 | 비고 |
|---|---|---|---|
| 0(본진) | Worker Drone | **Nanobot Repair** (기존 확정) | 동일 스탯 |
| 1(병영) | Assault Trooper | **Cyborg Soldier** (기존 확정) | 동일 스탯 |
| 1(병영) | Scout Drone | **Striker** (기존 확정) | 동일 스탯 |
| 1(병영) | Sharpshooter | **Railgunner** (확정) | 동일 스탯 |
| 2(공장) | Ranger IFV | **Brute Mech** (기존 확정, 사용자가 "근접공격 유닛"으로 지정) | 근접형 스탯 차별화 확정 |
| 2(공장) | Pulsar Tank | **Heavy Assault Tank** (기존 확정) | 동일 스탯 |
| 2(공장) | SkyLancer | **Ironhawk** (확정) | 동일 스탯 |
| 3(우주공항) | Firehawk | **Raven** (기존 확정) | 동일 스탯 |
| 3(우주공항) | Guardian Drone | **Strike Drone** (기존 확정) | 동일 스탯 |

### 신규 이름 확정

- **Sharpshooter 대응 → Railgunner** (병영, 고비용 저격수)
- **SkyLancer 대응 → Ironhawk** (공장, 고급유닛/대공 전용 워커형 차량)

### 스탯 설계 방향 — Brute Mech만 예외적으로 차별화 (확정)

사용자가 예전에 Brute Mech을 "근접공격 유닛"으로 명시했음. Ranger IFV(사거리 14, 원거리)를 그대로 복사하면
이 설정이 사라지므로, Brute Mech만 **사거리를 크게 줄이고 그만큼 공격력/HP를 보정**하는 근접형 스탯을 제안:

| | HP | 공격력 | 사거리 | 공격속도 | 비용 |
|---|---|---|---|---|---|
| Ranger IFV (NTA, 원거리) | 150 | 6 | 14 | 1.0 | 100/50 |
| Brute Mech (OC, 근접, 제안) | 180 | 14 | 2 | 0.8 | 100/50 |

근접이라 교전까지 접근 리스크가 있는 대신 맷집/한방딜을 높여 상쇄. 나머지 8종은 스토리 대칭성과 밸런스
단순화를 위해 **NTA와 완전히 동일한 스탯**(이름/외형/사이드만 다름)으로 가는 것을 기본안으로 제안.

## 확정된 최종 OC 구성

### 건물 (안, 아직 사용자 최종 확인 전)

| NTA | OC |
|---|---|
| CommandCenter | Omega Core |
| SupplyDepot | Cargo Silo |
| Barracks | Cyber Foundry |
| Factory | Mech Yard |
| Spaceport | Drone Hangar |
| Lab | Neural Lab |

### 유닛 (전부 확정)

| NTA | OC | 스탯 |
|---|---|---|
| Worker Drone | Nanobot Repair | 동일 |
| Assault Trooper | Cyborg Soldier | 동일 |
| Scout Drone | Striker | 동일 |
| Sharpshooter | Railgunner | 동일 |
| Ranger IFV | Brute Mech | 근접형 차별화 (HP180/공격14/사거리2/공속0.8) |
| Pulsar Tank | Heavy Assault Tank | 동일 |
| SkyLancer | Ironhawk | 동일 |
| Firehawk | Raven | 동일 |
| Guardian Drone | Strike Drone | 동일 |

## 사용자 확인 결과

- 건물 이름(Omega Core / Cargo Silo / Cyber Foundry / Mech Yard / Drone Hangar / Neural Lab) 그대로 확정
- 구현 범위: "캠페인 전용 EnemyController AI"와 "스커미시에서 고를 수 있는 플레이어 팩션" 둘 다 필요하며,
  우선순위는 나중에 결정하기로 함 → 즉 데이터(UnitDataSO/BuildingDataSO) 자체는 두 용도 모두에 재사용 가능한
  형태로 만들어야 함 (EnemyController 전용 하드코딩 X)

## 참고: 이미 만들어진 빈 폴더

`Assets/prefabs/OC/Unit`, `Assets/prefabs/OC/Building` 폴더가 이미 존재함(빈 폴더, .meta만 있음) -
사용자가 예전에 OC 프리팹을 넣을 자리를 미리 만들어둔 것으로 보임. 실제 프리팹/모델링은 아직 안 됨.

## 구현 완료 내용

사용자가 "적유닛도 추가하기 쉽도록 `EnemyUnitDataSO` + `EnemyBuildingDataSO`를 만들어서 9+6종 먼저 추가"를
요청해 아래와 같이 구현함.

### 신규 스크립트

- `Assets/Scripts/ScriptableObject/EnemyUnitDataSO.cs` — `UnitDataSO`와 동일하게 기존 `UnitData` 클래스를
  그대로 재사용하는 별도 데이터베이스 (`List<UnitData> unitData`). 스탯 필드를 중복 정의하지 않음.
- `Assets/Scripts/ScriptableObject/EnemyBuildingDataSO.cs` — 위와 동일한 방식으로 기존 `BuildingData` 재사용
  (`List<BuildingData> buildingData`).

`RTSUnitController`는 아직 이 두 SO를 참조하지 않음 (NTA용 `unitDatabase`/`buildingDatabase`만 그대로 사용).
캠페인 AI든 스커미시 상대 팩션이든, 실제로 이 데이터를 "누가 읽어서 유닛을 스폰할지"는 아직 붙어있지 않은
순수 데이터 상태.

### 신규 SO 에셋

- `Assets/Scripts/ScriptableObject/OC Unit Data SO.asset` — 위 표의 OC 유닛 9종 스탯 전부 입력 완료
  (Brute Mech는 근접형 차별화 스탯 반영: HP180/공격14/사거리2/공속0.8). Ironhawk는 SkyLancer와 동일한
  특성 2택1(hasTraitChoice) 구조를 그대로 반영.
- `Assets/Scripts/ScriptableObject/OC Building Data SO.asset` — 위 표의 OC 건물 6종 전부 입력 완료.
  선행 건물 조건도 NTA와 동일 구조로 연결 (Mech Yard ← Cyber Foundry, Drone Hangar ← Mech Yard).

**Icon/Prefab 필드는 전부 비워둠(fileID: 0)** — OC 전용 아트(모델/아이콘)가 아직 없어서, NTA 에셋을 임시로
연결하는 대신 빈 채로 남겨둠. 실제 OC 프리팹/아이콘이 준비되면 (`Assets/prefabs/OC/Unit`,
`Assets/prefabs/OC/Building`에 넣을 예정) 이 SO 에셋에서 연결만 해주면 됨.

## 남은 작업 (범위 큼 - 별도 논의 필요)

1. OC 전용 프리팹/모델/아이콘 준비 → SO 에셋의 Icon/Prefab 필드 연결
2. `EnemyUnitDataSO`/`EnemyBuildingDataSO`를 실제로 "누가" 사용할지 연결
   - 캠페인 전용 AI: `EnemyController` 쪽에 데이터 조회 로직 추가
   - 스커미시 플레이어 팩션: `RTSUnitController`가 NTA/OC 중 어느 DB를 쓸지 선택하는 구조 필요
     (현재는 `unitDatabase`/`buildingDatabase` 필드가 고정 1개씩이라 진영 전환 로직이 없음)
3. Brute Mech의 근접 사거리(2)가 실제 게임 내 이동/추격 로직(`UnitController`, `AttackRange`)에서
   자연스럽게 동작하는지 확인 필요 (원거리 유닛 위주로 튜닝된 로직일 수 있음)

## 변경사항

없음 (설계 제안 문서만 작성, 코드/에셋 미변경).
