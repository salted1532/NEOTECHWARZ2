# 0577. 적/아군 OC 유닛 생산 시간 1.5배 (제안)

**날짜:** 2026-08-14

## 요청 내용

1. "적 유닛 생산 시간을 기존 시간에서 1.5배 늘려줘 별동대 병력이랑 방어병력이 죽으면 생산되는 추가병력이 너무 빠르게 생산되는거 같아"
2. (같은 턴 추가 요청) "아군 OC도 같이 고쳐줘"

## 조사 내용

- `EnemyAIDirector.EnqueueProduction`(웨이브/별동대 보충 생산)과 `AllyAIDirector.EnqueueProduction`(아군 OC 보충 생산) 모두 동일한 소스에서 `productionTime`을 읽는다: `RTSUnitController.GetEnemyUnitData(unitID)` → `enemyUnitDatabase`(OC Unit Data SO) 우선, 없으면 `sporeBroodUnitDatabase`(Spore Brood Unit Data SO)에서 조회.
- 즉 "OC Unit Data SO"의 `productionTime`은 **적 OC**와 **아군 OC**가 완전히 같은 값을 공유한다 (`AllyAIDirector`도 같은 `GetEnemyUnitData` 호출로 OC 로스터를 그대로 씀, doc/0543). 그래서 이 SO 하나만 고치면 두 요청이 동시에 해결된다 — 코드 변경 없이 데이터(에셋)만 바꾸면 됨.
- 배치형 방어 유닛(`defenseUnits`)의 최초 1회 대체 생산(`RespawnDeadDefenseUnits`)은 `productionTime`을 아예 안 쓰고 즉시 Instantiate라서 이번 변경으로는 느려지지 않음 — 다만 슬롯당 딱 1번뿐이라(doc/0558) "너무 빠르게 무한 생산"의 원인은 아니고, 사용자가 느낀 "너무 빠름"은 별동대(raidGarrison)/웨이브(garrison) 보충 생산 쪽(`ReinforceRoutine` → `FillPool` → `EnqueueProduction`)일 가능성이 높음.
- 플레이어 본인 유닛(NTA)의 생산 시간은 별도 DB(`unitDatabase`, NTA Unit Data SO)라 이번 변경과 무관 — 그대로 둠.

## 계획 (데이터 변경 - 코드 변경 없음)

`OC Unit Data SO.asset`과 `Spore Brood Unit Data SO.asset`의 `productionTime`을 각각 1.5배(반올림)로 조정.

### OC Unit Data SO (적 OC + 아군 OC 공용)

| ID | 유닛 | 기존 | 변경 |
|----|------|-----:|-----:|
| 1 | Nanobot Repair | 12 | 18 |
| 2 | Cyborg Soldier | 15 | 23 |
| 3 | Striker | 19 | 29 |
| 4 | Railgunner | 50 | 75 |
| 5 | Brute Mech | 25 | 38 |
| 6 | Heavy Assault Tank | 31 | 47 |
| 7 | Ironhawk | 40 | 60 |
| 8 | Raven | 37 | 56 |
| 9 | Strike Drone | 63 | 95 |

### Spore Brood Unit Data SO (외계종족, 적 전용)

| ID | 유닛 | 기존 | 변경 |
|----|------|-----:|-----:|
| 10 | 립팽 (Ripfang) | 10 | 15 |
| 11 | 스피터 (Spitter) | 20 | 30 |
| 12 | 스키터윙 (Skitterwing) | 26 | 39 |

## 요약/영향받는 파일

- `Assets/Scripts/ScriptableObject/Data/OC Unit Data SO.asset` — productionTime 9종 1.5배
- `Assets/Scripts/ScriptableObject/Data/Spore Brood Unit Data SO.asset` — productionTime 3종 1.5배
- 코드(.cs) 변경 없음 — `EnemyAIDirector`/`AllyAIDirector` 둘 다 이 데이터를 그대로 읽으므로 자동 반영.
- 영향 범위: 적 OC/외계종족의 웨이브 보충 생산, 점령 별동대(raidGarrison) 보충 생산, 아군 OC 웨이브 보충 생산 — 모두 1.5배 느려짐. 배치형 방어 유닛의 1회성 즉시 재생산(RespawnDeadDefenseUnits)은 영향 없음(원래도 productionTime 미사용).

**구현 완료** - 사용자 확인("네, 적용") 후 위 표 그대로 두 SO 에셋 파일에 적용함.
