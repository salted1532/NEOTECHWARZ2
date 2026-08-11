# 0521. MiniMapIcon Y 오프셋 통일 (유닛 70 / 건물 40) - 조사/제안

**날짜:** 2026-08-11

## 요청 내용

> 현재 프리팹에서 유닛에 경우 MiniMapIcon의 y값을 70 건물은 40으로 조절해줘 이미 조절된건 그대로 둬

## 조사 내용

`Assets/prefabs/` 아래 `MiniMapIcon`이라는 자식 오브젝트(미니맵용 스프라이트)를 가진 프리팹 42개를 전수
조사해서 현재 `m_LocalPosition.y` 값을 확인함. 자원 노드(Ore/Gas, y=90)는 유닛/건물이 아니라 요청
범위 밖이라 제외.

**유닛 (목표 70) - 대부분 이미 70, 예외 3개**
| 프리팹 | 현재 y | 조치 |
|---|---|---|
| NTA Worker Drone / Assault Trooper / Scout Drone / Sharpshooter / Pulsar Tank / Ranger IFV / SkyLancer / Firehawk / Guardian Drone | 70 | 그대로 둠 |
| OC Nanobot Repair / Cyborg Soldier / Railgunner / Striker / Brute Mech / Heavy Assault Tank / Ironhawk / Raven / Strike Drone | 70 | 그대로 둠 |
| OC RescueUnit (Cyborg Soldier, Heavy Assault Tank) | 70 | 그대로 둠 |
| **Spore Brood Ripfang** | **40** | **70으로 변경** |
| **Spore Brood Skitterwing** | **40** | **70으로 변경** |
| **Spore Brood Spitter** | **40** | **70으로 변경** |

**건물 (목표 40) - NTA/OC 아군 건물은 이미 40, 적 진영 건물 9개가 예외**
| 프리팹 | 현재 y | 조치 |
|---|---|---|
| NTA BaseStructure / MainBase / SupplyDepot / Tier1 / Tier2 / Tier3 / Lab | 40 | 그대로 둠 |
| OC BaseStructure | 40 | 그대로 둠 |
| **OC Enemy_Lab / Enemy_MainBase / Enemy_SupplyDepot / Enemy_Tier1 / Enemy_Tier2 / Enemy_Tier3** | **20** | **40으로 변경** (6개) |
| **Spore Brood Bio_Reactor / Hive_Core / Spawning Pit** | **20** | **40으로 변경** (3개) |

## 변경 계획
총 12개 프리팹의 `MiniMapIcon` Transform `m_LocalPosition.y` 값만 수정 (x/z, 회전, 스케일 등은 안 건드림):
- 유닛 3개: `Spore_Brood/Unit/Ripfang.prefab`, `Skitterwing.prefab`, `Spitter.prefab` → 40 → 70
- 건물 9개: `OC/Building/Enemy_Lab.prefab`, `Enemy_MainBase.prefab`, `Enemy_SupplyDepot.prefab`, `Enemy_Tier1.prefab`, `Enemy_Tier2.prefab`, `Enemy_Tier3.prefab`, `Spore_Brood/Building/Bio_Reactor.prefab`, `Hive_Core.prefab`, `Spawning Pit.prefab` → 20 → 40

이대로 진행해도 될까요?

---

## 적용 (사용자 승인 후)

> 수정해줘

제안대로 12개 프리팹의 `MiniMapIcon` Transform `m_LocalPosition.y`만 수정, 적용 후 전부 재확인함:

- 유닛 3개 → 70: `Spore_Brood/Unit/Ripfang.prefab`, `Skitterwing.prefab`, `Spitter.prefab`
- 건물 9개 → 40: `OC/Building/Enemy_Lab.prefab`, `Enemy_MainBase.prefab`, `Enemy_SupplyDepot.prefab`, `Enemy_Tier1.prefab`, `Enemy_Tier2.prefab`, `Enemy_Tier3.prefab`, `Spore_Brood/Building/Bio_Reactor.prefab`, `Hive_Core.prefab`, `Spawning Pit.prefab`

x/z, 회전, 스케일 등 다른 값은 손대지 않음. 이미 목표값이었던 나머지 30개 프리팹은 그대로 둠.

## 변경된 파일
위 12개 프리팹 파일.
