# 유닛/건물 밸런스 레퍼런스 (실제 게임 값 기준)

> 최종 갱신: 2026-07-22 (`doc/0205` 적용 이후)

## 0. 지금 상태: SO 값이 실제로 쓰이나요?

**유닛의 체력/공격력/사거리/아이콘/장갑타입/크기타입 — 이제 `UnitDataSO` 값이 실제로 적용됩니다.** (`doc/0205`에서 `UnitController` 자신이 `Start()`에서 자기 `unitID`로 SO를 조회해 `ApplyUnitData(data)`를 스스로 호출하도록 고침 — `doc/0203`처럼 `UnitSpawner`가 밖에서 넣어주는 방식이 아니라, **생산 큐를 거쳤든 씬에 직접 배치됐든 어떤 경로로 생성된 인스턴스든** 항상 적용됨.) 유닛이 생성되는 순간, 프리팹에 미리 박혀 있던 값을 덮어쓰고 SO 값을 그대로 갖게 됩니다.

| 값 | 스폰 시 적용 소스 | 비고 |
|---|---|---|
| 체력(HP) | `UnitData.hp` | `HealthManager.InitializeHealth()`로 최대치 지정 + 풀피 시작 |
| 공격력 | `UnitData.attackDamge` | `UnitController.attackDamage`에 덮어씀 |
| 사거리 | `UnitData.attackRange` | 자식 `AttackRange.UnitRange`에 덮어씀 |
| 아이콘 | `UnitData.Icon` | `UnitController.icon`에 덮어씀 |
| 장갑 타입 | `UnitData.armorType` | `UnitController.armorType`에 덮어씀 |
| 크기 타입 | `UnitData.sizeType` | `UnitController.sizeType`에 덮어씀 |

**여전히 SO에 필드가 없어서 프리팹 값을 그대로 쓰는 것** (스폰돼도 안 바뀜):

| 값 | 어디 있나 |
|---|---|
| `unitID` (유닛 식별자) | 프리팹의 `UnitController.unitID` — ⚠️ 아래 1-2번 참고 |
| 고정 방어력(`armor`) | 프리팹의 `UnitController.armor` |
| 공격 방식(`attackType`: 소총/폭발/레이저/화염) | 프리팹의 `UnitController.attackType` |
| 고유 장갑타입 보너스(`bonusVersusArmorType`/`Percent`) | 프리팹의 `UnitController` |
| 이동 방식(`isAirUnit`) | 프리팹의 `UnitController.isAirUnit` |

프리팹에 남아있는 위 값들은 인스펙터에서 미리보기/테스트용으로만 의미가 있고, **밸런스 조정은 이제 대부분 `UnitDataSO`만 고치면 반영됩니다** (단, `unitID`/`armor`/`attackType`/고유보너스/`isAirUnit`는 예외 — 여전히 프리팹에서 고쳐야 함).

건물은 이전과 동일 — `BuildingData`(비용/크기/건설시간/최대인구)는 원래도 실제로 쓰였고, 체력만 완공 프리팹의 `HealthManager.maxHealth`를 그대로 씀.

## 1. ⚠️ 중요 — 남아있는 문제

1. **Sharpshooter/SkyLancer 프리팹의 `unitID`가 여전히 각각 2(Assault Trooper)/4(Ranger IFV)입니다.** `doc/0203` 적용 이후에도 이건 안 고쳐집니다(`unitID`는 SO 항목을 찾는 키라서 애초에 동기화 대상이 아님). 그래서 지금 상태는:
   - Sharpshooter를 생산하면: 겉모습은 Sharpshooter 프리팹, 능력치(체력45/공격력10/사거리7/경장갑/소형)는 SO의 Sharpshooter 항목대로 정상 적용되지만, **`GetUnitID()`가 2를 반환**해서 인구수 반환·이름 표시·Info_panel 등 `unitID` 기반 로직은 전부 Assault Trooper로 오인식됩니다.
   - SkyLancer도 마찬가지로 능력치(체력125/공격력30/사거리6/중장갑/중형)는 SO대로 정상 적용되지만 `unitID`가 4라서 Ranger IFV로 오인식됩니다.
   - 둘 다 `attackType`/`bonusVersusArmorPercent`/`isAirUnit`은 여전히 프리팹 값 그대로라 설계와 다릅니다(Sharpshooter는 소총·보너스0%, SkyLancer는 지상·보너스0%).
