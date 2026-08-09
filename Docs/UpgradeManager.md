# UpgradeManager

`Assets/Scripts/Upgrade/UpgradeManager.cs`

## 개요

연구로 얻은 전역 공격력/방어력 보너스를 저장하는 컴포넌트. `RTSUnitController`에서만 참조한다 — `ResearchQueue`(연구소)나 `UnitController`(유닛)가 직접 이 컴포넌트를 찾거나 호출하지 않는다. 항상 `RTSUnitController.AddGlobalBonus`/`GlobalAttackBonus`/`GlobalArmorBonus`를 거쳐서만 값이 오가도록 해서, 연구소 큐 시스템과 유닛 시스템이 서로 독립적으로 유지된다. `ResearchType`(Attack/Armor) enum도 이 파일에 정의돼 있으며 `ResearchQueue`와 공유한다.

## 주요 필드

| 필드 | 설명 |
|---|---|
| `attackBonus` / `armorBonus` | 누적된 전역 공격력/방어력 보너스 (private) |

## 메소드

| 메소드 | 설명 |
|---|---|
| `GetBonus(type)` | 지정한 타입의 현재 보너스 값 조회 |
| `AddBonus(type, amount)` | 지정한 타입의 보너스를 누적 |

## 연관 컴포넌트

- **RTSUnitController**: 이 컴포넌트에 접근하는 유일한 통로 — `AddGlobalBonus` 등을 통해서만 값이 오간다
- **ResearchQueue**: 연구 완료 시 `RTSUnitController.AddGlobalBonus()`를 호출(이 컴포넌트를 직접 만지지 않음), `ResearchType` enum을 공유
