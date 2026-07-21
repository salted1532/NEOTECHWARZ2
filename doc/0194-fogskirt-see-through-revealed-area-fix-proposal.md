# 0194 - Fog 큐브의 밝혀진 부분을 통해 옆면 내부가 비쳐 보이는 문제 - 수정 제안

## 요청

"그래도 밝혀진 부분을 통해서 카메라 확대하면 그 안을 옆으로 들여다 볼수 있어" — [[0193]]에서
fog plane을 `PrimitiveType.Plane`(평면)에서 `PrimitiveType.Cube`(두꺼운 블록)로 바꿨는데, 이미
밝혀진(리빌된) 지점 근처에서 카메라를 확대(줌인)하면 큐브 옆면을 뚫고 안쪽이 들여다보이는 새로운
문제가 생김.

## 원인

`Assets/AssetFolder/AOSFogWar/Shaders/FogPlane.shader`를 확인:

```
Blend SrcAlpha OneMinusSrcAlpha
CULL BACK
ZWrite OFF
ZTest Always
```

- `ZTest Always` + `ZWrite Off`: 이 셰이더는 원래부터 "실제 3D 깊이와 무관하게 항상 화면에 그려지는"
  방식으로 설계되어 있음. 즉 평면이든 큐브든, 지형이 앞에 있든 뒤에 있든 상관없이 자기 화면 영역
  안에서는 무조건 덧그려짐.
- `CULL BACK`: 각 면은 바깥쪽(법선 방향)에서만 보이는 단면 렌더링. Cube는 윗면/옆면 4개가 모두
  이 셰이더를 공유하는 **하나의 재질**을 쓰고 있음.
- 텍스처(`_MainTex`)는 `levelDimensionX x levelDimensionY` 크기의 타일별 리빌 상태 텍스처인데,
  Unity Cube 프리미티브는 윗면과 옆면 4개에 **같은 텍스처를 각자 다르게 스트레치해서** 입힌다(0193
  제안 시점에 이미 이 한계를 트레이드오프로 언급했었음). 그래서 옆면의 특정 지점이 실제로는 아직
  안 밝혀진 타일 옆인데도, 스트레치된 좌표가 우연히 텍스처의 "밝혀짐(투명)" 픽셀을 가리키면 그
  옆면도 같이 투명해져 버림.
- 결과적으로 카메라가 밝혀진 영역 근처에서 비스듬히 확대해서 보면, 윗면(투명)과 옆면(잘못된 매핑으로
  우연히 같이 투명해진 부분)이 동시에 뚫려서, 원래는 항상 막혀 있어야 할 큐브 내부(사실상 반대편
  옆면 안쪽 등 아무것도 없는 빈 공간)가 카메라에 노출됨.

핵심 문제: **옆면이 "항상 불투명해야 하는 벽"인데, 윗면과 같은 텍스처/재질을 공유하고 있어서 옆면도
리빌 상태에 따라 투명해질 수 있다는 것.**

## 계획된 코드 변경

윗면과 옆면을 아예 분리한다.

- **윗면**: 원래(0193 이전)의 얇은 `PrimitiveType.Plane`으로 되돌린다. 두께가 없으니 "속이 비쳐
  보이는" 문제 자체가 성립하지 않음(원래도 잘 동작하던 방식).
- **옆면(skirt)**: 별도의 오브젝트 4개(`PrimitiveType.Cube`, 맵 둘레 벽)로 분리하고, 리빌 텍스처를
  아예 붙이지 않은 채 `_Color`만 **항상 완전 불투명한 fogColor**로 고정한 재질을 사용한다. 리빌
  상태와 완전히 무관하므로 절대 투명해지지 않고, 항상 검은 벽으로 막아준다.
- `levelMidPoint`가 움직일 때(매 프레임 `UpdateFog()`에서 위치 갱신) 윗면+옆면 4개를 각각 따로
  움직이지 않도록, 빈 부모 오브젝트(`fogRoot`)를 하나 두고 그 아래 자식으로 배치한다. 매 프레임
  `fogRoot`의 위치만 `levelMidPoint`를 따라가면 자식들은 로컬 오프셋 그대로 자동으로 같이 움직임.

`Assets/AssetFolder/AOSFogWar/csFogWar.cs`

새 필드:
```csharp
private GameObject fogRoot = null; // fogPlane + 옆면 skirt를 묶는 빈 부모. levelMidPoint를 따라 이동.
```

`InitializeFog()`

Before (0193에서 바꾼 상태):
```csharp
        private void InitializeFog()
        {
            fogPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);

            fogPlane.name = "[RUNTIME] Fog_Plane";

            fogPlane.transform.position = new Vector3(
                levelMidPoint.position.x,
                levelMidPoint.position.y + fogPlaneHeight - (fogPlaneDepth / 2.0f),
                levelMidPoint.position.z);

            fogPlane.transform.localScale = new Vector3(
                levelDimensionX * unitScale,
                fogPlaneDepth,
                levelDimensionY * unitScale);

            fogPlaneTextureLerpTarget = new Texture2D(levelDimensionX, levelDimensionY);
            fogPlaneTextureLerpBuffer = new Texture2D(levelDimensionX, levelDimensionY);

            fogPlaneTextureLerpBuffer.wrapMode = TextureWrapMode.Clamp;

            fogPlaneTextureLerpBuffer.filterMode = FilterMode.Bilinear;

            fogPlane.GetComponent<MeshRenderer>().material = new Material(fogPlaneMaterial);

            fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", fogPlaneTextureLerpBuffer);

            fogPlane.GetComponent<BoxCollider>().enabled = false;
        }
```

