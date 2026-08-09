# EnemyUnitDataSO

`Assets/Scripts/ScriptableObject/EnemyUnitDataSO.cs`

## 개요

OC(오메가 코퍼레이션) 등 적 진영 유닛 데이터베이스. `UnitDataSO`와 동일한 `UnitData` 구조를 그대로 재사용해서 스탯 필드를 중복 정의하지 않고, 진영별로 SO 에셋만 따로 둔다(doc/0230). 필드 구성/의미는 `Docs/UnitDataSO.md`의 `UnitData` 항목을 그대로 따른다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `unitData` | `List<UnitData>` — 적 진영 유닛 스펙 목록 |

## 연관 컴포넌트

- **UnitDataSO**: 아군(NTA) 진영용 동일 구조 데이터베이스 — `UnitData` 정의를 공유
- **EnemyUnitController** 등: ID로 이 데이터베이스를 조회해 적 유닛 스펙을 사용
