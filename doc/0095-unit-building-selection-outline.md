# 0095 - 유닛/건물 선택 시 아웃라인 표시 (제안)

**날짜:** 2026-07-13

## 요청 내용

> 그럼 현재 prefabs폴더 안에있는 building unit들에서 mtrl_canopus-iii_set01-blue등으로 되어있는 메테리얼을 현재 shader폴더 안에있는 mainmaterials(testshader)를 적용시켜줘

이어서 대상 확인 질문에 대한 답변:
> mtrl_canopus-iii_set01-blue들로 된 mesh들이 현재 유닛들이 보여지는 메쉬 모델링이므로 여기에 적용시켜야함 만약 이걸 제어하기 위해 unitcontroller이나 buildingcontroller에 필드를 추가해야하면 추가해주고 건물,유닛 선택 시 아웃라인이 꺼졌다 켜졌다하는 제어가 가능하도록 구현

## 조사 내용 (중요 - 대상이 처음 짐작과 달랐음)

- `mtrl_canopus-iii_set01-blue/green/red.mat`를 실제로 참조하는 프리팹은 `Assets/prefabs/Asset/*.prefab` (예: `struct_Barracks_A_yup.prefab`)인데, 이 폴더는 어떤 씬이나 `BuildingDataSO`/`UnitDataSO`에서도 참조되지 않는 **미사용 프리팹**이었다.
- 실제 게임에서 쓰이는 진짜 유닛/건물 프리팹은 `Assets/prefabs/NTA/Building/*`, `Assets/prefabs/NTA/Unit/**/*` (MainBase, SupplyDepot, Tier1~3, Lab, BaseStructure / Worker Drone, Assault Trooper, Scout Drone, Pulsar Tank, Ranger Infantry Fighting Vehicle, Firehawk, Guardian Drone - 코드의 `BuildingID`/`UnitID`, UI 아이콘 이름과 정확히 일치)이고, 이것들은 이미 `mtrl_canopus-iii` 계열이 아니라 **`Assets/Material/White.mat`, `Green.mat`, `Blue.mat`** 라는 단색(텍스처 없음) 머티리얼 3개를 공유해서 쓰고 있다 (doc 0071 "canopus materials broken in urp" 이후 대체된 것으로 보임).
- 즉 "지금 화면에 보이는 유닛/건물 메쉬"에 적용하려면, 실제로 바꿔야 할 대상은 `mtrl_canopus-iii_set01-*.mat`이 아니라 **`Assets/Material/White.mat` / `Green.mat` / `Blue.mat`** 3개다. 사용자 확인 결과도 "현재 유닛들이 보여지는 메쉬 모델링"에 적용해달라는 것이므로 이 3개가 맞는 대상.
- 이 3개 머티리얼은 `testShader.mat`이 원래 쓰던 것과 똑같은 `Universal Render Pipeline/Lit` 쉐이더(guid `933532a4fcc9baf4fa0491de14d08ed7`)를 쓰고, 텍스처 없이 `_BaseColor`/`_Color`만 다르다(흰색/초록/파랑 틴트). 따라서 doc 0093과 동일한 패턴(쉐이더 guid만 `Custom/Outline`으로 교체 + 아웃라인 프로퍼티 추가)을 그대로 적용하면 되고, 각 머티리얼의 기존 `_BaseColor` 틴트는 그대로 유지된다.
- 선택 시에만 아웃라인을 켜려면(doc 0094 설계), `White/Green/Blue.mat`은 **여러 유닛/건물이 동시에 공유**하는 머티리얼이므로, 머티리얼 자체의 값을 바꾸면 안 되고(그러면 선택 안 한 다른 유닛까지 같이 켜짐) 유닛/건물별로 `MaterialPropertyBlock`을 통해 렌더러 인스턴스 단위로 켜고 꺼야 한다.
- `UnitController.SelectUnit()`/`DeselectUnit()` (`UnitController.cs:316-324`), `BuildingController.SelectBuilding()`/`DeselecBuilding()` (`BuildingController.cs:311-322`)가 이미 각각 `unitMarker`/`buildingMarker` 활성화로 선택 표시를 하고 있어서, 같은 자리에 아웃라인 토글을 추가하면 된다.
- `BaseStructure`(건설 중인 건물)는 이번 범위에서 제외한다 - 완공 건물(BuildingController)과 유닛(UnitController)만 우선 적용하고, 건설 중 표시가 필요하면 별도 요청으로 처리.

