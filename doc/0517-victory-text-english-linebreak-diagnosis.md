# 0517. 승리 문구("Victory!") 영어에서 이상하게 줄바꿈되는 문제 - 진단

**날짜:** 2026-08-11

## 요청 내용

> 승리 화면에서 승리 문구가 Victo / ry 영어 버전에서 이상한 줄바꿈되어서 표시되는거좀 확인해줘
> 내가 그냥 텍스트 필드 크기를 조절하면 되는건지

## 조사 내용

`Assets/prefabs/Game/GameManager.prefab`의 `VictoryPanel > VictoryText` (`LocalizedText`가 `ui.victory`
키를 연결):

- `RectTransform.m_SizeDelta: {x: 200, y: 50}` - 텍스트 박스 너비 200
- `TextMeshProUGUI.m_fontSize: 72`, `m_enableAutoSizing: 0`(자동 축소 없음), `m_TextWrappingMode: 1`(자동 줄바꿈 켜짐)
- 컴포넌트에 박혀있던 기본 텍스트는 한글 `"승리!"` (`en.json`의 `ui.victory`는 `"Victory!"`)

한글 "승리!"는 200 너비에 들어맞지만, 영어 로케일에서 `"Victory!"`로 바뀌면 폰트 크기 72 기준으로 200
너비 안에 안 들어가서 TMP가 단어 중간(`Victo` / `ry!`)에서 강제로 줄바꿈함. **텍스트 필드(RectTransform)
크기 문제가 맞다고 확인함.**

## 결과

사용자가 직접 인스펙터에서 수정함 (코드/스크립트 변경 없음).

### `Assets/prefabs/Game/GameManager.prefab` (VictoryText)
```diff
   m_AnchorMin: {x: 0.5, y: 0.5}
   m_AnchorMax: {x: 0.5, y: 0.5}
   m_AnchoredPosition: {x: 0, y: 100}
-  m_SizeDelta: {x: 200, y: 50}
+  m_SizeDelta: {x: 500, y: 50}
   m_Pivot: {x: 0.5, y: 0.5}
```

너비를 200 → 500으로 넓혀서 "Victory!"가 한 줄에 들어가도록 해결함.

(같은 diff에 `VictoryPanel`의 `m_IsActive: 0 → 1`도 포함돼 있는데, 확인차 에디터에서 켜둔 상태로
보임 - `VictoryPanelController.Awake()`가 런타임에 항상 `victoryPanel.SetActive(false)`로 초기화하므로
실제 게임 동작에는 영향 없음.)
