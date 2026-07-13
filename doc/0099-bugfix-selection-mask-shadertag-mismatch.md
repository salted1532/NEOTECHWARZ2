# 0099 - 버그수정: 선택 아웃라인이 전혀 안 보임 (ShaderTagId 불일치)

**날짜:** 2026-07-13

## 요청 내용

> 유닛 선택해도 외곽선이 안나타나는데 뭐 내가 연결해야하는게 있어?

## 원인

`SelectionOutlineFeature.cs`의 마스크 패스가 렌더러 리스트를 만들 때 쓰는 `ShaderTagId`를 임의로 지어낸 `"SelectionMask"`로 넣어놨었다:
```csharp
private static readonly ShaderTagId ShaderTag = new ShaderTagId("SelectionMask");
```
`DrawingSettings`에 `overrideMaterial`을 지정해도, 애초에 "이 렌더러를 리스트에 포함시킬지"는 **오버라이드 머티리얼이 아니라 렌더러가 실제로 물고 있는 원래 머티리얼의 쉐이더 패스 중 이 `ShaderTagId`(LightMode 태그)와 일치하는 패스가 있는지**로 걸러진다. 유닛/건물이 실제로 쓰는 `White.mat`/`Green.mat`/`Blue.mat`은 (doc 0096에서 되돌린 대로) `Universal Render Pipeline/Lit` 쉐이더라서 `"UniversalForward"` 태그를 가진 패스만 있고, `"SelectionMask"`라는 태그를 가진 패스는 세상 어디에도 없다. 그래서 필터링 결과 렌더러 리스트가 항상 비어 있었고, 마스크 텍스처엔 아무것도 안 그려졌고, 그 결과 컴포짓 패스가 경계를 하나도 못 찾아서 아웃라인이 전혀 안 보인 것 - 연결이 빠진 게 아니라 순수 코드 버그였다.

## 수정

`Assets/Scripts/Rendering/SelectionOutlineFeature.cs`

기존 코드:
```csharp
private static readonly ShaderTagId ShaderTag = new ShaderTagId("SelectionMask");
```
변경 코드:
```csharp
private static readonly ShaderTagId ShaderTag = new ShaderTagId("UniversalForward");
```
`White.mat`/`Green.mat`/`Blue.mat`이 실제로 갖고 있는 URP Lit의 `"UniversalForward"` 태그로 걸러서 렌더러 리스트에 정상적으로 포함시키고, 그렇게 걸러진 렌더러들을 (기존 로직 그대로) `SelectionMask` 머티리얼로 오버라이드해서 그린다.

## 확인 필요

- 여전히 이 환경에는 Unity 에디터가 없어 컴파일/실행 확인은 못 했다. 사용자가 에디터에서 다시 확인 필요.
- 그 외 "연결이 필요한 부분"은 없다 - `PC_Renderer.asset`에 머티리얼 참조까지 이미 다 연결해뒀고(doc 0098), 이번 건 순수 스크립트 버그였다.
- 그래도 안 보이면 다음을 확인: (1) Console에 컴파일 에러가 있는지, (2) `PC_Renderer.asset` 인스펙터에 "Selection Outline Feature"가 활성화(체크) 상태인지, (3) `_OutlineThickness`/`_OutlineColor`가 `OutlineComposite.mat`에 잘 들어있는지(시안색 기본값).

## 변경된 파일

- `Assets/Scripts/Rendering/SelectionOutlineFeature.cs`
