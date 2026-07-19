# 0171 - AOSFogWar Built-in 전용 데모 자료 삭제 (핑크 머티리얼 제거)

## 요청

[[aosfogwar-broken-materials-investigation]](0170)에서 제시한 3가지 옵션 중, 사용자가
**"Built-in 데모 자료만 삭제(권장)"**를 선택.

## 변경 사항

URP 프로젝트에서 전혀 쓰이지 않는 Built-in RP 전용 데모 자료를 삭제했다. 동일한 내용의 URP 버전이
`Demo/Materials/URP/`에 이미 있고 `Demo_URP.unity`가 그걸 참조하므로 기능 손실 없음.

Before:
```
Assets/AssetFolder/AOSFogWar/Demo/
├── Materials/Built-in/
│   ├── Eye.mat(.meta)
│   ├── Floor.mat(.meta)
│   ├── Monster.mat(.meta)
│   ├── Obstacle.mat(.meta)
│   ├── Revealer.mat(.meta)
│   └── Wall.mat(.meta)
├── Materials/Built-in.meta
├── Demo_Built-in.unity(.meta)
└── Eye_Built-in.prefab(.meta)
```

After: (위 전부 삭제, 경로 존재하지 않음)

- `Demo/Materials/Built-in/` 폴더 전체(머티리얼 6개 + 각 `.meta`) + `Demo/Materials/Built-in.meta` 삭제.
- `Demo/Demo_Built-in.unity` + `.meta` 삭제.
- `Demo/Eye_Built-in.prefab` + `.meta` 삭제.

남은 것: `Demo/Materials/URP/`(6개, 정상), `Demo/Demo_URP.unity`(정상), `Demo/Eye_URP.prefab`(정상),
`Demo/Scripts/csEyeController.cs`. 핵심 모듈(`csFogWar.cs`, `Shadowcaster.cs`, `FogPlane.mat`/
`.shader`, `Editor/`, `Examples/`)은 애초에 손대지 않음.

## 확인 필요 사항

Unity 에디터에서 `Assets/AssetFolder/AOSFogWar/Demo` 폴더를 열어 더 이상 핑크 썸네일이 없는지,
`Demo_URP.unity` 씬이 정상적으로 보이는지 확인 부탁.
