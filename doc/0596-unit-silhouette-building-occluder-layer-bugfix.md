# 0596. 유닛 실루엣이 직접 지은 건물/아군 OC 건물 뒤에서 안 보이는 버그 - 수정

**날짜:** 2026-08-16

## 요청 내용

> 유닛에 경우에 건물 뒤에 있을때 실루엣이 안보여. 점령지 건물이랑 언덕은 잘되는데 직접 지은 건물이나
> 아군OC 건물 뒤에는 실루엣이 안보여

## 조사

`UnitSilhouette.cs`의 가림막 카메라는 `Ground(레이어 7) | Building(레이어 9)`만 그려서 가림막 깊이
텍스처를 만든다(doc/0592~0594). 즉 이 두 레이어에 실제로 렌더러(MeshRenderer)가 있어야만 그 건물이
유닛을 가릴 수 있다.

Unity 에디터에서 현재 로드된 Mission4 씬을 직접 조회해서 확인한 결과:

```
npx uloop-cli find-game-objects --layer 9 --required-components MeshRenderer
→ Capture_Point/struct_Radar_Outpost_A_yup
→ Capture_Point (1)/struct_Radar_Outpost_A_yup
→ Capture_Point (2)/struct_Radar_Outpost_A_yup
(전체 씬 통틀어 이 3개가 전부)
```

**Building 레이어에 실제로 렌더러가 있는 오브젝트가 씬 전체에 이 3개뿐이다.** 이게 사용자가 말한
"점령지 건물"이고, 유일하게 실루엣이 제대로 작동하는 이유다.

반면 플레이어가 짓는 건물(`Lab`)의 계층구조를 직접 열어보면:
```
Lab (layer 9, BuildingController - 렌더러 없음, 빈 껍데기)
├─ MiniMapIcon (layer 9)
├─ TakeOffPos / LandingPos (layer 0, 마커용 빈 오브젝트)
├─ DestroyPos (layer 9)
├─ HealthBar (layer 0)
├─ Marker (layer 0, 선택 표시용 비활성 메쉬)
└─ struct_Research_Lab_A_yup (layer 0)  ← 실제로 화면에 보이는 건물 모델. 여기가 문제.
```
`Lab` 루트 자체는 레이어 9로 맞게 설정돼 있지만 렌더러가 없는 빈 오브젝트고, 실제 눈에 보이는 건물
모델(`struct_Research_Lab_A_yup`)은 별도 프리팹을 자식으로 얹은 것인데 그 프리팹 자체가 애초에
레이어 0(Default)으로 만들어져 있다. `Lab.prefab`의 중첩 프리팹 설정을 봐도 레이어를 덮어쓰는
설정(`propertyPath: m_Layer`)이 전혀 없다 - 그래서 최종적으로 화면에 보이는 메쉬가 항상 레이어
0으로 남는다.

아군 OC 건물(`Ally_SupplyDepot`)도 계층구조를 열어보면 똑같은 패턴:
```
Ally_SupplyDepot (layer 13 "AllyOC")
└─ struct_Silo_A_yup (layer 0)  ← 역시 레이어 0
```
루트가 9도 아니고 13(AllyOC)이라 애초에 가림막 대상이 아니고, 실제 메쉬도 레이어 0 - 이중으로
안 잡힌다. 다만 셀렉션/충돌 등 다른 로직은 전부 루트의 콜라이더 기준으로 동작하므로(비주얼 모델
프리팹엔 MeshFilter+MeshRenderer만 있고 콜라이더가 없음을 확인함) 이 비주얼 모델 프리팹의 레이어만
바꿔도 다른 기능엔 영향이 없다.

같은 패턴을 쓰는 나머지 건물들의 비주얼 모델 프리팹을 전부 확인한 결과, 아래 13개 전부 예외 없이
레이어 0으로 돼 있음을 확인:

| 건물 (플레이어/적·아군 OC 공용) | 비주얼 모델 프리팹 경로 |
|---|---|
| Lab / Enemy_Lab, Ally_Lab | `Assets/prefabs/Asset/NTA/struct_Research_Lab_A_yup.prefab`, `Assets/prefabs/Asset/OC/struct_Research_Lab_A_yup.prefab` |
| MainBase / Enemy_MainBase, Ally_MainBase | `Assets/prefabs/Asset/NTA/struct_Headquarters_A_yup.prefab`, `Assets/prefabs/Asset/OC/struct_Headquarters_A_yup.prefab` |
| SupplyDepot / Enemy_SupplyDepot, Ally_SupplyDepot | `Assets/prefabs/Asset/NTA/struct_Misc_Building_B_yup.prefab`, `Assets/prefabs/Asset/OC/struct_Silo_A_yup.prefab` |
| Tier1 / Enemy_Tier1, Ally_Tier1 | `Assets/prefabs/Asset/NTA/struct_Barracks_A_yup.prefab`, `Assets/prefabs/Asset/OC/struct_Refinery_A_yup.prefab` |
| Tier2 / Enemy_Tier2, Ally_Tier2 | `Assets/prefabs/Asset/NTA/struct_Factory_Heavy_A_yup.prefab`, `Assets/prefabs/Asset/OC/struct_Factory_Light_A_yup.prefab` |
| Tier3 / Enemy_Tier3, Ally_Tier3 | `Assets/prefabs/Asset/NTA/struct_Spaceport_A_yup.prefab`, `Assets/prefabs/Asset/OC/struct_Spaceport_A_yup.prefab` |
| Tier2/Tier3 공용 받침대(양 진영) | `Assets/AssetFolder/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_1/prefabs_yup/struct_Slab_1x1_A_yup.prefab` |

