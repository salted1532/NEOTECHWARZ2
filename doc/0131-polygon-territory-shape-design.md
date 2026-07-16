# 0131. 다각형(사다리꼴/오각형 등) 영토 모양 설계

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **설계 제안만** 담고 있고
> 실제 프로젝트 파일(`Assets/Scripts/**`)은 아직 건드리지 않았다. 검토 후 어디까지 실제로 반영할지
> 알려주면 그때 코드에 반영한다.

## 날짜
2026-07-16

## 요청
점령된 영역을 원(반경)이 아니라, 사다리꼴이나 오각형처럼 여러 각을 가진 임의의 다각형 모양으로 나누고 싶다. 맵 상황에 따라 각 거점(또는 구역)마다 모양을 원하는 대로 직접 지정할 수 있어야 한다.

## 1. 현재 상태
- `doc/0129`(제한 로직)와 `doc/0130`(시각 표시)은 둘 다 **원형 영토**(거점의 `SphereCollider` 반지름을 그대로 "영토 반경"으로 재사용, `TerritoryManager.IsInsideTerritory`가 좌표-중심점 거리 비교로 판정)를 전제로 설계됐다 — 아직 코드에는 반영 안 됨(두 문서 모두 설계 단계).
- `CaptureSystem.cs`(`Assets/Scripts/CaptureSystem/CaptureSystem.cs:40-52`)는 점령 판정용 트리거 콜라이더(`SphereCollider`)만 갖고 있고, "영토" 개념 자체는 아직 코드에 없다.
- 즉 지금은 다각형은커녕 원형 영토조차 미구현 상태 — 이번 요청은 0129/0130에서 전제한 "원"을 "임의의 다각형"으로 바꾸는 것.

## 2. 설계

### 2.1 핵심 방향: 반경(float) 대신 "다각형 정점 목록"을 데이터로 저장
원은 중심점 + 반지름 두 값이면 충분했지만, 다각형은 모양이 거점마다 완전히 다를 수 있으므로 **정점 목록**(`Vector2[]`, XZ 평면 좌표 — RTS는 탑다운이라 Y는 무시)을 데이터로 저장해야 한다.

```csharp
// Assets/Scripts/CaptureSystem/TerritoryZone.cs (신규)
public class TerritoryZone : MonoBehaviour
{
    public enum ShapeType { Circle, Polygon }

    [SerializeField] private ShapeType shapeType = ShapeType.Circle;
    [SerializeField] private float circleRadius = 10f;          // ShapeType.Circle일 때
    [SerializeField] private Vector2[] polygonPoints;           // ShapeType.Polygon일 때, transform.position 기준 로컬 XZ 오프셋들

    public bool Contains(Vector3 worldPos) { ... } // 원이면 거리 비교, 다각형이면 point-in-polygon
}
```
- `CaptureSystem`에 이 컴포넌트를 붙여서 쓰거나(1점령지 = 1영토), 아니면 완전히 분리된 오브젝트로 둬서 "거점 하나가 여러 영토 조각을 소유" 또는 "여러 거점이 하나의 넓은 영토를 공유"하는 맵 구성도 가능하게 할지는 결정 필요(4절 참고).
- `Circle`을 남겨둔 이유: 기존 0129/0130 설계·단순한 맵에서는 원이 더 저렴하고 편집하기 쉬움 — 맵 상황에 따라 거점별로 선택하게 하면 "원하는 대로 지정"이라는 요청과 "다각형 하나만 강제"보다 유연함.

