# EnemyBuildingDataSO

`Assets/Scripts/ScriptableObject/EnemyBuildingDataSO.cs`

## 개요

OC(오메가 코퍼레이션) 등 적 진영 건물 데이터베이스. `BuildingDataSO`와 동일한 `BuildingData` 구조를 그대로 재사용해서 스탯 필드를 중복 정의하지 않고, 진영별로 SO 에셋만 따로 둔다(doc/0230). 필드 구성/의미는 `Docs/BuildingDataSO.md`의 `BuildingData` 항목을 그대로 따른다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `buildingData` | `List<BuildingData>` — 적 진영 건물 스펙 목록 |

## 연관 컴포넌트

- **BuildingDataSO**: 아군(NTA) 진영용 동일 구조 데이터베이스 — `BuildingData` 정의를 공유
- **EnemyBuildingController** 등: ID로 이 데이터베이스를 조회해 적 건물 스펙을 사용
