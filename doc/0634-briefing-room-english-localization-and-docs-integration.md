# 0634 - 브리핑 룸 영어 번역 적용 및 0629 문서의 Docs 편입 (제안)

## 요청
1. 브리핑 룸 대사(`briefing.line.*`)의 영어 번역을 실제로 적용해달라.
2. 대사집은 [[0629-briefing-dialogue-script-by-character-en]] 문서를 참고.
3. 0629 문서를 브리핑 룸 "대사집"(정본)으로 삼아 `Docs/`(정규 문서 폴더)에 편입.

## 현황 확인
- `Assets/Resources/Localization/en.json`의 `briefing.line.0.0` ~ `briefing.line.5.6` (총 46개 키)이
  전부 `"(TODO) Translate: <한글 원문>"` 형태의 미번역 플레이스홀더로 남아 있음.
- `doc/0629-briefing-dialogue-script-by-character-en.md`에 동일한 46줄의 완성된 영어 번역이
  인물별·`Mission N - Line i` 태그별로 이미 정리돼 있고, 태그가 `briefing.line.N.i` /
  `briefing.line.subN.i` 키와 1:1 대응함(문서 자체에 명시).
- `briefing.speaker.*`, `briefing.button.*`, `briefing.start`, `briefing.end` 키는 이미 영어로
  채워져 있어 대상 아님.
- `Docs/` 폴더는 클래스/컴포넌트별 정규 레퍼런스 문서(`Docs/LocalizationManager.md` 등) 관례이며
  대사집 같은 콘텐츠 문서는 아직 없음. `doc/0629-...`는 세션 로그이므로 그대로 두고, 그 내용을
  `Docs/BriefingRoomDialogueScript.md`로 복사해 정규 문서화하는 방식 제안.

## 변경안

### 1) `Assets/Resources/Localization/en.json` — TODO 46건을 0629 번역으로 교체

