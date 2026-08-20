# 0641 - 최대 줌아웃(카메라 y50)에서 유닛 공격 소리 안 들림 (제안)

## 요청
카메라를 최대로 줌아웃하면(현재 카메라 높이 최대 50까지 올라감) 화면 가운데에서 유닛이 싸워도 공격 소리가 안 들림. 예전 카메라 최대 높이가 25였을 땐 잘 들렸음.

## 원인
`SoundManager.BuildPool()`(`Assets/Scripts/Audio/SoundManager.cs:139`)이 SFX 풀의 3D 거리감쇠를 `AudioRolloffMode.Linear`, `minDistance=10`, `maxDistance=45`로 고정해뒀다(doc/0286). `maxDistance`를 넘어서면 Linear 롤오프는 볼륨이 완전히 0이 된다.

이 45라는 값은 doc/0286 당시 `CameraControl.maxZoom` 기본값 35(카메라 높이 상한)를 기준으로 잡은 것 - 카메라가 55° 기울어진 채(`GameManager.prefab`의 Main Camera `m_LocalEulerAnglesHint.x: 55`) 화면 중앙을 내려다보므로, 화면 중앙 지점까지의 실제 3D 거리는 카메라 높이가 아니라 `높이 / sin(55°) ≈ 높이 × 1.221`이다. 높이 35 → 거리 약 42.7 ≈ 45(당시 값과 일치, doc/0277에서도 "카메라 거리 대략 10~45유닛"이라고 직접 언급).

그런데 지금 `GameManager.prefab`의 `CameraControl`은 `maxZoom: 40`, `tierZoomStep: 5`로 늘어나 있고, 언덕 지형(Layer1/2/3) 위에서는 `tierOffset`(현재 단 × tierZoomStep)만큼 줌 상한이 더 올라간다. 사용자가 실측한 "최대 50"은 언덕 2단(Layer2, tierOffset=10) 기준 40+10=50이고, 맵에 3단(Layer3)까지 있으면 이론상 40+15=55까지도 올라간다. 높이 50~55는 거리로 환산하면 약 61~67 - 지금의 `maxDistance=45`를 훌쩍 넘어가서 화면 가운데 전투음이 완전히 무음 처리된다.

## 제안 설계
`SoundManager.cs`의 `maxDistance`를 현재 카메라 스케일에 맞게 올린다:
```csharp
source.rolloffMode = AudioRolloffMode.Linear;
source.minDistance = 10f;
source.maxDistance = 70f; // 최대 줌 높이(maxZoom 40 + tierZoomStep 5 × 최대 3단 = 55) × 1/sin(55°) ≈ 67 + 여유
```
- `minDistance`는 그대로 유지 - 근접 줌(현재 `minZoom=4`, 거리 약 5)은 이미 `minDistance=10`보다 가까워서 항상 최대 음량 구간에 들어있고, 이번 문제와 무관.
- 70은 이론상 최대 거리(약 67.2)에 약간의 여유를 둔 값 - `CameraControl`의 `maxZoom`/`tierZoomStep`이 나중에 또 바뀌면 이 값도 같이 재계산해야 함(주석에 계산식 남겨서 다음에 바로 갱신 가능하게).

## 범위 밖
- `minDistance`/롤오프 곡선 형태(Linear→다른 모드) 변경 - 근접 줌 쪽은 문제 보고 없음.
- `CameraControl`의 줌 범위 자체를 줄이는 방향 - 요청은 "최대 줌에서도 들리게" 이지 줌 범위 축소가 아님.

## 구현 완료
`SoundManager.cs:159`의 `source.maxDistance`를 45 → 70으로 변경, 계산식(높이×1/sin55°)을 주석에 남김. 컴파일 성공(에러 0, 기존 경고 49개는 무관).

## 상태
완료. 최대 줌아웃(카메라 높이 최대 50~55)에서도 화면 중앙 전투음이 감쇠 구간 안에 들어와 들린다.