2. **Worker Drone의 실제 공격 방식이 화염이 아니라 소총입니다.** (`attackType`은 동기화 대상이 아니라 프리팹 값 그대로.)
3. **SO 자체에 적힌 수치가 이번에 주신 설계 스펙과 다른 유닛이 있습니다** (Sharpshooter 공격력 10↔설계 5, 사거리 7↔설계 6 등 — 2장 표 참고). 이건 `doc/0203`이 고치는 "적용되냐 안 되냐"의 문제가 아니라 SO 안의 숫자 자체가 설계와 다른 것이라 별도로 맞춰야 합니다.

## 2. 데미지 배율표 (`DamageMultiplierTable.asset`, 현재 값)

| 공격 방식 | 소형 | 중형 | 대형 |
|---|---|---|---|
| 소총 (Bullet) | 100% | 80% | 60% |
| 폭발 (Explosive) | 70% | 100% | 130% |
| 레이저 (Laser) | 100% | 100% | 100% |
| 화염 (Flame) | 130% | 100% | 60% |

최종 데미지 = `공격력(연구 보너스 포함) × 위 배율 × (대상이 고유보너스 대상 장갑타입이면 1+보너스%) → 반올림 → 대상 방어력(armor) 감산 → 최소 1 보장`

⚠️ "공격 범위: 지상/공중"(어느 도메인을 공격 가능한지)은 아직 코드에 구현되어 있지 않습니다. `AttackRange`는 `"Enemy"` 태그만 보고 지상/공중 구분 없이 아무나 공격합니다.

## 3. 유닛 (스폰 시 실제로 적용되는 값 = SO 값 기준)

### 3-1. 메인기지

#### 워커 드론 (Worker Drone, ID 1)

| 항목 | 값 | 설계 값과 일치 |
|---|---|---|
| 이동 방식 | 지상 (프리팹 `isAirUnit: 0`, 동기화 안 됨) | ✅ |
| 공격 방식 | 소총 (프리팹 `attackType: 0`, 동기화 안 됨) | ❌ (설계: 화염) |
| 장갑 | 경장갑 | ✅ |
| 크기 | 소형 | ✅ |
| 가격 (광물/가스/인구) | 50/0/1 | ✅ |
| 생산시간 | 12 | ✅ |
| 체력 | 40 | ✅ |
| 공격력 | 5 | ✅ |
| 사거리 | 1 | ✅ |
| 단축키 | W | ✅ |
| 프리팹 | `Assets/prefabs/NTA/Unit/MainBase/Worker Drone.prefab` | |

### 3-2. 병영 (티어1)

#### 어썰트 트루퍼 (Assault Trooper, ID 2)

| 항목 | 값 | 설계 값과 일치 |
|---|---|---|
| 이동 방식 | 지상 (공격범위는 지상/공중이지만 도메인 제약 미구현) | - |
| 공격 방식 | 소총 | ✅ |
| 장갑 | 경장갑 | ✅ |
| 크기 | 소형 | ✅ |
| 가격 (광물/가스/인구) | 50/0/1 | ✅ |
| 생산시간 | 15 | ✅ |
| 체력 | 40 | ✅ |
| 공격력 | 5 | ✅ |
| 사거리 | 5 | ✅ |
| 단축키 | A | ✅ |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab` | |

#### 스카웃 드론 (Scout Drone, ID 3)

| 항목 | 값 | 설계 값과 일치 |
|---|---|---|
| 이동 방식 | 지상 | ✅ |
| 공격 방식 | 레이저 | ✅ |
| 장갑 | 경장갑 | ✅ |
| 크기 | 소형 | ✅ |
| 가격 (광물/가스/인구) | 75/0/2 | ✅ |
| 생산시간 | 19 | ✅ |
| 체력 | 75 | ✅ |
| 공격력 | 5 | ✅ |
| 사거리 | 6 | ✅ |
| 단축키 | S | ✅ |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier1/Scout Drone.prefab` | |

