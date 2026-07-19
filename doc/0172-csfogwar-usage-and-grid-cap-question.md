# 0172 - `csFogWar` 사용법 상담 + 128 그리드 캡 질문

## 질문

`Assets/AssetFolder/AOSFogWar/csFogWar.cs`를 읽어보고 사용법을 설명해달라는 요청과 함께, 이전
프로젝트에서 유닛/건물 코드에 넣었던 것으로 보이는 예전 스니펫을 제시:

```csharp
try { fogWar = GameObject.Find("FogWar").GetComponent<csFogWar>(); }
catch { Debug.LogErrorFormat(...); return; }
try { uibuttonmaping = GameObject.Find("RTSUnitControlSystem").GetComponent<UiButtonMapiing>(); }
catch { Debug.LogErrorFormat(...); return; }

fogWar.AddFogRevealer(new csFogWar.FogRevealer(transform, 30, true));
```

이 방식이 괜찮은지, 현재 프로젝트에 어떻게 적용하면 좋을지, 그리고 `csFogWar`의 `levelDimensionX/Y`가
128까지만 되는데 늘릴 수 있는지 질문.

## 조사 및 답변 (코드 변경 없음, 상담만)

`csFogWar.cs`, `Shadowcaster.cs`, `Examples/csFogVisibilityAgent.cs`를 읽고 답변:

### 구조
- `FogRevealer(Transform, sightRange, updateOnlyOnMove)`을 `AddFogRevealer`/`RemoveFogRevealer`로
  등록·해제. `sightRange`는 월드 단위로 넘기면 내부에서 `unitScale`로 나눠 셀 단위로 변환(csFogWar.cs:474).
- 실제 시야는 `Shadowcaster`가 shadowcasting 알고리즘으로 장애물 가림까지 계산.
- 유닛을 실제로 숨기는 건 모듈이 직접 안 하고, `csFogWar.CheckVisibility(worldPos, additionalRadius)`를
  각자 물어봐서 `Renderer.enabled`를 토글하는 방식(`Examples/csFogVisibilityAgent.cs` 참고).

### 예전 스니펫 평가
- `AddFogRevealer` 호출 자체는 정식 사용법 맞음.
- `GameObject.Find("FogWar")`는 이름 문자열 의존이라 깨지기 쉬움 — 에셋 자체 예제도
  `FindObjectOfType<csFogWar>()`를 쓰고 "나중에 바꿔라"라고 주석에 명시. 이 프로젝트는
  `BuildingController`/`EnemyController`가 이미 `FindFirstObjectByType<RTSUnitController>()` 패턴을
  쓰고 있으므로 그 관례에 맞추는 게 낫다고 제안.
- `RTSUnitControlSystem`/`UiButtonMapiing` 조회 부분은 이 프로젝트에 존재하지 않는, 다른 프로젝트에서
  재사용된 무관한 코드로 보여 그대로 쓰면 안 된다고 지적.
- **`RemoveFogRevealer` 대응 코드가 없다는 점을 지적** — 유닛이 죽어 `Destroy()`될 때 리스트에서
  안 빼면 파괴된 Transform 참조로 `MissingReferenceException` 위험이 있어, `OnDestroy()`에서 해제
  호출이 반드시 필요하다고 조언.

### 적용 방향 제안 (아직 코드로 작성 안 함)
- 씬에 `FogWar` 오브젝트 + `csFogWar` 배치(`levelMidPoint`=맵 중심, `fogPlaneMaterial`=`FogPlane.mat`).
- 유닛/건물 프리팹에 붙일 얇은 래퍼 컴포넌트(이전에 만들었다 삭제한 `Eye.cs`와 발상은 같지만, 이번엔
  `csFogWar.AddFogRevealer`/`RemoveFogRevealer`를 호출하는 어댑터) — `Start()` 등록, `OnDestroy()` 해제.
- 적 은폐는 `EnemyController`에 `csFogVisibilityAgent`와 비슷한 로직을 붙이는 방향 제안.
- 실제 코드 작성은 사용자가 요청하면 별도 설계 문서로 진행하기로 함(이번엔 상담만).

### 128 그리드 캡
- `[Range(1, 128)]`는 인스펙터 슬라이더 제한일 뿐 알고리즘 하드 제한이 아님. 실제 비용 원인은
  `UpdateFogPlaneTextureBuffer()`가 **매 프레임** `Texture2D.GetPixels()`/`SetPixels()`로 텍스처 전체를
  통째로 lerp하는 부분(그리드 면적에 비례) — 이것 때문에 저자가 "128 넘으면 레이캐스팅 기반 모듈을
  고려하라"고 권고한 것.
- `[Range(1, 128)]` → `[Range(1, 256)]`처럼 직접 올려도 안전하다고 답변.
- 다만 그 전에 대안으로 **`unitScale`(셀 월드 크기)을 키우는 방법**을 제안: 이 프로젝트 맵은
  `CameraControl` 기준 약 170×170 월드 유닛이므로, `unitScale=1.5`(약 114칸) 또는 `2`(85칸)면 코드
  수정 없이 128 캡 안에서 해결 가능. RTS 안개는 픽셀 단위 정밀도가 필요 없어 이쪽을 우선 권장.

## 변경 사항
없음(상담/설명만, 코드·씬 변경 없음).