## 계획한 코드 변경

### 1. `Assets/Shader/Outline.shader` - on/off 토글 프로퍼티 추가 (doc 0094 설계 반영)

기존 코드:
```hlsl
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
```

변경 코드:
```hlsl
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
        [Toggle] _OutlineEnabled ("Outline Enabled", Float) = 0
```

기존 코드 (Outline 패스):
```hlsl
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(positionOS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
```

변경 코드:
```hlsl
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineEnabled;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionOS = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(positionOS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 꺼져 있으면 컬러/뎁스를 아예 기록하지 않는다 (너비를 0으로 낮추는 방식보다 겹쳐그리기 z-fighting 위험이 없음).
                clip(_OutlineEnabled - 0.5);
                return _OutlineColor;
            }
```
ForwardLit 패스의 `CBUFFER_START(UnityPerMaterial)`에도 동일하게 `float _OutlineEnabled;`를 추가해서(값을 안 쓰더라도) 두 패스의 UnityPerMaterial 레이아웃을 일치시킨다.

### 2. `Assets/Material/White.mat`, `Green.mat`, `Blue.mat` - 쉐이더 교체 + 아웃라인 프로퍼티 추가

세 파일 모두 동일한 패턴(색만 다름). `White.mat` 기준으로 예시:

기존 코드:
```yaml
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
```
변경 코드:
```yaml
  m_Shader: {fileID: 4800000, guid: 5e2a731fca4e4d0b8f19c6d3a7b2e001, type: 3}
```

`m_Floats`에 추가:
```yaml
    - _OutlineEnabled: 0
    - _OutlineWidth: 0.02
```
`m_Colors`에 추가:
```yaml
    - _OutlineColor: {r: 0, g: 0, b: 0, a: 1}
```
기존 `_BaseColor`/`_Color`(흰색/초록/파랑)는 그대로 둔다 - 새 쉐이더도 같은 이름의 `_BaseColor`를 읽으므로 색은 유지된다. `Green.mat`/`Blue.mat`도 동일하게 `m_Shader` guid만 바꾸고 `_OutlineEnabled`/`_OutlineWidth`/`_OutlineColor`를 추가한다 (각자의 `_BaseColor`는 손대지 않음).

### 3. `Assets/Scripts/Unit/UnitController.cs` - 선택 시 아웃라인 토글

필드 추가 (`unitMarker` 선언 근처):
```csharp
    [SerializeField]
    private GameObject unitMarker;
```
변경 코드 (필드 추가):
```csharp
    [SerializeField]
    private GameObject unitMarker;

    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private Renderer[] outlineRenderers;
    private MaterialPropertyBlock outlinePropertyBlock;
```

`Awake()`에 렌더러 캐싱 추가:

기존 코드 (`UnitController.cs:168-182`):
```csharp
    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();

        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            defaultAgentRadius = navMeshAgent.radius;
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position);
            isMovingAirUnit = true;
        }
```
변경 코드:
```csharp
    private void Awake()
    {
        isWorker = CompareTag("Worker");
        attackRange = GetComponentInChildren<AttackRange>();
        outlineRenderers = GetComponentsInChildren<Renderer>(true);
        outlinePropertyBlock = new MaterialPropertyBlock();

        if (!isAirUnit)
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            defaultAgentRadius = navMeshAgent.radius;
        }
        else
        {
            targetPosition = AirTargetPosition(transform.position);
            isMovingAirUnit = true;
        }
```

`SelectUnit()`/`DeselectUnit()` 수정:

기존 코드 (`UnitController.cs:316-324`):
```csharp
    public void SelectUnit()
    {
        unitMarker.SetActive(true);
    }

    public void DeselectUnit()
    {
        unitMarker.SetActive(false);
    }
```
변경 코드:
```csharp
    public void SelectUnit()
    {
        unitMarker.SetActive(true);
        SetOutline(true);
    }

    public void DeselectUnit()
    {
        unitMarker.SetActive(false);
        SetOutline(false);
    }

    // 머티리얼 자체(White/Green/Blue.mat)는 다른 유닛/건물과 공유되므로 값을 직접 바꾸지 않고,
    // MaterialPropertyBlock으로 이 렌더러 인스턴스에만 아웃라인을 켜고 끈다.
    private void SetOutline(bool enabled)
    {
        foreach (Renderer renderer in outlineRenderers)
        {
            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
```

