# 0461. 구조 비콘을 실제 트리거로, 마커는 Green 이펙트 토글 방식으로

**날짜:** 2026-08-08

## 요청 내용
> 현재 발견된 문제는 일단 비콘의 콜리전 트리거 범위 안에서 발동되도록 했으면 좋겠고 위치기반이 아니라
> 구조 된 유닛의 마커가 꺼진 상태로 유지되는데 마커 안에 Green이펙트가 활성화 되는식으로 작동해야해

## 조사 및 적용

### 1) 비콘 판정 - 거리/반경 → 실제 트리거 콜라이더 접촉

`Stage3Objectives`가 `rescueRadius`(고정 float) + `Vector3`/수평 거리 계산으로 판정하던 것을,
`doc/0456`(Stage2Objectives 유물/데이터 반납)과 동일한 패턴으로 교체함:

- `UnitController.cs`에 `MissionItem`과 동일한 트리거 추적 추가: `OnTriggerEnter`/`OnTriggerExit`로
  겹친 콜라이더를 `HashSet<Collider>`에 담아두고, `public bool IsTouching(Collider other)`로 조회.
- `Stage3Objectives.cs`의 `rescueBeacon` 필드를 `Transform` → `Collider`로 변경(비콘의 실제
  `SphereCollider`를 직접 연결), `rescueRadius` 필드 삭제. 판정 로직을
  `unit.IsTouching(rescueBeacon)`으로 교체.
- 비콘(`SphereCollider`, `Is Trigger` 켜짐, 반지름 10)엔 Rigidbody가 없지만 플레이어 유닛
  프리팹들(NTA/구조 유닛 공통)은 전부 Rigidbody가 있어서 물리 트리거가 정상 발동함(확인함).
- 필드 타입이 바뀌어서 기존 `Transform` 연결이 끊어짐 - 현재 열려 있던 `Mission3.unity`에서
  `Stage3Objectives.rescueBeacon`을 비콘의 `SphereCollider`로 다시 연결하고 씬 저장함.

### 2) 마커 - 별도 GameObject 전환 → 같은 마커 안의 Green 이펙트 토글

기존 구현은 `unitMarker`(Yellow)와 `rescuedMarker`(Green)를 **서로 다른 마커 오브젝트**로 보고
선택 시 `ActiveMarker`로 어느 쪽을 켤지 골랐음 - 사용자가 실제로 만든 구조는 `rescuedMarker`
("Circle Select Green")가 `unitMarker`("Marker")의 **자식**이라, 이 방식대로면 마커 전체가 계속
꺼진 채로 남는 문제가 있었음(정확히 사용자가 지적한 증상).

- `ActiveMarker` 프로퍼티 제거, `SelectUnit`/`DeselectUnit`/`FlashMarker(Routine)`을 전부 원래
  방식대로 `unitMarker`만 토글하도록 되돌림(구조 여부와 무관하게 마커 on/off는 항상 동일하게 동작).
  `Start()`에서 `unitMarker`/`rescuedMarker` 둘 다 꺼둠(기존 유지).
  `rescuedMarker`가 `unitMarker`의 자식이라, 부모가 꺼져 있으면(`activeInHierarchy`) 자식의 로컬
  active 값과 무관하게 안 보임 - 그래서 이 구조에서 그대로 활용 가능함.
- `Rescue()`: `rescuedMarker.SetActive(true)`만 호출(한 번 켜면 계속 켜진 채로 둠 - "구조했다"는
  사실은 되돌리지 않음). 마커 자체의 on/off는 안 건드림 - 이후 `SelectUnit()`이 부모(`unitMarker`)를
  켜는 순간, 이미 활성화해둔 Green 자식도 자동으로 같이 보임.

## 검증 (Play Mode)

- 지상 유닛을 비콘의 트리거 반경 안(중심에서 3만큼 떨어진 지점)으로 옮긴 뒤 확인:
  `survivorsRescued=True`, 구조 대상 유닛의 `isRescueUnit=False`, `rescuedMarker.activeSelf=True`지만
  `unitMarker.activeSelf=False`라 `rescuedMarker.activeInHierarchy=False`(아직 안 보임 - 의도대로).
- 그 유닛을 실제로 `SelectUnit()`(좌클릭 선택과 동일)해보니 `Marker.activeSelf=True`,
  `Circle Select Green.activeInHierarchy=True` - Green 이펙트가 정상적으로 보임 확인.
- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔: 새로 발생한 Error 없음(기존 Editor Inspector 관련 에러 6건은 무관하게 그대로).

## 변경된 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/Stage3Objectives.cs`
- `Assets/Scenes/Missions/Mission3.unity` (`Stage3Objectives.rescueBeacon` 재연결)
