# PreviewSystem

`Assets/Scripts/BuildSystem/PreviewSystem.cs`

## 개요

건물 배치 시 마우스 위치에 반투명 프리뷰(고스트 오브젝트)와 셀 커서를 보여주는 시스템. 배치 가능 여부(validity)에 따라 흰색/빨간색으로 피드백을 준다. 배치가 확정된 뒤에는 일꾼이 도착할 때까지 그 자리에 남아있는 **정적 건설 고스트**도 이 클래스가 만들어준다(마우스를 따라다니는 고스트와는 완전히 별개의 오브젝트/머티리얼).

## 주요 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `previewYOffset` | `float` | (마우스 따라다니는) 프리뷰 오브젝트를 띄우는 Y 오프셋 |
| `cellIndicator` | `GameObject` | 셀 커서 오브젝트 |
| `previewObject` | `GameObject` | 배치 대상 프리팹의 마우스 추적용 고스트 인스턴스 |
| `previewMaterialPrefab` / `previewMaterialInstance` | `Material` | 마우스 추적 고스트 전용 반투명 머티리얼 (인스턴스는 런타임에 흰색/빨간색으로 변경) |
| `cellIndicatorRenderer` | `Renderer` | 셀 커서의 렌더러 (텍스처 스케일/색상 조정용) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 프리뷰 머티리얼 인스턴스 생성, 셀 커서 비활성화 |
| `StartShowingPlacementPreview(prefab, size)` | 배치할 프리팹의 고스트(마우스 추적 프리뷰) 오브젝트를 생성하고 셀 커서 크기를 맞춰 표시 |
| `StartBuildModeCursor()` | 프리뷰 오브젝트 없이 1x1 셀 커서만 표시 (건물 철거 등 커서만 필요한 모드용) |
| `PrepareCursor(size)` (private) | 셀 커서 오브젝트의 크기와 텍스처 스케일을 배치 대상 크기에 맞게 조정 |
| `PreparePreview(previewObject)` (private) | `ApplyGhostMaterial` + `DisableGameplayComponents`를 호출해 마우스 추적 고스트를 "허상"으로 만든다 |
| `ApplyGhostMaterial(obj, material)` (private) | 오브젝트의 모든 렌더러 머티리얼을 지정한 머티리얼 인스턴스로 일괄 교체 (마우스 프리뷰/정적 고스트 공용 헬퍼) |
| `DisableGameplayComponents(obj)` (private) | 콜라이더/리지드바디/NavMeshObstacle 등 실제 게임플레이에 영향을 주는 컴포넌트를 전부 비활성화 (마우스 프리뷰/정적 고스트 공용 헬퍼) |
| `SpawnConstructionGhost(prefab, position)` | 배치가 확정된 위치에 "일꾼이 도착할 때까지 남아있는" **정적** 건설 고스트를 생성한다. 마우스를 따라다니는 `previewObject`와는 완전히 별개의 오브젝트/머티리얼 인스턴스를 사용하므로, 이후 다른 건물을 미리보기해도 서로 색이 간섭하지 않는다. 항상 고정된 흰색(반투명, 배치 가능 색)으로 표시되며 색이 바뀌지 않음(더 이상 유효성 판정 대상이 아니므로) |
| `StopShowingPreview()` | 마우스 추적 프리뷰 표시 종료: 셀 커서를 숨기고 `previewObject`를 파괴 (정적 건설 고스트는 건드리지 않음 - 그건 `PlacementSystem`이 직접 `Destroy` 호출) |
| `UpdatePosition(position, validity)` | 매 프레임 마우스 위치를 받아 마우스 추적 프리뷰/커서 위치와 배치 가능 여부에 따른 색상 피드백을 갱신 |
| `ApplyFeedbackToPreview(validity)` / `ApplyFeedbackToCursor(validity)` (private) | 배치 가능하면 흰색, 불가능하면 빨간색(반투명)으로 머티리얼 색상 적용 |
| `MoveCursor(position)` (private) | 셀 커서를 지면 살짝 아래(y -0.9)로 내려서 바닥에 밀착된 것처럼 보이게 함 |
| `MovePreview(position)` (private) | 마우스 추적 고스트를 지정 위치 + `previewYOffset`만큼 띄워서 배치 |
| `StartShowingRemovePreview()` (internal) | 건물 철거(제거) 모드용 커서 표시: 1x1 크기에 항상 "불가능(빨강)" 색으로 시작 |

## 연관 컴포넌트

- **PlacementSystem**: 배치 모드 진행 중 매 프레임 `UpdatePosition`을 호출해 마우스 프리뷰를 갱신하고, 배치 확정 시 `SpawnConstructionGhost`로 정적 고스트를 생성/관리(파괴는 `PlacementSystem`이 직접 수행)
