# 0339. 빌드에서만 발생하는 컴파일 에러 수정 (UnityEditor 참조 누락 가드)

**날짜:** 2026-07-31

## 요청

> 이제 프로토타입을 빌드해보려고 하는데 컴파일 에러가 발생했다는데 확인좀 해줘

## 원인

`Assets/Scripts/Unit/UnitController.cs`와 `Assets/Scripts/System/RTSUnitController.cs` 상단에
`#if UNITY_EDITOR` 가드 없이 `UnityEditor` 네임스페이스를 직접 참조하는 `using` 구문이 남아있었음:

- `UnitController.cs`: `using static UnityEditor.PlayerSettings;`
- `RTSUnitController.cs`: `using UnityEditor;`

`UnityEditor` 어셈블리는 **에디터 안에서만** 존재하고 실제 플레이어 빌드에는 포함되지 않는다. 그래서
유니티 에디터 안에서 돌리는 `uloop compile`(이번 세션 내내 확인해온 것)은 항상 에러 없이 통과했지만,
**실제 빌드(Player 빌드) 시에만** `The type or namespace name 'UnityEditor' could not be found`류의
컴파일 에러가 발생하는 전형적인 "에디터에선 되는데 빌드에선 깨지는" 케이스였음.

두 파일 모두 실제로는 `UnityEditor`의 어떤 멤버도 코드에서 쓰지 않고 있었음(둘 다 조사해서 확인) —
즉 죽은/불필요한 `using`이었고, 그 김에 같이 남아있던 다른 미사용 `using`들도 정리:

- `UnitController.cs`: `using System.Net;`, `using System.Resources;`,
  `using static UnityEngine.GraphicsBuffer;` — 전부 파일 안에서 실제로 참조되는 곳이 없었음
  (`System.Resources`는 특히 위험한 편 — `.NET`의 `System.Resources.ResourceManager`와 이 프로젝트의
  `ResourceManager` 클래스가 이름이 같아서, 나중에 `ResourceManager`를 이 파일에서 쓰게 되면 모호한
  참조 에러가 날 수 있었음).
- `RTSUnitController.cs`: `using System.Net.Sockets;`, `using UnityEngine.UIElements;`,
  `using static RTSUnitController;`(자기 자신을 `using static`하는 무의미한 구문) — 전부 미사용.
  `using static UIController;`는 `CommandButtonData`(중첩 타입)를 이 파일 곳곳에서 접두사 없이 쓰고
  있어서 그대로 유지함.

## 수정

두 파일에서 위 미사용/빌드 차단 `using` 구문을 전부 제거.

## 검증

1. `npx uloop-cli compile`: 에러 0개(기존과 동일 — 이 검사만으로는 이 버그를 못 잡는다는 게
   핵심이었음).
2. 프로젝트 전체에서 `UnityEditor` 참조가 남은 파일을 다시 검색 — `MainMenuController.cs`,
   `CaptureSystem.cs`, `TerritoryZone.cs` 3개만 남았고, 전부 `#if UNITY_EDITOR`로 올바르게
   감싸져 있음을 재확인.
3. **실제 Windows64 플레이어 빌드를 직접 실행해서 검증**(`BuildPipeline.BuildPlayer`, 씬 3개
   MainScene/TestScene/SampleScene 포함) — 결과: `result=Succeeded, totalErrors=0`(경고 522개는
   기존 세션 내내 봐온 `FindFirstObjectByType` deprecated 류와 동일 계열, 빌드 실패 원인 아님).
   빌드 산출물은 검증 목적의 스크래치 폴더에만 생성, 프로젝트에는 영향 없음.

## 영향받는 파일

- `Assets/Scripts/Unit/UnitController.cs`
- `Assets/Scripts/System/RTSUnitController.cs`
