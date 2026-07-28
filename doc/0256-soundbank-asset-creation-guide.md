# 0256 - SoundBank 에셋 생성 방법 안내

**날짜:** 2026-07-28

## 요청 내용

> SoundBank는 어떤식으로 만들지?

doc/0255에서 구현한 `UnitSoundBankSO`/`BuildingSoundBankSO`/`GlobalVoiceBankSO`를 유니티 에디터에서
실제로 어떻게 만들고 채우고 유닛/건물에 연결하는지에 대한 질문. 코드 변경 없는 사용법 안내라서 바로 답변.

## 답변 요약

1. **생성**: Project 창 우클릭 → `Create > Sound > Unit Sound Bank` /
   `Building Sound Bank` / `Global Voice Bank` (전부 `[CreateAssetMenu]`로 이미 노출돼 있음).
   유닛/건물 하나당 에셋 하나, `<이름> SoundBank.asset` 네이밍 권장.
2. **클립 채우기**: 각 카테고리(`attackSFX`, `selectVoice` 등)는 `SoundClipSet` - `clips` 리스트에
   오디오 파일을 드래그해서 여러 개 넣으면 재생 시 랜덤으로 하나를 고른다. `volumeScale`/`pitchVariance`는
   선택사항(기본값 그대로 둬도 됨). 안 쓰는 카테고리는 비워두면 코드가 알아서 스킵.
3. **연결**: `New Unit Data SO.asset`/`OC Unit Data SO.asset`/`New Building Data SO.asset`의 해당
   유닛/건물 항목 `Sound Bank` 필드에 드래그. `GlobalVoiceBankSO`는 씬의 `SoundManager` 컴포넌트
   인스펙터 `Global Voice Bank` 필드에 1개만 연결.

프로젝트 코드는 변경하지 않음 (순수 사용법 안내).

## 변경된 파일

없음.
