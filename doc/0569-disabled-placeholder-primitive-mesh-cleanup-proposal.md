# 0569 - 꺼져있는 플레이스홀더 큐브/캡슐 메쉬 렌더러 정리 (제안)

## 요청 내용

> 모든 건물,유닛 플레이어꺼 적OC, 적spore, 아군OC 모든 프리팹의 그 본연의 모델링 큐브나 캡슐일탠데
> 해당 현재 꺼져 있을거야 그 메쉬랜더러들 다 제거해줘

정리: NTA(플레이어), OC(적), Spore Brood(적), 아군 OC 모든 건물/유닛 프리팹에는 실제 비주얼 모델(자식
오브젝트)과 별도로, 루트 오브젝트 자신에 Unity 기본 도형(Cube/Capsule) MeshFilter+MeshRenderer가
남아있고 `m_Enabled: 0`으로 꺼져있음 - 이걸 다 제거해달라는 요청.

## 조사 결과

### 1) 실제로 전 프리팹에 꺼진 플레이스홀더가 있음 (스캔 결과)

`Assets/prefabs/{NTA,OC,Spore_Brood}` 하위 건물/유닛 프리팹을 전수 스캔한 결과, 루트 오브젝트에
Unity 빌트인 Cube(fileID 10202) 또는 Capsule(fileID 10208) 메쉬를 참조하는 MeshFilter가 있고, 같은
오브젝트의 MeshRenderer가 `m_Enabled: 0`인 프리팹은 **37개**:

- **유닛 20개** (루트 = Capsule): NTA/Unit 9개(Worker Drone, Assault Trooper, Scout Drone,
  Sharpshooter, Pulsar Tank, Ranger Infantry Fighting Vehicle, SkyLancer, Firehawk, Guardian Drone),
  OC/Unit 9개(Nanobot Repair, Cyborg Soldier, Railgunner, Striker, Brute Mech, Heavy Assault Tank,
  Ironhawk, Raven, Strike Drone), OC/RescueUnit 2개(Cyborg Soldier (Rescue), Heavy Assault Tank
  (Rescue))
- **건물 17개** (루트 = Cube): NTA/Building 7개(BaseStructure, Lab, MainBase, SupplyDepot, Tier1/2/3),
  OC/Building 7개(BaseStructure, Enemy_Lab, Enemy_MainBase, Enemy_SupplyDepot, Enemy_Tier1/2/3),
  Spore_Brood/Building 3개(Bio_Reactor, Hive_Core, Spawning Pit)

`Assets/prefabs/OC/Ally/Building/*`, `Assets/prefabs/OC/Ally/Unit/*` (아군 OC)는 스캔에 안 잡혔는데,
확인해보니 별도 메쉬를 갖고 있지 않고 **위 OC 원본 프리팹을 Nested Prefab(Variant)으로 그대로
참조**하는 구조였음 (예: `Ally_Lab.prefab` → `OC/Building/Enemy_Lab.prefab`을 `m_SourcePrefab`으로
참조, `Striker (Ally).prefab` → `OC/Unit/Tier1/Striker.prefab` 참조). 해당 MeshRenderer 필드에 대한
프리팹 오버라이드도 없음. **즉 OC 원본만 고치면 아군 OC 변형에도 자동 반영됨 - 아군 OC 프리팹은 따로
안 건드려도 됨.**

Spore_Brood/Unit(Ripfang, Skitterwing, Spitter) 3개는 다른 패턴: 루트에 Capsule MeshFilter는
있지만 **MeshRenderer 자체가 아예 없음**(껐다 켰다 할 대상이 없음, 처음부터 렌더러 미부착). 이 셋의
실제 비주얼 모델은 자식으로 붙은 수십 개의 "Cube (N)" 오브젝트들(복셀 스타일 모델링, 전부
`m_Enabled: 1`)이라 그건 손대면 안 됨 - 별도로 아래 "확인 필요" 항목에서 다룸.

