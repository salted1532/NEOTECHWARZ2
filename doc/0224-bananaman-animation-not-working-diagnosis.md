# 0224 - Bananaman 교체 후에도 애니메이션 미작동 - 원인 진단

날짜: 2026-07-24

## 요청 내용

기존 `unit_Infantry_Light_A_yup` 모델은 리깅에 부적합해 보여서, AssetStore에서 조인트가 잘
갖춰진 "Bananaman" 모델을 새로 받아 Mixamo에 업로드 → 애니메이션 적용 → 다운로드한 클립을
`Assets\Animation` 폴더에 넣고 전부 연결/적용했는데도 여전히 애니메이션이 정상 작동하지 않는다는
문제. 원인 확인 요청.

## 조사 내용

`Assets\prefabs\NTA\Unit\Tier1\Sharpshooter.prefab`을 다시 읽어서 확인한 결과, **서로 다른
원인 4가지**가 겹쳐 있음.

### 1) 중복된 두 개의 "Banana Man" fbx가 존재하고, 그중 잘못 임포트된 쪽이 실제로 쓰이고 있음

- `Assets\Plugins\Banana Yellow Games\Characters\Banana Man\Banana Man.fbx`
  (guid `81bb9eea30387e442899949a80011e55`) — AssetStore 원본. **animationType: 3 (Humanoid)**,
  `avatarSetup: 1`(Humanoid Avatar 자동 생성됨), `hasExtraRoot: 1`.
- `Assets\Animation\Banana Man.fbx` (guid `f86e12f43324e014bb96b99522475ad6`) — Mixamo에서
  다시 받으면서 같이 딸려온 것으로 보이는 **별도 사본**. **animationType: 2 (Generic, Humanoid
  아님)**, `avatarSetup: 0`(Avatar 없음), `hasExtraRoot: 0`, `optimizeBones: 1`(본 계층을
  임포트 시 최적화/병합함 — Plugins 원본은 이 옵션 자체가 없음, 즉 다른 임포트 설정).

Sharpshooter.prefab에 실제로 자식으로 붙은 "Banana Man" 오브젝트는 **guid
`f86e12f43324e014bb96b99522475ad6`, 즉 `Assets\Animation\Banana Man.fbx`(Generic, 최적화된
본 구조)** 쪽에서 인스턴스화됨.

### 2) Animator의 Avatar가 실제 모델과 다른 파일의 Avatar를 참조 중

그런데 그 오브젝트에 붙은 `Animator` 컴포넌트는:
```yaml
m_Avatar: {fileID: 9000000, guid: 81bb9eea30387e442899949a80011e55, type: 3}
```
→ **Plugins 원본(Humanoid, hasExtraRoot 1, optimizeBones 없음)의 Avatar**를 그대로 가리키고
있음. 즉 "실제로 화면에 있는 본 계층(Animation 폴더의 Generic/최적화된 사본)"과 "Animator가
쓰는 Avatar(Plugins 원본의 Humanoid Avatar)"가 서로 다른 임포트 결과물이라 본 계층 구조가
일치하지 않음 (extra root 유무, 본 최적화 여부가 다름). Avatar가 가리키는 본 경로와 실제
GameObject 계층이 어긋나므로, Animator는 State 전이는 정상 수행해도 실제 본에는 올바르게
데이터가 적용되지 않음 — 지난 진단(`doc/0223`)과 증상은 같지만 원인은 다름(그때는 이름 자체가
안 겹쳤고, 지금은 같은 모델의 "다른 임포트 결과물"끼리 Avatar를 섞어 써서 계층이 안 맞음).

### 3) 정체불명의 레거시 Animation 컴포넌트가 같이 붙어있음

