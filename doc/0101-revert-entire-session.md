# 0101 - 이번 세션의 모든 프로젝트 변경 되돌림

**날짜:** 2026-07-13

## 요청 내용

> 그냥 현재 세션에서 진행한 모든 변경을 되돌려줘

`git status` 확인 결과 이 세션에서 직접 만들지 않은 파일들(`SampleScene.unity`, `LightingData.asset`, `ReflectionProbe-0.exr`, `SampleSceneProfile.asset`, NTA 폴더의 건물/유닛 프리팹 14개)도 변경돼 있어서 범위를 먼저 확인했고, 사용자가 "git이 추적하는 모든 변경을 되돌린다"를 선택했다 - 즉 건설모드 우클릭 이동 수정(doc 0092)까지 포함해서 이번 세션에서 tracked 파일에 생긴 변경을 전부 원상복구.

## 수행한 작업

1. `git restore .`로 tracked 파일 전부를 마지막 커밋 상태로 복원:
   - `Assets/Scenes/SampleScene.unity`, `LightingData.asset`, `ReflectionProbe-0.exr`, `Assets/Settings/SampleSceneProfile.asset`
   - `Assets/Scripts/Building/BuildingController.cs`, `Assets/Scripts/System/RTSUnitController.cs`, `Assets/Scripts/Unit/UnitController.cs`, `Assets/Scripts/UserControl/UserControl.cs`
   - `Assets/Settings/PC_Renderer.asset`
   - `Assets/prefabs/NTA/Building/*.prefab` (BaseStructure, Lab, MainBase, SupplyDepot, Tier1, Tier2, Tier3)
   - `Assets/prefabs/NTA/Unit/**/*.prefab` (Worker Drone, Assault Trooper, Scout Drone, Pulsar Tank, Ranger Infantry Fighting Vehicle, Firehawk, Guardian Drone)
   - `ProjectSettings/TagManager.asset`
2. 이번 세션에 새로 만든(untracked) 프로젝트 파일 삭제:
   - `Assets/Scripts/Rendering/` 폴더 전체(`SelectionOutlineFeature.cs` 등) + `Assets/Scripts/Rendering.meta`
   - `Assets/Shader/MainMaterials.mat`(+`.meta`, 원래 `testShader.mat`이었던 걸 사용자가 에디터에서 리네임한 것)
   - `Assets/Shader/Outline.shader`(+`.meta`)
   - `Assets/Shader/SelectionMask.shader`/`.mat`(+각 `.meta`)
   - `Assets/Shader/OutlineComposite.shader`/`.mat`(+각 `.meta`)
3. `doc/0092`~`doc/0100.md`는 삭제하지 않고 남겨둠 - [[docs-session-logging-rule]]에 따라 세션 기록은 실제 코드가 되돌려지더라도 보존한다(과거에도 doc 0034 등에서 같은 방식으로 처리).

## 결과

`git status` 기준으로 tracked 파일 변경 사항 없음, untracked 파일은 `doc/0092`~`doc/0100.md` 세션 로그만 남음. 즉 프로젝트(코드/에셋/씬)는 이 세션 시작 시점과 동일한 상태로 완전히 복원됐다.

## 변경된 파일

- 이번 세션에서 수정했던 모든 tracked 파일 (위 목록) → 커밋된 상태로 복원
- 이번 세션에서 새로 만들었던 모든 untracked 프로젝트 파일 → 삭제 (`doc/*.md`는 제외)
