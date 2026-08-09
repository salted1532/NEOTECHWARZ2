# 0500 - MissionSelect Canvas 해상도 자동 비율 조절 제안

## 질문
MissionSelect 씬에서 canvas가 여러 해상도에 맞게 자동으로 비율 조절하도록 해줘. 메인씬이나 인게임에서도 적용된 방법 그대로 사용해줘.

## 조사 결과

### 현재 상태 (문제)
`Assets/Scenes/Missions/MissionSelect.unity` 의 Canvas (`CanvasScaler`, fileID 2119319465):

```yaml
m_UiScaleMode: 0          # Constant Pixel Size
m_ReferenceResolution: {x: 800, y: 600}
m_ScreenMatchMode: 0
m_MatchWidthOrHeight: 0
```

`Constant Pixel Size` 모드는 해상도가 바뀌어도 UI 요소가 화면 크기에 맞춰 스케일되지 않음 (해상도가 커지면 UI가 화면 왼쪽 위에 작게 몰려 보임).

### 메인씬 / 인게임에서 쓰는 방법
`Assets/Scenes/MainScene/MainScene.unity` 의 Canvas (fileID 2120285369):
```yaml
m_UiScaleMode: 1          # Scale With Screen Size
m_ReferenceResolution: {x: 1920, y: 1080}
m_ScreenMatchMode: 0      # Match Width Or Height
m_MatchWidthOrHeight: 0.5
```

인게임 씬(Mission0~5)은 `Assets/prefabs/Game/GameManager.prefab` 안의 Canvas가 담당하며, 동일하게:
```yaml
m_UiScaleMode: 1
m_ReferenceResolution: {x: 1920, y: 1080}
m_ScreenMatchMode: 0
m_MatchWidthOrHeight: 0.5
```

즉 메인씬과 인게임 둘 다 CanvasScaler를 `Scale With Screen Size` / 기준 해상도 1920x1080 / Match Width Or Height 0.5 로 통일해서 쓰고 있음. MissionSelect만 다른 설정(Constant Pixel Size, 800x600)을 쓰고 있어서 해상도 대응이 안 됨.

## 제안하는 변경
`MissionSelect.unity`의 CanvasScaler(fileID 2119319465) 설정을 메인씬/인게임과 동일하게 맞춘다:

```yaml
m_UiScaleMode: 1
m_ReferenceResolution: {x: 1920, y: 1080}
m_ScreenMatchMode: 0
m_MatchWidthOrHeight: 0.5
```

### Before / After

**Before**
```yaml
m_UiScaleMode: 0
m_ReferenceResolution: {x: 800, y: 600}
m_ScreenMatchMode: 0
m_MatchWidthOrHeight: 0
```

**After**
```yaml
m_UiScaleMode: 1
m_ReferenceResolution: {x: 1920, y: 1080}
m_ScreenMatchMode: 0
m_MatchWidthOrHeight: 0.5
```

MissionSelect UI가 800x600 기준으로 배치되어 있다면, 기준 해상도가 1920x1080으로 바뀌면서 UI 요소들이 화면 좌측 상단에 작게 몰려 보일 수 있음 (Canvas 좌표계 자체가 커지기 때문). 이 경우 UI 요소들의 RectTransform 위치/크기를 1920x1080 기준으로 재배치하는 추가 작업이 필요할 수 있음 — 이 부분은 실제로 에디터에서 확인 후 진행 여부를 판단하는 게 안전함.

## 적용 결과
사용자 승인 후 `MissionSelect.unity`의 CanvasScaler(fileID 2119319465) 값을 위 "After" 대로 수정 완료.

Unity 에디터에서 MissionSelect 씬을 열어 Game View(1920x1080)로 확인한 결과, UI 요소(행성 노드, 연결선, "Mission 0" 라벨, "Unlock ALL Mission" 버튼)가 화면 전체에 정상적으로 배치되어 있고 좌측 상단에 몰리는 문제 없음. 별도 RectTransform 재배치는 필요하지 않았음.

추가로 Game View를 4:3 해상도(1024x768, Custom preset)로 바꾸고 Play Mode에서 재확인함 (미션 노드/연결선은 런타임에 스크립트로 생성되는 것으로 보여 Edit Mode에서는 배경만 보이고 Play Mode에서만 정상 렌더링됨). Play Mode + 1024x768에서도 행성 노드, 연결선, 버튼이 비율에 맞게 함께 스케일되었고, 16:9 기준으로 설계된 화면 우측 요소 일부가 4:3 화면 가장자리에서 살짝 잘리는 정도만 있었음 — 이는 `Match Width Or Height 0.5` 설정에서 나타나는 정상적인 동작이며 메인씬/인게임과 동일한 특성임. 확인 후 Play Mode 종료 및 Game View 해상도를 기본값(Free Aspect)으로 복구함.
