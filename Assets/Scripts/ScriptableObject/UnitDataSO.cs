using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 게임 내 모든 유닛 종류의 데이터를 담는 ScriptableObject 데이터베이스 (에디터에서 에셋으로 생성해 관리).
[CreateAssetMenu]
public class UnitDataSO : ScriptableObject
{
    [SerializeField]
    public List<UnitData> unitData = new List<UnitData>();
}

// 액티브 스킬의 지정 방식 (doc/0323). None=자기 자신에게 즉시 발동(은신/쉴드 전개 등),
// SingleUnit=A공격모드처럼 적 유닛 하나를 지정(저격/집중 포화), AreaGround=땅의 한 지점을 지정해 범위 피해(지상 폭격).
public enum SkillTargetType { None, SingleUnit, AreaGround }

// 고급유닛 특성(트레이트) 하나의 정의 - 특성 선택 오버레이(SkillSelect) 카드 + (액티브인 경우) order panel
// 버튼/단축키에 필요한 정보를 모두 담는다 (doc/0228).
[System.Serializable]
public class UnitTraitOption
{
    [field: SerializeField]
    public string skillName { get; private set; }

    [field: SerializeField, TextArea(2, 4)]
    public string description { get; private set; }

    [field: SerializeField]
    public Sprite icon { get; private set; }

    // true면 order panel 슬롯 6에 버튼+단축키가 추가되는 액티브 스킬(쿨다운 있음).
    // false면 버튼 없이 UnitController.ApplyTrait()에서 스탯/행동에 조용히 반영되는 패시브 특성.
    [field: SerializeField]
    public bool isActiveSkill { get; private set; }

    [field: SerializeField]
    public KeyCode shortcutKey { get; private set; } // isActiveSkill=true일 때만 사용

    [field: SerializeField]
    public float cooldown { get; private set; } // isActiveSkill=true일 때만 사용

    // 지정형 액티브 스킬 전용 (doc/0323, targetType != None일 때만 의미 있음).
    [field: SerializeField]
    public SkillTargetType targetType { get; private set; }

    // 유닛 기본 공격 사거리(AttackRange.UnitRange)와는 별개인 이 스킬 전용 사거리 - 이 안에 들어와야 실제 발동한다.
    [field: SerializeField]
    public float skillRange { get; private set; }

    // AreaGround 전용 - 피해가 적용되는 범위 반지름.
    [field: SerializeField]
    public float areaRadius { get; private set; }
}

// 유닛 하나의 스펙(체력/공격력/비용/생산시간/프리팹 등)을 정의하는 데이터 항목.
[System.Serializable]
public class UnitData
{
    [field: SerializeField]
    public string unitName { get; private set; }

    // 생산 버튼 호버 시 툴팁에 표시할 설명(비용/단축키 등과 함께 노출). 비워두면 기본 문구가 대신 표시된다.
    [field: SerializeField, TextArea(2, 5)]
    public string description { get; private set; }

    // Info Panel(유닛 선택 시)에 표시할 설명 - 위 description(생산 버튼 툴팁)과는 별개로, 유닛 자체에 대한
    // 소개 문구다 (doc/0476).
    [field: SerializeField, TextArea(2, 5)]
    public string infoDescription { get; private set; }

    // 코드에서 유닛을 식별하는 데 쓰이는 고유 ID (RTSUnitController.UnitID 상수와 매칭)
    [field: SerializeField]
    public int ID { get; private set; }

    // 이 유닛을 생산할 수 있는 건물 종류: 0=본진(MainBase), 1=병영(Tier1), 2=공장(Tier2), 3=우주공항(Tier3).
    // 이 값만 지정하면 코드 수정 없이 해당 건물의 생산 패널에 자동으로 나타난다.
    [field: SerializeField]
    public int tier { get; private set; }

    // 장갑 타입(경장갑/중장갑)과 크기 타입(소형/중형/대형). 유닛이 스폰될 때 UnitController.ApplyUnitData()가
    // 이 값을 그대로 가져다 쓰므로(doc/0205), 실제 전투 계산에도 여기 값이 반영된다.
    [field: SerializeField]
    public ArmorType armorType { get; private set; }
    [field: SerializeField]
    public SizeType sizeType { get; private set; }

    // 방어력 - 받는 피해에서 고정으로 깎이는 값(공격 1회당 최소 1피해는 항상 들어감, HealthManager.GetDamage 참고).
    // 기본 0(기존 유닛 전부 무변화, doc/0556).
    [field: SerializeField]
    public int armor { get; private set; }

