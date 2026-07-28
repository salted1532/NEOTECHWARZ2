## 날짜
2026-07-28

## 요청 내용
SkyLancer Sound Bank에 move SFX 클립을 넣었는데 이동 명령 시 이동 음성(moveVoice)은 들리는데 이동 효과음(moveSFX)이 안 들린다는 문의.

## 조사 내용
단계적으로 원인을 좁혀나감:
1. `SkyLancer Unit Sound Bank SO.asset`의 `moveSFX` 필드 확인 — 최초 확인 시 `clips: []` (비어있음), `moveVoice`만 4개 클립 등록된 상태였음. 사용자가 이후 클립 1개(`vehicle_move.mp3`, guid `eb0b47bf72805e641ab1bbc0f8bc79eb`)를 추가/저장.
2. 재확인 결과 `moveSFX`에 클립 정상 등록, `NTA Unit Data SO.asset`의 SkyLancer 항목 `soundBank` 참조도 해당 에셋(guid `7b18ba2bf8bbe6747b3c62d398c9d71d`)과 정확히 일치 — 배선 자체는 문제없음.
3. `Sound_SFXMuted`/`Sound_SFXVolume` PlayerPrefs를 레지스트리(`HKCU\Software\Unity\UnityEditor\DefaultCompany\NEOTECHWARZ2`)에서 확인 — 저장된 값 없음 → 기본값(음소거 아님, 볼륨 1) 그대로라 음소거 문제 아님.
4. 오디오 클립 임포트 설정(`vehicle_move.mp3.meta`)을 정상 작동 중인 `moveVoice` 클립(`SkyLancer_order1.mp3.meta`)과 비교 — `loadType`, `preloadAudioData`, `3D` 등 설정 완전히 동일, 파일 크기도 46KB로 정상.
5. `PlayMoveSFX()`(3D, `spatialBlend=1`)와 `PlayMoveVoice()`(2D, `spatialBlend=0`)의 재생 방식 차이를 근거로 3D 거리 감쇠 가능성을 제시했으나, 사용자가 카메라 줌을 최대로 당겨도 안 들린다고 함.
6. `AudioListener`가 `GameManager.prefab` 내 `Main Camera`에 정상 부착되어 있고, `CameraControl.HandleZoom()`이 실제로 카메라 Transform을 이동시키는 방식(FOV 조정이 아님)임을 확인 — 리스너 위치 자체는 문제없어 보임.
7. Play 모드에서 풀에 생성된 `SFXSource_i` 오브젝트에 moveSFX 클립이 정상 할당되는 것까지 사용자가 직접 확인 — 여기까지는 정상 동작.

## 결론
코드/에셋 배선 전부 정상이었고, **원인은 단순히 삽입한 오디오 클립(`vehicle_move.mp3`) 자체의 볼륨(음량)이 너무 작아서 안 들렸던 것**으로 사용자가 직접 확인. 클립을 교체하거나 게인을 올리면 해결.

## 요약/남은 작업
순수 트러블슈팅 Q&A, 코드 변경 없음. (참고: 조사 중 `UnitAudio.PlayMoveSFX()`가 3D(`PlaySFX`)로 재생되는 반면 `PlayMoveVoice()`는 2D(`PlayOrderVoice`, `spatialBlend=0`)로 재생된다는 설계상 비대칭을 발견했으나, 이번 이슈의 직접 원인은 아니었음. 추후 다른 유닛에서 비슷한 "카메라 거리 따라 SFX가 안 들린다" 문의가 다시 나오면 이 비대칭을 먼저 의심할 것.)

## 변경된 파일
없음.
