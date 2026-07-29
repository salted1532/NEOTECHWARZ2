# 0300 - OC 원본 사운드 클립 폴더 스캐폴딩

날짜: 2026-07-30

## 요청 내용

"사운드 폴더에 OC에 빈 폴더로 각 유닛, 건물별 폴더 만들어주고 안에 voice랑 sfx폴더를 만들어줘"

## 조사 내용

- `Assets/Scripts/ScriptableObject/Sound/OC`(SO 에셋 보관용)와는 별개로, 실제 원본 음원 파일(.wav 등)은 프로젝트 루트의 `Assets/Sound/` 폴더에 따로 있다. 예: `Assets/Sound/NTA/Unit/Assault Trooper/SFX/Rifle_attack.wav`가 `NTA Unit Sound Bank SO`의 attackSFX 클립으로 연결되어 있음.
- 현재 구조:
  - `Assets/Sound/NTA/Unit/<유닛명>/SFX/`, `Assets/Sound/NTA/Unit/<유닛명>/Voice/` - 유닛 9종 전부 이 패턴.
  - `Assets/Sound/NTA/Building/` - 하위 폴더 없이 비어 있음(건물 뱅크가 진영 전체 공용 1개라서 아직 세분화 안 됨).
  - `Assets/Sound/OC/Unit/`, `Assets/Sound/OC/Building/` - 둘 다 완전히 비어 있음(하위 폴더 없음).
- 이번 요청은 NTA Unit 패턴을 OC에도 적용하되, **건물도 유닛과 동일하게 종류별 폴더 + SFX/Voice 하위 폴더**를 만들어 달라는 것 (NTA Building은 아직 세분화 안 돼 있지만, 이번 요청은 OC 건물도 유닛처럼 세분화하는 것으로 이해).
- 폴더 이름은 doc/0299에서 정리한 OC 유닛 9종 / 건물 6종 이름을 그대로 사용 (`OC Unit Data SO.asset`, `OC Building Data SO.asset` 기준).

## 확인 결과

"진행 (추천)" 선택 → 유닛 9종 + 건물 6종 전부 아래대로 생성 완료.

## 코드 변경 (적용 완료 - 에셋/코드 아님, 빈 폴더만 생성)

`Assets/Sound/OC/Unit/` 아래에 유닛별 폴더 9개, 각각 `SFX`, `Voice` 하위 폴더:

```
Assets/Sound/OC/Unit/Nanobot Repair/SFX
Assets/Sound/OC/Unit/Nanobot Repair/Voice
Assets/Sound/OC/Unit/Cyborg Soldier/SFX
Assets/Sound/OC/Unit/Cyborg Soldier/Voice
Assets/Sound/OC/Unit/Striker/SFX
Assets/Sound/OC/Unit/Striker/Voice
Assets/Sound/OC/Unit/Railgunner/SFX
Assets/Sound/OC/Unit/Railgunner/Voice
Assets/Sound/OC/Unit/Brute Mech/SFX
Assets/Sound/OC/Unit/Brute Mech/Voice
Assets/Sound/OC/Unit/Heavy Assault Tank/SFX
Assets/Sound/OC/Unit/Heavy Assault Tank/Voice
Assets/Sound/OC/Unit/Ironhawk/SFX
Assets/Sound/OC/Unit/Ironhawk/Voice
Assets/Sound/OC/Unit/Raven/SFX
Assets/Sound/OC/Unit/Raven/Voice
Assets/Sound/OC/Unit/Strike Drone/SFX
Assets/Sound/OC/Unit/Strike Drone/Voice
```

`Assets/Sound/OC/Building/` 아래에 건물별 폴더 6개, 각각 `SFX`, `Voice` 하위 폴더:

```
Assets/Sound/OC/Building/Omega Core/SFX
Assets/Sound/OC/Building/Omega Core/Voice
Assets/Sound/OC/Building/Cargo Silo/SFX
Assets/Sound/OC/Building/Cargo Silo/Voice
Assets/Sound/OC/Building/Cyber Foundry/SFX
Assets/Sound/OC/Building/Cyber Foundry/Voice
Assets/Sound/OC/Building/Mech Yard/SFX
Assets/Sound/OC/Building/Mech Yard/Voice
Assets/Sound/OC/Building/Drone Hangar/SFX
Assets/Sound/OC/Building/Drone Hangar/Voice
Assets/Sound/OC/Building/Neural Lab/SFX
Assets/Sound/OC/Building/Neural Lab/Voice
```

Unity는 빈 폴더를 커밋해도 그 자체로 `.meta`가 자동 생성되지는 않음(에디터가 열려서 스캔해야 `.meta` 생성) - 폴더만 만들어두면 다음에 Unity 에디터/Unity CLI가 프로젝트를 로드할 때 `.meta`가 자동으로 붙는다. 그 외 코드/SO 에셋 변경 없음.

## 요약

빈 폴더 생성만 하는 순수 스캐폴딩 작업 (SO 에셋이나 `.cs` 코드는 건드리지 않음). doc/0299에서 만든 사운드 뱅크 SO 에셋들이 나중에 참조할 원본 오디오 파일을 놓을 자리를 미리 만들어두는 목적.

## 변경된 파일

- `Assets/Sound/OC/Unit/<유닛명>/SFX/`, `/Voice/` (신규 빈 폴더 9쌍, 총 18개)
- `Assets/Sound/OC/Building/<건물명>/SFX/`, `/Voice/` (신규 빈 폴더 6쌍, 총 12개)
- `doc/0300-oc-sound-clip-folder-scaffolding.md` (이 파일, 신규)

참고: 빈 폴더는 git이 추적하지 않으므로 실제 오디오 파일이 들어가기 전까지는 `git status`에 나타나지 않는다.
