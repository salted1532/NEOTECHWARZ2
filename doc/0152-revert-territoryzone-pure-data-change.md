# 0152. `doc/0151` 변경 되돌림

## 날짜
2026-07-16

## 요청
`doc/0151`에서 적용한 "핀을 자식 오브젝트 대신 순수 데이터(Vector3 리스트)로 전환" 변경을 되돌려달라는 요청.

## 변경
`Assets/Scripts/CaptureSystem/TerritoryZone.cs`를 `doc/0149` 시점 상태로 복원:
- `List<Vector3> pinLocalOffsets` + 커스텀 에디터(`Handles.FreeMoveHandle`) 방식 → 되돌림
- `List<Transform> pinPoints`(자식 게임오브젝트 핀) + `TerritoryZonePin` 마커 + `OnValidate`/`SyncPinPoints`(자식 생성/정리) + `EditorApplication.isPlayingOrWillChangePlaymode` 가드 방식으로 복원.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (되돌림)

## 참고
`doc/0151`에서 겪었던 "Play 시 핀 참조가 None이 되는" 문제(프리팹 인스턴스에 스크립트로 추가된 자식 오브젝트가 Play의 씬 백업 스냅샷 복원 과정에서 참조가 끊기는 것) 자체는 아직 해결되지 않은 상태로 남아있다는 점 참고 바람.

## 비고
[[confirm_before_implementing]] — 사용자가 직접 되돌려달라고 명시적으로 요청해서 바로 반영.