```diff
- { "key": "briefing.line.0.0", "value": "(TODO) Translate: 프로메테우스 성계, ..." },
+ { "key": "briefing.line.0.0", "value": "Welcome to the Prometheus system, our forward base planet. We've arrived here to build humanity's new foothold." },
- { "key": "briefing.line.0.1", "value": "(TODO) Translate: 본격적인 개척에 앞서 ..." },
+ { "key": "briefing.line.0.1", "value": "Before full-scale colonization begins, all new recruits must complete basic tactical training. Learn movement, combat, construction, and unit production, in that order." },
- { "key": "briefing.line.0.2", "value": "(TODO) Translate: 훈련이라고 방심하지 마라. ..." },
+ { "key": "briefing.line.0.2", "value": "Don't let your guard down just because it's training. On the battlefield, the basics decide who survives." },
- { "key": "briefing.line.1.0", "value": "(TODO) Translate: 성계 내 자원 행성에서 ..." },
+ { "key": "briefing.line.1.0", "value": "A massive ore vein has been discovered on a resource planet within the system. However, the Omega Corporation is also moving to seize this planet." },
- { "key": "briefing.line.1.1", "value": "(TODO) Translate: 그 광맥은 오메가 코퍼레이션의 자산이다. ..." },
+ { "key": "briefing.line.1.1", "value": "That vein belongs to the Omega Corporation. Withdraw immediately." },
- { "key": "briefing.line.1.2", "value": "(TODO) Translate: 우리가 먼저 도착했다. ..." },
+ { "key": "briefing.line.1.2", "value": "We got here first. There's nothing to negotiate." },
- { "key": "briefing.line.1.3", "value": "(TODO) Translate: 적 전초기지가 방어 태세를 갖추고 있습니다." },
+ { "key": "briefing.line.1.3", "value": "The enemy outpost has taken up defensive positions." },
- { "key": "briefing.line.1.4", "value": "(TODO) Translate: 전 병력 전진. 광맥을 확보한다." },
+ { "key": "briefing.line.1.4", "value": "All forces, advance. We secure that vein." },
- { "key": "briefing.line.sub1.0", "value": "(TODO) Translate: 본대가 정면에서 놈들을 묶어두는 동안, ..." },
+ { "key": "briefing.line.sub1.0", "value": "While the main force pins them down at the front, the detachment takes out the radar base in the rear." },
- { "key": "briefing.line.sub1.1", "value": "(TODO) Translate: 레이더만 무력화되면 ..." },
+ { "key": "briefing.line.sub1.1", "value": "Once the radar's down, the main force's advance will be much easier." },
- { "key": "briefing.line.sub1.2", "value": "(TODO) Translate: 지원은 기대하지 마십시오. ..." },
+ { "key": "briefing.line.sub1.2", "value": "Don't expect support. This mission is carried out entirely by the detachment, alone." },
- { "key": "briefing.line.sub1.3", "value": "(TODO) Translate: 알겠다. 신속하게 끝낸다." },
+ { "key": "briefing.line.sub1.3", "value": "Understood. We'll finish this quickly." },
- { "key": "briefing.line.2.0", "value": "(TODO) Translate: 성계 외곽의 죽은 위성에서, ..." },
+ { "key": "briefing.line.2.0", "value": "We've obtained intelligence that the Omega Corporation discovered ancient ruins on a dead moon at the edge of the system and recovered an unidentified object." },
- { "key": "briefing.line.2.1", "value": "(TODO) Translate: 신형 병기일 수도 있고 ..." },
+ { "key": "briefing.line.2.1", "value": "It could be a new weapon, or a new energy source. We can't let them get to use it first." },
- { "key": "briefing.line.2.2", "value": "(TODO) Translate: 유물에서 매우 강한 에너지 반응이 ..." },
+ { "key": "briefing.line.2.2", "value": "We're detecting an extremely strong energy signature from the artifact. Nothing in our database matches it." },
- { "key": "briefing.line.2.3", "value": "(TODO) Translate: 유물을 확보한다. ..." },
+ { "key": "briefing.line.2.3", "value": "Secure the artifact. Recover their research data too, if possible." },
- { "key": "briefing.line.sub2.0", "value": "(TODO) Translate: 위성 잔해 지역에서 ..." },
+ { "key": "briefing.line.sub2.0", "value": "We're picking up additional energy readings in the debris field around the moon. It looks like a separate fragment, not part of the main body." },
- { "key": "briefing.line.sub2.1", "value": "(TODO) Translate: 본대가 유물 본체를 확보하는 동안, ..." },
+ { "key": "briefing.line.sub2.1", "value": "While the main force secures the artifact itself, the detachment secures that fragment first." },
- { "key": "briefing.line.sub2.2", "value": "(TODO) Translate: OC도 같은 신호를 쫓고 있을 겁니다." },
+ { "key": "briefing.line.sub2.2", "value": "OC is likely chasing the same signal." },
- { "key": "briefing.line.sub2.3", "value": "(TODO) Translate: 그럼 서둘러야겠군. ..." },
+ { "key": "briefing.line.sub2.3", "value": "Then we'd better hurry. We're not handing it over." },
- { "key": "briefing.line.3.0", "value": "(TODO) Translate: OC의 주력 식민 행성 전역에서 ..." },
+ { "key": "briefing.line.3.0", "value": "Distress calls are coming in from across OC's main colony world." },
- { "key": "briefing.line.3.1", "value": "(TODO) Translate: 확인 결과... 공격한 것은 ..." },
+ { "key": "briefing.line.3.1", "value": "Confirmed... it wasn't our forces that attacked. That thing... it isn't human." },
- { "key": "briefing.line.3.2", "value": "(TODO) Translate: 미확인 외계 생명체가 ..." },
+ { "key": "briefing.line.3.2", "value": "Unidentified alien life forms are attacking the OC base. The scale of the damage is beyond anything we imagined." },
- { "key": "briefing.line.3.3", "value": "(TODO) Translate: 생존자를 구조한다. ..." },
+ { "key": "briefing.line.3.3", "value": "We rescue the survivors. This is no time for humans to be fighting each other." },
- { "key": "briefing.line.sub3.0", "value": "(TODO) Translate: 칼립소 전역에서 산발적인 ..." },
+ { "key": "briefing.line.sub3.0", "value": "We're picking up scattered survivor signals across Calypso. While the main force heads for the outpost, a separate rescue team needs to be formed." },
- { "key": "briefing.line.sub3.1", "value": "(TODO) Translate: 기지를 세우고 병력을 갖춰라. ..." },
+ { "key": "briefing.line.sub3.1", "value": "Establish a base and build up your forces. Push through the resistance and reach the survivors." },
- { "key": "briefing.line.sub3.2", "value": "(TODO) Translate: 흩어진 인원부터 위치를 확인하겠습니다." },
+ { "key": "briefing.line.sub3.2", "value": "We'll start by pinpointing the scattered survivors' locations." },
- { "key": "briefing.line.sub3.3", "value": "(TODO) Translate: 한 명도 남기지 마라." },
+ { "key": "briefing.line.sub3.3", "value": "Leave no one behind." },
- { "key": "briefing.line.4.0", "value": "(TODO) Translate: 우리 병력의 절반 이상을 잃었다." },
+ { "key": "briefing.line.4.0", "value": "We've lost more than half our forces." },
- { "key": "briefing.line.4.1", "value": "(TODO) Translate: NTA도 상황은 다르지 않다." },
+ { "key": "briefing.line.4.1", "value": "NTA's situation is no different." },
- { "key": "briefing.line.4.2", "value": "(TODO) Translate: 이번만큼은 휴전이다." },
+ { "key": "briefing.line.4.2", "value": "Just this once, it's a ceasefire." },
- { "key": "briefing.line.4.3", "value": "(TODO) Translate: 동의한다." },
+ { "key": "briefing.line.4.3", "value": "Agreed." },
- { "key": "briefing.line.4.4", "value": "(TODO) Translate: 양측 병력이 최초의 공동 작전을 시작합니다." },
+ { "key": "briefing.line.4.4", "value": "Both forces are beginning their first joint operation." },
- { "key": "briefing.line.4.5", "value": "(TODO) Translate: 외계 사령기지를 파괴하면 ..." },
+ { "key": "briefing.line.4.5", "value": "If we destroy the alien command base, we can stop their offensive." },
- { "key": "briefing.line.4.6", "value": "(TODO) Translate: 오늘부터 우린 같은 편이다." },
+ { "key": "briefing.line.4.6", "value": "From today, we're on the same side." },
- { "key": "briefing.line.sub4.0", "value": "(TODO) Translate: 본대가 사령기지를 치는 동안, ..." },
+ { "key": "briefing.line.sub4.0", "value": "While the main force strikes the command base, if the rear line breaks, it's all over." },
- { "key": "briefing.line.sub4.1", "value": "(TODO) Translate: 혼성 방어부대를 배치한다. ..." },
+ { "key": "briefing.line.sub4.1", "value": "We're deploying a mixed defense force. How long can you hold?" },
- { "key": "briefing.line.sub4.2", "value": "(TODO) Translate: 장비만 버텨준다면, 끝까지 지키겠습니다." },
+ { "key": "briefing.line.sub4.2", "value": "As long as the equipment holds, we'll defend it to the very end." },
- { "key": "briefing.line.sub4.3", "value": "(TODO) Translate: 끝까지, 반드시." },
+ { "key": "briefing.line.sub4.3", "value": "To the end. No matter what." },
- { "key": "briefing.line.5.0", "value": "(TODO) Translate: 외계 함대는 후퇴했지만, ..." },
+ { "key": "briefing.line.5.0", "value": "The alien fleet has withdrawn, but we're detecting a massive command signal from the Zeus Platform they've occupied. It used to be one of our observation outposts." },
- { "key": "briefing.line.5.1", "value": "(TODO) Translate: 그 신호가 놈들의 지휘 코어다. ..." },
+ { "key": "briefing.line.5.1", "value": "That signal is their command core. The OC fleet will hit the platform's rear by a separate route. NTA takes the front." },
- { "key": "briefing.line.5.2", "value": "(TODO) Translate: 알겠다. 이번엔 지상은 우리 단독이다." },
+ { "key": "briefing.line.5.2", "value": "Understood. This time, the ground is ours alone." },
- { "key": "briefing.line.5.3", "value": "(TODO) Translate: OC 지상 병력의 지원은 없습니다. ..." },
+ { "key": "briefing.line.5.3", "value": "There will be no OC ground support. This landing operation is carried out by NTA alone." },
- { "key": "briefing.line.5.4", "value": "(TODO) Translate: 상관없다. 전 함대, 플랫폼으로 진입한다." },
+ { "key": "briefing.line.5.4", "value": "Doesn't matter. All forces, move in on the platform." },
- { "key": "briefing.line.5.5", "value": "(TODO) Translate: 이번 작전의 목표는 ..." },
+ { "key": "briefing.line.5.5", "value": "The objective of this operation is the complete destruction of the alien command structure." },
- { "key": "briefing.line.5.6", "value": "(TODO) Translate: 오늘, 이 전쟁을 끝낸다." },
+ { "key": "briefing.line.5.6", "value": "Today, we end this war." },
```

