# 0496 - 자원(광물) 설정 정보 Docs/ 정식 등록

## 요청 내용
"광물 관련 설정 정보도 정리해서 Docs에다가 문서로 작성해줘"

## 조사 내용
- doc/0493(명칭/컨셉 확정)·doc/0494(로컬라이제이션 구현+검증)에서 이미 아이로나이트 광석(Ore)/
  페트로나이트(Gas) 관련 작업을 전부 마쳤으므로, 그 결과를 스포어 브루드(doc/0495, `Docs/
  SporeBrood.md`)와 동일한 방식으로 `Docs/`에 정식 등록.
- `Docs/ResourceManager.md`/`Docs/ResourceNode.md`(스크립트별 상세 문서)는 이미 존재하지만
  이번 세션에서 추가한 `ResourceNode.GetName()`/`GetDescription()` 메소드가 반영되어 있지 않았음 -
  최신화 필요.
- `Assets/Scripts/Unit/UnitController.cs:206`에서 `amountPerTrip = 5`(자원 채취 1회당 캐가는 양)
  확인 - 새 문서에 채취 메커니즘을 요약할 때 참고.

## 변경 사항
- **신규**: `Docs/ResourceSystem.md` - 아이로나이트 광석/페트로나이트 컨셉, `ResourceManager`(팀
  저장소)/`ResourceNode`(채취 지점) 동작 개요, 이름/설명 조회 방식, 로컬라이제이션 키 표, 구현 상태
  참고(프리팹 파일명은 안 바뀜 등)를 한 문서로 정리. 스크립트별 상세는 기존
  `ResourceManager.md`/`ResourceNode.md`로 링크.
- **수정**: `Docs/ResourceNode.md` 메소드 표에 `GetName()`/`GetDescription()` 행 추가.
- **수정**: `README.md`의 "유닛/건물 수치 문서" 목록에 `Docs/ResourceSystem.md` 링크 추가.

## 요약
doc/0493/0494에서 이미 구현된 자원 명칭·설명 작업을 `Docs/ResourceSystem.md`로 정식 문서화하고,
누락돼 있던 `ResourceNode.md`의 신규 메소드도 최신화함.

## 변경된 파일
- `Docs/ResourceSystem.md` (신규)
- `Docs/ResourceNode.md` (메소드 표 갱신)
- `README.md` (Docs 목록에 링크 추가)
