# 0462. 구조 마커 Yellow/Green 상호 배타 처리

**날짜:** 2026-08-08

## 요청 내용
> Green이 켜진 상태에선 Yellow는 꺼져야해

`doc/0461`에서 `Rescue()`가 `rescuedMarker`(Green)만 켜고 `unitMarker`(Marker)의 다른 자식인
"Circle Select Yellow"는 그대로 켜진 채로 남아있던 문제.

## 조사 및 적용

- `Marker` 하위 구조 확인: `Circle Select Green`(기본 꺼짐), `Circle Select Yellow`(기본 켜짐) -
  서로 형제 관계의 자식.
- `UnitController.cs`에 `preRescueMarker` 필드 추가(Yellow 연결용). `Rescue()`에서
  `rescuedMarker.SetActive(true)`와 함께 `preRescueMarker.SetActive(false)` 호출하도록 수정 -
  마커 자체의 on/off(SelectUnit/DeselectUnit)는 그대로 두고, 그 안의 두 이펙트만 상호 배타적으로 전환.
- `Cyborg Soldier (Rescue).prefab`, `Heavy Assault Tank (Rescue).prefab` 양쪽 모두
  `preRescueMarker` → `Circle Select Yellow`로 연결(`PrefabUtility.LoadPrefabContents` +
  `SerializedObject`로 연결 후 `SaveAsPrefabAsset`).

## 검증 (Play Mode)

- `Cyborg Soldier (Rescue)` 대상으로 `Rescue()` 호출 후 `SelectUnit()` 호출해 확인:
  `Marker.activeSelf=True`, `Green.activeSelf=True`/`activeInHierarchy=True`,
  `Yellow.activeSelf=False`/`activeInHierarchy=False` - Yellow/Green 상호 배타 정상 동작.
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).
- `git status` 확인: water-mesh 애셋 노이즈 없음. `AttackRange.cs` 변경은 이 세션이 만든 게
  아니라 동시에 작업 중인 다른 세션의 `doc/0460` 관련 변경(건드리지 않음).

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs` (`preRescueMarker` 필드 추가, `Rescue()` 수정)
- `Assets/prefabs/OC/RescueUnit/Cyborg Soldier (Rescue).prefab` (`preRescueMarker` 연결)
- `Assets/prefabs/OC/RescueUnit/Heavy Assault Tank (Rescue).prefab` (`preRescueMarker` 연결)
