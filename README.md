# NEOTECHWARZ2

Unity로 제작 중인 스타크래프트 스타일의 RTS(실시간 전략) 게임입니다. 이전 프로젝트 **네오테크워즈**의 문제점과 미구현 기능을 보완하고, UI/그래픽을 개선한 후속작으로 개발하고 있습니다.

## 기술 스택

| 구분 | 내용 |
|------|------|
| 엔진 | Unity 6000.4.8f1 |
| 렌더 파이프라인 | Universal Render Pipeline (URP) 17.4.0 |
| 입력 | Unity Input System 1.19.0 |
| 길찾기 | AI Navigation (NavMesh) 2.0.12 |
| UI | UGUI, TextMesh Pro |

## 프로젝트 구조

```
Assets/
├─ Scripts/
│  ├─ Building/        # 건물 컨트롤러
│  ├─ BuildSystem/      # 건물 배치 시스템 (그리드, 미리보기, 입력)
│  ├─ Camera/           # RTS 카메라 이동/조작
│  ├─ Enemy/            # 적 AI
│  ├─ FogOfWar/         # 전장의 안개
│  ├─ Resource/         # 자원 노드 및 자원 관리
│  ├─ ScriptableObject/ # 유닛/건물 데이터 정의(SO)
│  ├─ System/           # RTS 유닛 통합 컨트롤 시스템
│  ├─ UI/               # 생산 슬롯, 인게임 UI 컨트롤러
│  ├─ Unit/             # 유닛 컨트롤러, 공격 범위, 체력 관리
│  ├─ UnitSpawner/      # 유닛 생산/스폰
│  └─ UserControl/      # 유닛 선택 및 명령 입력 처리
├─ Scenes/              # 게임 씬 (SampleScene 등)
├─ prefabs/             # 유닛/건물 프리팹
├─ Material, Shader/    # 머티리얼 및 커스텀 셰이더
└─ Settings/            # URP 렌더 파이프라인 설정

doc/                     # 스크립트별 코드 문서 (역할/필드/메소드 정리)
Docs/                    # 추가 설계 노트(한글)
```

### 핵심 스크립트

각 스크립트의 상세 문서(필드, 메소드별 동작 방식)는 [`doc/`](doc) 폴더에 스크립트 이름과 동일한 파일명으로 정리되어 있습니다.

| 스크립트 | 역할 | 문서 |
|---|---|---|
| `RTSUnitController` | 유닛/건물 선택 상태, 전체 목록, UI 갱신, 생산·건설 자원 검증을 총괄하는 중앙 허브 | [doc](doc/RTSUnitController.md) |
| `UserControl` | 마우스/키보드 입력을 해석해 선택·명령을 `RTSUnitController`에 전달 | [doc](doc/UserControl.md) |
| `UnitController` | 유닛의 이동/전투/순찰/자원 채취 상태머신 (지상+공중 유닛 공통) | [doc](doc/UnitController.md) |
| `AttackRange` | 사거리 내 적 감지 및 자동 공격/추격 | [doc](doc/AttackRange.md) |
| `BuildingController` | 건물 선택, 랠리 포인트, 생산 위임, 사망 처리 | [doc](doc/BuildingController.md) |
| `UnitSpawner` | 건물의 유닛 생산 대기열(FIFO) 관리 및 스폰 | [doc](doc/UnitSpawner.md) |
| `PlacementSystem` | 그리드 기반 건물 배치, 배치 가능 여부 판정 | [doc](doc/PlacementSystem.md) |
| `GridData` | 그리드 셀 점유 정보 관리 (순수 데이터 클래스) | [doc](doc/GridData.md) |
| `PreviewSystem` | 배치 프리뷰(고스트 오브젝트) 및 셀 커서 표시 | [doc](doc/PreviewSystem.md) |
| `InputManager` | 건물 배치 전용 입력 처리 (클릭/ESC/마우스 좌표) | [doc](doc/InputManager.md) |
| `ResourceManager` | 팀의 광물/가스/인구수 중앙 관리 | [doc](doc/ResourceManager.md) |
| `ResourceNode` | 자원 채취 지점, 대기열(줄서기) 및 고갈 처리 | [doc](doc/ResourceNode.md) |
| `HealthManager` | 체력/데미지/치유/사망 처리 공용 컴포넌트 | [doc](doc/HealthManager.md) |
| `UnitDataSO` | 유닛 스탯(체력/공격력/비용 등) 데이터베이스 | [doc](doc/UnitDataSO.md) |
| `BuildingDataSO` | 건물 스펙(비용/크기/생산시간 등) 데이터베이스 | [doc](doc/BuildingDataSO.md) |
| `CameraControl` | RTS 시점 카메라 이동/줌 | [doc](doc/CameraControl.md) |
| `UIController` | 커맨드 패널, 생산 대기열, 자원 표시 UI 총괄 | [doc](doc/UIController.md) |
| `ProductionSlot` | 커맨드/생산 대기열의 버튼 슬롯 하나 | [doc](doc/ProductionSlot.md) |

## 주요 기능

- **유닛 시스템**: 단일/다수 선택, 이동, 공격, 정지, 홀드, 순찰, NavMesh를 사용하지 않는 공중 유닛 이동
- **건물 시스템**: 그리드 기반 건설, 생산 대기열, 렐리 포인트 지정
- **자원 시스템**: 자원 노드 관리 (일부 기능 진행 중)
- **UI**: 패널 기반 UI, 선택 상태에 따른 UI 전환, 유닛/건물 선택 UI

> 스크립트별 상세 동작 방식은 위 표의 [`doc/`](doc) 링크를 참고하세요. 생산 대기열 UI에 대한 추가 설계 노트는 [Docs/ProductionQueue_UI_설계.md](Docs/ProductionQueue_UI_설계.md)에 별도로 남아 있습니다.

## 시작하기

1. [Unity Hub](https://unity.com/download)에서 **Unity 6000.4.8f1** 버전을 설치합니다.
2. 저장소를 클론한 뒤 Unity Hub에서 프로젝트 폴더를 엽니다.
3. `Assets/Scenes/SampleScene`을 실행하여 플레이합니다.

## 로드맵

- 미니맵 구현
- 전장의 안개(Fog of War) 구현
- 유닛 채광/건설 명령 추가
- 생산 대기열 및 렐리 포인트 버그 수정
- UI 디자인 개선