### 2.2 다각형 포함 판정 (point-in-polygon)
원처럼 단순 거리 비교가 안 되므로, 표준 **crossing-number(레이캐스팅) 알고리즘**으로 판정한다 — 오목한 다각형에도 정확하게 동작:
```csharp
static bool PointInPolygon(Vector2 point, Vector2[] verts)
{
    bool inside = false;
    for (int i = 0, j = verts.Length - 1; i < verts.Length; j = i++)
    {
        if ((verts[i].y > point.y) != (verts[j].y > point.y) &&
            point.x < (verts[j].x - verts[i].x) * (point.y - verts[i].y) / (verts[j].y - verts[i].y) + verts[i].x)
            inside = !inside;
    }
    return inside;
}
```
`TerritoryManager.IsInsideTerritory`(0129 2.1)는 그대로 두고, 안쪽 판정만 `zone.Contains(worldPos)` 호출로 바꾸면 원/다각형 어느 쪽이든 동일한 질의 API를 쓸 수 있다.

### 2.3 다각형을 에디터에서 자유롭게 그리기
"내가 원하는대로 지정" 요구의 핵심은 **씬 뷰에서 직접 정점을 찍고 옮길 수 있어야** 한다는 것. Unity의 `PolygonCollider2D` 편집 UX와 동일한 패턴을 커스텀 에디터로 구현:
```csharp
// Assets/Scripts/Editor/TerritoryZoneEditor.cs (신규, Editor 전용)
[CustomEditor(typeof(TerritoryZone))]
public class TerritoryZoneEditor : Editor
{
    private void OnSceneGUI()
    {
        // 정점마다 Handles.FreeMoveHandle로 드래그 가능한 점 표시
        // Scene 뷰 클릭(Shift+클릭 등)으로 정점 추가/삭제
        // 정점들을 Handles.DrawAAPolyLine로 이어서 실시간 프리뷰
    }
}
```
- 이러면 거점마다 사다리꼴/오각형/뭐든 맵을 보면서 바로 그릴 수 있음 — 코드로 좌표를 직접 입력할 필요 없음.
- Play 모드가 아닌 **에디터 편집 시점에 미리 그려두는 정적 데이터**(런타임에 모양이 안 바뀜)로 충분한지, 아니면 게임 중에도 영토 모양이 동적으로 변해야 하는지는 결정 필요(4절) — 후자면 에디터 툴 외에 런타임 편집 API도 필요.

### 2.4 점령 판정(물리 트리거)과 영토 모양의 분리
0130에서 이미 "판정(트리거 콜라이더)"과 "질의(좌표 비교)"는 분리해야 한다고 정리했다. 다각형에서는 이 분리가 더 중요해진다:
- **점령 진행 판정**(아군이 몇 초 머물렀는지)은 지금처럼 거점 주변의 작은 `SphereCollider` 트리거로 계속 처리 — 다각형 전체를 물리 콜라이더로 만들 필요 없음.
- **영토 범위(넓은 다각형)**는 물리 콜라이더가 필요 없는 순수 좌표 질의(2.2)로만 쓰인다 — 건설 가능 여부, 자원 채취 가능 여부, 생산/회복 차단(0129) 전부 `TerritoryZone.Contains(worldPos)` 호출이면 충분하고 콜라이더를 아예 안 만들어도 됨.
- 예외: 유닛이 "영토 경계를 넘었다"는 이벤트를 물리적으로 감지하고 싶다면(예: 진입 시 알림/이펙트), Unity 6000.4는 논콘벡스(non-convex) 트리거 `MeshCollider`를 지원하므로 다각형을 눌러 펴서(extrude) 만든 프리즘 메쉬를 트리거로 쓸 수 있다 — 필요하면 후속으로 추가, 이번 MVP 범위에는 안 넣는 걸 추천(질의만으로 0129의 모든 요구사항 충족됨).

