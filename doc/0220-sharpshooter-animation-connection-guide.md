# 0220 - Sharpshooter 애니메이션 연결 방법 안내

날짜: 2026-07-24

## 요청 내용

`Assets\prefabs\Asset\unit_Infantry_Light_A_yup.prefab`에 Bone Renderer와 Rig Builder까지는
추가했는데, `Assets\Animation` 폴더 안의 라이플 사격/걷기 애니메이션을 어떻게 연결해야 하는지 질문.
(Sharpshooter 유닛은 `Assets\prefabs\NTA\Unit\Tier1\Sharpshooter.prefab`에 있으며, 현재는
임시 Cube 메쉬만 있고 실제 비주얼은 아직 연결 전 상태.)

## 조사 내용

- 애니메이션 클립 파일 확인: `Assets\Animation\walking.fbx`, `Assets\Animation\firing rifle.fbx`
- 두 클립과 `unit_Infantry_Light_A_yup.fbx` 모두 Import 설정상 `animationType: 2`
  (= Generic 리그, Humanoid 아님) → 리타겟팅은 "본 계층/이름이 같은 모델끼리" 방식으로 동작.
  같은 에셋팩에서 나온 파일들이라 본 이름이 일치할 가능성이 높음.
- `unit_Infantry_Light_A_yup.prefab`에는 이미 BoneRenderer, RigBuilder 컴포넌트가 추가돼 있음
  (Animation Rigging 패키지) — 다만 이 둘은 IK/조준 보정용이고, 기본 애니메이션 재생(Animator +
  AnimatorController)과는 별개 기능. Sharpshooter처럼 "조준" 연출을 넣을 때 나중에 쓰는 용도.
- 아직 Animator Controller 에셋은 프로젝트 어디에도 없음 (`Assets/prefabs/Asset/*.controller` 없음).
- `Assets\Scripts\Unit\UnitController.cs`에 이동 상태를 폴링용으로 노출하는
  `IsCurrentlyMoving()` (line 1403), `IsIdle()/IsMove()/IsAttack()` (line 1397-1399)이 이미 있음.
  `HoverBob` 같은 이펙트 스크립트들이 이 패턴으로 Update에서 폴링하고 있으므로, 애니메이션 파라미터
  갱신도 같은 패턴(전용 컴포넌트가 매 프레임 폴링해서 Animator에 반영)을 쓰면 기존 코드 스타일과
  일치함.

## 답변 (에디터 작업 절차 안내 — 코드 변경 없음)

라이플 사격/걷기 클립을 실제로 재생시키려면 **Animator + Animator Controller**가 필요합니다.
Bone Renderer/Rig Builder는 그 위에 얹는 보정 레이어라 지금 당장은 없어도 애니메이션 자체는 재생됩니다.

1. **Animator 컴포넌트 확인/추가**
   - `unit_Infantry_Light_A_yup` 프리팹의 루트 GameObject에 `Animator` 컴포넌트가 있는지 확인.
     없으면 Add Component → Animator.
   - Avatar 슬롯: 모델(`unit_Infantry_Light_A_yup.fbx`)이 Generic이므로, fbx를 펼쳐서 나오는
     Avatar 서브에셋을 그대로 넣으면 됨 (보통 자동으로 채워져 있음).

2. **Animator Controller 에셋 생성**
   - `Assets/prefabs/Asset/` 같은 곳에 우클릭 → Create → Animator Controller
     (예: `Sharpshooter.controller`).
   - Animator 컴포넌트의 `Controller` 슬롯에 드래그.

3. **State 만들고 클립 연결**
   - Animator 창을 열고, 빈 곳에 우클릭 → Create State → Empty 를 두 개 만들어 Walk, Fire로 이름 변경
     (혹은 클립을 Animator 창에 바로 드래그하면 State가 자동 생성됨).
   - `Assets\Animation\walking.fbx`를 프로젝트 창에서 화살표로 펼치면 안에 실제 AnimationClip
     서브에셋이 있음 → 그걸 Walk state의 Motion에 드래그.
   - `Assets\Animation\firing rifle.fbx`도 마찬가지로 펼쳐서 클립을 Fire state의 Motion에 드래그.
   - 만약 재생했을 때 팔다리가 뒤틀리거나 본이 하나도 안 움직이면, 두 클립의 본 이름이 모델과 달라서
     리타겟팅이 안 되는 것 — 이 경우 각 fbx의 Rig 탭에서 Avatar Definition을 "Copy From Other
     Avatar"로 바꾸고 `unit_Infantry_Light_A_yup`의 Avatar를 지정해주면 해결됨.

4. **파라미터 & 트랜지션**
   - Animator 창 좌측 Parameters 탭에서 `IsMoving` (Bool), `Fire` (Trigger) 추가.
   - Idle → Walk: condition `IsMoving == true`.
   - Walk → Idle: condition `IsMoving == false`.
   - Any State → Fire: trigger `Fire`, Has Exit Time 체크 해제(즉시 반응), Fire → 이전 상태로
     돌아가는 트랜지션은 Exit Time ≈ 1(클립 끝날 때) 로 설정.

5. **스크립트에서 파라미터 갱신**
   - `UnitController`에 이미 `IsCurrentlyMoving()`, `IsAttack()` 같은 폴링용 공개 메서드가 있으므로,
     `HoverBob`처럼 별도의 작은 컴포넌트(예: `UnitAnimatorDriver`)를 만들어 매 프레임
     `animator.SetBool("IsMoving", unitController.IsCurrentlyMoving())`,
     공격 판정 시점에 `animator.SetTrigger("Fire")` 를 호출하는 식으로 연결하면 기존 코드 스타일과
     일관됨. (현재는 안내만 드린 상태이며, 이 스크립트 작성은 별도 요청 시 진행)

6. **Rig Builder/Bone Renderer는 나중에**
   - Sharpshooter가 타겟을 조준하는 연출(예: 상체만 타겟 방향으로 회전)을 넣고 싶을 때, Rig Builder
     밑에 Rig GameObject를 만들고 Multi-Aim Constraint 등을 추가해서 Rig Layer에 등록하면 됨.
     기본 Walk/Fire 애니메이션 재생과는 순서상 독립적이라 지금 당장 급하지 않음.

## 요약/남은 작업

- 이번 요청은 순수 Q&A로, 프로젝트 파일 변경 없음.
- 실제 Animator/Controller 생성, State/트랜지션 연결은 Unity 에디터 GUI 작업이라 사용자가 직접
  진행하는 것을 권장(에디터 조작은 텍스트로 대신하기 위험도가 높음).
- `UnitAnimatorDriver` 같은 폴링 스크립트를 실제로 작성해 붙이는 건 사용자가 원하면 별도 요청으로
  진행(코드 변경이 필요한 부분이라 confirm-before-implementing 규칙 적용 대상).

## 변경된 파일

없음 (안내만 제공)
