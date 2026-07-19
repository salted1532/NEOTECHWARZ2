# 0167 - 전장의 안개: `FogOfWarManager.cs` / `Eye.cs` 실제 생성

## 요청

"이대로 한번 만들어줘" — [[fogofwar-folder-and-eye-script-design]](0166) 설계 문서 내용을 그대로
`Assets/Scripts/FogOfWar/` 폴더에 코드로 반영해달라는 승인.

## 변경 사항

0166 문서의 6절 제안 코드를 수정 없이 그대로 신규 파일로 생성했다(기존 파일은 건드리지 않음).

### `Assets/Scripts/FogOfWar/Eye.cs` (신규)

Before: (파일 없음)

After: 0166 문서 6.1절 코드와 동일. `sightRadiusCells`(셀 칸 수, 대상별 조절)를 들고 있고
`OnEnable`/`OnDisable`에서 `FogOfWarManager.Register/Unregister`로 자가 등록·해제.

### `Assets/Scripts/FogOfWar/FogOfWarManager.cs` (신규)

Before: (파일 없음)

After: 0166 문서 6.2절 코드와 동일. `cellSize` + `mapOrigin`/`mapSize`로부터 `gridWidth`/`gridHeight`를
`OnValidate`/`Awake`에서 자동 산출, `Eye` 정적 레지스트리(`HashSet<Eye>`)를 0.2초 주기로 순회해
셀 상태(Unexplored/Explored/Visible)를 갱신하고 `Texture2D` 알파값으로 인코딩.

기존 스크립트(`RTSUnitController.cs`, `UnitController.cs`, `BuildingController.cs`,
`EnemyController.cs`, `UserControl.cs`)는 설계대로 **수정 없음.**

## 남은 작업 (0166 문서 7절/8절 참고)

- 씬 설정: 안개 평면(`FogOfWarPlane`, MeshCollider 제거) + 머티리얼, 미니맵 오버레이 `RawImage`
  (Raycast Target 끄기), `FogOfWarManager` 오브젝트 배치 및 인스펙터 연결, 시야를 줄 유닛/건물
  프리팹에 `Eye` 컴포넌트 부착 — 이 부분은 에디터 수동 작업이라 아직 진행하지 않았다.
- 0166 8절의 확인 필요 사항(적 은폐 포함 여부, `mapSize` 실측값, 자원 노드에 `Eye`를 붙일지)은
  아직 답변 대기 중.
