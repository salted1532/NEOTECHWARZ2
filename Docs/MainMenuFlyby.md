# MainMenuFlyby

`Assets/Scripts/UI/MainMenuFlyby.cs`

## 개요

메인화면 배경 연출용 컴포넌트. 대각선으로 날아간 뒤 시작점으로 텔레포트, 랜덤 대기 후 반복한다 — 배경에 떠다니는 우주선 등의 장식 오브젝트에 붙인다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `startX` / `startZ` / `endX` / `endZ` | 시작점과 끝점의 X/Z 좌표 (Y는 오브젝트의 원래 위치 유지) |
| `speed` | 이동 속도 |
| `minWaitSeconds` / `maxWaitSeconds` | 텔레포트 후 다시 출발하기 전 대기 시간 범위 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `OnEnable()` | 시작점/끝점 좌표 계산, 위치를 시작점으로 초기화, `FlyLoop()` 코루틴 시작 |
| `FlyLoop()` (private) | 끝점까지 `MoveTowards`로 이동 → 시작점으로 텔레포트 → 랜덤 시간 대기 → 반복 |

## 연관 컴포넌트

- 없음 (독립적인 장식용 컴포넌트, 다른 스크립트와 상호작용하지 않음)
