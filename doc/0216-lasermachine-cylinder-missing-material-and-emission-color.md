# 0216 — LaserMachine "Default-Material 핑크" 및 파랑 레이저가 빨간색으로 보이는 문제 재조사

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **조사 결과 + 제안 수정**만 담고
> 실제 파일은 아직 건드리지 않았다(관련해서 [[0215-lasermachine-materials-broken-in-urp]]의 `.mat` 4개 변경은 사용자 요청으로 전부 원본 상태로 되돌림).

## 1. 사용자 피드백
"Default-Material이 핑크색으로 나오거든 이게 깨진거 같은데 다른거를 뭔가 변경하니깐 Laser_Blue_3D같은 경우 파란색이여야하는데
빨간색으로 보이고 Default-Material은 여전히 핑크색이야" → 0215의 `.mat` 셰이더 수정을 원상복구하고 재조사 요청.

## 2. 결론 먼저

0215에서 다룬 "`.mat` 파일 자체의 깨진 셰이더 참조" 문제와는 **완전히 별개인 문제 3가지**를 새로 발견함.
0215의 셰이더 매핑 자체는 여전히 맞지만, 그것만 고쳐서는 사용자가 본 증상이 해결되지 않음.

### 문제 A — 레이저 빔 실린더에 머티리얼이 아예 할당 안 되어 있음 (진짜 "Default-Material" 정체)
`Laser_Red_3D.prefab`, `Laser_Blue_3D.prefab` 둘 다 `Graphics` 밑에 두 개의 메쉬가 있음:
- `center sphere` (발광점 작은 구) → `Laser_RED.mat`/`Laser_BLUE.mat`을 정상적으로 참조 중 (`m_Materials: [{guid: 33398c1d...}]`/`[{guid: d5e9978...}]`)
- **`Cylinder`(실제 레이저 빔 몸통)** → 머티리얼이 `{fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}`, 즉 **Unity 내장 "Default-Material"** 을 참조. 이건 애초에 이 메쉬에 아무 머티리얼도 할당된 적이 없다는 뜻(Unity가 비어있는 슬롯에 자동으로 채워넣는 플레이스홀더). Default-Material은 Built-in RP 전용 셰이더라서 URP에서 항상 핑크로 보임.

즉 사용자가 본 "Default-Material 핑크"는 0215에서 손댄 4개 `.mat` 파일과 무관하게, **레이저 빔 실린더 메쉬 자체가 원래부터 머티리얼 미할당 상태**였던 것. 0215의 수정을 적용하든 되돌리든 이 실린더는 계속 핑크로 보일 수밖에 없었음.

### 문제 B — `Laser_BLUE.mat`의 `EmissionColor`가 원본부터 빨간색으로 박혀 있음
0215 3.2절에서 이미 짚었던 부분: `Laser_BLUE.mat`은 `_Color`(파랑)는 맞는데 `_EmissionColor: {r:1, g:0, b:0, a:1}`(빨강)로 저장돼 있음. 이건 셰이더가 깨져서(핑크) 아무것도 안 보일 때는 드러나지 않다가, 0215 수정으로 URP에서 발광이 실제로 렌더링되기 시작하자 `center sphere`가 파란 바탕색 대신 빨간 발광색으로 압도되어 보인 것 — 사용자가 "Laser_Blue_3D인데 빨간색으로 보인다"고 한 부분이 정확히 이 증상과 일치함. **셰이더 매핑 문제가 아니라 원본 에셋 데이터 자체의 색상 값 문제.**

### 문제 C — `Sparks_Blue_3D`/`Sparks_Blue_2D` 프리팹이 엉뚱한 머티리얼을 참조
파티클 프리팹의 머티리얼 참조를 대조:
- `Sparks_Red_3D.prefab` → `Sparks_RED.mat` (guid `19e512963e4da2241bae72d203742fe1`) — 정상
- `Sparks_Blue_3D.prefab` / `Sparks_Blue_2D.prefab` → guid `d5e9978240b94de4289e442d1d47b90b` — 이건 `Sparks_BLUE.mat`(guid `b4c069bc6bb57e14eb90b0c9ae4afba8`)이 아니라 **`Laser_BLUE.mat`** 임! Red 쪽은 제대로 `Sparks_RED.mat`과 짝지어져 있는데 Blue만 어긋나 있어서, 원본 에셋 제작 과정에서 Blue 버전을 복제할 때 잘못 드래그한 실수로 보임. (Laser_BLUE.mat은 Opaque/Standard 계열이라 파티클에 그대로 쓰면 소프트 블렌딩/Additive 없이 불투명하게 렌더링되어 스파크 이펙트가 이상하게 보임.)

