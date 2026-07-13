# 0096 - White/Green/Blue.mat에서 Outline 쉐이더 제거 (제안)

**날짜:** 2026-07-13

## 요청 내용

> Assets/Material/White.mat/Green.mat/Blue.mat 등에는 outline 쉐이더를 뺴줘

doc 0095에서 이 세 머티리얼의 쉐이더를 `Universal Render Pipeline/Lit`에서 `Custom/Outline`으로 바꿨는데, 이걸 다시 되돌려달라는 요청.

## 계획한 변경

세 파일 모두 doc 0095 이전 상태로 되돌린다: 쉐이더 guid를 원래 URP Lit(`933532a4fcc9baf4fa0491de14d08ed7`)로 복원하고, 그때 추가했던 `_OutlineEnabled`/`_OutlineWidth`/`_OutlineColor` 프로퍼티 항목도 함께 제거한다(안 쓰는 값으로 남겨봐야 혼란만 주므로).

`Assets/Shader/Outline.shader` 자체와 `UnitController.cs`/`BuildingController.cs`의 `SetOutline()` 토글 코드는 **그대로 둔다** - 요청이 "이 세 머티리얼에서"로 한정돼 있고, 스크립트 쪽은 셰이더가 안 바뀌어도(=`_OutlineEnabled` 프로퍼티가 없는 셰이더가 되어도) `MaterialPropertyBlock.SetFloat`가 조용히 무시되기만 할 뿐 에러 없이 안전하게 동작한다. 다만 이 상태에서는 유닛/건물 선택 시 아웃라인이 더 이상 보이지 않는다(테두리 기능 자체가 비활성화되는 셈).

### `Assets/Material/White.mat`

기존 코드:
```yaml
  m_Shader: {fileID: 4800000, guid: 5e2a731fca4e4d0b8f19c6d3a7b2e001, type: 3}
```
변경 코드:
```yaml
  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}
```
`m_Floats`에서 제거:
```yaml
    - _OutlineEnabled: 0
    - _OutlineWidth: 0.02
```
`m_Colors`에서 제거:
```yaml
    - _OutlineColor: {r: 0, g: 0, b: 0, a: 1}
```

`Green.mat`, `Blue.mat`도 동일하게 (각자의 `_BaseColor`는 그대로 유지).

## 영향받는 파일 (승인 시)

- `Assets/Material/White.mat`
- `Assets/Material/Green.mat`
- `Assets/Material/Blue.mat`

## 참고

- `Assets/Shader/testShader.mat`(doc 0093)은 이번 요청 대상이 아니라 그대로 Outline 쉐이더를 유지한다.
- 아직 프로젝트 파일에는 반영하지 않음 - 승인 시 위 내용 그대로 적용 예정.
