# 0223 - 모델에서 애니메이션이 재생 안 되는 원인 진단

날짜: 2026-07-24

## 요청 내용

Sharpshooter.controller(state 전이)는 정상 작동하는데 실제 모델(`unit_Infantry_Light_A_yup`)에서는
애니메이션이 눈에 보이게 재생되지 않는다는 문제. 애니메이션 클립이 잘못된 건지, 리깅
(Bone Renderer/Rig Builder)이 문제인지 확인 요청.

## 조사 내용 (진단만, 코드/에셋 변경 없음)

### 1) 본(Bone) 이름 불일치 확인 — 이게 원인

`findstr`로 각 fbx 안에 들어있는 문자열(본 이름)을 직접 비교:

- 모델(`unit_Infantry_Light_A_yup.fbx`)의 본 이름: `unit_Infantry_Light_A_Torso_yup`,
  `unit_Infantry_Light_A_Chest_yup`, `unit_Infantry_Light_A_Head_yup`,
  `unit_Infantry_Light_A_Arm_L_Upper_yup` 등 — 이 에셋팩 전용 커스텀 이름.
- 애니메이션 클립(`Assets\Animation\walking.fbx`)을 같은 방식으로 스캔한 결과, 안에
  `mixamorig:Hips`, `mixamorig:Spine` 같은 문자열이 들어있음(`Hips`, `Spine`, `mixamorig`,
  `Head`, `Root`, `Model` 토큰 전부 FOUND). 즉 `walking.fbx`/`firing rifle.fbx`는
  **Mixamo 스켈레톤 이름 체계**로 만들어진 애니메이션이고, 모델의 본 이름과 하나도 겹치지 않음.

두 fbx 모두 Import 설정이 `animationType: 2`(Generic, Humanoid 아님) — Generic 타입은
"뼈대 이름/계층 경로가 정확히 일치하는 오브젝트"에만 애니메이션 커브를 적용한다. 이름이 하나도
안 겹치니, Animator는 State 전이를 정상적으로 수행하지만(그래서 컨트롤러 자체는 "잘 작동하는
것처럼" 보임) 실제로는 모델의 어떤 본에도 커브가 매칭되지 않아 겉보기엔 아무 움직임이 없음.
에러도 안 뜸 — Unity가 "매칭되는 게 없으면 조용히 무시"하는 방식이라 증상만으로는 원인이
안 보이는 케이스.

### 2) Rig Builder / Bone Renderer는 원인이 아님 (배제)

`unit_Infantry_Light_A_yup.prefab` 안의 `Rig` GameObject(RigBuilder가 참조하는 레이어)를
확인한 결과:
```yaml
m_Script: ...Rigging.Rig
m_Weight: 1
m_Effectors: []
```
자식도 없고(`m_Children: []`) 어떤 Constraint(Multi-Aim, Two-Bone IK 등)도 추가되어 있지 않은
빈 껍데기 상태. 즉 지금은 애니메이션에 아무 영향도 주지 않음 — 나중에 조준 IK를 넣을 때만
관련 있어짐. 리깅 컴포넌트 자체는 문제 원인이 아니라고 결론.

## 해결 방법 (선택지 2가지 — 아직 미적용, 사용자 확인 필요)

**A) (권장) 모델 + 클립을 Humanoid로 전환하고 Avatar를 수동 매핑**
- 모델 본 목록(Torso, Chest, Head, Arm_L/R_Upper/Lower, Hand_L/R, Leg_L/R_Upper/Lower, Foot_L/R)이
  마침 Humanoid 필수 15본 자리에 1:1로 대응 가능해 보임
  (Torso→Hips, Chest→Spine/Chest, Head→Head, 나머지 팔다리는 이름 그대로 대응).
- `walking.fbx`/`firing rifle.fbx`는 Mixamo 표준 스켈레톤이라 Humanoid로 바꾸면 Unity가
  자동 매핑을 거의 완벽하게 인식함.
- Humanoid는 이름이 아니라 "근육(muscle) 공간" 기준으로 리타겟팅하므로, 모델 쪽 Avatar만
  Configure 창에서 수동으로 15개 본을 채워주면 이름이 달라도 애니메이션이 정상 재생됨.
- 절차: 모델 fbx Import 설정 → Rig 탭 → Animation Type: Humanoid → Apply → Configure 버튼 →
  Mapping 탭에서 각 슬롯에 해당 본을 드래그. 클립 2개도 Animation Type: Humanoid로 변경
  (Mixamo 본이라 자동 매핑이 거의 그대로 맞을 가능성 높음).

**B) 이 모델 전용으로 만들어진(같은 본 이름을 쓰는) 애니메이션 클립으로 교체**
- Generic을 유지하고 싶다면, 에셋팩 안에 `unit_Infantry_Light_A_yup` 전용 walk/fire 클립이
  따로 있는지 확인 후 그걸 쓰는 방법. (현재 프로젝트에서는 이런 클립을 찾지 못함 — 있는지
  직접 확인 필요.)

## 요약/남은 작업

- 원인은 확정됨(본 이름 불일치, Mixamo 클립 vs 커스텀 모델 스켈레톤). Rig Builder/Bone
  Renderer는 원인에서 배제.
- A안(Humanoid 전환)을 진행할지 사용자 확인 필요 — Avatar 휴먼 본 매핑은 Unity 에디터
  Configure 창에서 하는 게 정확하고 안전함(메타 파일에 쿼터니언/스켈레톤 데이터를 텍스트로
  직접 써넣는 건 오류 위험이 커서 권장하지 않음).

## 변경된 파일

없음 (진단만 수행)
