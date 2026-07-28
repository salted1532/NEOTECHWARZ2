# 0257 - SoundBank를 유닛별로 다 따로 만들어야 하는지

**날짜:** 2026-07-28

## 요청 내용

> 그럼 각 유닛별로 SoundBank를 따로 다 만들어야 하나?

## 답변 요약

- 기술적으로는 강제 아님: `UnitData.soundBank`는 단순 에셋 참조 필드라 여러 유닛이 같은
  `UnitSoundBankSO` 하나를 공유해도 코드상 문제없이 동작한다. `soundBank`가 비어있으면(null) 그
  유닛은 그냥 조용할 뿐 에러가 나지 않으므로, 전체를 한 번에 다 채우지 않고 하나씩 순차적으로
  채워나가도 된다.
- 다만 원래 요청("각 유닛별로 대사 클립이 다 다르다")의 취지상, 최소한 **음성(Voice) 대사가 다른
  유닛끼리는 별도 에셋**이 필요하다. SFX(공격음/사망음 등)는 비슷한 무기 계열끼리 공유해도 자연스러운
  경우가 많다.
- 진행 순서 제안: NTA 유닛 9종(Worker Drone/Assault Trooper/Scout Drone/Sharpshooter/Ranger IFV/
  Pulsar Tank/SkyLancer/Firehawk/Guardian Drone) 중 대사가 특히 많이 필요한 워커부터 우선 제작,
  나머지는 사운드 소스가 준비되는 대로 점진적으로 채워도 무방.

프로젝트 코드는 변경하지 않음 (순수 Q&A).

## 변경된 파일

없음.