    [field: SerializeField]
    public int hp { get; private set; }

    [field: SerializeField]
    public int attackDamge { get; private set; }

    [field: SerializeField]
    public int attackRange { get; private set; }

    // ===== 치유 (doc/0661) - "Healer" 태그가 붙은 유닛만 사용. 나머지 유닛은 0으로 비워둠(무효과) =====
    [field: SerializeField]
    public int healRange { get; private set; }
    [field: SerializeField]
    public float healPerSecond { get; private set; }

    // 공격 1회 후 다음 공격까지 걸리는 시간(초). 값이 작을수록 더 빨리(자주) 공격한다.
    // (UnitController.timeBetweenAttacks와 동일한 의미 - "공격속도"가 아니라 "공격 간격"이지만
    // 기획 쪽 명칭인 attackSpeed를 그대로 필드명으로 씀)
    [field: SerializeField]
    public float attackSpeed { get; private set; }

    // 이 유닛이 지상/공중 유닛을 공격할 수 있는지. 기본값은 둘 다 true(기존 동작과 동일 - 제한 없음).
    // 대공 사격이 불가능한 유닛은 canAttackAir를, 대지 공격이 불가능한 유닛은 canAttackGround를 false로.
    [field: SerializeField]
    public bool canAttackGround { get; private set; } = true;
    [field: SerializeField]
    public bool canAttackAir { get; private set; } = true;

    // 공격 전달 방식 (doc/0290). Projectile 선택 시 해당 유닛 프리팹에 ProjectileAttack 컴포넌트를 붙이고
    // 투사체 프리팹/firePoint를 연결해야 실제로 데미지가 들어간다 - 안 붙어있으면 Hitscan으로 자동 폴백.
    [field: SerializeField]
    public AttackDeliveryType attackDelivery { get; private set; } = AttackDeliveryType.Hitscan;

    [field: SerializeField]
    public int mineral { get; private set; }
    [field: SerializeField]
    public int gas { get; private set; }
    // 이 유닛을 생산하는 데 필요한 인구수(보급) 비용
    [field: SerializeField]
    public int population { get; private set; }
    [field: SerializeField]
    public int productionTime { get; private set; }
    // 이 유닛을 생산하기 전에 미리 완공되어 있어야 하는 건물의 ID (RTSUnitController.BuildingID 상수, 0이면 조건 없음)
    [field: SerializeField]
    public int requiredBuildingID { get; private set; }
    // 선택 시 Squad Panel/Info Panel에 표시할 아이콘.
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    // 생산 버튼(ShowUnitTierPanel)/생산 대기열에 표시할 아이콘 - 위 Icon(선택 아이콘)과는 별개로 관리 (doc/0514).
    [field: SerializeField]
    public Sprite ProductionIcon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }

    // 아군 OC 전용 프리팹(AllyController가 붙은 Prefab Variant) - AllyAIDirector가 unitID로 자동
    // 스폰할 때 이 필드를 쓴다(doc/0543). Prefab(적대 EnemyUnitController 변형)과는 별개 - 아군 프리팹이
    // 아직 없는 유닛 종류는 비워둬도 되고, 그 경우 AllyAIDirector는 해당 unitID를 조용히 건너뛴다.
    [field: SerializeField]
    public GameObject AllyPrefab { get; private set; }

    // 이 유닛 종류의 공격/생성/사망 SFX + 선택/이동/공격명령/생성/사망 음성을 모아둔 에셋 (비워두면 조용함).
    // 유닛이 늘어나도 코드 수정 없이 새 UnitSoundBankSO 에셋만 만들어 연결하면 된다 (doc/0255).
    [field: SerializeField]
    public UnitSoundBankSO soundBank { get; private set; }

    // 이 유닛의 생산 버튼을 대신 누르는 키보드 단축키 (없으면 KeyCode.None)
    [field: SerializeField]
    public KeyCode shortcutKey { get; private set; }

    [Header("고급유닛 특성 (2택1, 비워두면 일반 유닛 - hasTraitChoice로 판정)")]
    [field: SerializeField]
    public bool hasTraitChoice { get; private set; }
    [field: SerializeField]
    public UnitTraitOption traitA { get; private set; } // 장점 극대화
    [field: SerializeField]
    public UnitTraitOption traitB { get; private set; } // 단점 보완
}
