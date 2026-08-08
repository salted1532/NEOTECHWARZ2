# 0457. 유물/데이터베이스 선택 마커 연결

**날짜:** 2026-08-08

## 요청 내용
> 유물이랑 데이터에다가 마커 추가했는데 선택시 마커 작동하도록 해줘

## 조사 내용

`MissionItem.cs`(doc/0455)는 이미 `selectionMarker` 필드와 `SelectItem()`/`DeselectItem()`(마커
`SetActive` 토글)을 갖고 있었음 - 당시엔 마커 오브젝트가 없어서 필드를 비워뒀었음(`fileID: 0`).
사용자가 `Artifact.prefab`/`Database.prefab`에 "Marker"라는 자식 오브젝트를 직접 추가해뒀는데, 아직
`selectionMarker` 필드에 연결이 안 돼 있어서 `SelectItem()`이 아무것도 못 건드리는 상태였음. 또한 두
Marker 오브젝트 모두 프리팹 기본 상태가 활성(`m_IsActive: 1`)이라, 연결만 하면 오히려 "항상 켜져
있다가 선택 여부와 무관"해지는 문제가 남아있었음 - `ResourceNode.resourceMarker`/
`EnemyUnitController.enemyMarker`처럼 시작 시 꺼두는 코드가 `MissionItem`엔 없었음.

## 적용한 변경

- `MissionItem.cs` - `Start()`를 추가해 `selectionMarker`를 시작 시 꺼둠(기존 마커 패턴과 동일).
- `Artifact.prefab`/`Database.prefab` - `MissionItem.selectionMarker` 필드를 각자의 "Marker" 자식
  오브젝트로 연결, 마커의 기본 활성 상태도 꺼둠(에디터에서 미리보기 시 항상 보이지 않도록).

## 검증

- `npx uloop-cli compile --wait-for-domain-reload true`: `Success: true`, `ErrorCount: 0`,
  `WarningCount: 39`(기존 베이스라인과 동일).
- Unity 콘솔 Error 0건.
- 프리팹 확인: `selectionMarker` 필드가 각 프리팹의 Marker fileID를 정확히 가리킴.

## 변경된 파일

- `Assets/Scripts/System/MissionItem.cs`
- `Assets/prefabs/MissionObject/Artifact.prefab`
- `Assets/prefabs/MissionObject/Database.prefab`