### 2) 건물 루트의 Cube MeshFilter는 실제로 코드가 참조 중 - MeshRenderer만 지우고 MeshFilter는 남겨야 함

`Assets/Scripts/BuildSystem/PlacementSystem.cs:369-392`의 `GetGroundOffsetY()` /
`GetBuildingHeight()`가 **프리팹 루트의 MeshFilter**(`prefab.TryGetComponent<MeshFilter>()`, 자식은
안 봄)의 `sharedMesh.bounds`로 다음을 계산함:
- `GetGroundOffsetY`: 건물 피벗이 지면에 닿도록 하는 y 오프셋 (건물 스폰/고스트 프리뷰/착륙 위치 등에
  전부 사용 - `PlacementSystem.cs` 5곳, `BaseStructure.cs` 2곳, `BuildingController.cs` 1곳,
  `EnemyBuildingController.cs` 1곳)
- `GetBuildingHeight`: 건설 상승 애니메이션에서 건물을 얼마나 땅속에 파묻고 시작할지 (`doc/0527`)

두 함수 모두 주석에 "메쉬가 없으면 안전한 고정값(1 또는 2)으로 대체"라고 되어 있음 - 즉 지금은 이
꺼진 Cube 메쉬의 바운드(대략 건물 크기에 맞게 스케일된 값)로 정확한 오프셋을 구하고 있고, **루트
MeshFilter를 통째로 지우면 이 계산이 전부 고정값 폴백으로 바뀌어 건물 배치 높이/상승 애니메이션이
어긋나는 회귀**가 생김. 반면 `MeshRenderer`(꺼진 상태, 시각적으로 이미 아무 효과 없음)는 이 두 함수와
무관 - `MeshRenderer`만 제거해도 안전함.

유닛 쪽 루트 Capsule은 이런 코드 의존이 없음(전수 grep 결과 `PlacementSystem`의 저 두 함수만 프리팹
루트 MeshFilter를 쓰고, 전부 건물에만 호출됨) - 유닛은 MeshFilter+MeshRenderer 둘 다 제거해도 됨.

### 3) 이 꺼진 렌더러가 `doc/0567`(반쯤 밝은 곳 미발견 유닛 숨기기)의 버그 원인으로 보임

`EnemyUnitController.cs:116`의 `bodyRenderers = GetComponentsInChildren<Renderer>()`는 루트의 이
꺼진 Capsule MeshRenderer까지 통째로 잡음. `UpdateFogVisibility()`는 매 프레임
`r.enabled = effectivelyVisible`로 **무조건** 덮어쓰므로, 유닛이 "발견됨" 상태가 되는 순간 원래
계속 꺼져있어야 할 이 플레이스홀더 캡슐까지 강제로 켜짐 - 지난번 질문하신 "모든 메쉬가 다 렌더링되는
것 같다"는 증상의 실제 원인이 이것으로 보임. 이번 정리(유닛 루트 MeshRenderer 완전 제거)를 하면
`bodyRenderers` 목록에 애초에 안 잡히므로 이 문제도 같이 해결됨.

건물(`EnemyBuildingController.cs`)은 미니맵 아이콘만 토글하고 몸체 Renderer는 안 건드리므로 이 버그
자체가 없음 - 그래도 죽은 컴포넌트 정리 차원에서 MeshRenderer는 동일하게 제거.

## 제안하는 수정

Unity 에디터에서 `PrefabUtility.LoadPrefabContents` → 루트 오브젝트의 `MeshFilter`/`MeshRenderer`
제거 → `PrefabUtility.SaveAsPrefabAsset`로 처리 (YAML 직접 편집은 fileID/컴포넌트 리스트 정합성이
깨지기 쉬워서 지양).

1. **유닛 20개**: 루트의 `MeshFilter` + `MeshRenderer` 둘 다 제거.
2. **건물 17개**: 루트의 `MeshRenderer`만 제거, `MeshFilter`는 유지(`GetGroundOffsetY`/
   `GetBuildingHeight`가 계속 씀).
