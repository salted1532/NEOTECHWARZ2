# 0364 — StageObject 미션 목표 텍스트가 마우스 클릭을 관통하도록

**날짜:** 2026-08-02

## 요청

"마우스가 UI클릭을 무시할수 있는 방법을 추가해줘 원하는 UI 의 경우는 마우스 클릭이 관통할수 있도록
현재 StageObject라고 미션 목표를 알려주는 텍스트가 있는데 이건 화면에 존재하는 UI라서 인게임 마우스
클릭을 관통했으면 좋겠거든"

## 조사

`UserControl.cs`가 이미 `EventSystem.current.IsPointerOverGameObject()`로 "지금 마우스가 UI 위인가"를
확인해서 게임 월드 클릭(이동/공격/선택 등) 처리 여부를 결정하고 있었다(216/245/985줄). Unity의
`EventSystem`은 `Graphic.raycastTarget = true`인 UI 요소 위에서만 "UI 위"로 판정하므로, **이미 있는
`raycastTarget` 옵션을 끄기만 하면** 별도 코드 없이 클릭이 자연스럽게 게임 월드로 관통한다 - "마우스가
UI 클릭을 무시할 수 있는 방법"은 Unity에 이미 내장돼 있었음(Inspector의 "Raycast Target" 체크박스).

`Assets/prefabs/Game/GameManager.prefab`에서 `StageObject`(`Stage0Objectives` 컴포넌트가 참조하는
미션 목표 텍스트 6개: `Label`("(주목표)" 헤더), `Main1/2/3`(주목표 3개), `sub1/2`(서브목표 2개))가
전부 `TextMeshProUGUI.raycastTarget = true`(기본값)로 돼 있어서 그 위에서 클릭하면
`IsPointerOverGameObject()`가 true를 반환해 게임 월드 클릭이 씹히고 있었음. 배경 패널(Image)은
따로 없이 텍스트 6개가 Canvas에 바로 얹혀있는 구조.

## 적용

새 코드 없이, `execute-dynamic-code`로 Unity 에디터를 직접 조작해 `StageObject` 하위 모든
`TextMeshProUGUI`의 `raycastTarget`을 `false`로 변경하고 프리팹 저장:

```csharp
GameObject root = PrefabUtility.LoadPrefabContents("Assets/prefabs/Game/GameManager.prefab");
Transform stageObject = /* "StageObject" 찾기 */;
foreach (var text in stageObject.GetComponentsInChildren<TextMeshProUGUI>(true))
    text.raycastTarget = false;
PrefabUtility.SaveAsPrefabAsset(root, path);
```

실행 후 6개 전부(`Label`/`Main1`/`Main2`/`Main3`/`sub1`/`sub2`) `raycastTarget = False`로 저장된 것
로그로 확인. 스크립트 변경 없음 - 프리팹 데이터만 수정.

## 참고 (앞으로 같은 걸 다른 UI에 적용하고 싶을 때)

같은 방법(해당 UI 요소의 Inspector에서 "Raycast Target" 체크 해제, 또는 코드에서
`graphic.raycastTarget = false`)을 다른 UI 텍스트/이미지에도 그대로 쓰면 된다 - 배경 패널처럼 여러
자식을 가진 UI는 그 부모/자식 전체의 `Image`/`TextMeshProUGUI` 등 `Graphic` 상속 컴포넌트를 전부
꺼야 완전히 관통한다(자식 중 하나라도 `raycastTarget=true`면 그 부분만 여전히 클릭을 막음).
