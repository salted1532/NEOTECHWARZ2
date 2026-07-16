# 0132. 핀포인트(게임오브젝트 리스트) 기반 다각형 영토 구현 제안

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안 + 실제 반영할 코드 초안**만
> 담고 있고 `Assets/Scripts/**` 파일은 아직 만들지 않았다. 아래 내용을 확인해 주면 그대로 파일을 생성한다.

## 날짜
2026-07-16

## 요청
`doc/0131`에서 제안한 "씬 뷰에서 커스텀 Handles로 정점을 찍는" 방식 대신, 더 단순한 방식을 원함:
- 인스펙터에 **게임오브젝트 리스트** 필드를 추가.
- 리스트의 **개수(꼭짓점 개수)를 정하면 그 개수만큼 pinPoint 오브젝트가 자동 생성**됨.
- 생성된 pinPoint들을 (일반 Move 툴로) 원하는 위치에 하나씩 배치.
- 코드는 각 pinPoint의 **위치 중 X, Z 값만 뽑아서** 다각형 영역 계산에 사용.

이 방식은 0131의 2.3(커스텀 Handles 에디터)보다 구현이 훨씬 간단하다 — 정점 배치를 유니티 기본 Move 기즈모로 하면 되므로 별도 씬 뷰 클릭/드래그 처리 코드가 필요 없다. 그 대신 0131의 `TerritoryZone` 자체(다각형 데이터 + `Contains` 판정)는 그대로 재사용한다.

## 설계

### 1. 데이터: `pinPoints`는 리스트 개수만 맞추면 자동 채워짐
인스펙터의 기본 `List<>` UI는 "Size" 칸에 숫자를 입력하면 그만큼 슬롯이 생기지만 전부 `null`이다. `OnValidate()`에서 `null`인 슬롯을 찾아 빈 게임오브젝트(핀)를 만들어 채워 넣으면, "개수만 정하면 자동 생성"이 그대로 구현된다.

**주의할 점**: 유니티는 `OnValidate()` 안에서 `Instantiate`/`new GameObject`처럼 씬을 바로 바꾸는 호출을 하면 경고나 에러(`SendMessage cannot be called during OnValidate`)가 날 수 있다. 안전하게 하려면 실제 생성은 `UnityEditor.EditorApplication.delayCall`로 한 프레임 미뤄야 한다.

```csharp
// Assets/Scripts/CaptureSystem/TerritoryZone.cs (신규)
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TerritoryZone : MonoBehaviour
{
    [Tooltip("리스트 크기(Size)만 원하는 꼭짓점 개수로 맞추면, 빈 슬롯에 핀 오브젝트가 자동으로 채워진다.")]
    [SerializeField] private List<Transform> pinPoints = new List<Transform>();

    [SerializeField] private CaptureOwner owner; // 이 영역을 누가 점령했는지 (0129의 TerritoryManager 질의에 사용)

#if UNITY_EDITOR
    private void OnValidate()
    {
        // OnValidate 안에서 바로 씬을 건드리면 안 되므로 다음 에디터 틱으로 미룬다.
        EditorApplication.delayCall += FillEmptyPinSlots;
    }

    private void FillEmptyPinSlots()
    {
        if (this == null) return; // 오브젝트가 그 사이 삭제됐을 수 있음

        for (int i = 0; i < pinPoints.Count; i++)
        {
            if (pinPoints[i] != null) continue;

            var pin = new GameObject($"PinPoint_{i}");
            Undo.RegisterCreatedObjectUndo(pin, "Create Territory Pin Point");
            pin.transform.SetParent(transform);
            pin.transform.localPosition = Vector3.zero; // 생성 직후 위치는 직접 옮겨서 지정
            pinPoints[i] = pin.transform;
        }
    }
#endif

    // 각 핀의 위치에서 X, Z만 뽑아 다각형 정점 배열로 반환 (Y는 탑다운 판정에 안 씀)
    public Vector2[] GetPolygonXZ()
    {
        var result = new Vector2[pinPoints.Count];
        for (int i = 0; i < pinPoints.Count; i++)
            result[i] = new Vector2(pinPoints[i].position.x, pinPoints[i].position.z);
        return result;
    }

    // point-in-polygon (crossing-number). 오목한 다각형도 정확히 판정.
    public bool Contains(Vector3 worldPos)
    {
        if (pinPoints.Count < 3) return false;

        Vector2 p = new Vector2(worldPos.x, worldPos.z);
        Vector2[] v = GetPolygonXZ();
        bool inside = false;

        for (int i = 0, j = v.Length - 1; i < v.Length; j = i++)
        {
            if ((v[i].y > p.y) != (v[j].y > p.y) &&
                p.x < (v[j].x - v[i].x) * (p.y - v[i].y) / (v[j].y - v[i].y) + v[i].x)
                inside = !inside;
        }
        return inside;
    }

    // 씬 뷰에서 정점을 이은 외곽선을 보여줘서 배치하기 편하게 함
    private void OnDrawGizmos()
    {
        if (pinPoints.Count < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < pinPoints.Count; i++)
        {
            if (pinPoints[i] == null) continue;
            Gizmos.DrawSphere(pinPoints[i].position, 0.4f);

            Transform next = pinPoints[(i + 1) % pinPoints.Count];
            if (next != null)
                Gizmos.DrawLine(pinPoints[i].position, next.position);
        }
    }
}
```