After:
```csharp
        private void InitializeFog()
        {
            fogRoot = new GameObject("[RUNTIME] Fog_Root");
            fogRoot.transform.position = levelMidPoint.position;

            fogPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);

            fogPlane.name = "[RUNTIME] Fog_Plane";

            fogPlane.transform.SetParent(fogRoot.transform, false);

            fogPlane.transform.localPosition = new Vector3(0, fogPlaneHeight, 0);

            fogPlane.transform.localScale = new Vector3(
                (levelDimensionX * unitScale) / 10.0f,
                1,
                (levelDimensionY * unitScale) / 10.0f);

            fogPlaneTextureLerpTarget = new Texture2D(levelDimensionX, levelDimensionY);
            fogPlaneTextureLerpBuffer = new Texture2D(levelDimensionX, levelDimensionY);

            fogPlaneTextureLerpBuffer.wrapMode = TextureWrapMode.Clamp;

            fogPlaneTextureLerpBuffer.filterMode = FilterMode.Bilinear;

            fogPlane.GetComponent<MeshRenderer>().material = new Material(fogPlaneMaterial);

            fogPlane.GetComponent<MeshRenderer>().material.SetTexture("_MainTex", fogPlaneTextureLerpBuffer);

            fogPlane.GetComponent<MeshCollider>().enabled = false;

            CreateFogSkirt();
        }

        // 지형 높이 차(언덕/저지대)로 fog plane 아래가 뚫려 보이는 것을 막기 위한 벽 4개.
        // 리빌 텍스처를 쓰지 않고 색만 완전 불투명으로 고정해서, 밝혀진 타일 옆에서도 절대
        // 투명해지지 않도록 한다(윗면과 재질을 공유하면 스트레치된 UV 때문에 옆면이 같이
        // 투명해져 속이 비쳐 보이는 문제가 있었음).
        private void CreateFogSkirt()
        {
            Material skirtMaterial = new Material(fogPlaneMaterial);
            skirtMaterial.SetColor("_Color", new Color(fogColor.r, fogColor.g, fogColor.b, 1f));

            float mapWidth = levelDimensionX * unitScale;
            float mapDepth = levelDimensionY * unitScale;
            float wallThickness = Mathf.Max(unitScale, 1f);
            float skirtCenterY = fogPlaneHeight - (fogPlaneDepth / 2.0f);

            CreateSkirtWall(skirtMaterial, new Vector3(0, skirtCenterY, mapDepth / 2.0f), new Vector3(mapWidth, fogPlaneDepth, wallThickness));
            CreateSkirtWall(skirtMaterial, new Vector3(0, skirtCenterY, -mapDepth / 2.0f), new Vector3(mapWidth, fogPlaneDepth, wallThickness));
            CreateSkirtWall(skirtMaterial, new Vector3(mapWidth / 2.0f, skirtCenterY, 0), new Vector3(wallThickness, fogPlaneDepth, mapDepth));
            CreateSkirtWall(skirtMaterial, new Vector3(-mapWidth / 2.0f, skirtCenterY, 0), new Vector3(wallThickness, fogPlaneDepth, mapDepth));
        }

        private void CreateSkirtWall(Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);

            wall.name = "[RUNTIME] Fog_Skirt";

            wall.transform.SetParent(fogRoot.transform, false);

            wall.transform.localPosition = localPosition;

            wall.transform.localScale = localScale;

            wall.GetComponent<MeshRenderer>().material = material;

            wall.GetComponent<BoxCollider>().enabled = false;
        }
```

`UpdateFog()`

Before:
```csharp
        private void UpdateFog()
        {
            fogPlane.transform.position = new Vector3(
                levelMidPoint.position.x,
                levelMidPoint.position.y + fogPlaneHeight - (fogPlaneDepth / 2.0f),
                levelMidPoint.position.z);
```

After:
```csharp
        private void UpdateFog()
        {
            fogRoot.transform.position = levelMidPoint.position;
```

## 유지되는 것

- `fogPlaneDepth` 필드는 그대로 유지(스커트 벽의 두께/깊이를 조절하는 용도로 의미가 이어짐, Inspector에서
  계속 조절 가능).
- `fogPlaneTextureLerpTarget`/`Buffer` 갱신 로직(`UpdateFogPlaneTextureBuffer`/`Target`)은 손대지
  않음 — 여전히 `fogPlane`(윗면) 하나의 `MeshRenderer.material`만 갱신하고, 스커트 벽의 재질은
  최초 생성 시 한 번만 설정되고 이후 건드리지 않음(항상 불투명 고정).

## 영향받는 파일

- `Assets/AssetFolder/AOSFogWar/csFogWar.cs`

## 결과

사용자 확인 후 위 제안대로 그대로 적용 완료.
