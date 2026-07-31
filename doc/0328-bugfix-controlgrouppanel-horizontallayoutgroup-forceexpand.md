# 0328. ControlGroupPanel HorizontalLayoutGroup 강제확장(ForceExpand) 버그 수정

**날짜:** 2026-07-31

## 요청

사용자가 채팅에서 직접 명시적으로 요청함: "안에 있는 글씨도 같이 크기를 조절해줘 그리고 호라이즌할떄
버튼 크기는 보장되고 위치도 그냥 왼쪽에 딱 붙어서 계속 늘어나는식이면 좋겠어 개수에 따라 위치가 계속
변하는게 아니라" — 요청 문장 자체가 이미 구체적인 완료 조건(글씨 크기 연동 + 버튼 크기 고정 + 왼쪽
packed 배치)을 명시하고 있어 별도 승인 없이 바로 반영함([[confirm_before_implementing]]은 설계가
모호할 때 확인받기 위한 절차인데, 이번엔 사용자가 이미 원하는 동작을 정확히 지정함).

## 조사 내용 (coordinator 서브에이전트가 먼저 조사한 내용, 그대로 채택)

씬 `TestScene.unity`의 `GameManager/Canvas/HorizontalLayoutGroup` 오브젝트에 있는
`UnityEngine.UI.HorizontalLayoutGroup` 컴포넌트가 `childForceExpandWidth = true`,
`childForceExpandHeight = true`로 되어 있어서, `ControlGroupPanel`(doc/0327)이 런타임에
동적으로 Instantiate/Destroy하는 버튼들의 위치/폭이 현재 형제(sibling) 개수에 따라 흔들림.
왼쪽 끝에 고정 크기로 packed 배치되고, 버튼이 추가될수록 오른쪽으로만 늘어나야 함.
`childControlWidth = false`, `childControlHeight = false`는 요청대로 그대로 유지.

## 조사 내용

- doc/0327에서 바로 이 `HorizontalLayoutGroup` 컨테이너와 `ControlGroupPanel`
  (동적 Instantiate/Destroy + `SetSiblingIndex`로 순서만 맞추는 방식)이 설계됨 —
  씬 구성(레이아웃 그룹 배치)은 사용자가 직접 하기로 했던 부분.
- 원인: `childForceExpandWidth/Height = true`이면 레이아웃 그룹이 남는 공간을 자식들에게
  균등 분배하려고 시도한다. 여기에 `childControlWidth/Height = false`(자식 자신의
  RectTransform 크기를 레이아웃 그룹이 건드리지 않고 그대로 존중)가 같이 걸려 있으면,
  "자식 개수가 바뀔 때마다 각 자식의 유효 슬롯 폭이 흔들리는" 전형적인 증상으로 이어짐 —
  버튼 프리팹 자체의 고정 크기와 레이아웃 그룹의 확장 시도가 충돌하기 때문.
- 표준 수정: `childForceExpandWidth = false`, `childForceExpandHeight = false`로 바꾸면
  각 자식은 자기 RectTransform이 가진 고정 크기를 그대로 유지한 채, 왼쪽부터 `spacing`만큼만
  띄워 packed로 배치된다 — 요청한 동작과 정확히 일치.
- `childControlWidth/Height`, `childAlignment`, `spacing`, `padding` 등 다른 필드는 변경 대상이 아님.
- `Assets/Scripts/**`의 `.cs` 코드는 건드리지 않음 — 이번 변경은 씬(`TestScene.unity`)에 저장된
  컴포넌트 필드 값 하나뿐이라 소스 변경이 아니라 씬 데이터 변경.

## 적용한 변경

1. **`Assets/Scenes/TestScene.unity`** — `GameManager/Canvas/HorizontalLayoutGroup`의
   `HorizontalLayoutGroup` 컴포넌트: `m_ChildForceExpandWidth: 1→0`, `m_ChildForceExpandHeight: 1→0`
   (다른 필드는 그대로). Play Mode를 정지(Edit Mode)한 상태에서 씬 YAML을 직접 수정.
   `find-game-objects`로 최종 값 `Child Force Expand Width/Height = false` 확인.
2. **`Assets/prefabs/UI/Squadbutton.prefab`**(이 세션에서 별도 요청으로 이미 `SizeDelta 20→80`
   적용됨) — 자식 `TextMeshProUGUI`의 `m_enableAutoSizing: 0→1`로 변경. 텍스트 RectTransform이
   이미 부모(버튼)를 꽉 채우도록 앵커가 걸려있어서(`AnchorMin (0,0)~AnchorMax (1,1)`), TMP의 Auto
   Size 기능이 버튼 크기에 맞춰 폰트 크기를 자동으로 조절함(기존 `fontSizeMin 18`~`fontSizeMax 72`
   범위 그대로 재사용 - 새 로직/코드 없이 네이티브 TMP 기능만 켬).
3. `npx uloop-cli get-logs --log-type Error`로 콘솔 에러 0개 확인.

## 작업 중 발생한 사고(참고용 기록)

씬 수정을 처음엔 `execute-dynamic-code` 서브에이전트에 위임하려 했으나, 그 서브에이전트가
`[[confirm_before_implementing]]` 규칙을 이유로 실행을 거부하고 별도 제안 문서(이 문서의 초안)만
작성함. 하지만 이번 요청은 사용자가 채팅에서 이미 구체적인 완료 조건을 직접 지정한 경우라 재승인
절차가 불필요하다고 판단, coordinator가 직접 씬 파일을 수정함. 그 과정에서 Unity 에디터가 일시적으로
`SkyLancer.prefab`의 프리팹 격리 모드에 들어가 있는 상태가 발견돼(원인 불명 - 이전 세션/도구 호출의
부작용으로 추정) `StageUtility.GoToMainStage()`로 메인 씬으로 복귀시킨 뒤 계속 진행함.

## 결과

`ChildForceExpandWidth/Height = false` + 기존 `ChildControlWidth/Height = false` 조합으로, 각
부대 버튼은 프리팹에 저장된 고정 크기(80×80)를 그대로 유지한 채 왼쪽 끝부터 packed로 배치되고,
버튼 개수가 바뀌어도 이미 배치된 버튼들의 위치/크기는 흔들리지 않음(오른쪽으로만 늘어남).
