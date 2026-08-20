# BriefingRoomDialogueScript

브리핑 룸 영문 대사집(정본). `Assets/Resources/Localization/en.json`의 `briefing.line.*` 키값의
출처이며, 새 브리핑 대사를 추가/수정할 때는 이 문서를 먼저 갱신한 뒤 `en.json`(및 필요 시 `ko.json`)에
반영한다.

세션 로그 원본: doc/0629-briefing-dialogue-script-by-character-en.md (doc/0634에서 en.json 46개
TODO 항목을 이 대사로 교체 적용).

---

Same lines as doc/0628-briefing-room-english-dialogue-script-for-tts.md, regrouped by speaker instead
of by mission, so each character's full line set can be diffed/copy-pasted in one pass. Lines are
in mission playback order within each character block.

Each line is tagged with its clip ID, `Mission N - Line i` (or `Mission N Side - Line i` for the
side-mission briefings), matching the scene's own `briefing.line.N.i` / `briefing.line.subN.i`
localization keys 1:1 — so a generated clip named after the tag drops straight back into the
right spot. Tag is on its own line above the dialogue; the dialogue line itself is the plain text
below it, ready to paste into TTS as-is.

---

## [Adrian]

**Mission 0 - Line 0**
Welcome to the Prometheus system, our forward base planet. We've arrived here to build humanity's new foothold.

**Mission 0 - Line 2**
Don't let your guard down just because it's training. On the battlefield, the basics decide who survives.

**Mission 1 - Line 2**
We got here first. There's nothing to negotiate.

**Mission 1 - Line 4**
All forces, advance. We secure that vein.

**Mission 1 Side - Line 0**
While the main force pins them down at the front, the detachment takes out the radar base in the rear.

**Mission 2 - Line 1**
It could be a new weapon, or a new energy source. We can't let them get to use it first.

**Mission 2 - Line 3**
Secure the artifact. Recover their research data too, if possible.

**Mission 2 Side - Line 1**
While the main force secures the artifact itself, the detachment secures that fragment first.

**Mission 2 Side - Line 3**
Then we'd better hurry. We're not handing it over.

**Mission 3 - Line 3**
We rescue the survivors. This is no time for humans to be fighting each other.

**Mission 3 Side - Line 1**
Establish a base and build up your forces. Push through the resistance and reach the survivors.

**Mission 3 Side - Line 3**
Leave no one behind.

**Mission 4 - Line 1**
NTA's situation is no different.

**Mission 4 - Line 3**
Agreed.

**Mission 4 - Line 6**
From today, we're on the same side.

**Mission 4 Side - Line 1**
We're deploying a mixed defense force. How long can you hold?

**Mission 5 - Line 2**
Understood. This time, the ground is ours alone.

**Mission 5 - Line 4**
Doesn't matter. All forces, move in on the platform.

**Mission 5 - Line 6**
Today, we end this war.

---

## [Selena]

**Mission 1 - Line 1**
That vein belongs to the Omega Corporation. Withdraw immediately.

**Mission 4 - Line 0**
We've lost more than half our forces.

**Mission 4 - Line 2**
Just this once, it's a ceasefire.

**Mission 4 - Line 5**
If we destroy the alien command base, we can stop their offensive.

**Mission 4 Side - Line 0**
While the main force strikes the command base, if the rear line breaks, it's all over.

**Mission 4 Side - Line 3**
To the end. No matter what.

**Mission 5 - Line 1**
That signal is their command core. The OC fleet will hit the platform's rear by a separate route. NTA takes the front.

---

## [Adjutant]

**Mission 0 - Line 1**
Before full-scale colonization begins, all new recruits must complete basic tactical training. Learn movement, combat, construction, and unit production, in that order.

**Mission 1 - Line 0**
A massive ore vein has been discovered on a resource planet within the system. However, the Omega Corporation is also moving to seize this planet.

**Mission 1 - Line 3**
The enemy outpost has taken up defensive positions.

**Mission 1 Side - Line 2**
Don't expect support. This mission is carried out entirely by the detachment, alone.

**Mission 2 - Line 0**
We've obtained intelligence that the Omega Corporation discovered ancient ruins on a dead moon at the edge of the system and recovered an unidentified object.

**Mission 3 - Line 0**
Distress calls are coming in from across OC's main colony world.

**Mission 3 - Line 2**
Unidentified alien life forms are attacking the OC base. The scale of the damage is beyond anything we imagined.

**Mission 3 Side - Line 0**
We're picking up scattered survivor signals across Calypso. While the main force heads for the outpost, a separate rescue team needs to be formed.

**Mission 4 - Line 4**
Both forces are beginning their first joint operation.

**Mission 5 - Line 0**
The alien fleet has withdrawn, but we're detecting a massive command signal from the Zeus Platform they've occupied. It used to be one of our observation outposts.

**Mission 5 - Line 3**
There will be no OC ground support. This landing operation is carried out by NTA alone.

**Mission 5 - Line 5**
The objective of this operation is the complete destruction of the alien command structure.

---

## [Scout]

**Mission 2 - Line 2**
We're detecting an extremely strong energy signature from the artifact. Nothing in our database matches it.

**Mission 2 Side - Line 0**
We're picking up additional energy readings in the debris field around the moon. It looks like a separate fragment, not part of the main body.

**Mission 3 - Line 1**
Confirmed... it wasn't our forces that attacked. That thing... it isn't human.

---

## [Maneuver Commander]

**Mission 1 Side - Line 1**
Once the radar's down, the main force's advance will be much easier.

**Mission 1 Side - Line 3**
Understood. We'll finish this quickly.

**Mission 2 Side - Line 2**
OC is likely chasing the same signal.

---

## [Rescue Leader]

**Mission 3 Side - Line 2**
We'll start by pinpointing the scattered survivors' locations.

---

## [Defense Commander]

**Mission 4 Side - Line 2**
As long as the equipment holds, we'll defend it to the very end.

---

## [System] (non-character announcement, appears once per mission before/after the lines above)

**Every mission - start**
Briefing start.

**Every mission - end**
End of briefing.
