# 0580 - 유닛 Marker의 꺼져있는 Cylinder 메쉬 렌더러 정리 (제안)

## 요청 내용

> 각 유닛들의 Marker 에서 꺼져있는 메쉬 랜더러 삭제해줘

[[0569]](0569-disabled-placeholder-primitive-mesh-cleanup-proposal.md)에서 "확인 필요" 항목 3번으로
남겨뒀던 `enemyMarker`(선택 표시용 `Marker` 오브젝트)의 죽은 Cylinder 플레이스홀더 정리 요청.

## 조사 결과

### 1) 유닛 프리팹의 "Marker" 오브젝트 구조

`UnitController`/`EnemyUnitController`/`AllyController`가 `[SerializeField] GameObject unitMarker` /
`enemyMarker`로 참조하는 `Marker`라는 이름의 자식 오브젝트가 모든 유닛 프리팹에 있음 (선택 시
`SetActive(true/false)`로 켜고 끄는 선택 표시 컨테이너, `FlashMarker()`가 공격 대상 지정 시 깜빡이는
데도 씀).

`Striker.prefab` 기준으로 실제 구조를 까본 결과:
- `Marker` GameObject 자신에 `Transform + MeshFilter(Cylinder, fileID 10206 빌트인 메쉬) +
  MeshRenderer(m_Enabled: 0)` 3개 컴포넌트만 있고, 다른 컴포넌트(Collider, 스크립트)는 없음.
- `Marker`의 자식으로 Nested Prefab 하나(`Circle Select Red.prefab`, `Assets/prefabs/Effect/Selected/`)가
  붙어있음 - **이게 실제로 화면에 보이는 선택 링 비주얼**임.

즉 `Marker` 자신의 MeshFilter+MeshRenderer(Cylinder, 꺼짐)는 실제 선택 링과 무관한 죽은 컴포넌트고,
진짜 비주얼은 자식의 `Circle Select Red` 프리팹이 담당함 - 0569에서 다뤘던 유닛/건물 루트의
Cube/Capsule 플레이스홀더와 완전히 같은 패턴(모델링 과정에서 남은 도형이 꺼진 채 방치됨).

### 2) 코드에서 이 MeshRenderer를 직접 건드리는 곳 없음

`unitMarker`/`enemyMarker` 관련 코드(`UnitController.cs`, `EnemyUnitController.cs`,
`AllyController.cs`, `UserControl.cs`)를 전수 확인한 결과 전부 `SetActive(...)`로 **GameObject
전체**를 켜고 끌 뿐, `Marker`에 붙은 `MeshRenderer.enabled`나 `MeshFilter`를 직접 참조하는 코드는
없음. 0569에서 확인했던 `PlacementSystem.GetGroundOffsetY`/`GetBuildingHeight`도 프리팹 **루트**의
MeshFilter만 보고, `Marker`는 유닛 루트의 자식이라 그 계산과도 무관함. → 제거해도 안전.

### 3) 대상 프리팹 (Unity 다이나믹 코드로 `Assets/prefabs` 전체 스캔)

`Marker`라는 이름의 자식 오브젝트 하위에서 `enabled == false`인 `MeshRenderer`가 있는 유닛 프리팹
**31개**를 찾았고, 그중 실제로 직접 수정이 필요한 건 아래 **23개**(나머지 8개는 Ally OC 유닛으로,
OC 원본을 그대로 참조하는 Nested `PrefabInstance` 래퍼라 원본만 고치면 자동 반영됨 - 0569와 동일한
구조, 직접 확인함: `Brute Mech (Ally).prefab`이 `OC/Unit/Tier2/Brute Mech.prefab`의
`m_SourcePrefab` guid를 그대로 참조):

- **NTA/Unit 9개**: Worker Drone, Assault Trooper, Scout Drone, Sharpshooter, Pulsar Tank, Ranger
  Infantry Fighting Vehicle, SkyLancer, Firehawk, Guardian Drone
- **OC/Unit 8개**: Cyborg Soldier, Railgunner, Striker, Brute Mech, Heavy Assault Tank, Ironhawk,
  Raven, Strike Drone