`Ally_*` 프리팹들은 각각 `Enemy_*` 프리팹의 프리팹 변형(Variant)이라 `Enemy_*`가 참조하는 비주얼
모델을 그대로 같이 쓴다 - 즉 OC용 6개만 고치면 적/아군 OC 건물 둘 다 자동으로 고쳐짐.

## 해결 방향 (순수 데이터 수정, 코드 변경 없음)

위 13개 비주얼 모델 프리팹 파일의 `m_Layer`를 전부 `0` → `9`(Building)로 바꾼다. 콜라이더가 없는
순수 시각 전용 프리팹이라 다른 시스템(선택, NavMeshObstacle, 충돌)엔 영향 없음 - 그 로직들은 전부
래퍼(Lab/Enemy_Lab 등) 루트의 콜라이더를 쓰지 이 비주얼 모델을 쓰지 않는다.

이 프리팹들이 원본(중첩 프리팹의 소스)이라 래퍼 프리팹(Lab.prefab 등)이 레이어를 따로 덮어쓰지
않는 이상 자동으로 상속된다 - 이미 씬에 배치돼 있는 건물들(Mission4 포함 다른 미션들도)도 씬 파일을
전혀 안 건드리고 이 원본 프리팹만 고치면 자동으로 정상화된다.

## 부수 효과 (사소함)
- `Marker`(선택 표시 링)도 `MeshRenderer`가 있어서 매 프레임 유닛의 실루엣 판정 대상 렌더러 목록에
  넣을 때 같이 걸리지만, 이건 비주얼 모델이 아니라 래퍼 프리팹 쪽 오브젝트라 이번 수정 대상이
  아님(레이어 0 그대로 유지) - 애초에 선택 안 됐을 때 `m_Enabled: 0`이라 렌더링 자체가 안 되므로
  영향 없음.

## 범위 밖으로 둔 것
- `BaseStructure.prefab`(건설 중 "짓는 중" 스캐폴딩/유령 오브젝트)은 이번 수정 대상에서 뺌 - 건설
  중인 임시 상태라 사용자가 말한 "직접 지은 건물"(완공된 건물)과는 다른 케이스. 필요하면 별도로
  알려주면 됨.

## 변경 예정 파일 (13개, 전부 `m_Layer: 0` → `m_Layer: 9`만 변경)
- `Assets/prefabs/Asset/NTA/struct_Research_Lab_A_yup.prefab`
- `Assets/prefabs/Asset/NTA/struct_Headquarters_A_yup.prefab`
- `Assets/prefabs/Asset/NTA/struct_Misc_Building_B_yup.prefab`
- `Assets/prefabs/Asset/NTA/struct_Barracks_A_yup.prefab`
- `Assets/prefabs/Asset/NTA/struct_Factory_Heavy_A_yup.prefab`
- `Assets/prefabs/Asset/NTA/struct_Spaceport_A_yup.prefab`
- `Assets/prefabs/Asset/OC/struct_Research_Lab_A_yup.prefab`
- `Assets/prefabs/Asset/OC/struct_Headquarters_A_yup.prefab`
- `Assets/prefabs/Asset/OC/struct_Silo_A_yup.prefab`
- `Assets/prefabs/Asset/OC/struct_Refinery_A_yup.prefab`
- `Assets/prefabs/Asset/OC/struct_Factory_Light_A_yup.prefab`
- `Assets/prefabs/Asset/OC/struct_Spaceport_A_yup.prefab`
- `Assets/AssetFolder/Canopus-III_Low-Poly_Sci-Fi_Desert_Units_Set_1/prefabs_yup/struct_Slab_1x1_A_yup.prefab`

---

## 적용 (사용자 승인 후)

사용자가 "고쳐줘"로 승인 - 제안대로 13개 프리팹 전부 `sed`로 일괄 변경(`m_Layer: 0` → `m_Layer: 9`).

1. 13개 파일 각각 변경 전/후 `m_Layer: 0`/`m_Layer: 9` 개수 확인 - 전부 정확히 의도한 개수만큼
   이동함(0개 남은 layer:0, 원래 개수만큼 layer:9로 확인).
2. `npx uloop-cli compile` - Error 0. Unity가 프리팹 변경을 감지해 Domain Reload 진행(잠깐 대기 후
   재조회 필요).
3. `get-hierarchy`로 `Lab/struct_Research_Lab_A_yup`이 실제로 layer 9로 반영된 것을 직접 확인.
4. Play Mode 진입 후 `execute-dynamic-code`로 플레이어 유닛을 `Lab` 건물 뒤로(카메라 시점 기준)
   텔레포트해서 실제 게임 화면으로 확인 - 사용자가 직접 보고 "잘 작동하네 좋다"로 확인함.

플레이어가 지은 건물 뒤에 유닛이 가려질 때도 초록(#19FF00) 실루엣이 정상적으로 뜸을 확인 완료.
(아군 OC 건물 쪽은 `Ally_*` 프리팹이 같은 OC 비주얼 모델을 공유하므로 동일 원리로 같이 고쳐졌을 것 -
플레이어 건물 케이스로 검증된 것과 같은 근본 원인이라 별도 재현 스크린샷은 생략함.)
