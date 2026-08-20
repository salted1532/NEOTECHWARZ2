# 0640 - 모든 유닛/건물 미니맵 마커 월드 Y=100 통일

## 요청 내용

> 모든 프리팹의 미니맵마커 위치를 카메라보다 위에 존재하게 변경. 모든 프리팹이라는게
> 유닛, 건물을 말하는건데 현재 카메라가 layer 지형 스크립트까지 작동하다보면 최대 y55까지
> 올라가는데 각 프리팹의 미니맵마커의 경우 월드 기준(카메라 기준?)으로 보았을때 y100에
> 위치하도록 정렬해줘. 이게 각 프리팹의 크기가 1,1,1로 일정하지 않고 2.5,2.5,2.5거나
> 3,3,3이거나 이런식이라서 다 같은 미니맵 마커의 y좌표가 일정하지 않아서 이걸 일정하게
> y100으로 변경해줘.

범위: 유닛/건물 (자원 노드 Ore/Gas는 제외 - doc/0521 때도 동일하게 범위 밖).

## 원인 조사

`MiniMapIcon` 자식 오브젝트의 `m_LocalPosition.y`는 **부모(프리팹 루트) 기준 로컬 좌표**라서,
실제 월드 공간에서 얼마나 떠 있는지는 `로컬 Y * 루트의 m_LocalScale.y`로 정해진다. doc/0521,
doc/0562에서는 유닛 70 / 건물 40이라는 "로컬 Y 값"만 통일했는데, 그때는 프리팹 루트 스케일이
다르다는 걸 고려하지 않아서 실제 월드 오프셋이 프리팹마다 제각각이었다.

전체 프리팹(유닛/건물, 42개 중 Ore/Gas 2개 제외 40개 + Ally 건물 오버라이드 6개)을 스크립트로
전수 조사해서 `MiniMapIcon`의 부모 체인을 루트까지 따라가며 스케일을 누적 계산함:

| 루트 스케일 | 현재 로컬 Y | 현재 월드 오프셋 | 목표(월드=100)의 새 로컬 Y |
|---|---|---|---|
| 1.0 (유닛 대부분) | 70 | 70 | **100** |
| 1.25 (Tier2/3 일부 유닛 - 전차/전투기 등) | 70 | 87.5 | **80** |
| 4.0 (건물 대부분: Lab/MainBase/SupplyDepot/Tier1~3, Enemy_*, Spore Brood 건물) | 40 | 160 | **25** |
| 2.0 (BaseStructure - 건설 중 임시 프리팹, NTA/OC 공통) | 40 | 80 | **50** |

→ 예: 스케일 1.0인 유닛과 스케일 1.25인 유닛이 로컬 Y 둘 다 70이어도, 실제 월드 오프셋은
70 대 87.5로 이미 어긋나 있었음. 건물도 스케일 4.0(160)과 2.0(80)이 섞여 있어서 최대 2배 차이.
이번 요청대로 각 프리팹의 루트 스케일로 나눠서 **월드 오프셋을 전부 100으로** 맞추면 이 어긋남이
사라진다.

## 변경 계획

`MiniMapIcon` Transform의 `m_LocalPosition.y`만 아래 표대로 교체 (x/z, 회전, 스케일, 다른
필드는 손대지 않음).

**건물 (루트 스케일 4.0) - 40 → 25** (13개)
- `Assets/prefabs/NTA/Building/Lab.prefab`
- `Assets/prefabs/NTA/Building/MainBase.prefab`
- `Assets/prefabs/NTA/Building/SupplyDepot.prefab`
- `Assets/prefabs/NTA/Building/Tier1.prefab`
- `Assets/prefabs/NTA/Building/Tier2.prefab`
- `Assets/prefabs/NTA/Building/Tier3.prefab`
- `Assets/prefabs/OC/Building/Enemy_Lab.prefab`
- `Assets/prefabs/OC/Building/Enemy_MainBase.prefab`
- `Assets/prefabs/OC/Building/Enemy_SupplyDepot.prefab`
- `Assets/prefabs/OC/Building/Enemy_Tier1.prefab`
- `Assets/prefabs/OC/Building/Enemy_Tier2.prefab`
- `Assets/prefabs/OC/Building/Enemy_Tier3.prefab`
- `Assets/prefabs/Spore_Brood/Building/Bio_Reactor.prefab`
- `Assets/prefabs/Spore_Brood/Building/Hive_Core.prefab`
- `Assets/prefabs/Spore_Brood/Building/Spawning Pit.prefab`

