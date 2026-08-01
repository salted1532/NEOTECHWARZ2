# 0353 — 버그수정(제안): 미니맵 우클릭/A공격이 전혀 동작하지 않는 문제

**날짜:** 2026-08-01

## 질문

"미니맵에다가 유닛이 이동하길 원하는 위치로 우클릭 했는데도 이동을 안해 A 공격으로도 해봤는데도 안되고
미니맵 image에 원하는 위치에 우클릭, A공격 등 명령을하면 그 위치에 맞게 그곳으로 땅에다가 우클릭이나 공격명령한거와
같이 작동하게 해줘야해"

## 원인 확인

[[0349-minimap-commands-mission-markers-attack-pings-proposal]]에서 구현한 `MinimapController.OnPointerClick()`
로직(우클릭 → `userControl.IssueRightClickMoveAt()`, 좌클릭(대기 중인 A공격 등) → `userControl.ConfirmPendingOrderAt()`)
자체는 코드상 정상이지만, **`Assets/prefabs/Game/GameManager.prefab`의 `MinimapController` 컴포넌트에서
`[SerializeField] private UserControl userControl;` 필드가 인스펙터에 연결이 안 돼 있어 항상 `null`**임을 Unity
에디터에서 직접 확인함:

- `MinimapController`는 `MiniMap_image` 오브젝트에 있음.
- `UserControl`은 `RTSUnitControlSystem` 오브젝트에 있음.
- `MinimapController`의 `minimapRect`/`minimapCamera`/`mainCameraControl` 필드는 정상 연결돼 있는데, `userControl`만
  비어 있음(0349 구현 시점에 필드는 코드에 추가됐지만 프리팹 인스펙터에 드래그해서 연결하는 걸 빠뜨린 것으로 보임).

`userControl`이 `null`이므로 `OnPointerClick()` 내부의 `userControl.IssueRightClickMoveAt(...)` /
`userControl.HasPendingGroundOrder()` 호출이 `NullReferenceException`을 던져서, 미니맵을 클릭해도 콘솔에 에러만
남고 겉으로는 "아무 반응 없음"으로 보인다 — 우클릭 이동도, A공격 확정도 둘 다 이 한 지점에서 막힘.

## 제안 수정

코드 수정이 아니라 **프리팹 인스펙터 참조 연결**: `Assets/prefabs/Game/GameManager.prefab`의 `MinimapController`
컴포넌트(`MiniMap_image` 오브젝트) `userControl` 필드에 `RTSUnitControlSystem` 오브젝트의 `UserControl` 컴포넌트를
연결한다. (Unity 에디터 스크립트로 `SerializedObject`를 통해 `PrefabUtility.LoadPrefabContents` →
필드 할당 → `PrefabUtility.SaveAsPrefabAsset`로 처리 예정 — 인스펙터에서 수동으로 드래그하는 것과 동일한 결과.)

## 적용 결과 (2026-08-01)

사용자 확인 후 적용. `Assets/prefabs/Game/GameManager.prefab`을 `PrefabUtility.LoadPrefabContents` →
`SerializedObject`로 열어서, `MinimapController`(`MiniMap_image` 오브젝트) 컴포넌트의 `userControl` 필드를
`RTSUnitControlSystem` 오브젝트의 `UserControl` 컴포넌트로 연결하고 `PrefabUtility.SaveAsPrefabAsset`로 저장.
저장 후 다시 프리팹을 로드해서 필드가 더 이상 `null`이 아님을 재확인함.

**확인 필요 사항**: Unity 에디터에서 미니맵 우클릭 이동과 A공격 확정이 실제 지면 클릭과 동일하게 동작하는지
실제 플레이로 확인 부탁.
