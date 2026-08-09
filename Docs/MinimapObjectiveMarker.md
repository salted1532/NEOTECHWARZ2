# MinimapObjectiveMarker

`Assets/Scripts/Camera/MinimapObjectiveMarker.cs`

## 개요

미션 목표와 관련된 오브젝트에 붙이면 미니맵에 아이콘으로 표시되는 마커(doc/0349). 점령해야 할 거점(`TerritoryZone`), 방어해야 할 건물 등에 그냥 붙이기만 하면 되고, 스테이지별 목표 스크립트(`Stage0Objectives` 등)를 건드릴 필요가 없다. 오브젝트가 비활성화/파괴되면(예: 거점을 점령해서 더 이상 목표가 아니게 됨) 아이콘도 자동으로 사라진다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `iconColor` | 미니맵에 표시할 아이콘 색상 |
| `iconSize` | 아이콘 크기 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `OnEnable()` | `MinimapObjectiveOverlay.Instance.Register(this)` 호출 |
| `OnDisable()` | `MinimapObjectiveOverlay.Instance.Unregister(this)` 호출 |

## 연관 컴포넌트

- **MinimapObjectiveOverlay**: 등록/해제되면 미니맵 위에 실제 아이콘을 생성/제거
