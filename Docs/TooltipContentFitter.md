# TooltipContentFitter

`Assets/Scripts/UI/Tooltip/TooltipContentFitter.cs`

## 개요

툴팁 배경(root)의 세로(및 필요 시 가로) 크기를 "지금 실제로 표시 중인 제목/설명 텍스트 분량"에 맞춰 매번 다시 계산해서 맞추는 전담 컴포넌트. `TooltipUI`는 텍스트/비용 표시 여부만 세팅하고 `Fit()`만 호출하면 되므로, 제목만/설명 포함/비용 포함/설명이 몇 줄이든 전부 이 하나의 로직으로 처리된다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `topPadding` / `titleDescriptionGap` / `bottomPadding` | 위쪽 여백, 제목-설명 간격, 아래쪽 여백(비용 3줄이 이 안에 위치) — 기존 씬에 배치된 좌표를 역산한 기본값 |
| `compactVerticalPadding` | 설명/비용이 전혀 없을 때 제목 위아래로 남길 여백 합 |
| `horizontalPadding` | 좌우 여백 합 (텍스트 폭 자동 조절 시 사용, doc/0471) |
| `root` / `titleText` / `descriptionText` / `costRowRects` | `Configure()`로 전달받는 대상 참조 (private) |
| `defaultRootHeight/Width` / `defaultTitleWidth` / `defaultDescriptionWidth` / `defaultCostRowPositions` | `Configure()` 시점의 기본 크기/위치 스냅샷 (private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Configure(root, titleText, descriptionText, costRows)` | `TooltipUI`가 이미 갖고 있는 참조를 그대로 넘겨받아 초기화 — 씬/프리팹에 새 필드를 연결할 필요가 없음. 기본 크기 캐싱 + `SetupAutoHeight()` 세팅 |
| `SetupAutoHeight(text)` (private, static) | `ContentSizeFitter`를 추가/설정 — 세로는 항상 `PreferredSize`, 가로는 기본 `Unconstrained` |
| `SetHorizontalFit(text, autoWidth, defaultWidth)` (private, static) | 폭 자동조절 여부에 따라 `ContentSizeFitter.horizontalFit` 전환. `autoWidth=false`일 땐 폭을 원래 값으로 명시적으로 되돌림 — 그렇지 않으면 직전에 autoWidth였던 다른 텍스트의 좁은 폭이 이어져 "이름이 2글자씩 줄바꿈"되는 문제가 있었음 |
| `Fit(hasDescription, hasCost)` | 실제 크기 재계산의 핵심. 비용이 없을 때만 폭도 자동 조절(비용 아이콘은 고정 좌표라 폭이 늘면 배치가 어긋남, doc/0471). `LayoutRebuilder.ForceRebuildLayoutImmediate`로 이번 프레임 `ContentSizeFitter` 결과를 즉시 읽어와 높이/폭을 계산하고, 제목만 있는 컴팩트 모드/설명 포함 모드를 구분해 root 크기와 텍스트 위치를 재배치. 비용 3줄은 늘어난 높이의 절반만큼 아래로 밀어 원래 간격 유지 |
| `SetY(rect, y)` / `SetX(rect, x)` (private, static) | `anchoredPosition`의 Y/X만 개별 갱신하는 헬퍼 |

## 연관 컴포넌트

- **TooltipUI**: `Configure()`로 참조를 넘기고, 텍스트/비용을 세팅한 뒤 `Fit()`을 호출해 크기를 맞춤
