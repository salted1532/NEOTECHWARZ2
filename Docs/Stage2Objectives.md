# Stage2Objectives

`Assets/Scripts/System/Stage2Objectives.cs`

## 개요

2스테이지("미지의 신호") 임무 목표 체크리스트. 외계 유물(주목표)과 OC 연구 데이터(서브목표) 각각의 "줍기 → 따라가기 → 반납" 흐름을 이 스크립트가 매 프레임 거리 판정으로 직접 처리한다(스테이지당 스크립트 1개로 완결시키기 위함, 요청사항). "반납 완료" 판정만 비콘의 실제 트리거 콜라이더 접촉 여부로 확인한다(doc/0456 — `MissionItem`이 `OnTriggerEnter/Exit`로 겹친 콜라이더를 추적해두고, 여기서는 그 결과만 물어봄). 로직: 아무도 안 든 아이템은 `pickupRadius` 안의 가장 가까운 일꾼이 자동으로 들고, 든 동안은 아이템이 그 일꾼 위치(+오프셋)를 따라가며, 비콘 트리거에 닿으면 반납 완료 처리 후 비활성화된다. 든 일꾼이 죽으면(참조 null) 자동으로 다시 주울 수 있는 상태로 돌아간다. 유물 반납이 주목표(승리 조건), 연구 데이터 반납은 서브목표다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `artifact` / `artifactBeacon` / `collectArtifactText` | 주목표 — 유물 오브젝트, 반납 지점 트리거 콜라이더, 표시 텍스트 |
| `researchData` / `researchDataBeacon` / `collectResearchDataText` | 서브목표 — 연구 데이터 오브젝트, 반납 지점, 표시 텍스트 |
| `pickupRadius` | 일꾼이 이 범위 안에 들어오면 자동으로 아이템을 듦 |
| `carryOffset` | 들린 동안 일꾼 기준 위치 오프셋 |
| `artifactCarrier` / `dataCarrier` | 현재 들고 있는 일꾼 참조 |
| `artifactDelivered` / `dataDelivered` | 반납 완료 여부 |
| `artifactSuccessSfxPlayed` / `dataSuccessSfxPlayed` | 성공 SFX 최초 1회 재생 보장용 플래그 |

## 메소드

| 메소드 | 설명 |
|---|---|
| `Start()` | 텍스트 UI 자동 연결, `RTSUnitController` 캐싱 |
| `Update()` | 두 아이템(유물/연구 데이터)에 대해 `UpdateCarry()` 호출, 텍스트 갱신, 유물 반납 완료 시 승리 보고, 목표별 성공 SFX를 최초 1회만 재생 |
| `PlayMissionSuccessSfxOnce(delivered, ref alreadyPlayed)` (private) | `delivered` 플래그는 한 번 켜지면 계속 true라 `Update()`가 매 프레임 들어오므로, SFX는 목표별 최초 1회만 울리도록 별도 플래그로 방지(doc/0464, doc/0465) |
| `UpdateCarry(item, beacon, ref carrier, ref delivered)` (private) | 아이템 하나의 줍기/따라가기/반납 로직 전체 — 이미 반납됐으면 스킵, 든 사람이 없으면 `FindNearestWorkerInRange()`로 탐색, 있으면 위치 추적, 비콘에 닿으면 반납 처리 |
| `FindNearestWorkerInRange(position, radius)` (private) | "Worker" 태그를 가진 유닛 중 반경 내 가장 가까운 것을 탐색 |

## 연관 컴포넌트

- **MissionItem**: 유물/연구 데이터 오브젝트의 트리거 접촉 판정(`IsTouching`) 제공
- **StageManager**: `WireObjectiveTexts()`/`ReportVictory()` 호출 대상
- **SoundManager**: 목표 완료 시 `PlayMissionSuccessVoice()` 호출