**건물 - Ally 오버라이드 (기반 프리팹과 동일 스케일 4.0) - 40 → 25** (`m_Modifications`의
`MiniMapIcon` propertyPath: `m_LocalPosition.y` 값만 교체, doc/0562와 같은 방식) (6개)
- `Assets/prefabs/OC/Ally/Building/Ally_Lab.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_MainBase.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_SupplyDepot.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Tier1.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Tier2.prefab`
- `Assets/prefabs/OC/Ally/Building/Ally_Tier3.prefab`

**건설 중 임시 프리팹 (루트 스케일 2.0) - 40 → 50** (2개)
- `Assets/prefabs/NTA/Building/BaseStructure.prefab`
- `Assets/prefabs/OC/Building/BaseStructure.prefab`

**유닛 (루트 스케일 1.0) - 70 → 100** (17개)
- `Assets/prefabs/NTA/Unit/MainBase/Worker Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Assault Trooper.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Scout Drone.prefab`
- `Assets/prefabs/NTA/Unit/Tier1/Sharpshooter.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Guardian Drone.prefab`
- `Assets/prefabs/OC/RescueUnit/Cyborg Soldier (Rescue).prefab`
- `Assets/prefabs/OC/Unit/Mainbase/Nanobot Repair.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Cyborg Soldier .prefab`
- `Assets/prefabs/OC/Unit/Tier1/Railgunner.prefab`
- `Assets/prefabs/OC/Unit/Tier1/Striker.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Raven.prefab`
- `Assets/prefabs/OC/Unit/Tier3/Strike Drone.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Ripfang.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Skitterwing.prefab`
- `Assets/prefabs/Spore_Brood/Unit/Spitter.prefab`

**유닛 (루트 스케일 1.25 - 전차/전투기 계열) - 70 → 80** (7개)
- `Assets/prefabs/NTA/Unit/Tier2/Pulsar Tank.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/Ranger Infantry Fighting Vehicle.prefab`
- `Assets/prefabs/NTA/Unit/Tier2/SkyLancer.prefab`
- `Assets/prefabs/NTA/Unit/Tier3/Firehawk.prefab`
- `Assets/prefabs/OC/RescueUnit/Heavy Assault Tank (Rescue).prefab`
- `Assets/prefabs/OC/Unit/Tier2/Brute Mech.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Heavy Assault Tank.prefab`
- `Assets/prefabs/OC/Unit/Tier2/Ironhawk.prefab`

**Ally 유닛 9개**: `MiniMapIcon` 오버라이드 없이 기반 Unit 프리팹 값을 그대로 상속하는 걸
확인함 → 위 유닛 프리팹만 고치면 자동으로 같이 반영되어 별도 수정 불필요.

**제외 (요청 범위 밖)**: `Assets/prefabs/Resource/Ore.prefab`, `Gas.prefab` (자원 노드, 유닛/건물
아님).

총 47개 파일(건물 15 + Ally 건물 6 + BaseStructure 2 + 유닛 17 + 유닛 7) 수정.
C# 코드 변경 없음(프리팹 값만 조정).

이대로 진행해도 될까요?

## 적용 결과

사용자 승인 후 위 47개 파일(자원 노드 2개는 제외, 39개 일반 프리팹 + 6개 Ally 오버라이드)을
제안대로 그대로 적용. `MiniMapIcon` Transform의 `m_LocalPosition.y`(일반 프리팹) 또는 해당
`m_Modifications` 블록의 `value`(Ally 오버라이드)만 교체, x/z·회전·스케일 등 다른 필드는
건드리지 않음 (프리팹별 `git diff` 로 1줄짜리 변경만 있는지 전수 확인).

수정 후 스크립트로 전체 재검증: 40개 일반 프리팹 전부 `currentWorldOffset=100.00`으로 통일됨을
확인 (예: 건물 로컬 25 × 스케일 4.0 = 100, 유닛 로컬 100 × 스케일 1.0 = 100, 유닛 로컬 80 ×
스케일 1.25 = 100, BaseStructure 로컬 50 × 스케일 2.0 = 100). Ally 건물 6개도 대응하는 Enemy
소스 프리팹과 동일한 스케일이라 값(25)만 맞추면 월드 오프셋도 100으로 함께 맞음.

## 컴파일 확인
`npx uloop-cli compile` 결과 `Success: true, ErrorCount: 0, WarningCount: 0` (프리팹 값만 바꾼
변경이라 C# 컴파일에는 영향 없음).
