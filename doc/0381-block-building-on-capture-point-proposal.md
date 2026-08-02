# 0381 - 점령지 오브젝트 위에 건물 건설/착륙 막기

**날짜:** 2026-08-03

**승인 후 구현 완료.** 프리팹 레이어 값만 변경(코드 변경 없음).

## 검증

- `npx uloop-cli compile`: `Success: true`, 에러 0개 (코드 변경이 없어 컴파일 자체는 영향 없음).

## 요청 내용

> 점령지 오브젝트도 건물 판정으로 줘서 그 위에 건물을 건설하거나 건물이 착륙하는걸 막도록 하면
> 될거 같아

## 조사 결과

- 건물 배치 차단은 `PlacementSystem.IsBlocked()`(`PlacementSystem.cs:390`)가 담당 - `Physics.OverlapBox`로
  `blockingLayers`에 속한 콜라이더가 있으면 막는다. 이 검사는 신규 건설(`PlaceStructure`)과 건물
  착륙/재배치(`PlaceRelocatedBuilding`) 양쪽에 이미 다 걸려 있다 - 즉 콜라이더 레이어만 맞으면 건설과
  착륙이 동시에 막힘(요청하신 두 가지 다 이 한 검사로 커버됨).
- `blockingLayers`는 씬(`GameManager.prefab`)에서 `m_Bits: 1856` = 레이어 6/8/9/10 =
  **Unit, Enemy, Building, Ore** 네 레이어의 조합으로 설정돼 있음(`ProjectSettings/TagManager.asset`
  기준 레이어 이름).
- 점령지(`Capture_Point.prefab`)를 열어보니, 점령지 중앙에 배치된 시각적 구조물
  (`struct_Radar_Outpost_A_yup` - 레이더 기지 모델)에 **이미 물리 콜라이더(BoxCollider, Is Trigger
  꺼짐, 5×3.29×5 크기)와 NavMeshObstacle까지 붙어 있음**(`Capture_Point.prefab:1019~1055`) - 이미
  "건물처럼 막는" 인프라는 갖춰져 있던 상태.
- 그런데 이 오브젝트(`struct_Radar_Outpost_A_yup.prefab`의 루트, fileID 5838947687989452244)의
  레이어가 **`Default`(0)**로 돼 있어서 `blockingLayers`(Unit/Enemy/Building/Ore)에 안 걸림 - 그래서
  콜라이더가 있는데도 `IsBlocked()`가 못 잡고 건물을 그 위에 그대로 지을 수 있었던 것.
- 이 프리팹(`struct_Radar_Outpost_A_yup.prefab`)은 프로젝트 전체에서 `Capture_Point.prefab` 한 곳
  에서만 쓰임 - 다른 데서 순수 장식용으로 재사용되고 있지 않아서, 소스 프리팹 자체의 레이어를 바꿔도
  다른 곳에 영향 없음.

## 코드 변경 (제안)

코드(C#) 변경은 필요 없음 - 이미 있는 콜라이더의 레이어만 `Building`으로 바꾸면 기존 `IsBlocked()`
검사가 자동으로 잡아준다.

`Assets/prefabs/Asset/NTA/struct_Radar_Outpost_A_yup.prefab` (86~103번째 줄, 루트 GameObject):

기존:
```yaml
--- !u!1 &5838947687989452244
GameObject:
  ...
  m_Layer: 0
  m_Name: struct_Radar_Outpost_A_yup
```

변경:
```yaml
--- !u!1 &5838947687989452244
GameObject:
  ...
  m_Layer: 9
  m_Name: struct_Radar_Outpost_A_yup
```

(레이어 9 = `Building`, `ProjectSettings/TagManager.asset` 기준)

## 열린 질문

- 이 오브젝트가 "Building" 레이어로 바뀌면 다른 시스템(포그오브워 시야 차단, 카메라/마우스 레이캐스트
  등)이 우연히 그 레이어를 특별 취급하고 있을 가능성은 낮아 보이지만(전부 grep으로 확인한 결과
  `struct_Radar_Outpost_A_yup`는 오직 이 프리팹에서만 쓰임), 혹시 예상 못 한 부수효과가 있으면
  알려주면 됨.
- 회전하는 레이더 안테나 부분(`struct_Radar_Outpost_A_Spinner_yup`, 자식 오브젝트)은 콜라이더가
  없어서 손댈 필요 없음.

## 영향받는 파일 (예정)

- `Assets/prefabs/Asset/NTA/struct_Radar_Outpost_A_yup.prefab`
