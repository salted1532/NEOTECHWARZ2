# 0016 - RTS 그래픽 기법 리서치 및 개선 제안

**날짜:** 2026-07-07

## 요청 내용
RTS게임들의 그래픽적인 면(포스트프로세싱, SSR, 쉐이더 등)에서 어떤 효과/요소를 넣으면 더 좋은 그래픽으로 느껴지는지 조사하고, 현재 프로젝트(NEOTECHWARZ2)에 어떤 요소를 추가하면 좋을지 답변 요청. 코드 변경은 요청되지 않음 (조사/제안 답변만).

## 조사 방법
- 현재 프로젝트의 URP 설정 파일 직접 확인: `Assets/Settings/PC_RPAsset.asset`, `Assets/Settings/PC_Renderer.asset`, `Assets/Settings/DefaultVolumeProfile.asset`, `Packages/manifest.json`
- WebSearch로 RTS 그래픽 기법(포스트프로세싱, SSR, 쉐이더), Fog of War, 유닛 셀렉션/아웃라인/데칼/GPU 인스턴싱 관련 최신 자료 조사

## 현재 프로젝트 상태 (진단 결과)
- Unity 6 / URP 17.4.0, Forward+ 렌더링, HDR/MSAA On, Shadow Cascade 4단, Soft Shadow 지원, SRP Batcher 사용 중
- SSAO는 렌더러 피처로 이미 활성화 (Intensity 0.4)
- `DefaultVolumeProfile.asset`에 Bloom/DoF/MotionBlur/Vignette/ChromaticAberration/ColorGrading 등 표준 URP 포스트프로세싱 오버라이드가 존재하지만 값이 전부 0/기본값 — 사실상 비활성 상태 (추가가 아니라 튜닝이 필요)
- SSR(Screen Space Reflection) 렌더러 피처는 아직 없음
- GPU Resident Drawer가 꺼져 있음 (`m_GPUResidentDrawerMode: 0`) — 다수 유닛 처리에 중요한 Unity 6 기능
- `DefaultVolumeProfile.asset`에 `OutlineVolumeComponent`, `OasisFogVolumeComponent` 및 Unity 내부 테스트용 컴포넌트(`CopyPasteTestComponent1~3` 등)가 참조되어 있으나 실제 스크립트가 프로젝트에 없어 깨진 참조 상태 — 추후 정리 필요 (이번 세션에서는 수정하지 않음)

## 답변 요약 (제안된 그래픽 요소)
1. **성능 기반**: GPU Resident Drawer 활성화 (다수 유닛 처리 여유 확보, 다른 효과의 전제조건)
2. **기존 요소 튜닝**: SSAO 강화, Bloom/Color Grading 최소 세팅 활성화, TAA
3. **RTS 전용 가독성 이펙트**: 셀렉션 아웃라인 셰이더(스텐실/Sobel), 팀 컬러 마스크 셰이더, 선택 링 데칼, Fog of War 전용 RenderTexture 파이프라인
4. **환경/지형**: SSR(URP 17 내장 볼륨 오버라이드로 추가 가능), 스플랫맵/트라이플래너 지형 셰이더, Height/Volumetric Fog 완성, Decal Projector 활용
5. **비권장/주의**: Motion Blur, Depth of Field, Chromatic Aberration, Film Grain은 톱티어 RTS에서 대개 게임플레이 카메라에 꺼져 있음 (가독성/APM 저해) — 켜더라도 극히 미세하게, 또는 컷신 전용으로

우선순위 제안: GPU Resident Drawer → SSAO/Bloom/ColorGrading 튜닝 → 셀렉션 아웃라인 구현 → SSR/지형 셰이더 → Height Fog/Decal → Fog of War 파이프라인.

## 변경된 파일
없음 (리서치/제안 답변만 제공, 코드/에셋 변경 없음)

## 다음 단계 (사용자 선택 대기)
사용자가 위 제안 중 하나를 골라 실제 구현을 요청하면 진행 예정 (예: 셀렉션 아웃라인 렌더러 피처, SSR 렌더러 피처 추가 등).
