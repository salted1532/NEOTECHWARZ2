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
}

// 유닛 하나의 스펙(체력/공격력/비용/생산시간/프리팹 등)을 정의하는 데이터 항목.
[System.Serializable]
public class UnitData
{
    [field: SerializeField]
    public string unitName { get; private set; }

    // 툴팁에 표시할 설명(역할 등). 비워두면 기본 문구가 대신 표시된다.
    [field: SerializeField, TextArea(2, 5)]
    public string description { get; private set; }

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

    [field: SerializeField]
    public int hp { get; private set; }

    [field: SerializeField]
    public int attackDamge { get; private set; }

    [field: SerializeField]
    public int attackRange { get; private set; }

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
    [field: SerializeField]
    public Sprite Icon { get; private set; }
    [field: SerializeField]
    public GameObject Prefab { get; private set; }

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
