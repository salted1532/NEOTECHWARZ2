# 0169 - FogOfWar 스크립트/폴더 제거 (에셋 사용으로 전환)

## 요청

"그냥 fogofWar는 에셋 사용할게 현재 너가 만들어준 코드들이랑 스크립트폴더 안에 fogofwar폴더까지
제거좀 해줘" — 전장의 안개는 직접 구현한 스크립트 대신 에셋스토어(또는 외부) 에셋을 쓰기로 결정,
[[fogofwar-eye-scripts-implementation]](0167)에서 만든 `Eye.cs`/`FogOfWarManager.cs`와
`Assets/Scripts/FogOfWar/` 폴더 자체를 제거해달라는 요청.

## 변경 사항

Before:
```
Assets/Scripts/FogOfWar/
├── Eye.cs
├── Eye.cs.meta
├── FogOfWarManager.cs
└── FogOfWarManager.cs.meta
Assets/Scripts/FogOfWar.meta
```

After: (전부 삭제, 경로 자체가 존재하지 않음)

`Assets/Scripts/FogOfWar/` 폴더 전체(스크립트 2개 + 각 `.meta`)와 상위 `Assets/Scripts/FogOfWar.meta`를
`Remove-Item -Recurse -Force`로 삭제했다. 이 폴더는 [[fogofwar-eye-scripts-implementation]](0167)에서
생성된 직후였고 아직 씬에 배치되거나 커밋된 적이 없어(git 미추적 신규 파일) 되돌릴 필요 없이 바로
제거해도 안전한 상태였다.

## 관련 이전 문서

- [[fogofwar-folder-and-eye-script-design]] (0166) — 설계 문서. 이제 미적용 상태로 폐기됨(참고용으로만 남김).
- [[fogofwar-eye-scripts-implementation]] (0167) — 이번에 제거된 코드를 생성했던 문서.
- [[fog-plane-must-cover-mapsize-area]] (0168) — 위 구현을 전제로 한 씬 설정 질문. 이번 제거로 더 이상
  해당 시나리오가 유효하지 않게 됨.

## 참고

전장의 안개는 앞으로 외부 에셋으로 대체할 예정이므로, 향후 그 에셋을 프로젝트에 통합/연동하는 작업이
생기면 새 문서로 다룰 것.
