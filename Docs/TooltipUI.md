# TooltipUI

`Assets/Scripts/UI/Tooltip/TooltipUI.cs`

## 개요

툴팁 표시를 전담하는 싱글턴. 버튼(`ProductionSlot`, Info_panel 스탯 아이콘 등)이 호버 시 `Show()`/`Hide()`를 직접 호출하고, 툴팁은 보여지는 동안 매 프레임 호버 중인 버튼의 상단 중앙 위치를 따라간다. (기존 TooltipTrigger/TooltipData 두 스크립트를 이 한 곳으로 통합한 것.)

## 주요 필드

| 필드 | 설명 |
|---|---|
| `Instance` | 정적 싱글턴 인스턴스 (`Awake()`에서 자기 자신으로 설정) |
| `root`, `canvasRect`, `uiCamera` | 툴팁 루트 RectTransform, 소속 캔버스, UI 카메라(Overlay 캔버스면 비워둠) |
| `verticalMargin` | 버튼 상단에서 툴팁을 얼마나 띄울지 |
| `titleText`, `descriptionText` | 제목/설명 텍스트 (TextMeshPro) |
| `costRows[]`, `oreText`, `gasText`, `populationText` | 비용 표시 행(광물/가스/인구수 이미지)과 각 텍스트 — 유닛 생산/건물 건설 버튼에서만 표시 |
| `compactVerticalPadding` | 설명/비용 없이 제목 한 줄만 표시할 때(컴팩트 모드) 위아래로 남길 여백 |
| `isVisible`, `currentTarget` | 현재 표시 여부와 따라가고 있는 대상 RectTransform |
| `defaultRootSize`, `defaultTitlePosition` | 컴팩트 레이아웃 적용 전 원래 크기/위치를 복원하기 위한 캐시 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Awake()` | 싱글턴 등록, 툴팁 배경/텍스트의 `Graphic.raycastTarget`을 전부 꺼서(버튼 위에 떠 있는 동안 버튼의 `OnPointerExit`가 발생해 깜빡이는 것 방지) 기본 크기/제목 위치를 캐싱한 뒤 `Hide()` |
| `Update()` | 보이는 동안 매 프레임 `PositionAboveTarget(currentTarget)` 호출 |
| `Show(target, title, description)` | 비용 없는 일반 명령 버튼용 (이동/공격/정지 등 - 제목/설명만 표시) |
| `Show(target, title, description, ore, gas, population)` | 유닛 생산/건물 건설 버튼용 (제목/설명 + 광물/가스/인구수 비용 표시) |
| `Hide()` | 툴팁을 숨기고 상태 초기화 |
| `ShowInternal(target, title, description, hasCost, ore, gas, population)` (private) | 실제 표시 로직: 텍스트/비용 행 채움 → 설명·비용이 모두 없으면 컴팩트 레이아웃 적용 → 레이아웃 즉시 리빌드 → 대상 위로 위치 계산 |
| `ApplyCompactLayout(isCompact)` (private) | 제목 한 줄만 있는 경우(예: 공격력/방어력 호버) 배경을 제목 높이에 맞게 줄이고, 그 외에는 기본 크기·위치로 복원 |
| `PositionAboveTarget(target)` (private) | 대상(호버 중인 버튼)의 상단 중앙을 기준으로 캔버스 로컬 좌표를 계산해 툴팁이 그 위에 뜨도록 배치 |

## 연관 컴포넌트

- **ProductionSlot**: 커맨드/생산 대기열 버튼 호버 시 `Show`/`Hide` 호출
- **UIController**: Info_panel의 공격력/방어력 아이콘 호버 시(`SetupInfoStatHoverTooltips`) `Show`/`Hide` 호출