#### Sharpshooter (저격수, SO ID 8) — ⚠️ `unitID` 버그로 실제로는 Assault Trooper로 오인식됨

| 항목 | 값 (스폰 시 적용됨) | 설계 값과 일치 |
|---|---|---|
| 장갑 | 경장갑 | ✅ |
| 크기 | 소형 | ✅ |
| 가격 (광물/가스/인구) | 125/75/1 (설계: 75/0/2) | ❌ |
| 생산시간 | 50 | ✅ |
| 체력 | 45 | ✅ |
| 공격력 | 10 (설계: 5) | ❌ |
| 사거리 | 7 (설계: 6) | ❌ |
| 단축키 | S | ✅ |
| 공격 방식 | **소총** (동기화 안 됨, 프리팹 값) | ❌ (설계: 레이저) |
| 고유 보너스 | `bonusVersusArmorType: Heavy`만 설정, `Percent: 0` → **미적용** | ❌ (설계: 중장갑 +80%) |
| `unitID` | **2**(Assault Trooper와 동일, 동기화 안 됨) | ❌ (SO 기준 8이어야 함) |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier1/Sharpshooter.prefab` | |

### 3-3. 공장 (티어2)

#### 펄스탱크 (Pulasr Tank, ID 5)

| 항목 | 값 |
|---|---|
| 이동 방식 | 지상 |
| 공격 방식 | 폭발 (동기화 안 됨, 프리팹 값) |
| 장갑 | 중장갑 |
| 크기 | **대형** (SO 편집값 — 이제 스폰 시 실제로 적용됨) |
| 가격 (광물/가스/인구) | 150/100/2 |
| 생산시간 | 31 |
| 체력 | 150 |
| 공격력 | 20 |
| 사거리 | 7 |
| 단축키 | P |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier2/Pulsar Tank.prefab` |

#### IFV 레인저 (Ranger IFV, ID 4)

| 항목 | 값 |
|---|---|
| 이동 방식 | 지상 |
| 공격 방식 | 폭발 (동기화 안 됨, 프리팹 값) |
| 장갑 | **중장갑** (SO 편집값 — 이제 스폰 시 실제로 적용됨) |
| 크기 | 중형 |
| 가격 (광물/가스/인구) | 100/50/2 |
| 생산시간 | 25 |
| 체력 | 100 |
| 공격력 | 10 |
| 사거리 | 6 |
| 단축키 | I |
| 수송(유닛 탑승) 기능 | 미구현 (코드 없음) |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab` |

#### SkyLancer (스카이랜서, SO ID 9) — ⚠️ `unitID` 버그로 실제로는 Ranger IFV로 오인식됨

| 항목 | 값 (스폰 시 적용됨) | 설계 값과 일치 |
|---|---|---|
| 장갑 | 중장갑 | (설계에 명시 안 됨) |
| 크기 | 중형 | (설계에 명시 안 됨) |
| 가격 (광물/가스/인구) | 100/50/2 | |
| 생산시간 | 40 | |
| 체력 | 125 | |
| 공격력 | 30 | |
| 사거리 | 6 | |
| 단축키 | S | |
| 공격 방식 | 폭발 (동기화 안 됨, 프리팹 값 — 우연히 설계와 일치) | ✅ |
| 이동 방식 | **지상** (동기화 안 됨, 프리팹 값) | ❌ (설계: 공중) |
| 고유 보너스 | `bonusVersusArmorType: Light`만 설정, `Percent: 0` → **미적용** | ❌ (설계: 경장갑 +50%) |
| `unitID` | **4**(Ranger IFV와 동일, 동기화 안 됨) | ❌ (SO 기준 9여야 함) |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier2/SkyLancer.prefab` | |

