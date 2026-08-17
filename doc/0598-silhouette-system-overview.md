# 0598. 유닛 실루엣 시스템 - 현재 구조 정리

**날짜:** 2026-08-16

언덕/건물에 가려진 유닛도 위치를 알 수 있게 해주는 실루엣(#19FF00) 기능이 doc/0592 ~ 0594를 거쳐
지금 형태로 자리잡았고, 관련 버그 수정(doc/0596)까지 반영된 상태. 여러 문서에 흩어진 내용을 지금
시점 기준으로 한 곳에 정리.

## 목적
카메라 기준으로 언덕이나 건물 뒤에 가려진 **내 부대(플레이어 NTA 유닛 + 아군 OC 유닛)** 가 어디
있는지 놓치지 않도록, 가려진 부분만 초록색(#19FF00) 실루엣으로 표시한다. 적 유닛에는 적용하지
않는다 - 적용하면 안개(Fog of War)로 가려야 할 적 위치가 그대로 노출돼 밸런스가 깨지기 때문
(doc/0592).

## 구성 파일
| 파일 | 역할 |
|---|---|
| `Assets/Shader/UnitSilhouette.shader` | 가려진 픽셀만 단색으로 그리는 URP 언릿 셰이더 |
| `Assets/Resources/UnitSilhouette.mat` | 위 셰이더 + `_Color = #19FF00` |
| `Assets/Scripts/Effects/UnitSilhouette.cs` | 머티리얼을 렌더러에 붙이고, 가림막 전용 깊이 카메라/텍스처를 관리하는 정적 헬퍼 |
| `Assets/Scripts/Unit/UnitController.cs` (`Awake()`) | `UnitSilhouette.Apply(gameObject)` 호출 - 플레이어 유닛 |
| `Assets/Scripts/FogOfWar/Ally/AllyController.cs` (`Awake()`) | `UnitSilhouette.Apply(gameObject)` 호출 - 아군 OC 유닛 |
| `Assets/Scripts/FogOfWar/Enemy/EnemyUnitController.cs` | **호출 없음** - 의도적으로 제외 |

## 동작 원리

### 1. 렌더링 방식: 추가 머티리얼 슬롯
`UnitSilhouette.Apply()`가 유닛의 `MeshRenderer`/`SkinnedMeshRenderer`마다 실루엣 머티리얼을
마지막 슬롯으로 하나 더 붙인다. 즉 유닛의 기존 표면 패스는 그대로 두고, 매 프레임 같은 메쉬를
한 번 더(단색으로) 덧그리는 구조. `ParticleSystemRenderer`/`TrailRenderer`/`LineRenderer`는
`GetComponentsInChildren<Renderer>()`로 뭉뚱그리면 같이 걸려서 이펙트에도 실루엣이 붙는 사고가
났었기 때문에, 몸체 메쉬 렌더러 두 타입만 명시적으로 골라 처리한다.

### 2. 가려짐 판정: 전용 가림막 깊이 텍스처
`UnitSilhouette.cs`가 메인 카메라의 자식으로 보조 카메라(`SilhouetteOccluderDepthCamera`)를 만들어
**Ground(레이어 7) + Building(레이어 9)만** 깊이로 렌더링해 `_OccluderDepthTex`(Depth 전용
RenderTexture)에 담고, `Shader.SetGlobalTexture`로 전역 공개한다. 부모-자식 관계라 위치/회전은
자동으로 메인 카메라를 따라가고, FOV/줌은 위치 이동으로 구현돼 있어(`CameraControl.cs`) 최초
설정 시 한 번만 복사하면 된다. 화면 해상도가 바뀌면(창 크기 조절 등) 텍스처를 다시 만든다
(`OccluderCameraResizeWatcher`가 매 프레임 정수 비교로 감시).

셰이더(`UnitSilhouette.shader`)는 `ZTest Always`로 그려지되, 프래그먼트 안에서 자기 자신의 선형
깊이와 `_OccluderDepthTex`의 선형 깊이를 직접 비교해서(`Linear01Depth`), 자신이 가림막보다
뚜렷하게 더 멀 때만(`clip(myLinear01 - occluderLinear01 - 0.0005)`) 픽셀을 그린다. 가림막이 없는
지점은 깊이버퍼가 원거리 클리어값을 그대로 가져 자연스럽게 "가려짐 없음"으로 처리되므로 별도
예외 처리가 필요 없다. `Blend One Zero`로 알파블렌딩 없이 단색으로 덮어써서 가려진 부분에서도
또렷하게 보인다.

## 왜 이 방식인가 (설계가 바뀐 이유)

1. **doc/0592 (최초):** 화면 전체 깊이버퍼와 `ZTest Greater`로 비교하는 가장 단순한 트릭으로
   시작. 셰이더 하나로 끝나 별도 판정 코드가 필요 없었음.
2. **doc/0593:** 화면 전체 깊이버퍼를 쓰다 보니 유닛이 **자기 자신의 다른 부품**(터렛 등)에 가려진
   것도 "가려짐"으로 오판하는 버그 발생. `Offset` 트릭으로 임시 봉합.
3. **doc/0594 (현재 구조로 전환):** 근본 원인은 "화면 전체" 깊이버퍼에 유닛 자신도 포함된다는
   것이었으므로, 지형/건물 레이어만 담은 **별도의 가림막 전용** 깊이 텍스처를 새로 만들어 그것과만
   비교하도록 변경. 유닛은 이 텍스처에 아예 없으므로 자기 부품에 가려서 오판하는 경우가 구조적으로
   사라짐 (0593의 Offset 트릭도 더 이상 필요 없어져 제거됨).
4. **doc/0596 (버그 수정):** Building 레이어(9)에 실제 `MeshRenderer`가 있는 오브젝트가 씬 전체에
   점령지 건물 3개뿐이라, 플레이어가 직접 지은 건물/아군 OC 건물 뒤에서는 실루엣이 안 뜨는 문제가
   있었음. 두 진영이 공유하는 건물 비주얼 모델 프리팹 13개의 `m_Layer`를 0(Default) → 9(Building)로
   수정해서 해결 (코드 변경 없이 순수 데이터 수정).

## 현재 적용 범위 / 한계
- **적용됨:** 플레이어(NTA) 유닛, 아군 OC 유닛. 언덕과 건물(직접 지은 것 포함) 양쪽 모두 가려짐
  판정에 들어감.
- **적용 안 됨:** 적 유닛(의도적 - 밸런스), 건물 자체(가려진 유닛이 아니라 가리는 쪽으로만 쓰임).
- **보류된 확장 (doc/0595):** 건물도 가려졌을 때 실루엣을 띄우는 안이 제안됐으나 사용자가 doc/0596
  버그 수정을 먼저 요청해 보류 상태. 재개할 경우 건물은 자기 자신도 Building 레이어에 속해 있어
  Ground+Building 가림막을 그대로 쓰면 0594와 같은 자기 부품 오판이 재발하므로, **Ground 전용**
  가림막 텍스처를 별도로 만들어야 함(설계는 0595에 이미 정리돼 있음). 또한 "다른 건물에 가려진
  건물"은 이 방식으로는 감지 못 함(오브젝트별 식별 없이는 불가) - 건물은 고정 목표라 실사용상
  필요성이 낮다고 판단해 범위 밖으로 둠.