### 참고 — DemoScene3D의 수십 개 Cube/Plane도 핑크
`DemoScene3D.unity`에는 `Cube (1)`~`Cube (18)`, `Plane`~`Plane (3)` 등 약 19개 오브젝트가 전부 `fileID: 10303`(Default-Material)을 참조함. 이건 데모 씬의 맨 블록아웃 테스트 지형(바닥/벽)이고, LaserMachine 자체 머티리얼 에셋과는 무관 — 애초에 아무 텍스쳐/머티리얼도 없이 만들어진 회색 테스트 지형으로 보임. 이번 조사 범위(LaserMachine 머티리얼 폴더) 밖이라고 판단해서 이번 제안에는 포함 안 시켰음. 원하면 별도로 살펴볼 수 있음.

## 3. 제안하는 수정

1. **0215의 `.mat` 셰이더 매핑을 재적용** — 4개 파일을 `Universal Render Pipeline/Lit`/`Particles/Lit`로 다시 전환 (0215 4절 내용 그대로, 이미 검증됨).
2. **`Laser_Red_3D.prefab`/`Laser_Blue_3D.prefab`의 `Cylinder` 메쉬에 머티리얼 할당** — 현재 `{fileID: 10303, guid: 000...f000...}`(Default-Material)로 되어 있는 `m_Materials`를, 같은 프리팹의 `center sphere`가 쓰는 것과 동일한 머티리얼로 교체 (Red→`Laser_RED.mat`, Blue→`Laser_BLUE.mat`).
3. **`Sparks_Blue_3D.prefab`/`Sparks_Blue_2D.prefab`의 머티리얼 참조를 `Sparks_BLUE.mat`(guid `b4c069bc6bb57e14eb90b0c9ae4afba8`)으로 정정** — 현재 잘못 물려있는 `Laser_BLUE.mat` 참조 대신.
4. **`Laser_BLUE.mat`의 `_EmissionColor`를 파란 계열로 수정할지 여부** — 원본 에셋 데이터 버그로 보이지만, 의도적으로 "파란 레이저인데 빨간 스파크 발광" 컨셉일 가능성도 배제 못 해서 사용자 확인 필요 (아래 질문 참고).

## 4. 확인 필요 사항
위 1~3번을 그대로 적용해도 될지, 4번(EmissionColor)은 어떻게 할지 확인 부탁.

## 5. 적용 완료

사용자가 "전부 적용" + "EmissionColor 파란 계열로 수정"을 선택하여 다음을 전부 적용함:

1. `Laser_RED.mat`/`Laser_BLUE.mat`/`Sparks_RED.mat`/`Sparks_BLUE.mat` — [[0215-lasermachine-materials-broken-in-urp]] 4절의 URP 셰이더 매핑을 재적용.
   - `Laser_BLUE.mat`만 `_EmissionColor`를 `{r:1,g:0,b:0,a:1}`(빨강) → `{r:0, g:0.35928416, b:1, a:1}`으로 변경. `_Color`의 파란 색상값에서 R 채널만 0으로 낮춰 순수한 파란 발광이 되도록 함 — `Laser_RED.mat`이 `_Color{r:1,g:0.0735,b:0.0735}` → `_EmissionColor{r:1,g:0,b:0}`처럼 원색 채널만 남기고 나머지를 0으로 만드는 것과 동일한 패턴.
2. `Laser_Red_3D.prefab` / `Laser_Blue_3D.prefab`의 `Cylinder`(레이저 빔 몸통) MeshRenderer `m_Materials`를 `{fileID: 10303, guid: 0000000000000000f000000000000000, type: 0}`(Default-Material)에서 같은 프리팹의 `center sphere`가 쓰는 것과 동일한 머티리얼로 교체:
   - Red → `Laser_RED.mat` (`{fileID: 2100000, guid: 33398c1da1c950e418147f243c52554f, type: 2}`)
   - Blue → `Laser_BLUE.mat` (`{fileID: 2100000, guid: d5e9978240b94de4289e442d1d47b90b, type: 2}`)
3. `Sparks_Blue_2D.prefab` / `Sparks_Blue_3D.prefab`의 (Particle System)Renderer `m_Materials[0]`을 잘못 물려있던 `Laser_BLUE.mat`(`guid: d5e9978240b94de4289e442d1d47b90b`)에서 `Sparks_BLUE.mat`(`guid: b4c069bc6bb57e14eb90b0c9ae4afba8`)로 정정. 트레일 머티리얼 슬롯(`m_Materials[1]`, `{fileID: 0}`)은 원래도 비어있던 정상 상태라 그대로 둠.

## 6. 확인 필요 사항
Unity 에디터에서 `DemoScene3D`/`DemoScene2D`를 열어:
- 레이저 빔(Cylinder)과 발광점(center sphere) 둘 다 핑크 없이 각각 빨강/파랑으로 보이는지
- Sparks_Blue 파티클이 이제 파란 Additive 발광 스파크로 정상 렌더링되는지 (기존엔 Laser_BLUE.mat의 Opaque 셰이더 때문에 불투명하게 보였을 가능성)
확인 부탁. `DemoScene3D`의 Cube/Plane 블록아웃 테스트 지형(2절 참고)은 이번 범위 밖이라 여전히 핑크로 보일 수 있음 — 원하면 별도로 처리 가능.