(각 줄의 원문 대응은 [[0629-briefing-dialogue-script-by-character-en]] 문서의 `Mission N - Line i` /
`Mission N Side - Line i` 태그를 `briefing.line.N.i` / `briefing.line.subN.i` 순서로 그대로 매핑한 것.
`briefing.speaker.*`/`briefing.button.*`/`briefing.start`/`briefing.end`는 이미 영어라 변경 없음.)

`BriefingRoomController.cs`/씬은 키만 참조하므로 코드/씬 변경 불필요. 46개 값 교체 후 JSON 문법만 재검증하면 됨.

### 2) `Docs/BriefingRoomDialogueScript.md` 신설 — 0629를 정규 문서로 편입

- `doc/0629-briefing-dialogue-script-by-character-en.md`의 내용(인물별 영문 대사 전체)을 그대로
  `Docs/BriefingRoomDialogueScript.md`로 옮겨 브리핑 룸 대사집의 정본으로 삼음.
- `doc/0629-...md`는 세션 로그이므로 삭제하지 않고 그대로 보존, 문서 상단에 정본 위치를
  `Docs/BriefingRoomDialogueScript.md`로 안내하는 한 줄만 추가.
- `Docs/BriefingRoomDialogueScript.md` 상단에는 이 문서가 en.json `briefing.line.*` 키의
  정본 출처임을 명시.

## 상태
완료. 사용자 확인 후 다음을 적용:
- `Assets/Resources/Localization/en.json`: `briefing.line.*` 46건을 위 diff대로 교체, `node -e "JSON.parse(...)"`로 문법 검증 통과.
- `Docs/BriefingRoomDialogueScript.md` 신설 — 0629 내용을 정본으로 편입.
- `doc/0629-briefing-dialogue-script-by-character-en.md` 상단에 정본 위치 안내 문구 추가(세션 로그로 보존).
