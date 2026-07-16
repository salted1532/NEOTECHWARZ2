# 0151. TerritoryZone 핀을 자식 오브젝트에서 순수 데이터로 전환 (Play 시 참조 유실 버그 해결)

> **⚠️ 되돌려짐**: 이 문서의 변경은 바로 다음 요청(`doc/0152`)에서 사용자가 되돌려달라고 요청해 반영을 취소했다. `TerritoryZone.cs`는 다시 `doc/0149` 시점(자식 오브젝트 핀 + `isPlayingOrWillChangePlaymode` 가드) 상태로 복원됨. 아래 내용은 한때 적용됐던 변경의 기록으로만 남긴다.

## 날짜
2026-07-16

## 요청
Play 버튼을 누르면 `Pin Points`에 연결돼 있던 핀들이 전부 `None`으로 바뀌고, `LineRenderer`의 `Size`도 0이 되는 문제 확인 및 해결.

## 원인
`ProjectSettings/EditorSettings.asset`을 확인해보니 `m_EnterPlayModeOptionsEnabled: 1`, `m_EnterPlayModeOptions: 0`(둘 다 유지) — Play를 누를 때마다 씬이 백업 스냅샷에서 다시 로드되는 정상 동작이 켜져 있다(에디터 로그에도 `Loaded scene 'Temp/__Backupscenes/0.backup'`가 실제로 찍힘).

`Capture_Point`는 프리팹 인스턴스이고, `PinPoint_0`~`N`은 그 프리팹 원본에는 없던 걸 스크립트(`TerritoryZone.SyncPinPoints`)가 나중에 자식으로 추가한 오버라이드였다. **프리팹 인스턴스에 스크립트로 나중에 추가된 자식 게임오브젝트("added GameObject" 오버라이드)는 이 백업 스냅샷 직렬화/역직렬화 과정에서 참조가 끊기기 쉬운, 잘 알려진 유니티의 약점**이다. 그래서 Play를 누르면 `List<Transform> pinPoints`가 참조하던 자식들이 `None`이 돼버렸고, `GetPolygonXZ()`가 빈 배열을 반환해 `LineRenderer.positionCount`도 0이 됐다.

## 해결: 핀을 자식 게임오브젝트 대신 순수 데이터로 저장
사용자 선택에 따라, 정점을 별도 게임오브젝트(핀) 없이 **이 컴포넌트 자신의 로컬 오프셋 데이터**(`List<Vector3> pinLocalOffsets`)로만 저장하도록 구조를 바꿨다. 값 데이터(리스트/배열 등)는 프리팹 인스턴스 오버라이드로도 안정적으로 직렬화되므로, 이번 버그의 원인이었던 "추가된 자식 오브젝트" 자체가 더 이상 존재하지 않는다.

### 주요 변경 (`Assets/Scripts/CaptureSystem/TerritoryZone.cs`, 전체 재작성)
- `List<Transform> pinPoints` → `List<Vector3> pinLocalOffsets`(이 오브젝트 기준 로컬 오프셋).
- 핀의 실제 위치는 `transform.TransformPoint(pinLocalOffsets[i])`로 즉석 계산(`PinWorldPosition`).
- `TerritoryZonePin` 마커 클래스, `OnValidate`/`SyncPinPoints`(자식 생성/정리 로직), `Application.isPlaying`/`isPlayingOrWillChangePlaymode` 가드 전부 제거 — 더 이상 씬에 오브젝트를 만들거나 지우지 않으므로 이 문제 자체가 원천적으로 발생할 수 없음.
- 인스펙터의 `Pin Local Offsets` 리스트 `Size`만 원하는 꼭짓점 개수로 맞추면(빈 슬롯은 `Vector3.zero`로 자동 채워짐, 유니티 기본 리스트 UI 동작), 씬 뷰에서 커스텀 에디터가 그려주는 노란 구체 핸들을 드래그해서 위치를 잡는다.
- 신규 `TerritoryZoneEditor`(`[CustomEditor(typeof(TerritoryZone))]`, `TerritoryZone` 내부 중첩 클래스, `#if UNITY_EDITOR`): `OnSceneGUI()`에서 각 핀을 `Handles.FreeMoveHandle`로 그리고, 드래그하면 `Undo.RecordObject` 후 로컬 오프셋을 갱신. 정점을 잇는 노란 다각형 미리보기도 같이 그림.
- `LineRenderer` 외곽선/색상 로직(`Awake`/`Update`/`ApplyOutlineStyle`)은 기존과 동일하게 유지.

## 영향받는 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (전체 재작성)

## ⚠️ 남은 작업 (씬 편집, 코드 아님)
- 기존 씬(`TestScene` 등)에 남아있는 `PinPoint_0`~`N` 자식 오브젝트들은 더 이상 쓰이지 않으니(스크립트 `TerritoryZonePin`도 삭제됨 → "Missing Script" 경고가 뜰 수 있음) **수동으로 삭제**하는 걸 권장.
- 각 `Capture_Point`의 `TerritoryZone` 컴포넌트에서 `Pin Local Offsets`의 `Size`를 원하는 꼭짓점 개수로 다시 설정하고, 씬 뷰에서 노란 구체 핸들을 드래그해 다각형을 새로 그려야 한다(기존 위치 데이터는 이전 버그로 이미 다 같은 좌표였으므로 어차피 새로 그려야 하는 상황이었음).

## 확인/테스트 필요
- `TerritoryZone`을 씬 뷰에서 선택했을 때 노란 구체 핸들이 보이고 드래그로 옮겨지는지.
- Play를 여러 번 껐다 켜도 이번엔 위치가 유지되는지(더 이상 `None`/`Size 0`이 안 되는지).
- 점령 완료 시 외곽선 색이 여전히 잘 바뀌는지(로직 자체는 안 건드림).

## 비고
[[confirm_before_implementing]] — 두 가지 해결 방식(프리팹 언팩 vs 순수 데이터 구조 변경) 중 순수 데이터 구조 변경을 사용자가 직접 선택한 뒤 반영함. `doc/0131`~`0149`의 핀/영토 관련 논의를 최종적으로 대체하는 구현.
