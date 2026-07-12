# 0076 — 프리뷰/유닛 명령 포인터를 포스트프로세싱에서 제외하기

## 질문
"현재 내가 적용한 포스트프로세싱, 라이트 세팅, Ambient Occlusion 그래픽 세팅들을 확인하고 프리뷰나 유닛명령 pointer 등이
이 포스트프로세싱에 적용 안받도록 하려면 어떻게 해야해?"

## 1. 현재 적용된 그래픽 세팅 확인

### 포스트프로세싱 (Volume Profile)
씬(`Assets/Scenes/SampleScene.unity`)의 `Global Volume`(`m_IsGlobal: 1`, Layer 0)이
`Assets/Settings/SampleSceneProfile.asset`를 참조. 활성화된 오버라이드 3개:
- **Bloom**: intensity `4`, threshold `0.9`, scatter `0.7`, tint `(1, 0.56, 0.56)`(붉은끼), High Quality Filtering 켜짐
- **Tonemapping**: mode `0`(None) — HDR paperWhite/브라이트니스 리밋 값들은 있지만 모드가 None이라 실제 톤매핑 커브는 비적용 상태
- **Color Adjustments**: postExposure `0.19`, contrast `30`, colorFilter `(1, 0.948, 0.948)`(살짝 붉은 톤)

(참고: `Assets/Settings/DefaultVolumeProfile.asset`는 Unity 패키지 테스트용 더미 프로파일이라 실제 씬에는 안 쓰임 — 착오하지 않도록 기록)

### 라이트
`Directional Light` 하나: Intensity `2`, Color 흰색, Shadow Type `Soft`, Bias `0.05`, Normal Bias `0.4`.
환경광(Ambient)은 `Skybox` 모드, Sky/Equator/Ground 트라이컬러 값 세팅됨, Ambient Intensity `1`,
스카이박스는 기본 Built-in 스카이박스(프로젝트에 있는 "Animated Sun Skybox" 에셋은 이 씬에 적용 안 돼있음).

### Ambient Occlusion
`Assets/Settings/PC_Renderer.asset`의 Renderer Feature로 URP 기본 **Screen Space Ambient Occlusion**이 켜져 있음:
Intensity `1.5`, Radius `0.1`, Direct Lighting Strength `0.25`, Downsample 켜짐. 렌더링 모드는 Forward+.

### Main Camera
Post Processing 켜짐(`m_RenderPostProcessing: 1`), Volume Layer Mask = `Default`(Layer 0)만, HDR/MSAA 켜짐,
Culling Mask = Everything.

## 2. 왜 프리뷰/포인터가 포스트프로세싱의 영향을 받는가

건설 프리뷰(`PreviewSystem.cs`의 고스트 오브젝트/`cellIndicator`)와 유닛 명령 포인터(`UserControl.cs`의
`movePointer`/`attackPointer`, 프리팹은 `TestPointer.prefab`/`TestAttackPointer.prefab`)가 전부 **Layer 0(Default)**에
있고, Main Camera가 Culling Mask = Everything으로 씬의 모든 오브젝트를 한 번에 렌더링한 뒤 그 결과 화면 전체에
Bloom/Tonemapping/Color Adjustments(Volume 포스트프로세싱)와 SSAO(Renderer Feature)가 적용됨. URP는
"이 오브젝트만 포스트프로세싱 제외" 같은 오브젝트 단위 토글이 없어서, 지금 구조로는 씬의 모든 오브젝트가 같은 후처리를
동일하게 받음.

## 3. 제외 방법 (URP 표준 기법: 오버레이 카메라 + 전용 레이어)

이 프로젝트에 이미 비슷한 선례가 있음 — `MiniMap_Camera`가 `m_RenderPostProcessing: 0`으로 세팅된 별도 카메라임
(다만 미니맵은 렌더텍스처로 빠지는 독립 Base 카메라라 구조가 다름). 이번엔 그 카메라를 **Main Camera 위에 스택되는
Overlay 카메라**로 만들어서 재사용하는 방식.

1. **새 Layer 추가** (예: `NoPostFX` 또는 `Indicators`) — `ProjectSettings > Tags and Layers`에서 빈 슬롯(11~31번대)에 등록.
2. **프리뷰/포인터 오브젝트들을 그 레이어로 이동**:
   - `PreviewSystem`의 `previewObject`(고스트), `cellIndicator`
   - `UserControl`의 `movePointer`, `attackPointer` (즉 `TestPointer.prefab`, `TestAttackPointer.prefab`)
   - (선택) `PlacementSystem`의 `mouseIndicator`도 같은 취급이면 포함
3. **Main Camera Culling Mask에서 그 레이어 제외** — 안 그러면 Main Camera가 한 번, 새 오버레이 카메라가 또 한 번
   그려서 이중 렌더링됨.
4. **새 Overlay 카메라 추가**:
   - `Camera` 컴포넌트: Culling Mask = 새 레이어만, Clear Flags = `Depth Only`(Main Camera가 그린 배경 위에 겹쳐 그리기)
   - `UniversalAdditionalCameraData`: `m_RenderPostProcessing: 0` (Bloom/Tonemapping/ColorAdjustments 전부 비적용)
   - Main Camera의 **Camera Stack**에 이 오버레이 카메라를 추가 (URP Base 카메라 인스펙터의 "Renderer > Stack" 목록에 등록)
