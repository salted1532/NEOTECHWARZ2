# 0170 - AOSFogWar 에셋 머티리얼 깨짐 원인 조사 및 수정안

> **문서 성격**: [[confirm_before_implementing]] 규칙에 따라, 이 문서는 **조사 결과 + 수정 옵션**만
> 담고 아직 실제 파일은 건드리지 않았다. 옵션을 검토한 뒤 알려주면 그때 적용한다.

## 1. 요청

"AssetFolder안에 AOSFogWar가 내가 다운로드 받은 전장의 안개 에셋인데 이거 안에 메테리얼 깨진것좀
고쳐줘"

## 2. 조사 결과

`Assets/AssetFolder/AOSFogWar/`의 머티리얼(`.mat`) 전부를 확인했다. 프로젝트는 URP([[0071-canopus-materials-broken-in-urp]]에서
이미 확인)이고, 이번에도 원인은 같은 패턴("Built-in RP 전용 셰이더 = URP에서 핑크로 깨짐")이지만,
**실제로 깨진 파일은 일부뿐**이고 나머지는 이미 정상이다.

### 2.1 실제로 깨진 것 — `Demo/Materials/Built-in/` 6개

```
Demo/Materials/Built-in/Eye.mat
Demo/Materials/Built-in/Floor.mat
Demo/Materials/Built-in/Monster.mat
Demo/Materials/Built-in/Obstacle.mat
Demo/Materials/Built-in/Revealer.mat
Demo/Materials/Built-in/Wall.mat
```

전부 `m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000, type: 0}` (Built-in RP의
**Standard 셰이더**, Surface Shader 계열)를 참조 — URP에서는 렌더링 자체가 안 돼 프로젝트 창
썸네일/씬 모두 핑크(마젠타)로 보인다. 이 6개는 에셋에 같이 들어있는 `Demo_Built-in.unity`
(Built-in RP 전용 데모 씬)에서만 쓰이도록 만들어진 것들이다.

### 2.2 이미 정상인 것 (건드릴 필요 없음)

- **`Demo/Materials/URP/` 6개**(`Eye.mat`, `Floor.mat`, `Monster.mat`, `Obstacle.mat`, `Revealer.mat`,
  `Wall.mat`): 전부 `m_Shader: {..., guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}` — 이 프로젝트가
  실제로 쓰는 URP `Universal Render Pipeline/Lit` 셰이더와 정확히 같은 GUID. `Demo_URP.unity` 씬을
  열어 확인해보니 오브젝트들의 `m_Materials`가 실제로 이 URP 폴더 쪽 GUID(예: `Obstacle.mat` →
  `3b93b12b5ace6084184eb920703fa736`)를 정확히 참조하고 있어 셰이더 연결에 문제가 없다.
- **`FogPlane.mat`**(루트) → `FogPlane.shader`(안개 자체를 그리는 핵심 셰이더): CGPROGRAM 기반의
  단순 Unlit 셰이더(`UnityCG.cginc`만 사용, Surface Shader 아님)라서 URP의 SRPDefaultUnlit 호환
  경로로 정상 렌더링된다. GUID 연결(`FogPlane.mat` → `FogPlane.shader`, 데모 씬의 `csFogWar`
  컴포넌트 → `fogPlaneMaterial` 필드)도 전부 정확히 일치함을 확인했다. **이건 애초에 안 깨진 상태.**
  즉 전장의 안개 기능 자체(핵심 모듈)는 셰이더 문제가 없다.

### 2.3 결론

"깨진 머티리얼"은 사실상 **URP 프로젝트에서는 아예 쓰이지 않는 `Demo/Materials/Built-in/` 6개**뿐이다.
이 폴더는 에셋에 같이 포함된 `Demo_Built-in.unity`(별도의 Built-in RP 전용 데모 씬)용이고, 우리
프로젝트가 실제로 열어볼 일이 있는 `Demo_URP.unity`나 핵심 모듈(`csFogWar.cs`, `FogPlane.mat` 등)은
멀쩡하다. Unity 프로젝트 창은 현재 활성 렌더 파이프라인(URP) 기준으로 모든 머티리얼 썸네일을
미리보기 렌더링하기 때문에, 이 폴더를 열어보기만 해도(어느 씬에서도 안 쓰였어도) 핑크로 보인다 —
아마 이게 "깨졌다"고 느낀 지점일 것이다.

또한 에셋 자체의 `README.txt`에도 "데모 폴더는 사용법을 익힌 뒤 지워도 된다(Feel free to delete
the demo folder once you've got the grasp of how things work). 다른 폴더만 그대로 두면 된다"고
명시돼 있어, 제작자도 데모 폴더가 핵심 기능과 무관하다는 걸 전제하고 있다.

## 3. 수정 옵션 (택1, 적용 전 확인 필요)

1. **(A) Built-in 데모 머티리얼 6개를 URP 셰이더로 변환** — [[0071-canopus-materials-broken-in-urp]]/
   [[0075-yoge-materials-broken-in-urp]]와 동일한 방식으로 `m_Shader`를 URP Lit로 바꿔 핑크를 없앤다.
   다만 이렇게 해도 **어차피 안 쓰이는 `Demo_Built-in.unity`용**이라 실질적 이득은 "프로젝트 창에서
   핑크로 안 보인다"는 것뿐이고, 이미 완전히 동일한 내용의 URP 버전(`Demo/Materials/URP/*.mat`)이
   존재하므로 사실상 중복 작업이다.
2. **(B, 권장) `Demo/Materials/Built-in/` 폴더와 `Demo_Built-in.unity`, `Eye_Built-in.prefab`만 삭제**
   — URP 프로젝트에서 절대 쓰이지 않는 Built-in RP 전용 사본이므로 지워도 기능 손실이 전혀 없다
   (URP 버전이 이미 존재하고 `Demo_URP.unity`가 그걸 씀). "핑크로 깨져 보이는" 원인 자체를 제거하는
   가장 깔끔한 방법.
3. **(C) `Demo` 폴더 전체 삭제** — 에셋 자체 README가 권장하는 방식. 핵심 모듈(`csFogWar.cs`,
   `Shadowcaster.cs`, `FogPlane.shader`/`FogPlane.mat`, `Editor/`, `Examples/`)은 `Demo` 폴더 밖에
   있어 전혀 영향받지 않는다. 다만 데모 씬을 참고용으로 남겨두고 싶다면 과할 수 있음.

## 4. 확인 필요 사항

A/B/C 중 어느 것으로 진행할지 알려주면 적용하겠다. (개인적으론 B를 권장 — 실제 사용하지 않는
Built-in 전용 자료만 제거해서 핑크 문제를 없애고, `Demo_URP.unity`는 참고용으로 남길 수 있음.)
