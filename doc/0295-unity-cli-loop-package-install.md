# 0295 - Unity CLI Loop 패키지 설치

- 날짜: 2026-07-29

## 요청 내용
사용자가 `https://github.com/hatayama/unity-cli-loop` 저장소가 무엇인지 물었고, 설명 후 설치를 요청함. AI(Claude)가 Unity 에디터를 CLI/Skills로 조작(컴파일, 테스트, 스크린샷, 플레이모드 입력 시뮬레이션 등)할 수 있게 해주는 도구.

## 조사 내용
- GitHub README 확인: 17개 도구 제공 (compile, run-tests, get-logs, screenshot, execute-dynamic-code, simulate-mouse-input 등)
- 요구사항: Unity 2022.3+, Node.js 22.0+
- 패키지 메타데이터(`Packages/src/package.json`) 확인: name = `io.github.hatayama.uloopmcp`, version = `2.2.0`
- CLI(`uloop-cli`)는 이미 npm 전역 설치 완료 (프로젝트 외부 변경이라 별도 doc 없이 진행함)

## Planned 코드 변경

### 기존 코드 (`Packages/manifest.json`)
```json
{
  "dependencies": {
    "com.unity.2d.sprite": "1.0.0",
    ...
  }
}
```

### 변경 코드 (`Packages/manifest.json`)
```json
{
  "dependencies": {
    "io.github.hatayama.uloopmcp": "https://github.com/hatayama/unity-cli-loop.git?path=/Packages/src",
    "com.unity.2d.sprite": "1.0.0",
    ...
  }
}
```

의존성 목록에 `io.github.hatayama.uloopmcp` 항목 1줄 추가.

## 요약 / 영향받는 파일
- `Packages/manifest.json`: uloop-mcp 패키지 git URL 의존성 추가
- 이후 `uloop skills install --claude` CLI 명령 실행 (프로젝트 내 `.claude/skills` 등에 Skills 파일 생성 가능성 있음, 실행 후 실제 변경분 확인 필요)
