# 0081 — Lab 체력바가 실제 체력과 무관하게 보이던 문제

## 질문
"lap에선 왜 체력바가 표시되고 슬라이더라서 그런가 막 조절되네 근데 체력은 그대로인데 체력바 표시되거든 확인좀 해줘"

## 원인 (2가지가 겹친 문제)

### 1. `Lab.prefab`만 `HealthManager.healthSlider`가 연결 안 돼 있었음
건물 프리팹들의 `healthSlider` 참조를 전부 비교해봄:

```
BaseStructure.prefab:  healthSlider: {fileID: 1798028007937400207}
MainBase.prefab:       healthSlider: {fileID: 6281857778393333767}
Tier1.prefab:          healthSlider: {fileID: 2456449002600290041}
Tier2.prefab:          healthSlider: {fileID: 6399763462648880560}
Tier3.prefab:          healthSlider: {fileID: 2995629044118874101}
SupplyDepot.prefab:    healthSlider: {fileID: 7749477720260782760}
Lab.prefab:             healthSlider: {fileID: 0}   ← 비어있음(연결 안 됨)
```

`HealthManager.UpdateHealthSlider()`는 `healthSlider == null`이면 아무 것도 안 하고 그냥 리턴한다
(`Assets/Scripts/Unit/HealthManager.cs:39`). 즉 Lab은 HealthBar 오브젝트 자체는 씬에 있지만(자식으로 붙어있음),
실제 체력 변화(`GetDamage`/`Heal`/`SetHealth`)가 이 슬라이더에 전혀 반영되지 않고 있었음 — **다른 건물은 데미지를
입을 때마다 슬라이더 값이 자동으로 덮어써지지만, Lab은 그 "자동 보정"이 아예 일어나지 않음.**

### 2. HealthBar UI의 Slider가 플레이어가 직접 드래그할 수 있게 설정돼 있었음
`Assets/prefabs/UI/HealthBar.prefab`의 `Slider` 컴포넌트가 `m_Interactable: 1`(상호작용 가능)이었음. 체력바는
표시 전용이어야 하는데 이 값 때문에 마우스로 클릭+드래그하면 실제로 슬라이더 값이 바뀌어버림 —
"슬라이더라서 그런가 막 조절되네"가 정확히 이 현상.

**두 문제가 합쳐진 결과**: 다른 건물은 슬라이더를 실수로 건드려도 다음 데미지/힐 이벤트 때 `UpdateHealthSlider()`가
실제 체력값으로 다시 덮어써서 금방 정상으로 돌아오는데, **Lab은 애초에 연결이 안 돼 있어서 그 "자동 보정"이 절대
일어나지 않음** — 그래서 한 번 잘못 건드리면(또는 프리팹 기본값이 이상하면) 실제 체력(`currentHp`)은 그대로인 채로
체력바만 계속 어긋난 채 남아있던 것.

## 수정

- **`Assets/prefabs/NTA/Building/Lab.prefab`**: `HealthManager.healthSlider`를 Lab 자신의 HealthBar 인스턴스의
  `Slider` 컴포넌트에 연결(다른 건물들과 동일하게). 프리팹 내부에 해당 컴포넌트를 가리키는 stripped 참조가 아직
  없어서 새로 추가한 뒤 연결함.
- **`Assets/prefabs/UI/HealthBar.prefab`**: `Slider.m_Interactable`을 `1` → `0`으로 변경. 모든 유닛/건물이 공유하는
  프리팹이라 이 수정으로 Lab뿐 아니라 전체 체력바가 더 이상 마우스로 드래그되지 않음(표시 전용으로 고정).

## 확인 필요 사항
Unity 에디터에서 Lab을 지어서 데미지를 입혀보고(또는 회복시켜보고) 체력바가 실제 체력에 맞게 정상적으로 움직이는지,
그리고 이제 어떤 건물/유닛의 체력바를 마우스로 클릭+드래그해도 값이 안 바뀌는지 확인 부탁.
