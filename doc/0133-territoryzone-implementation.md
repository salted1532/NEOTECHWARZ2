# 0133. TerritoryZone 구현 (핀포인트 다각형 영토)

## 날짜
2026-07-16

## 요청
`doc/0132`의 4가지 결정 사항에 대한 답:
1. 핀은 빈 오브젝트로 생성.
2. 자동 생성된 핀은 그대로 둔다 (Undo 등록 등 추가 처리 없음).
3. 리스트 크기를 줄일 때 남는 핀은 자동 삭제.
4. 판정 로직 + 다각형이 보이는 외곽선까지 이번에 같이 구현.

→ 이대로 실제 코드 생성.

## 변경 파일
- `Assets/Scripts/CaptureSystem/TerritoryZone.cs` (신규)

## 구현 내용 (before/after)

### before
파일 없음 (신규 생성).

### after
```csharp
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TerritoryZonePin : MonoBehaviour { }

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class TerritoryZone : MonoBehaviour
{
    [SerializeField] private List<Transform> pinPoints = new List<Transform>();
    [SerializeField] private CaptureOwner owner = CaptureOwner.Neutral;

    [Header("외곽선 표시")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 0.3f;

    private LineRenderer outlineRenderer;

    public CaptureOwner Owner { get => owner; set => owner = value; }

    private void Awake() { /* LineRenderer 초기 설정 */ }
    private void Update() { RefreshOutline(); }

    public Vector2[] GetPolygonXZ() { /* 각 핀의 X,Z만 뽑아 배열로 반환 */ }
    public bool Contains(Vector3 worldPos) { /* crossing-number point-in-polygon */ }
    private void RefreshOutline() { /* LineRenderer에 핀 위치 반영 */ }

#if UNITY_EDITOR
    private void OnValidate() => EditorApplication.delayCall += SyncPinPoints;

    private void SyncPinPoints()
    {
        // 1) null 슬롯 -> 빈 GameObject(TerritoryZonePin 마커) 생성해서 채움
        // 2) pinPoints에 더 이상 없는 TerritoryZonePin 자식은 DestroyImmediate로 정리
    }
#endif
}
```
(전체 코드는 `Assets/Scripts/CaptureSystem/TerritoryZone.cs` 참고 — 위는 요약)

## 동작 요약
- 인스펙터에서 `Pin Points`의 `Size`를 원하는 꼭짓점 개수로 입력 → 빈 슬롯마다 `PinPoint_N`(빈 오브젝트, `TerritoryZonePin` 마커 포함)이 자동 생성.
- 각 핀을 씬 뷰에서 Move 툴로 옮기면, `LineRenderer`가 매 프레임(`Update`, `[ExecuteAlways]`라 에디터에서도 동작) 그 위치들을 이어 그려서 다각형 외곽선이 실시간으로 보인다.
- `GetPolygonXZ()`가 각 핀의 X/Z만 뽑아 배열로 만들고, `Contains(worldPos)`가 crossing-number 알고리즘으로 점-다각형 포함 여부를 판정(오목 다각형도 정확).
- 리스트 Size를 줄이면 `SyncPinPoints()`가 더 이상 리스트에 없는 `TerritoryZonePin` 자식을 찾아 자동 삭제.
- `owner`(`CaptureOwner`) 필드로 이 영역이 누구 소유인지 표시 — `doc/0129`의 `TerritoryManager.IsInsideTerritory`가 원 대신 이 `Contains()`를 호출하도록 연결하는 건 아직 반영 안 됨(범위 밖, 필요하면 후속).

## 확인/테스트 필요
- 유니티 에디터에서 빈 오브젝트에 `TerritoryZone` 부착 → `Pin Points` Size를 4~5로 바꿔 핀이 생성되는지, 핀을 옮기면 외곽선이 따라오는지, Size를 다시 줄이면 남는 핀이 삭제되는지 직접 확인 필요(코드만 작성, 에디터에서 아직 실행 확인 안 함).

## 비고
- [[docs_session_logging_rule]], [[confirm_before_implementing]] 참고. `doc/0131`(설계) → `doc/0132`(핀포인트 방식 제안, 결정 대기) → `doc/0133`(본 문서, 실제 구현) 순서.
- `TerritoryManager`(0129)와의 실제 연결, 채우기 메쉬(0131 2.5의 ear-clipping 면), 점령 로직과의 결합은 아직 미반영 — 필요하면 후속 요청으로 진행.