같은 "Banana Man" 오브젝트에 `Animator` 말고 **레거시 `Animation` 컴포넌트**도 추가돼 있음:
```yaml
--- !u!111 &8362633621800528607
Animation:
  m_Animation: {fileID: -203655887218126122, guid: 2e98fb84fb9d3b7419c1b87db108b285, type: 3}
  m_PlayAutomatically: 1
```
guid `2e98fb84fb9d3b7419c1b87db108b285`는 `firing rifle` 클립 — 즉 시작하자마자 레거시
Animation 시스템으로 사격 애니메이션을 자동 재생하도록 세팅되어 있음. 이건 보통 씬/프리팹의
GameObject에 AnimationClip을 실수로 직접 드래그했을 때 Unity가 자동으로 만들어주는 컴포넌트라,
의도한 게 아닐 가능성이 높음. Animator(Mecanim)와 레거시 Animation이 같은 오브젝트에서 같은
본들을 동시에 건드리면 서로 충돌해서 예측 불가능하게 동작함.

### 4) 예전 `unit_Infantry_Light_A_yup` 모델이 그대로 자식으로 남아있음

Sharpshooter.prefab 안에 `unit_Infantry_Light_A_yup` 자식(예전 프리팹 인스턴스, guid
`4659ca7a15dbaeb4c88a19731c055e56`)이 제거되지 않고 `Banana Man`과 함께 둘 다 자식으로 붙어
있음. 비활성화 처리(`m_IsActive: 0`)도 안 돼 있어서, 두 모델이 같은 위치에 겹쳐서 같이
렌더링되고 있을 가능성이 있음 — 화면에서 뭘 보고 있는 건지 헷갈리는 원인이 될 수 있음.

## 해결 방법 (제안 — 아직 미적용, 확인 필요)

1. **레거시 `Animation` 컴포넌트 삭제** (Banana Man 오브젝트에서). 확실한 버그이므로 바로
   제거해도 안전.
2. **`Assets\Animation\Banana Man.fbx`의 Import 설정을 Humanoid로 통일**
   (`animationType: 2 → 3`), 그래서 이 파일 자체의 Avatar가 새로 생성되게 하고, Animator의
   `m_Avatar`를 Plugins 원본이 아니라 **이 파일 자체의 Avatar**로 다시 연결. (또는 더 깔끔한
   대안: `Assets\Animation\Banana Man.fbx` 사본을 아예 지우고, Sharpshooter의 Banana Man
   자식을 Plugins 원본에서 새로 인스턴스화 — 모델 파일이 두 곳에 중복돼 있을 이유가 없음.)
3. **`walking.fbx`/`firing rifle.fbx`/`rifle aiming idle.fbx` 세 클립도 Humanoid로 재설정**
   (지금 셋 다 `animationType: 2`). Banana Man 모델이 Humanoid이니 클립도 Humanoid로 맞춰야
   근육(muscle) 기반 리타겟팅이 공식적으로 보장되는 방식대로 동작함.
4. **예전 `unit_Infantry_Light_A_yup` 자식 제거** (또는 최소한 비활성화) — 더 이상 쓰지 않는
   모델이면 Sharpshooter.prefab에서 지우는 게 맞음.

## 요약/남은 작업

- 원인 4가지 모두 확인됨: (a) 잘못된 사본의 모델을 씀, (b) Animator Avatar가 다른 파일 걸
  참조, (c) 레거시 Animation 컴포넌트 충돌, (d) 예전 모델이 안 지워짐.
- 1번(레거시 Animation 삭제)과 4번(예전 모델 제거)은 명확한 정리 작업이라 바로 진행 가능.
- 2번(Import 설정 변경 + Avatar 재연결)과 3번(클립 Humanoid 전환)은 Unity가 Humanoid Avatar를
  자동으로 다시 계산하는 과정이 껴 있어서, 메타 파일의 `animationType` 숫자만 텍스트로 바꾸는
  것 자체는 가능하지만 **Avatar 자동 매핑이 실제로 올바르게 됐는지는 Unity 에디터에서 임포트가
  다시 돌아간 뒤 Configure 창으로 확인이 필요** — 사용자 확인 후 진행 권장.

## 결정

사용자가 "설명만 듣고 직접 다 처리"를 선택 — 4가지 모두 사용자가 Unity 에디터에서 직접 진행.
Claude는 코드/에셋을 건드리지 않음.

## 변경된 파일

없음 (진단 및 설명만 제공, 실제 수정은 사용자가 에디터에서 직접 진행)