- **OC/RescueUnit 2개**: Cyborg Soldier (Rescue), Heavy Assault Tank (Rescue) - Ally와 달리 Nested
  Prefab이 아니라 독립된 사본이라 별도로 고쳐야 함 (직접 확인함)
- **Spore_Brood/Unit 3개**: Ripfang, Skitterwing, Spitter
- **Test 1개**: TestEnemy

`OC/Unit/Mainbase/Nanobot Repair.prefab`, `Test/TestUnit.prefab`, `Test/TestAirUnit.prefab`은 애초에
`Marker` 하위에 꺼진 MeshRenderer가 없어서 제외.

## 제안하는 수정

0569와 같은 방식: `PrefabUtility.LoadPrefabContents` → `Marker`라는 이름의 자식 오브젝트를 찾아 그
`MeshFilter` + `MeshRenderer`를 `Object.DestroyImmediate`로 제거 (컴포넌트만 제거, `Marker`
GameObject 자체와 그 자식인 `Circle Select Red` 선택 링은 그대로 유지) → `PrefabUtility.SaveAsPrefabAsset`.

대상: 위 23개 프리팹. Ally OC 8개는 수정 없음(자동 반영).

## 확인 필요

1. 위 23개 프리팹, `Marker`의 `MeshFilter`+`MeshRenderer`만 제거(오브젝트 자체와 `Circle Select Red`
   자식은 유지)하는 방식으로 진행해도 될지 확인 부탁드립니다. → 승인.

## 구현 결과

Unity 에디터 다이나믹 코드 실행(`PrefabUtility.LoadPrefabContents` → `Marker` 자식 탐색 → 조건
재검증(메쉬가 빌트인 Cylinder(fileID 10206)인지, `MeshRenderer.enabled == false`인지) →
`Object.DestroyImmediate`로 `MeshFilter`+`MeshRenderer`만 제거 → `PrefabUtility.SaveAsPrefabAsset`)으로
대상 23개 프리팹 전부 처리, 스킵 없이 전부 `REMOVED`.

`git diff`로 확인:
- `OC/Unit/Tier1/Striker.prefab`, `OC/RescueUnit/Cyborg Soldier (Rescue).prefab` 등: `Marker`
  GameObject의 `m_Component` 목록에서 `MeshFilter`/`MeshRenderer` 참조 2줄만 빠지고, 두 컴포넌트
  문서 블록이 그대로 삭제됨. `Marker` GameObject 자신과 그 자식(Nested Prefab `Circle Select Red`,
  fileID 참조)은 변경 없이 유지됨.
- `OC/Ally/Unit/*` 8개(Ironhawk (Ally) 등): 이번 작업으로 인한 diff는 전혀 없음(예상대로 Nested
  `PrefabInstance`라 OC 원본만 고치면 자동 반영됨). `Ironhawk (Ally).prefab`에 있던 유일한 diff는
  `m_LocalScale.x/z: 1→2`로, 세션 시작 전부터 이미 있던 별개의 미커밋 변경이었고 이번 작업과 무관함.

컴파일 확인: `npx uloop-cli compile` → `Success: true, ErrorCount: 0, WarningCount: 0`.

## 최종 요약

- NTA/OC/Spore_Brood/OC-RescueUnit/Test 유닛 프리팹 23개에서 `Marker`(선택 표시 컨테이너) 자신에
  남아있던 죽은 Cylinder 플레이스홀더(`MeshFilter`+꺼진 `MeshRenderer`)가 전부 제거됨.
- `Marker` GameObject와 실제 선택 링 비주얼을 담당하는 자식 Nested Prefab(`Circle Select Red`)은
  그대로 유지 - 선택 표시 기능(`SetActive`/`FlashMarker`)에는 영향 없음.
- Ally OC 유닛 8개는 Nested `PrefabInstance` 구조 덕분에 별도 수정 없이 자동 반영됨.
- 0569에서 미뤄뒀던 "확인 필요" 3번 항목이 이번으로 해소됨.
