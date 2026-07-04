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

doc/                     # 시스템별 설계 문서
Docs/                    # 기능별 설계 문서(한글)
```

### 핵심 스크립트

| 스크립트 | 역할 |
|---|---|
| `RTSUnitController` | 유닛 선택/이동/명령을 총괄하는 RTS 유닛 시스템 |
| `UserControl` | 마우스/키보드 입력을 받아 유닛 선택 및 명령 전달 |
| `PlacementSystem`, `GridData`, `PreviewSystem` | 그리드 기반 건물 배치 및 배치 미리보기 |
| `BuildingController` | 건물 생산 대기열, 렐리 포인트 처리 |
| `ResourceManager`, `ResourceNode` | 자원 채취 및 자원량 관리 |
| `HealthManager`, `AttackRange` | 유닛 체력 및 공격 사거리/타겟팅 |
| `UnitDataSO`, `BuildingDataSO` | 유닛/건물 스탯을 데이터로 관리하는 ScriptableObject |
| `CameraControl` | RTS 시점 카메라 이동 |
| `UIController`, `ProductionSlot` | 선택/생산 관련 인게임 UI |

## 주요 기능

- **유닛 시스템**: 단일/다수 선택, 이동, 공격, 정지, 홀드, 순찰, NavMesh를 사용하지 않는 공중 유닛 이동
- **건물 시스템**: 그리드 기반 건설, 생산 대기열, 렐리 포인트 지정
- **자원 시스템**: 자원 노드 관리 (일부 기능 진행 중)
- **UI**: 패널 기반 UI, 선택 상태에 따른 UI 전환, 유닛/건물 선택 UI

> 세부 기능별 구현 현황(체크리스트)은 [`doc/`](doc), [`Docs/`](Docs) 폴더의 설계 문서를 참고하세요.

## 설계 문서

| 문서 | 내용 |
|---|---|
| [full-codebase-analysis-report.md](doc/full-codebase-analysis-report.md) | 전체 코드베이스 분석 리포트 |
| [health-manager-design.md](doc/health-manager-design.md) | 체력 관리 시스템 설계 |
| [attack-range-target-selection-design.md](doc/attack-range-target-selection-design.md) | 공격 사거리/타겟 선택 설계 |
| [air-unit-overlap-separation-design.md](doc/air-unit-overlap-separation-design.md) | 공중 유닛 겹침 분리 설계 |
| [resource-manager-design.md](doc/resource-manager-design.md) | 자원 관리 시스템 설계 |
| [worker-resource-gathering-design.md](doc/worker-resource-gathering-design.md) | 일꾼 자원 채취 설계 |
| [worker-return-cargo-design.md](doc/worker-return-cargo-design.md) | 일꾼 자원 반환 설계 |
| [production-queue-ui-design.md](doc/production-queue-ui-design.md) | 생산 대기열 UI 설계 |
| [ui-resource-display-design.md](doc/ui-resource-display-design.md) | 자원 표시 UI 설계 |
| [ProductionQueue_UI_설계.md](Docs/ProductionQueue_UI_설계.md) | 생산 대기열 UI 설계(국문) |

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