### 2.5 시각적 표시 (다각형 채우기 + 외곽선)
0130의 "원반" 안은 원 전용이라 다각형에는 안 맞음. 다각형용으로 두 요소가 필요:
- **채우기(면)**: 다각형은 오목할 수도 있으므로 단순 팬(fan) 삼각분할로는 부족 — **ear-clipping** 삼각분할로 메쉬를 만들고 진영 색 머티리얼을 입힌 평면 메쉬를 지면에 깐다. 정점이 에디터에서 바뀔 때(2.3) 매번 재생성.
- **외곽선**: `LineRenderer`로 정점들을 루프로 이어서 그리면 원형 원반보다 경계가 훨씬 선명하게 보임 — 사다리꼴/오각형 모양이 실제로 눈에 보이길 원하는 요청과 잘 맞음.
- 여러 영토가 이어 붙는 맵(0130의 "이음새" 문제)에서는 다각형을 서로 맞닿게(변을 공유하게) 그리면 원보다 오히려 이음새가 깔끔해짐 — 다각형 방식의 장점.
- 미니맵 반영은 0069(FoW)와 동일하게 그리드/텍스처로 굽는 방식(0130의 B안)을 다각형에도 그대로 적용 가능 — 텍스처에 다각형을 채우는 것도 좌표 질의(2.2)만 있으면 됨(래스터화 시 각 텍셀에 대해 `Contains` 호출).

## 3. 신규/수정 파일 목록 (제안)

| 파일 | 종류 | 내용 |
|---|---|---|
| `Assets/Scripts/CaptureSystem/TerritoryZone.cs` | 신규 | `ShapeType`(Circle/Polygon), 정점 데이터, `Contains(worldPos)` 판정 |
| `Assets/Scripts/Editor/TerritoryZoneEditor.cs` | 신규 | 씬 뷰에서 정점 추가/삭제/드래그하는 커스텀 에디터 |
| `Assets/Scripts/CaptureSystem/TerritoryManager.cs` | 신규 또는 0129안 수정 | 등록된 `TerritoryZone` 목록 순회, `IsInsideTerritory`가 원/다각형 구분 없이 `zone.Contains()` 호출 |
| `Assets/Scripts/CaptureSystem/CaptureSystem.cs` | 수정 | `TerritoryZone` 참조 추가(또는 같은 오브젝트에 컴포넌트 병용) |
| (신규, 이름 미정) `TerritoryVisual.cs` | 신규 | ear-clipping 메쉬 생성 + `LineRenderer` 외곽선, 소유권 변경 시(0129의 `TerritoryChanged` 이벤트) 재생성 |

## 4. 결정이 필요한 부분 (구현 전 확인 요청)
1. **거점과 영토의 관계**: 지금처럼 "거점 하나 = 영토 하나"(1:1, `CaptureSystem`에 `TerritoryZone` 부착)로 갈지, 아니면 거점과 별개로 영토 구역을 자유 배치(거점 하나가 여러 조각을 소유하거나, 거점 없이 지형 경계로만 나뉜 구역도 가능)하게 할지.
2. **모양 결정 시점**: 맵 제작 시 에디터에서 미리 그려서 고정(정적)이면 되는지, 아니면 플레이 중에도 모양이 바뀌어야 하는지(예: 점령 상황에 따라 영토가 늘어나는 연출).
3. **원/다각형 혼용 허용 여부**: 2.1처럼 거점마다 원 또는 다각형을 선택하게 할지, 아니면 전부 다각형으로 통일(원도 다각형의 특수 케이스로 취급)할지 — 후자면 `ShapeType` 분기 자체가 없어져 코드는 더 단순해지지만 단순한 원형 거점도 매번 정점을 그려야 함.
4. **물리 트리거 확장(2.4 예외)**: 지금 제안대로 "점령 트리거는 원, 영토 질의는 다각형"으로 분리해도 충분한지, 아니면 다각형 경계 자체의 물리적 진입/이탈 감지도 필요한지.
5. **0129/0130과의 통합 순서**: 이 문서는 0129(제한 로직)·0130(시각 표시)이 아직 코드 미반영 상태라는 전제로 "원 대신 다각형"만 바꾼 설계다. 세 문서(0129+0130+0131)를 한 번에 합쳐서 구현할지, 다각형 부분만 먼저 반영할지.

## 다음 단계
이 문서는 설계까지만이다. 위 5가지 결정 사항에 답을 주면, 그 답을 반영해서 3절의 파일들을 실제로 구현하겠다.

## 변경 파일
없음 (설계 제안만, 코드 미반영).
