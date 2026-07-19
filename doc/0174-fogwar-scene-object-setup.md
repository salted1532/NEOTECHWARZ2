# 0174 - `SampleScene.unity`에 `FogWar` 오브젝트 직접 배치

## 요청

"이대로 진행시켜줘" — [[csfogwar-grid-cap-256-and-fograveleragent]](0173)에서 남겨둔 씬/프리팹 설정을
진행해달라는 지시. 다만 Unity 에디터가 열려있는 상태에서 `.unity`/`.prefab` 파일을 직접 고치면
에디터 메모리 상태와 충돌(저장 시 덮어쓰기)할 위험이 있어 [[docs_session_logging]] 규칙에 따라
먼저 확인을 구했고, 사용자가 **"프리팹만 내가 처리, 씬 설정은 직접 해달라"**를 선택.

## 조사

- 프로젝트 실제 게임플레이 유닛/건물 프리팹은 `Assets/prefabs/NTA/Unit/*`(7개)와
  `Assets/prefabs/NTA/Building/*`(6개), 테스트용 `Assets/prefabs/Test/*`(4개) — `UnitController.cs`/
  `BuildingController.cs`의 스크립트 GUID로 역추적해서 확인.
- **`CameraControl`의 실제 씬 값이 스크립트 기본값과 다름**을 발견: 스크립트 필드 초기값은
  `minX/maxX/minZ/maxZ = -130/40/-130/40`였지만, `SampleScene.unity`에 실제 배치된 인스펙터 값은
  `minX=-85, maxX=85, minZ=-115, maxZ=50`. 즉 실제 맵은 X 170, Z 165 범위이고 중심은
  `(0, 0, -32.5)` — [[fogofwar-folder-and-eye-script-design]](0166)에서 스크립트 기본값(-130~40)을
  근거로 추정했던 맵 크기가 틀렸다는 걸 이번에 바로잡음.
- `csFogWar.cs.meta`(guid `b5de54c084f472845b61ee9a97cb8a48`), `FogPlane.mat`(guid
  `36b93ac6fcf129c449491654e397fe4a`, fileID `2100000`) GUID 확인.

## 변경 사항

`Assets/Scenes/SampleScene.unity`에 씬 루트로 2개 오브젝트를 YAML 직접 추가(기존 오브젝트는 전혀
건드리지 않고 파일 끝, `SceneRoots` 블록 바로 앞에 추가만 함):

```
FogWar (위치: 0, 0, -32.5 — 맵 중심)
├─ csFogWar 컴포넌트
└─ Fog Mid Point (자식, 로컬 위치 0,0,0 → 월드로는 부모와 동일한 맵 중심)
```

`csFogWar` 인스펙터 값:
- `levelMidPoint` → `Fog Mid Point`의 Transform
- `fogPlaneMaterial` → `FogPlane.mat`
- `levelDimensionX = 114`, `levelDimensionY = 110`, `unitScale = 1.5`
  (170÷1.5≈114, 165÷1.5≈110 — [[csfogwar-usage-and-grid-cap-question]](0172)에서 논의한 대로 256 캡
  안에서 여유 있게, 그러면서 RTS 안개치고 과하게 촘촘하지 않은 절충값으로 선택)
- `obstacleLayers`는 비워둠(`m_Bits: 0`) — `csFogWar.InitializeVariables()`가 값이 0이면 자동으로
  `LayerMask.GetMask("Default")`로 채우는 fallback을 그대로 활용(어떤 레이어가 "Default"인지 직접
  단정하지 않고 스크립트 자체의 안전장치에 맡김).
- 나머지 필드(`FogRefreshRate`, `fogColor`, `fogLerpSpeed`, `saveDataOnScan`, `scanSpacingPerUnit` 등)는
  스크립트 기본값 그대로.

## 확인 필요 / 후속 작업

1. **Unity 에디터에서 씬을 리로드(또는 새로 열기)해서 확인 부탁.** 외부에서 직접 `.unity` 파일을
   수정했으므로, 에디터가 이 씬을 이미 메모리에 들고 있었다면 반드시 다시 로드해야 이번 변경이 반영됨
   (안 하고 저장하면 이번 변경이 덮어써질 수 있음).
2. `LevelDataToLoad`를 비워뒀기 때문에 **Play 최초 실행 시 `ScanLevel()`이 자동으로 돌면서
   `Assets/LevelData/Default.json`을 새로 생성**한다(에디터 전용 동작). 처음 보면 낯설 수 있어 미리 안내.
3. 프리팹 13개(`Assets/prefabs/NTA/Unit/*`, `Assets/prefabs/NTA/Building/*`)에 `FogRevealerAgent`
   부착은 사용자가 직접 진행하기로 함 — 각 프리팹을 열어 Add Component로 `FogRevealerAgent`를 추가하고
   `sightRange`(월드 단위)를 유닛/건물 성격에 맞게 설정하면 됨.
4. `unitScale=1.5`, 그리드 114×110 값은 실측 후 필요하면 조정 가능(0172/0173에서 설명한 성능 트레이드
   오프 참고).
