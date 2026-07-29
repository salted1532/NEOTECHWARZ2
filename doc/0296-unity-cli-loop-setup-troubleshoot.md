# 0296 - Unity CLI Loop 설치 후 트러블슈팅 및 설정

- 날짜: 2026-07-29

## 요청 내용
`0295`에서 `Packages/manifest.json`에 `io.github.hatayama.uloopmcp` 의존성을 추가한 뒤, Unity가 패키지를 resolve하는 과정에서 에러 발생. 이후 에디터 재시작으로 해결, 이어서 실제 사용을 위한 설정 진행 요청.

## 조사 내용 (Unity 패키지 resolve 에러)
- 에러 메시지: `io.github.hatayama.uloopmcp: Failed to rename [...PackageCache\.tmp-11940-DIC4M7lC9j89\move] to [...PackageCache\io.github.hatayama.uloopmcp@7ab2561c15dd] ... error code [EPERM]`
- 원인 분석: `.tmp-11940-...` 폴더명의 `11940`이 실행 중이던 `UnityPackageManager.exe`의 PID와 일치 → Unity 자체의 git clone → rename 과정에서 Windows 파일 잠금(백신 실시간 검사 등)으로 인한 일시적 EPERM. 프로젝트 코드/설정 문제 아님.
- `Library/`는 `.gitignore`에 의해 무시되는 캐시 폴더라 삭제해도 안전함을 확인.
- 해결: 사용자가 Unity 재시작 → 정상적으로 패키지 resolve 완료 (`Library/PackageCache/io.github.hatayama.uloopmcp@7ab2561c15dd` 생성 확인).

## 실행한 변경
### 명령 실행
```
uloop skills install --claude
```

### 결과
```
Installing uloop skills (project)...

Claude Code:
  ✓ Installed: 17
  ↑ Updated: 0
  - Skipped (up-to-date): 0
  Location: C:\Users\nobbl\Desktop\NEOTECHWARZ2\.claude\skills
```

### 생성된 파일 (신규, `.claude/skills/`)
- uloop-clear-console
- uloop-compile
- uloop-control-play-mode
- uloop-execute-dynamic-code
- uloop-find-game-objects
- uloop-focus-window
- uloop-get-hierarchy
- uloop-get-logs
- uloop-launch
- uloop-raycast
- uloop-record-input
- uloop-replay-input
- uloop-run-tests
- uloop-screenshot
- uloop-simulate-keyboard
- uloop-simulate-mouse-input
- uloop-simulate-mouse-ui

## 요약 / 영향받는 파일
- `Packages/manifest.json`: 변경 없음 (0295에서 이미 반영)
- `.claude/skills/*`: uloop-mcp 스킬 17개 신규 생성 (프로젝트 로컬)
- 남은 선택 작업: Unity 에디터 `Window > Unity CLI Loop > Settings`에서 Security Settings(Allow Tests Execution, Allow Third Party Tools, Dynamic Code Security Level) 활성화 — 미실행 상태
