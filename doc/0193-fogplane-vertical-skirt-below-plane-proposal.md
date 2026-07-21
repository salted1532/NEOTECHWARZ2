# 0193 - Fog Plane 아래쪽(높이 방향)도 검은색으로 채우기 - 수정 제안

## 요청

"cs fog war를 조금 고쳐서 fog plane 아래로도 검은색으로 처리 되로록 할 수 있어?" — 현재 fog plane이
평면 한 장이라서, 그 평면의 Y높이보다 아래(또는 지형 높이 변화가 있는 곳)는 안개로 덮이지 않고
그대로 보이는 문제를 고쳐달라는 요청.

## 원인

`Assets/AssetFolder/AOSFogWar/csFogWar.cs`의 `InitializeFog()`(380~408번 줄)에서 fog plane을
`GameObject.CreatePrimitive(PrimitiveType.Plane)`로 생성한다. Unity의 기본 Plane 프리미티브는
**두께가 없는 한 장짜리 평면이고, 한쪽 면(윗면, +Y 방향)만 렌더링되는 단면(single-sided) 메쉬**다.
평면은 `levelMidPoint.y + fogPlaneHeight` 높이에 지도 전체 크기로 깔린다(`UpdateFog()`도 매 프레임
같은 Y에 위치를 다시 맞춤).

이 게임은 언덕 2단(Layer1/Layer2 태그, `CameraControl.cs`의 지형 단 로직 참고)처럼 지형 높이 변화가
있는데, 평면 형태의 fog는 딱 한 Y 높이에서만 존재하므로:
- fog plane보다 **높은 지형**(언덕 위)은 카메라 시점에서 fog plane보다 카메라에 더 가깝게 그려져서
  fog plane 자체가 그 뒤에 가려져(depth 상 안 그려짐) 안개 없이 원래 지형이 그대로 보임.
- fog plane보다 **낮은 지형**(경사면 아래, 언덕 사이 저지대)도 카메라가 살짝 비스듬한 각도로 보고
  있을 때, 시선이 fog plane의 Y를 스치듯 지나가거나 언덕에 가려 fog plane이 안 그려지는 지점 뒤로
  내려가면서 안개 없이 노출됨.
- 게다가 Plane이 단면 메쉬라, 혹시라도 시야가 fog plane의 아랫면(뒤쪽)에서 접근하면 아예 안 그려져서
  뚫려 보인다.

즉 "fog plane 아래로도 검은색 처리"의 근본 원인은 fog가 두께 없는 2D 평면 한 장이라, 지형의 높이
변화 앞에서 안개가 항상 완전히 덮지 못하는 구조적 한계다.

## 계획된 코드 변경

`PrimitiveType.Plane` 대신 `PrimitiveType.Cube`를 사용해서, fog를 "윗면은 기존과 같은 높이에 있고
아래로 두꺼운 블록"으로 바꾼다. 옆면(4개 측면)도 같은 재질/텍스처를 쓰는 실제 표면이 생기므로, 시야가
비스듬하거나 지형이 fog plane 아래로 파여 있어도 옆면이 검은 벽처럼 막아준다. Cube는 6면 모두
법선이 바깥을 향해 있어 단면 렌더링 문제도 자연히 해결됨.

새 필드 추가 (Inspector에서 조절 가능, "이 안개 블록이 fogPlaneHeight를 기준으로 아래로 얼마나
두껍게 내려가는지"):
```csharp
[SerializeField]
[Range(1, 200)]
private float fogPlaneDepth = 50;
```

`InitializeFog()`

Before:
```csharp
        private void InitializeFog()
        {
            fogPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);

            fogPlane.name = "[RUNTIME] Fog_Plane";

            fogPlane.transform.position = new Vector3(
                levelMidPoint.position.x,
                levelMidPoint.position.y + fogPlaneHeight,
                levelMidPoint.position.z);

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
        }
```

After:
```csharp
        private void InitializeFog()
        {
            fogPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);

            fogPlane.name = "[RUNTIME] Fog_Plane";

            fogPlane.transform.position = new Vector3(
                levelMidPoint.position.x,
                levelMidPoint.position.y + fogPlaneHeight - (fogPlaneDepth / 2.0f),
                levelMidPoint.position.z);

            // Cube는 기본 크기가 1x1x1이라 Plane(기본 10x10)과 달리 /10.0f 보정이 필요 없다.
            // Y는 fogPlaneDepth를 그대로 두께로 사용해, 윗면이 기존과 동일하게 fogPlaneHeight에 오도록 한다.
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

`UpdateFog()`의 위치 갱신 부분도 동일하게 맞춘다.

Before:
```csharp
        private void UpdateFog()
        {
            fogPlane.transform.position = new Vector3(
                levelMidPoint.position.x,
                levelMidPoint.position.y + fogPlaneHeight,
                levelMidPoint.position.z);
```

After:
```csharp
        private void UpdateFog()
        {
            fogPlane.transform.position = new Vector3(
                levelMidPoint.position.x,
                levelMidPoint.position.y + fogPlaneHeight - (fogPlaneDepth / 2.0f),
                levelMidPoint.position.z);
```

## 참고/트레이드오프

- 옆면(4개 측면)은 윗면처럼 타일 단위로 정확히 매핑되지 않고, 같은 안개 텍스처가 세로로 늘어나서
  입혀진다. 이미 밝혀진(투명한) 구간과 안 밝혀진(불투명 검정) 구간의 경계가 옆면에서는 약간 흐릿하게
  보일 수 있지만, 옆면은 "가림벽" 역할이 목적이라 시각적으로 크게 티나지 않을 것으로 예상.
- `fogPlaneDepth` 기본값은 50으로 넉넉하게 잡았음 — 언덕/저지대 높이 차가 이보다 크면 Inspector에서
  값을 올려야 함. 반대로 너무 깊게 잡으면 의미 없는 폴리곤이 늘어날 뿐이라(오브젝트 1개짜리라 성능
  영향은 미미) 크게 걱정할 부분은 아님.
- Collider가 `MeshCollider`에서 `BoxCollider`로 바뀜(Cube 프리미티브의 기본 컴포넌트) — 기존 코드도
  바로 `.enabled = false`로 꺼두므로 동작에 영향 없음.
- `csFogWar.cs`는 서드파티 에셋(AOSFogWar) 코드라 직접 수정하면 나중에 에셋을 업데이트할 때 덮어쓰일
  수 있음 — 필요하면 이 사실을 감안해서 진행.

## 영향받는 파일

- `Assets/AssetFolder/AOSFogWar/csFogWar.cs` (`InitializeFog()`, `UpdateFog()`, `fogPlaneDepth` 필드 추가)

## 결과

사용자 확인 후 위 제안대로 그대로 적용 완료. 추가로 "큐브의 높이를 내가 조절할수 있도록 해줘"
요청은 이미 계획에 포함된 `fogPlaneDepth` 필드(`[SerializeField] [Range(1, 200)]`)로 충족됨 —
Inspector의 "Fog Properties" 섹션에서 `Fog Plane Depth` 값으로 직접 조절 가능.