### 2. 사용 흐름 (에디터에서)
1. 빈 게임오브젝트에 `TerritoryZone` 컴포넌트를 붙인다.
2. 인스펙터에서 `Pin Points`의 `Size`를 원하는 꼭짓점 개수로 입력(사다리꼴=4, 오각형=5, ...).
3. 즉시 `PinPoint_0` ~ `PinPoint_N-1` 자식 오브젝트가 자동 생성된다(전부 부모 위치에 겹쳐서 생김).
4. 각 핀을 씬 뷰에서 Move 툴로 원하는 위치로 하나씩 드래그 — 노란 점/선(Gizmo)으로 지금 그려지는 모양이 실시간으로 보인다.
5. 다 배치하면 `TerritoryZone.Contains(worldPos)` 하나로 그 다각형 안인지 질의 가능 — `doc/0129`의 `TerritoryManager.IsInsideTerritory`에서 원 대신 이 함수를 호출하도록 바꾸면 그대로 이어붙는다.

### 3. `doc/0129`/`0131`과의 연결
- `TerritoryManager`(0129 2.1)의 순회 로직은 그대로 두고, 판정 부분만 `point.CurrentOwner == owner`(원형) → `zone.owner == owner && zone.Contains(worldPos)`(다각형)로 교체.
- `CaptureSystem`이 점령 완료 처리를 하고, `TerritoryZone`은 "이 다각형을 누가 소유했는가 + 좌표 질의"만 맡는 것으로 역할을 나누는 걸 추천 — 거점(점령 판정)과 영역(범위 질의)이 1:1로 안 묶여도 되게(한 거점이 여러 `TerritoryZone`을 소유하거나, 여러 거점이 하나의 큰 다각형을 같이 소유하는 맵 구성도 가능).
- 시각 표시(채우기 메쉬 + 외곽선)는 `Gizmos`(에디터 전용, 게임 화면엔 안 보임)와는 별개로, 실제 플레이 중 보여줄 거면 `GetPolygonXZ()` 결과로 ear-clipping 메쉬 + `LineRenderer`를 만드는 부분(0131의 2.5)이 추가로 필요 — 이번 문서는 "정점 지정 + 판정"까지만 다루고, 런타임 시각화는 원하면 후속 문서로 분리 제안.

## 결정이 필요한 부분
1. **핀 오브젝트 형태**: 위 코드는 빈 게임오브젝트(`new GameObject`)로 생성한다 — 씬 뷰에서 아이콘도 없이 그냥 빈 점이라 클릭해서 잡기 다소 불편할 수 있다. 대신 작은 구체 메쉬나 커스텀 기즈모 아이콘이 달린 프리팹을 만들어서 그걸 인스턴스화하는 게 나을지.
2. **`Undo.RegisterCreatedObjectUndo` 포함 여부**: 에디터 Undo(Ctrl+Z)로 자동 생성된 핀도 함께 되돌리고 싶으면 필요한 코드인데, 이 프로젝트에 `UnityEditor` 의존 코드(에디터 전용 블록)를 넣는 패턴이 지금까지 없었다 — `#if UNITY_EDITOR`로 감싸는 지금 방식이 괜찮은지, 아니면 Undo 지원 없이 더 단순하게 갈지.
3. **줄어들 때 처리**: 리스트 크기를 줄이면(예: 5→3) 남는 `pinPoints` 뒤쪽 슬롯은 사라지지만, 이미 만들어졌던 `PinPoint_3`, `PinPoint_4` 게임오브젝트 자체는 씬에 고아로 남는다 — 자동으로 같이 삭제할지(추가 구현 필요), 아니면 수동으로 지우게 둘지.
4. **런타임 시각화 포함 여부**: 이번엔 판정 로직까지만 반영하고 화면에 보이는 채우기/외곽선(0131 2.5)은 다음 문서로 미룰지, 아니면 이번에 같이 만들지.

## 다음 단계
위 4가지에 답을 주면 `Assets/Scripts/CaptureSystem/TerritoryZone.cs`를 실제로 생성하고, `doc/0129`의 `TerritoryManager` 설계도 이 다각형 질의를 쓰도록 맞춰서 반영한다.

## 변경 파일
없음 (설계 제안 + 코드 초안만, 실제 파일 미생성).
