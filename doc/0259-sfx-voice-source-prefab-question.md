# 0259 - SoundManager의 SFX/Voice Source Prefab 필드를 연결해야 하는지

**날짜:** 2026-07-28

## 요청 내용

> sound manager에서 Sfx랑 voice Source prefab은 자동생성되는건가 이건 연결할 필요가 없나?

## 답변 요약

`SoundManager.BuildPool()`이 `prefab != null ? Instantiate(prefab, transform) : new GameObject(...).AddComponent<AudioSource>()`
로 구현돼 있어서, `sfxSourcePrefab`/`voiceSourcePrefab`을 비워두면 `Awake()` 시점에 빈 AudioSource를
자동 생성해 풀(`sfxPoolSize`=16, `voicePoolSize`=4)을 채운다. 재생 시 clip/pitch/볼륨/spatialBlend는
코드가 매번 새로 세팅하므로 비워둬도 기능상 문제없다 - 연결은 선택 사항.

단, 코드가 건드리지 않는 유니티 기본 AudioSource 값(특히 3D 효과음의 `minDistance`/`maxDistance`
=1/500, `rolloffMode`=Logarithmic)은 비워두면 유니티 기본값 그대로 적용된다. RTS 탑다운 카메라 특성상
기본 거리 감쇠가 안 맞을 수 있으므로, 나중에 조정하고 싶으면 AudioSource만 붙은 빈 GameObject를
프리팹으로 만들어 원하는 Min/Max Distance 등을 세팅한 뒤 `sfxSourcePrefab`에 연결하면 된다
(`voiceSourcePrefab`은 2D라 거리 설정이 의미 없어 기본값으로 둬도 무방).

프로젝트 코드는 변경하지 않음 (순수 Q&A, 기존 doc/0255 구현 내용 설명).

## 변경된 파일

없음.
