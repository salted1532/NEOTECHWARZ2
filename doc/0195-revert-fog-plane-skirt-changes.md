# 0195 - Fog Plane 아래쪽 처리 관련 변경 전부 되돌리기

## 요청

"cs fog war를 조금 고쳐서 fog plane 아래로도 검은색으로 처리 되로록 할 수 있어? 이 명령을 시키기
전으로 되돌려줘" — [[0193]](fog plane을 Cube로 교체)과 [[0194]](윗면을 다시 Plane으로 되돌리고
리빌 텍스처와 무관한 스커트 벽 4개 추가)에서 만든 모든 변경을 되돌려서, 그 요청이 있기 전 상태로
복원해달라는 요청.

## 되돌린 대상

`Assets/AssetFolder/AOSFogWar/csFogWar.cs`에서 이번 세션(0193, 0194) 동안 만든 변경 전부:

1. `fogPlaneDepth` 필드 (0193에서 추가, 0194에서 툴팁만 수정) → 제거.
2. `fogRoot` 필드 (0194에서 추가) → 제거.
3. `InitializeFog()` — `fogPlane`을 다시 `PrimitiveType.Plane`으로, `fogRoot` 없이 직접
   `levelMidPoint` 기준으로 위치/스케일을 계산하는 원래 방식으로 복원. `CreateFogSkirt()`/
   `CreateSkirtWall()` 헬퍼(0194에서 추가된 스커트 벽 생성 로직) → 제거.
4. `UpdateFog()` — `fogRoot.transform.position` 갱신 대신, `fogPlane.transform.position`을
   직접 갱신하는 원래 코드로 복원.

git이 이 환경 PATH에 없어(`git` 명령 인식 안 됨) `git checkout`으로 되돌리지 못하고, 이번 세션
대화 초반에 읽어둔 원본 코드(0192 질문에 답하기 전 최초 Read 결과)를 기준으로 Edit 툴로 한 줄씩
수동 복원함. 복원 후 `InitializeFog()` 전체를 다시 읽어 원본과 동일한지 확인.

## 결과

`csFogWar.cs`가 0193 이전 상태로 완전히 복원됨 — `fogPlaneDepth`/`fogRoot`/스커트 벽 관련 코드
전혀 없음, `fogPlane`은 단순 `PrimitiveType.Plane`.

## 영향받는 파일

- `Assets/AssetFolder/AOSFogWar/csFogWar.cs` (0193/0194에서 만든 변경 전부 되돌림)