### 3-4. 공항 (티어3)

#### 파이어호크 (Firehawk, ID 6)

| 항목 | 값 |
|---|---|
| 이동 방식 | 공중 |
| 공격 방식 | 레이저 |
| 장갑 | 경장갑 |
| 크기 | 중형 |
| 가격 (광물/가스/인구) | 150/100/2 |
| 생산시간 | 37 |
| 체력 | 150 |
| 공격력 | 15 |
| 사거리 | 7 |
| 단축키 | F |
| 고유 보너스 | 경장갑 대상 +30% (정상 적용) |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab` |

#### 가디언 드론 (Guardian Drone, ID 7)

| 항목 | 값 |
|---|---|
| 이동 방식 | 공중 |
| 공격 방식 | 폭발 |
| 장갑 | 중장갑 |
| 크기 | 대형 |
| 가격 (광물/가스/인구) | 400/300/6 |
| 생산시간 | 63 |
| 체력 | 400 |
| 공격력 | 30 |
| 사거리 | 7 |
| 단축키 | D |
| 고유 보너스 | 중장갑 대상 +40% (정상 적용) |
| 프리팹 | `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab` |

#### 지원기 (Support Ship) — 구상 단계, 미구현

"공격 X / 주변 유닛 버프?"로 언급됨. `UnitDataSO`/씬 프리팹 어디에도 없고, 버프 시스템 자체가 프로젝트에 없음.

## 4. 건물

| 건물 (ID) | 건설 위치 | 크기 | 비용(광물/가스) | 최대인구 증가 | 건설시간 | 체력 | 선행 건물 |
|---|---|---|---|---|---|---|---|
| CommandCenter (메인기지, 1) | - | 3×3 | 400/0 | +10 | 50 | 1500 | 없음 |
| SupplyDepot (보급고, 2) | - | 2×2 | 100/0 | +8 | 25 | 500 | 없음 |
| Barracks (병영, 3) | 티어1 | 3×3 | 150/0 | +0 | 37 | 1000 | 없음 |
| Factory (공장, 4) | 티어2 | 3×3 | 200/100 | +0 | 37 | 1250 | Barracks |
| Spaceport (공항, 5) | 티어3 | 3×3 | 150/100 | +0 | 37 | 1300 | Factory |
| Lab (연구소, 6) | - | 2×2 | 150/0 | +0 | 37 | 850 | 없음 |

건물의 비용/크기/건설시간/최대인구 증가량/체력은 이전과 동일한 방식(비용류는 `BuildingDataSO`, 체력은 완공 프리팹의 `HealthManager.maxHealth`)입니다 — `doc/0203`은 유닛 스폰 경로만 바꿨고 건물 쪽은 손대지 않았습니다.

## 5. 밸런스를 조정하려면 어디를 고쳐야 하나

| 필드 | 고쳐야 하는 곳 |
|---|---|
| 유닛 체력/공격력/사거리/아이콘/장갑타입/크기타입 | `UnitDataSO` (이제 여기가 authoritative) |
| 유닛 `unitID`/`armor`/`attackType`/고유보너스/`isAirUnit` | 유닛 프리팹의 `UnitController` (여전히 프리팹이 authoritative) |
| 유닛 생산비용/생산시간/tier/단축키 | `UnitDataSO` |
| 건물 비용/크기/건설시간/최대인구/선행건물 | `BuildingDataSO` |
| 건물 체력 | 건물 프리팹의 `HealthManager` |
| 공격방식×크기 데미지 배율 | `DamageMultiplierTable.asset` |