3. **아군 OC 프리팹**: 수정 없음 (원본 Nested Prefab 수정이 자동 반영).

## 확인 필요

1. 위 20+17 = 37개 프리팹, 방식(유닛=완전 제거 / 건물=렌더러만 제거) 이대로 진행해도 될지. → 승인.
2. Spore_Brood/Unit 3개(Ripfang/Skitterwing/Spitter) 루트의 **렌더러 없는 오빠 MeshFilter**(Capsule)도
   같이 지울지 - 렌더러가 아예 없어서 "꺼진 렌더러"엔 해당 안 하지만, 어차피 아무것도 안 그리는
   죽은 컴포넌트라 같이 정리 가능. 이 셋의 진짜 모델(Cube (N)... 다수, 전부 켜져 있음)은 그대로 둠.
   → 같이 제거로 승인.
3. 진행 도중 추가로 발견한 `enemyMarker`(선택 표시, `Marker` 오브젝트)의 죽은 Cylinder 플레이스홀더는
   범위 밖(큐브/캡슐이 아님)이라 이번엔 제외, 별도 확인 후 진행하기로 함.

## 구현 결과

Unity 에디터 다이나믹 코드 실행(`PrefabUtility.LoadPrefabContents` → 조건 재검증(메쉬 이름 +
렌더러 활성 상태) → `Object.DestroyImmediate` → `PrefabUtility.SaveAsPrefabAsset`)으로 40개 프리팹
전부 처리, 스킵 없이 전부 REMOVED:
- 유닛 20개: 루트 `MeshFilter`(Capsule) + `MeshRenderer`(비활성) 완전 제거.
- 건물 17개: 루트 `MeshRenderer`(비활성)만 제거, `MeshFilter`(Cube)는 유지.
- Spore_Brood 유닛 3개: 루트의 렌더러 없는 orphan `MeshFilter`(Capsule) 제거, 자식의 "Cube (N)" 실제
  모델(전부 `m_Enabled: 1`)은 그대로 둠.

`git diff`로 각 케이스 확인:
- `NTA/Building/BaseStructure.prefab`: `MeshRenderer` 문서와 `m_Component` 참조만 삭제, `MeshFilter`
  (Cube, fileID 10202) 그대로 유지됨.
- `OC/Unit/Tier1/Cyborg Soldier .prefab`: `MeshFilter` + `MeshRenderer` 문서와 `m_Component` 참조
  둘 다 삭제됨.
- `OC/Ally/Building/*` 6개는 손대지 않았고(Nested Prefab이라 원본만 고치면 자동 반영), 이번 diff에
  나온 변경분은 세션 시작 전부터 이미 있던 별개의 미커밋 변경(`m_LocalPosition.y` 값 등)이었음 - 이번
  작업으로 인한 변화 아님.

컴파일 확인: `npx uloop-cli compile` → `Success: true, ErrorCount: 0, WarningCount: 0`.

## 최종 요약

- NTA/OC/Spore_Brood/OC-RescueUnit의 유닛·건물 프리팹 루트에 남아있던 "본연의" Cube/Capsule
  플레이스홀더가 전부 정리됨.
- 건물은 `PlacementSystem.GetGroundOffsetY`/`GetBuildingHeight`가 여전히 참조하는 `MeshFilter`는
  보존해 배치 높이/건설 상승 애니메이션 회귀 없음.
- 부수 효과: `EnemyUnitController.bodyRenderers`가 더 이상 이 죽은 플레이스홀더를 잡지 않으므로,
  `doc/0567` 이후 관찰됐던 "발견 시 플레이스홀더까지 같이 켜지는" 현상도 같이 해소됨.
- 아군 OC 프리팹은 Nested Prefab 구조 덕분에 별도 수정 없이 자동 반영됨.
- `enemyMarker`의 죽은 Cylinder 플레이스홀더는 이번 범위에서 제외 - 필요시 별도 후속 확인.
