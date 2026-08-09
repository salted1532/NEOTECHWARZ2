# MissionItem

`Assets/Scripts/System/MissionItem.cs`

## 개요

유물/데이터베이스 등 미션용 오브젝트에 붙는 컴포넌트. "줍기/반납" 로직 자체는 개별 스테이지 스크립트(`Stage2Objectives` 등)가 직접 처리하고, 이 컴포넌트는 두 가지만 담당한다 — "좌클릭으로 선택했을 때 Info Panel에 무엇을 보여줄지"(체력/전투 스탯 없이 `ResourceNode`의 선택 관련 부분만 떼어낸 축소판, doc/0455)와 "지금 어떤 트리거 콜라이더에 실제로 닿아 있는지"(비콘 반납 판정용, doc/0456).

## 주요 필드

| 필드 | 설명 |
|---|---|
| `itemID` | 로컬라이제이션 키 구분용 ID(예: `"artifact"`, `"researchdata"`) — 프리팹별로 지정 |
| `itemName` | 아이템 이름 (Info Panel 표시용, 번역 없을 때 폴백) |
| `description` | 아이템 설명(TextArea, Info Panel 표시용, 번역 없을 때 폴백) |
| `icon` | 아이템 아이콘 |
| `selectionMarker` | 선택 시 표시할 마커 (없으면 표시 없이 선택만 됨) |
| `overlappingTriggers` | 현재 겹쳐 있는 트리거 콜라이더 집합(private) — `IsTouching()`으로 특정 콜라이더에 닿아있는지 조회 가능. 이 오브젝트가 스크립트로 매 프레임 위치를 직접 옮기므로 물리 트리거가 안정적으로 발동하려면 Kinematic Rigidbody가 필요함(doc/0456) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `OnTriggerEnter/Exit(other)` | 겹친 콜라이더를 `overlappingTriggers`에 등록/해제 |
| `IsTouching(other)` | 지정한 콜라이더에 실제로 닿아 있는지 조회 (비콘 반납 판정용) |
| `Start()` | `selectionMarker`를 초기 비활성화 — `ResourceNode.resourceMarker`/`EnemyUnitController.enemyMarker`와 동일한 패턴(doc/0457) |
| `SelectItem()` / `DeselectItem()` | 선택 마커 켜기/끄기 |
| `GetIcon()` | Info Panel 표시용 아이콘 조회 |
| `GetItemName()` / `GetDescription()` | `LocalizationManager.GetTextOrFallback($"missionitem.{itemID}.name/desc", ...)`로 번역 조회, 키 없음/매니저 없음/예외 시 인스펙터 원문(`itemName`/`description`)을 그대로 반환(doc/0490) |

## 연관 컴포넌트

- **Stage2Objectives**: 유물/연구 데이터 "줍기 → 따라가기 → 반납" 로직을 이 컴포넌트의 `transform.position` 갱신과 `IsTouching()`으로 직접 처리
- **ResourceNode**: 선택 관련 로직(마커 표시)의 원본 패턴 제공
