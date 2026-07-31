# 0334. SampleScene의 Capture_Point 미작동 + 핀 중복/손상 버그 수정

**날짜:** 2026-07-31

## 요청

> 이거 SampleScene에서 capture_point가 작동안하는데 왜그런거야 그리고 막 핀 개수 늘어나고 또 버그
> 생기네 그리고 핀 6개 사용해서 새로운 도형을 만들으면 제대로 작동 안해

## 원인 (독립적인 버그 2개)

### 1) Capture_Point가 점령 자체가 진행되지 않는 이유
`Assets/prefabs/Capture_Point/Capture_Point.prefab`의 `SphereCollider`(점령 판정용 트리거)가
**`Is Trigger = false`**로 저장돼 있었음. `CaptureSystem.cs`는 `OnTriggerEnter`/`OnTriggerExit`로만
유닛 진입/이탈을 감지하는데, 콜라이더가 트리거가 아니면 이 이벤트 자체가 전혀 발생하지 않아
`alliesInRange`/`enemiesInRange`가 항상 비어있는 상태로 남고, 점령 진행(`controlValue`)이 절대
움직이지 않음. **프리팹 원본의 값**이라 SampleScene뿐 아니라 TestScene 등 이 프리팹을 쓰는 모든 씬에
동일하게 영향을 줌.

### 2) 핀이 계속 늘어나고 새 도형이 제대로 안 만들어지는 이유
`Capture_Point/Capture_territory`(`TerritoryZone` 컴포넌트)의 `pinPoints` 리스트(4칸)가 **전부
null**로 끊어져 있었음. 원인 추적 결과, `TerritoryZonePin` 클래스가 예전엔 별도 `.cs` 파일이었다가
(이 프로젝트엔 현재 그런 파일이 없음 - `TerritoryZone.cs` 안에 통합돼 있음) 나중에 `TerritoryZone.cs`
안으로 합쳐지면서, 그 이전에 SampleScene에 만들어둔 핀들의 스크립트 참조(guid)가 더 이상 어떤 에셋과도
매칭되지 않는 상태가 됨. Unity는 이런 경우 씬 파일 안에 깨진 참조를 표시하는 임시 `MonoScript` 블록을
따로 만들어두는데, 실제로 콘솔에 `"The referenced script (Unknown) on this Behaviour is missing!"`
경고가 뜨고 있었음.

`TerritoryZone.SyncPinPoints()`(`Assets/Scripts/CaptureSystem/TerritoryZone.cs:144-169`)는:
- 빈 슬롯(`null`)마다 새 핀을 만든다 → 4칸이 전부 null이니 인스펙터를 건드릴 때마다 계속 새로 생성.
- 더 이상 안 쓰는 핀은 `child.GetComponent<TerritoryZonePin>() != null`인 것만 지운다 → 깨진 스크립트
  참조 때문에 컴포넌트가 제대로 인식 안 되는 낡은 핀은 이 조건을 통과 못 해 **영원히 안 지워짐**.

결과적으로 SampleScene의 `Capture_territory`엔 `PinPoint_0`(x2), `PinPoint_1`(x2), `PinPoint_2`,
`PinPoint_3` 총 6개가 쌓여 있었고, 그중 1개만 정상 인식, 2개는 스크립트 자체가 깨짐(콘솔 경고),
나머지 3개는 컴포넌트가 아예 없는 빈 오브젝트였음. `pinPoints` 리스트 자체가 전부 null이라 다각형이
0개 정점으로 취급돼(`GetPolygonXZ()`가 빈 배열 반환) 그 구역은 애초에 "영토"로 전혀 기능하지 않는
상태였고, 6개로 늘리려는 시도도 기존 쓰레기 핀 위에 새 핀만 계속 얹는 식이라 정상적인 육각형이 만들어질
수 없었음.

## 수정

### 1. `Assets/prefabs/Capture_Point/Capture_Point.prefab`
`SphereCollider.m_IsTrigger`를 `0 → 1`로 변경 (프리팹 원본 수정이라 모든 씬에 적용됨).

### 2. `Assets/Scenes/SampleScene.unity` — 핀 데이터 정리
`npx uloop-cli execute-dynamic-code`로 Unity 라이브 API를 통해:
- `Capture_territory` 아래 기존 `PinPoint_*` 자식 6개(중복/손상 전부) 삭제
- `PinPoint_0`~`PinPoint_3` 4개를 새로 생성(`AddComponent<TerritoryZonePin>()`으로 정상적인 컴포넌트
  참조를 갖도록 함), `Capture_territory`의 자식으로 배치, 로컬 위치는 전부 `(0,0,0)`
- `TerritoryZone.pinPoints`(`SerializedProperty`)를 이 4개로 새로 연결
- 씬 저장

검증: 저장 후 다시 읽어서 `pinPoints` 4칸 전부 비어있지 않고 각각 `GetComponent<TerritoryZonePin>()`이
정상 반환됨을 확인. 콘솔을 지운 뒤 씬을 다시 열어 "missing script" 경고 0개, 에러 0개 확인.

> 사람 손으로 직접 씬 YAML을 편집하는 대신 Unity 라이브 API(`execute-dynamic-code`)를 쓴 이유: 이
> 프리팹 인스턴스 내부의 중첩 컴포넌트(`TerritoryZone.pinPoints`)에 대한 override 직렬화 포맷과,
> 다중 클래스 `.cs` 파일 안의 보조 클래스(`TerritoryZonePin`)가 갖는 스크립트 참조 fileID를 손으로
> 정확히 계산하는 것은 실수 위험이 커서(바로 이번에 고친 버그 자체가 그런 깨진 참조 문제였음),
> Unity가 직접 올바른 참조를 만들어 직렬화하도록 하는 편이 안전함.

## 남은 수동 작업 (씬 편집, 코드/자동화로는 불가능)

새로 만든 `PinPoint_0`~`3` 4개는 전부 `Capture_territory`와 같은 좌표(0,0,0)에 겹쳐 있는 상태 —
`TerritoryZone.SyncPinPoints()`가 새 핀을 만들 때 항상 그렇게 동작함(TestScene에서도 같은 패턴).
Scene 뷰에서 4개를 원하는 위치로 하나씩 드래그해서 영토 다각형을 다시 그려야 함. 육각형(핀 6개)을
시도하고 싶으면, 이번에 깨끗해진 `TerritoryZone` 컴포넌트의 `Pin Points` 리스트 `Size`를 인스펙터에서
`6`으로 늘리면(이제 정상 동작하므로) 빈 슬롯 2개에 새 핀이 자동 생성됨 — 그 뒤 6개를 원하는 순서로
배치.

## 영향받는 파일

- `Assets/prefabs/Capture_Point/Capture_Point.prefab`
- `Assets/Scenes/SampleScene.unity`
