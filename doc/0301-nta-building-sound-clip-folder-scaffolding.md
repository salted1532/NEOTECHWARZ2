# 0301 - NTA 건물 사운드 클립 폴더 스캐폴딩

날짜: 2026-07-30

## 요청 내용

"NTA 건물폴더에도 각 이름별로 폴더 만들어줄래"

## 조사 내용

- `NTA Building Data SO.asset`의 건물 6종 이름(앞뒤 공백 포함된 원본 값 트리밍): `CommandCenter`, `SupplyDepot`, `Barracks`, `Factory`, `Spaceport`, `Lab`.
- `Assets/Sound/NTA/Building/`는 현재 하위 폴더 없이 비어 있음 (doc/0299 조사 당시 확인한 상태 그대로).
- 방금(doc/0300) OC 건물 6종에 이름별 폴더 + `SFX`/`Voice` 하위 폴더를 만든 것과 동일한 구조를 NTA 건물에도 적용할지 확인 → "SFX/Voice도 함께" 선택.

## 코드 변경 (적용 완료 - 빈 폴더만 생성, 에셋/코드 변경 없음)

`Assets/Sound/NTA/Building/` 아래에 건물별 폴더 6개, 각각 `SFX`, `Voice` 하위 폴더:

```
Assets/Sound/NTA/Building/CommandCenter/SFX
Assets/Sound/NTA/Building/CommandCenter/Voice
Assets/Sound/NTA/Building/SupplyDepot/SFX
Assets/Sound/NTA/Building/SupplyDepot/Voice
Assets/Sound/NTA/Building/Barracks/SFX
Assets/Sound/NTA/Building/Barracks/Voice
Assets/Sound/NTA/Building/Factory/SFX
Assets/Sound/NTA/Building/Factory/Voice
Assets/Sound/NTA/Building/Spaceport/SFX
Assets/Sound/NTA/Building/Spaceport/Voice
Assets/Sound/NTA/Building/Lab/SFX
Assets/Sound/NTA/Building/Lab/Voice
```

## 요약

doc/0300과 동일한 패턴(이름별 폴더 + SFX/Voice)을 NTA 건물 6종에도 적용. SO 에셋(`NTA Building Data SO.asset`, `NTA Building Sound Bank SO.asset`)은 건드리지 않음 - 여전히 건물 사운드는 진영 공용 뱅크 1개를 쓰는 구조 그대로이고, 이번 건은 원본 클립 파일을 놓을 자리만 마련.

## 변경된 파일

- `Assets/Sound/NTA/Building/<건물명>/SFX/`, `/Voice/` (신규 빈 폴더 6쌍, 총 12개)
- `doc/0301-nta-building-sound-clip-folder-scaffolding.md` (이 파일, 신규)

참고: 빈 폴더는 git이 추적하지 않으므로 실제 오디오 파일이 들어가기 전까지는 `git status`에 나타나지 않는다.