5. **SSAO까지 제외하려면**: SSAO는 "Render Post Processing" 토글과 무관한 Renderer Feature라서, 4번만으로는 오버레이
   카메라가 그리는 오브젝트끼리도 SSAO가 계산될 수 있음(다만 프리뷰/포인터는 보통 반투명/Unlit이라 실질 영향은 미미할 가능성 높음).
   완전히 배제하려면 SSAO 피처가 빠진 별도 `Universal Renderer Data` 애셋을 하나 더 만들어서, 오버레이 카메라의
   "Renderer" 드롭다운에서 그 애셋을 선택하면 됨.

## 4. 적용한 변경 (사용자 승인: Layer+Overlay 카메라 방식 진행 / SSAO는 Post Processing 토글만으로 충분)

- **`ProjectSettings/TagManager.asset`**: 비어있던 Layer 12번을 `Indicators`로 등록.
- **`Assets/prefabs/Test/TestPointer.prefab`**, **`TestAttackPointer.prefab`** (`movePointer`/`attackPointer` 원본):
  `m_Layer: 0` → `12`.
- **`Assets/prefabs/CoursorindicatorParent.prefab`** (`PreviewSystem.cellIndicator`가 가리키는 오브젝트,
  부모+실제 렌더러가 있는 자식 둘 다): `m_Layer` → `12`.
- **`Assets/Scenes/SampleScene.unity`**의 `Sphere`(`PlacementSystem.mouseIndicator`/`cellIndicator`): `m_Layer` → `12`.
- **`Assets/Scripts/BuildSystem/PreviewSystem.cs`**: 건물마다 다른 프리팹을 그대로 `Instantiate`하는 이동 중
  프리뷰 고스트(`previewObject`)는 프리팹 자체를 건드릴 수 없으므로, `PreparePreview()`에서
  `SetLayerRecursively(previewObject, indicatorsLayer)`로 런타임에 자식까지 전부 `Indicators` 레이어로 이동시키는
  방식으로 처리. (`SpawnConstructionGhost()`로 만드는, 배치 확정 후 건설 중 표시되는 고스트는 이번 범위에서 제외 —
  이건 "프리뷰/포인터"가 아니라 실제로 짓고 있는 건물의 반투명 표현이라 일반 조명/포스트프로세싱을 받는 게 자연스럽다고
  판단.)
- **`Assets/Scenes/SampleScene.unity`의 Main Camera**:
  - `m_CullingMask`: `4294967295`(Everything) → `4294963199` (Layer 12 `Indicators`만 제외한 나머지 전부).
  - `UniversalAdditionalCameraData.m_Cameras`에 새 Overlay 카메라를 추가해서 Camera Stack 구성.
- **새 GameObject `Indicator Camera`** 추가 (Main Camera의 자식으로, `m_Father`가 Main Camera Transform을 가리켜서
  로컬 좌표 0으로 두면 `CameraControl`이 Main Camera를 움직여도 항상 같은 시점을 자동으로 따라감):
  - `Camera`: Culling Mask = Layer 12(`Indicators`)만, near/far/FOV/뷰포트는 Main Camera와 완전히 동일하게 맞춤(안 맞추면
    오버레이가 그리는 오브젝트와 베이스가 그린 배경의 원근이 어긋나 보임).
  - `UniversalAdditionalCameraData`: `m_CameraType: 1`(Overlay), `m_RenderPostProcessing: 0`(Bloom/Tonemapping/
    ColorAdjustments 전부 미적용), `m_ClearDepth: 0`(Main Camera가 이미 그린 깊이 버퍼를 그대로 이어받아서, 프리뷰/포인터가
    지형이나 다른 오브젝트에 자연스럽게 가려지거나 얹혀 보이도록 함 — 뎁스 테스트 없이 무조건 맨 앞에 그려지는 게 아님).

SSAO는 이번엔 Post Processing 토글만으로 처리하기로 했음(별도 Renderer 애셋은 만들지 않음). SSAO는 "Render Post
Processing" 토글과 무관하게 Renderer Feature로 항상 켜져 있어서, Overlay 카메라가 그리는 오브젝트끼리도 이론상 SSAO
연산 대상이 될 수 있음 — 다만 프리뷰 고스트는 반투명(URP는 반투명에 SSAO를 적용 안 함)이고, 포인터/셀 커서는 다른
오브젝트와 근접하는 형태가 아니라 실질적인 시각적 차이는 거의 없을 것으로 예상. 만약 실제로 확인했을 때 SSAO 흔적이
보이면 그때 별도 Renderer 애셋을 추가하는 후속 작업으로 처리하면 됨.

## 5. 확인 필요 사항

Unity 에디터에서 SampleScene을 열어서:
1. Hierarchy에 `Main Camera` 밑에 `Indicator Camera` 자식이 생겼는지, Main Camera 인스펙터의 Camera Stack에
   `Indicator Camera`가 Overlay로 등록돼 있는지 확인.
2. 빌드 모드에서 프리뷰 고스트/셀 커서, 그리고 유닛 이동/공격 명령 포인터가 화면에 정상적으로(Main Camera와 어긋남 없이,
   깊이 가림도 자연스럽게) 보이는지 확인.
3. Bloom(붉은끼 tint)/Color Adjustments(contrast 30 등)가 이 오브젝트들에는 더 이상 안 걸리는지 (주변 지형/건물에는
   여전히 걸려야 정상) 육안 확인.
4. 카메라를 드래그/줌/회전(`CameraControl`)했을 때 프리뷰/포인터가 메인 화면과 어긋나지 않고 잘 따라오는지 확인.
