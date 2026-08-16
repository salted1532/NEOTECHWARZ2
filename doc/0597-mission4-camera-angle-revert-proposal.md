# 0597 - Mission4 카메라 각도 원복 제안

## 질문
"카메라 각도를 원래대로 고쳐줘 원래 몇이였더라"

## 조사 결과
- Main Camera(`CameraControl`)는 `Assets/prefabs/Game/GameManager.prefab` 안에 있고, 기본(prefab) 회전값은
  `m_LocalRotation: {x: 0.46174863, y: -0, z: -0, w: 0.8870109}` (`m_LocalEulerAnglesHint: {x: 55, y: 0, z: 0}`) →
  **X축 55도 틸트**.
- 다른 모든 씬(Mission0, Mission1, Mission2, Mission3, Mission5, SampleScene, TestScene)은 이 카메라에 대해
  회전 오버라이드가 없음 → 전부 prefab 기본값인 55도를 그대로 사용 중.
- **Mission4.unity만** 회전 오버라이드가 걸려 있음:
  ```
  m_LocalRotation.w: 0.9319748
  m_LocalRotation.x: 0.362523
  ```
  이는 약 **X축 42.5도**에 해당 (오늘 각도 테스트/조정 중 바뀐 것으로 보임).
- 이 오버라이드는 오늘 커밋 `867f9ed` ("건물 뒤, 언덕 뒤 실루엣 보이도록 수정")에서 새로 추가됨 (diff에서 `+` 로만 나타남,
  이전 커밋 `f66b462`에는 없었음). 즉 언덕/건물 실루엣 확인차 씬 뷰에서 카메라를 돌려보다가 그대로 씬에 저장된 것으로 추정.

## 결론
"원래" 각도 = **X축 55도** (다른 모든 미션 씬과 동일한 prefab 기본값).

## 변경 제안
`Assets/Scenes/Missions/Mission4.unity`에서 해당 카메라(target fileID `7555572131848590869`)의
`m_LocalRotation.w` / `m_LocalRotation.x` 오버라이드 두 항목을 삭제하여, 다른 씬들처럼 prefab 기본값(55도)을
그대로 상속받도록 되돌림.

## 실행 여부
사용자 확인 완료 → 적용함.

## 실제 변경 (Assets/Scenes/Missions/Mission4.unity)

Before:
```
    - target: {fileID: 7555572131848590869, guid: b2e95921e88a13d45a9b298421d751c2, type: 3}
      propertyPath: m_LocalPosition.z
      value: -77.93112
      objectReference: {fileID: 0}
    - target: {fileID: 7555572131848590869, guid: b2e95921e88a13d45a9b298421d751c2, type: 3}
      propertyPath: m_LocalRotation.w
      value: 0.9319748
      objectReference: {fileID: 0}
    - target: {fileID: 7555572131848590869, guid: b2e95921e88a13d45a9b298421d751c2, type: 3}
      propertyPath: m_LocalRotation.x
      value: 0.362523
      objectReference: {fileID: 0}
    m_RemovedComponents: []
```

After:
```
    - target: {fileID: 7555572131848590869, guid: b2e95921e88a13d45a9b298421d751c2, type: 3}
      propertyPath: m_LocalPosition.z
      value: -77.93112
      objectReference: {fileID: 0}
    m_RemovedComponents: []
```

`m_LocalRotation.w`/`.x` 오버라이드 두 항목 삭제. 이제 Mission4의 카메라도 다른 씬들과 동일하게 prefab 기본값
(X축 55도)을 그대로 상속받음. `m_LocalPosition.x/y/z` 오버라이드는 그대로 유지(위치는 원래 의도된 변경이었을 수 있어
건드리지 않음, 각도만 요청받음).