### 4. `Assets/Scripts/Building/BuildingController.cs` - 선택 시 아웃라인 토글 (유닛과 동일 패턴)

필드 추가:
```csharp
    private static readonly int OutlineEnabledID = Shader.PropertyToID("_OutlineEnabled");
    private Renderer[] outlineRenderers;
    private MaterialPropertyBlock outlinePropertyBlock;
```

`Start()`에 캐싱 추가:

기존 코드 (`BuildingController.cs:74-92`):
```csharp
    void Start()
    {
        buildingMarker.SetActive(false);

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
```
변경 코드:
```csharp
    void Start()
    {
        buildingMarker.SetActive(false);
        outlineRenderers = GetComponentsInChildren<Renderer>(true);
        outlinePropertyBlock = new MaterialPropertyBlock();

        // 전역 RTSUnitController에 자신을 등록해 선택/관리 대상이 되게 한다.
        rtsController = FindFirstObjectByType<RTSUnitController>();
        placementSystem = FindFirstObjectByType<PlacementSystem>();
```

`SelectBuilding()`/`DeselecBuilding()` 수정:

기존 코드 (`BuildingController.cs:311-322`):
```csharp
    public void SelectBuilding()
    {
        //Debug.Log(name + " ????");
        buildingMarker.SetActive(true);
    }

    // 건물 선택 해제 시 마커를 비활성화한다.
    public void DeselecBuilding()
    {
        //Debug.Log(name + " ???? ????");
        buildingMarker.SetActive(false);
    }
```
변경 코드:
```csharp
    public void SelectBuilding()
    {
        buildingMarker.SetActive(true);
        SetOutline(true);
    }

    // 건물 선택 해제 시 마커를 비활성화한다.
    public void DeselecBuilding()
    {
        buildingMarker.SetActive(false);
        SetOutline(false);
    }

    // 머티리얼 자체(White/Green/Blue.mat)는 다른 유닛/건물과 공유되므로 값을 직접 바꾸지 않고,
    // MaterialPropertyBlock으로 이 렌더러 인스턴스에만 아웃라인을 켜고 끈다.
    private void SetOutline(bool enabled)
    {
        foreach (Renderer renderer in outlineRenderers)
        {
            renderer.GetPropertyBlock(outlinePropertyBlock);
            outlinePropertyBlock.SetFloat(OutlineEnabledID, enabled ? 1f : 0f);
            renderer.SetPropertyBlock(outlinePropertyBlock);
        }
    }
```
(불필요한 디버그 `//Debug.Log(...)` 주석 두 줄은 정리 차원에서 함께 제거.)

## 영향받는 파일 (승인 시)

- `Assets/Shader/Outline.shader` (토글 프로퍼티 추가)
- `Assets/Material/White.mat`, `Assets/Material/Green.mat`, `Assets/Material/Blue.mat` (쉐이더 교체 + 아웃라인 프로퍼티 추가)
- `Assets/Scripts/Unit/UnitController.cs` (필드 3개 + `Awake()` 캐싱 + `SelectUnit()`/`DeselectUnit()`/`SetOutline()`)
- `Assets/Scripts/Building/BuildingController.cs` (필드 3개 + `Start()` 캐싱 + `SelectBuilding()`/`DeselecBuilding()`/`SetOutline()`)

## 참고 / 확인 필요 사항

- White/Green/Blue.mat은 유닛/건물마다 공유되는 자산이라, 쉐이더를 바꾸면 **이 세 머티리얼을 쓰는 모든 오브젝트**(NTA 폴더의 모든 유닛/건물)가 한꺼번에 영향을 받는다 - 이번 요청 범위(유닛/건물 전체에 아웃라인 기능 추가)와 일치한다.
- 아웃라인 두께(`_OutlineWidth` 0.02)는 모든 유닛/건물에 공통 적용되는데, 유닛 크기가 상당히 다양해서(드론 vs 탱크 vs 건물) 큰 오브젝트는 상대적으로 얇게, 작은 오브젝트는 두껍게 보일 수 있다. 필요하면 나중에 유닛별 조정이나 화면공간 두께 보정을 추가할 수 있음(이번 범위 밖).
- `BaseStructure`(건설 중 건물)는 이번 변경에 포함하지 않음.
- 아직 프로젝트 파일에는 반영하지 않음 - 승인 시 위 내용 그대로 적용 예정.
