# Style Guide Agent run `2026-11-22T203354Z`

Model `claude-sonnet-5-5` · threshold 85/100 · every hard rule >= 7/10 · max 3 refinements · no human input during the run.

**Planted-violation demo.** Iteration 0 of each task is a hand-written, deliberately off-style draft from `fixtures/`, not Generator output. Everything after it is the agent working alone.

| Task | Result | Score per iteration |
|---|---|---|
| `trailer_backdrop` | **approved** | 1.1 → 90.6 |
| `trash_talk_prematch` | **approved** | 10.5 → 81.6 → 86.8 |

## `trailer_backdrop` (art_brief)

*Brief:* An image-generation brief for one single-wide trailer for the back row of the Duckville arena backdrop (the 'trailer along the back' in the chosen arena look). The Art Wrangler would feed the prompt to an image model to make a placeholder sprite.

### Iteration 0 (from seed): 1.1/100, below bar; hard rules under 7: A-01, A-02, A-04, A-05, A-08

- **subject:** A sleek modern mobile home in a city trailer lot
- **prompt:** Photorealistic 3D render of a brand-new silver mobile home, freshly painted and spotless, parked in an urban lot at night with neon signs, a glowing city skyline and snowy mountains behind it, smooth gradients, cinematic 8k lighting, palette #00FFFF #FF00FF #FFFFFF #1A1A1A
- **negative_prompt:** pixel art, low resolution
- **palette:** `["#00FFFF", "#FF00FF", "#FFFFFF", "#1A1A1A"]`
- **lighting:** Night, cool blue moonlight with pink neon glow
- **details:** Clean siding, manicured lawn, modern solar panels on the roof.
- **size_px:** `{"width": 1200, "height": 260}`
- **scale_note:** Fits the screen nicely.
- **layer:** background

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **0** | 0: The prompt asks for a photorealistic 3D render with smooth gradients and cinematic 8k lighting. That is the opposite of crisp early-90s arcade pixel art. The negative prompt also bans 'pixel art'. | 0: 0/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game); off-style terms: "3D" (3D render), "Photorealistic" (photorealism), "8k" (photorealism), "smooth gradients" (painterly/smooth) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **0** | 0: The palette is four neon and white hex values, and none of them is one of the Style Guide's 14 colours. There is no justification for going off-palette. | 0: 0/4 hex colours are on the Style Guide palette; off-palette: #00FFFF, #FF00FF, #FFFFFF, #1A1A1A; only 0 palette colours used (need at least 4 for shading ramps) |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **0** | 0: The lighting is night with cool moonlight and pink neon. The guide requires late-afternoon golden hour with a warm orange sky and long shadows. | 0: 0/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows); conflicting light: "night", "Night" |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **0** | 0: The scene includes snowy mountains and a city skyline. The guide forbids mountains and allows only trees, skies, electric poles and trailers. | 0: Banned scenery in the prompt: "mountains" (mountains), "city" (city), "urban" (city), "city" (city), "skyline" (city), "neon" (city fixtures), "neon" (city fixtures), "snowy" (wrong biome/genre) |
| A-05 (hard) Deep woods, not a city | 2 | **0** | 0: The brief puts the trailer in an urban lot with neon signs and a city skyline. That is the opposite of the middle of the woods. | 0: Banned scenery in the prompt: "mountains" (mountains), "city" (city), "urban" (city), "city" (city), "skyline" (city), "neon" (city fixtures), "neon" (city fixtures), "snowy" (wrong biome/genre) |
| A-06 Disrepair: nothing has been kept up | 3 | **0** | 0: The draft asks for the reverse of disrepair: brand-new, freshly painted, spotless, clean siding and a manicured lawn. | 0: 0 distinct neglect details (none); contradicting kept-up wording: "brand-new", "freshly painted", "spotless", "manicured" |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **0** | 1: The roof has modern solar panels instead of an old TV antenna or a DirecTV-style dish. That contradicts the rule. | 0: No old TV antenna or satellite dish on the trailer roof. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **1** | 1: A size is given (1200×260 px), but it is a wide, short shape. A single-wide trailer should be about 480 px tall, twice Barry's 240 px. The scale note is vague and never mentions Barry. | 1: height 260px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height); scale_note does not tie the size to the 240-px wrestlers |

*Evaluator summary:* The draft contradicts almost every rule: it asks for a photorealistic 3D neon city night scene with a new trailer and mountains, where the docs call for a pixel-art, golden-hour, rundown woodland trailer at 480 px tall.

**Refiner** (revising iteration 0):
- A-01: Rewrote the prompt as crisp early-90s arcade pixel art at 1 native px per art pixel, naming WrestleFest, Final Fight and Streets of Rage. Moved photorealism, 3D, gradients and 8k to the negative prompt.
- A-06: Replaced the new, clean look with concrete neglect details: rust, sagging patched roof, faded siding, duct-taped cracked window, missing skirting, flat tire, weeds.
- A-02: Palette field and prompt now use the Style Guide's 14 hexes only.
- A-03: Lighting is now late-afternoon golden hour with warm orange light and long shadows. Night was removed.
- A-04: Removed the skyline and mountains. The scene has only pines, sky, an electric pole and the trailer, with dirt, gravel and grass.
- A-05: Set the scene in a trailer park in the woods with thick pines. Banned city terms are now in the negative prompt.
- A-07: Replaced the solar panels with a crooked old TV antenna and a rusty DirecTV-style dish.
- A-08: Size is now 1000×480 px. The scale note ties the 480 px height to twice Barry's 240-px sprite.

### Iteration 1 (from refiner): 90.6/100, PASS

- **subject:** A rundown single-wide trailer for the back row of the Duckville arena, in a trailer park in the middle of the North Louisiana woods
- **prompt:** Crisp hand-placed early-90s arcade pixel art sprite, in the style of WWF WrestleFest, Final Fight and Streets of Rage, 16-bit look, drawn at 1 native pixel per art pixel with hard pixel edges and limited-colour shading ramps. A single-wide trailer seen straight-on, neglected for years: rust streaks running down faded #E6DFCC and #795037 siding, a sagging roof with patched tar and a dented corner, a cracked window held with duct tape, a missing skirting panel showing dark #10151A underneath, cinder blocks for steps, a flat tire, weeds and tufts of grass pushing through gravel at the base. A crooked old rooftop TV antenna and a rusty DirecTV-style dish bolted to the roof. Late-afternoon golden-hour light from low sun, warm orange glow #E19A36 #D26938 #FFD65A on the sunlit side, long shadows stretching across the dirt. Thick North Louisiana pines behind it in #25372C and #35523A, an electric pole at the side. Palette: #10151A #17243A #25372C #35523A #657A3B #795037 #A74727 #D26938 #E19A36 #FFD65A #2872B8 #7435A8 #C7352C #E6DFCC. Transparent background, sprite only, single trailer 480 px tall.
- **negative_prompt:** photorealistic, 3D render, vector art, anime, painterly, smooth gradients, blur, anti-aliasing, 8k, city, skyline, urban, neon, highway, crowds, mountains, hills, cliffs, snow, night, overcast, new, clean, freshly painted, manicured lawn, solar panels, text, watermark
- **palette:** `["#10151A", "#17243A", "#25372C", "#35523A", "#657A3B", "#795037", "#A74727", "#D26938", "#E19A36", "#FFD65A", "#2872B8", "#7435A8", "#C7352C", "#E6DFCC"]`
- **lighting:** Late-afternoon golden hour: low warm orange sun from the side, warm orange sky glow, long shadows falling across the dirt.
- **details:** Rust streaks, sagging patched roof, faded siding, cracked window with duct tape, missing skirting panel, cinder-block steps, flat tire, weeds through gravel, crooked old TV antenna and rusty DirecTV-style dish on the roof, electric pole and thick pines nearby.
- **size_px:** `{"width": 1000, "height": 480}`
- **scale_note:** In native pixels (960×540 view, 1 art px = 1 native px), the trailer is 480 px tall, about twice Barry's 240-px standing sprite, as a single-wide should be. Peaux is 215 px.
- **layer:** background

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **9** | 9: The prompt asks for crisp, hand-placed 1:1 pixel art in the style of WrestleFest, Final Fight and Streets of Rage, and the negative prompt blocks 3D, vector, anime, painterly and smooth gradients. The only gap is that Technos, Capcom and SNK are never named as studios. | 10: 3/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **9** | 9: All 14 Style Guide hexes are listed in the prompt and the palette field, with correct values. Several are also tied to specific parts of the image, such as #E6DFCC and #795037 for siding and #10151A for the skirting gap. No off-palette colour is introduced. The prompt does not tell the model to stay strictly within the palette. | 10: 14/14 hex colours are on the Style Guide palette |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **8** | 8: The prompt and lighting field describe late-afternoon golden hour with a low sun, warm orange glow and long shadows across the dirt, matching D-016. However, 'transparent background, sprite only' removes the orange sky, and a transparent sprite has no dirt for the shadows to fall on. | 10: 4/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows) |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **9** | 9: The scene stays within the whitelist of trailer, pines, electric pole, gravel and grass. The negative prompt excludes mountains, hills and cliffs. Cinder blocks, a flat tire and duct tape are small details on the trailer itself, not landmarks. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-05 (hard) Deep woods, not a city | 2 | **8** | 8: Pines, an electric pole, gravel and weeds read as rural woods, and the negative prompt blocks city, skyline, highway, neon and crowds. The 'middle of the woods' feeling appears only in the subject field, not in the prompt that reaches the model. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-06 Disrepair: nothing has been kept up | 3 | **10** | 10: The disrepair is shown in concrete, visible details: rust streaks, faded siding, a sagging patched roof with a dented corner, a cracked window with duct tape, a missing skirting panel, a flat tire and weeds through gravel. The negative prompt also rules out new, clean and freshly painted looks. | 10: 10 distinct neglect details (cracked, dented, duct tape, faded, missing, patched, rust, rusty, sagging, weeds) |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **10** | 10: The prompt and details both put a crooked old rooftop TV antenna and a rusty DirecTV-style dish on the roof, as the Style Guide asks. | 10: Roof carries a TV antenna or satellite dish. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **9** | 9: The brief gives 480 px tall, about twice Barry's 240 px, in native pixels, and the scale_note ties it to Barry and Peaux. Two small gaps: the 1000 px width is not justified, and the prompt's '480 px tall' sits beside pines and a pole, so it is unclear what the 480 px measures. The prompt also never mentions Barry. | 10: height 480px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height) |

*Evaluator summary:* The prompt asks for a "sprite only" on a transparent background but also asks for pines, an electric pole, dirt shadows and sky glow, so the image model gets conflicting instructions about what is in the frame and how the 480 px is measured.


## `trash_talk_prematch` (trash_talk)

*Brief:* A four-line pre-match trash-talk exchange between Barry and Peaux, spoken in the trailer park just before the bell. Candidate flavour text for the moment before the versus text; it is not wired into the game.

### Iteration 0 (from seed): 10.5/100, below bar; hard rules under 7: N-01, N-02, N-03

> **Barry:** Tonight at the arena I'm gonna pin you one-two-three and take the world championship belt, kid!  
> **Peaux:** Not before my piledriver and a steel chair put you through the announce table, old man.  
> **Barry:** The crowd came to see "The Hammer" win, and the Hammer always wins!  
> **Barry:** Hulk Hogan taught me everything. Let's give the fans a great show and shake hands after.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **0** | 2: Speakers are only barry and peaux, but the draft invents a new named persona "The Hammer" and names real wrestler Hulk Hogan, violating the no real/invented wrestlers rule. Barry's nickname "Hot Potato" is never used. | 0: other wrestlers named: ['Hogan', 'Hulk']; nicknames not in the GDD: ['The Hammer'] |
| N-02 (hard) Knockout is the only way to win | 3 | **0** | 0: Talks of pinfalls, three-counts and a world championship belt, all explicitly out of scope; the only win condition is KO. | 0: Non-MVP mechanics mentioned: "championship" (title/belt), "belt" (title/belt), "pin" (pinfall), "one-two-three" (pinfall) |
| N-03 (hard) Only moves that exist in the MVP | 2 | **0** | 1: Peaux threatens a piledriver and steel chair, a move and prop not in the MVP. No MVP move (punch/kick, lock-up, bodyslam, Irish Whip, stomp) is referenced. | 0: Moves the game doesn't have: "piledriver", "steel chair", "table" |
| N-04 Barry sounds like Barry | 3 | **2** | 2: Barry's lines are generic wrestling bravado. Nothing refers to trucking, the Hoon dirt track, cigarette-butt punks, mustache or beer gut. The last line, with its handshake and "great show", is out of character for a stubborn brute. |  |
| N-05 Peaux sounds like Peaux | 3 | **2** | 2: Peaux's one line is a generic promo with only "old man" hinting at youth. It has no goth, eyeliner, expulsion, brothers or redneck-contempt detail, and it isn't sullen. |  |
| N-06 Rooted in Duckville | 2 | **0** | 1: Set in an arena with crowds, fans and an announce table, which the rule forbids. There is no Duckville, trailer, gravel or neighbour dynamic. | 0: place names: none; trailer-park details: none |
| N-07 Dark Southern humor | 3 | **2** | 2: Reads as a stock wrestling promo, and the ending is wholesome (handshake, great show). It has no dark, blunt Southern irreverence. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **2** | 2: There are four lines, but the speakers do not alternate (barry speaks lines 3 and 4 in a row). Lines 1, 2 and 4 are over 90 characters. | 5: speakers don't alternate; 1 line(s) over 90 characters ([95]) |

*Evaluator summary:* The draft is a generic arena wrestling promo with pinfalls, belts, piledrivers, Hulk Hogan and a wholesome ending, so it breaks nearly every hard rule and has none of Rural Rumble's Duckville trailer-park character.

**Refiner** (revising iteration 0):
- N-02: Removed pin, belt and championship talk; the only stakes are knocking someone out cold.
- N-01: Removed The Hammer and Hulk Hogan; only Barry and Peaux speak and no one else is named.
- N-03: Threats use only the bodyslam, Irish Whip and stomp; dropped the piledriver, chair and table.
- N-06: Moved the exchange to the trailer park, with gravel, a driveway and neighbours who can't stand each other; cut the arena and crowd.
- N-04: Barry is now blunt and trucker-y: cigarette butts, freight hauling, a nod to his Hoon dirt track, and no sportsmanship.
- N-05: Peaux is now sullen and cutting: eyeliner, his brothers, and contempt for 'redneck' types.
- N-07: Rewrote it as dark, blunt, irreverent humor and removed the wholesome handshake ending.
- N-08: Four lines, strictly alternating barry/peaux/barry/peaux, each at most 90 characters.

### Iteration 1 (from refiner): 81.6/100, below bar

> **Barry:** Pick up your cigarette butts, boy, or I'll bodyslam you into this gravel.  
> **Peaux:** Loud as your Hoon dirt track, and twice as dead. My brothers hit harder than you.  
> **Barry:** Thirty years hauling freight, and I'll Irish Whip that eyeliner clean off your face.  
> **Peaux:** Swing, redneck. I'll stomp you out cold in your own driveway. Knockout, then bed.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **9** | 9: Only the two GDD wrestlers speak and no other characters appear. The speaker ids are lowercase 'barry' and 'peaux', and the full GDD name and nickname never appear. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **10** | 10: Every win or loss threat is a knockout or being put down. There are no pins, titles or other match types. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: Only MVP moves are named: the bodyslam, the Irish Whip and a stomp on a downed opponent. No invented finishers. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **6** | 6: The cigarette-butt grievance and the trucker background are right and blunt. But 'Thirty years hauling freight' contradicts the Style Guide's 'Early Thirties' and invents a career length. Barry's own lines never touch his dirt track, mustache or gut. |  |
| N-05 Peaux sounds like Peaux | 3 | **8** | 8: Peaux is sullen and cutting. He calls Barry 'redneck', brings up his football-playing brothers, and Barry's line mentions the eyeliner. It misses his youth, the eyeliner-over-eye-black line and the expulsion. |  |
| N-06 Rooted in Duckville | 2 | **7** | 7: Gravel and driveway place it in a trailer park, and the two read as neighbours. Duckville and the trailers are never named, and Hoon is mentioned only as Barry's track. | 10: place names: ['hoon']; trailer-park details: ['cigarette butts', 'dirt track', 'driveway', 'gravel'] |
| N-07 Dark Southern humor | 3 | **7** | 7: The tone is blunt and irreverent, and 'twice as dead' is dark. Lines like 'Knockout, then bed' and 'Swing, redneck' read as fairly stock trash talk, not Squidbillies or Eastbound & Down absurdity. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: There are exactly four lines, the speakers alternate, and the longest line is about 84 characters, under the 90 limit. | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* Barry's "Thirty years hauling freight" contradicts his early-thirties age and invents a backstory, and the exchange never names Duckville or leans hard on the grim humor.

**Refiner** (revising iteration 1):
- N-04: Removed 'thirty years' claim; added Barry's mustache/gut as targets, Hoon dirt track, blunt country voice ('them cigarette butts').
- N-07: Replaced stock bravado ('Knockout, then bed') with darker, weirder insults: burying him behind the track, gut-came-with-the-truck jab.
- N-06: Named Duckville gravel and kept driveway; neighbours feel.
- N-05: Added eyeliner-over-eye-black, expelled at sixteen, tougher-than-Barry edge; removed 'then bed'.
- N-01: Speaker ids kept as schema enum values 'barry' and 'peaux' (schema doesn't allow full names); only the two wrestlers speak.

### Iteration 2 (from refiner): 86.8/100, PASS

> **Barry:** Pick up them cigarette butts, boy, or I'll bodyslam you into the Duckville gravel.  
> **Peaux:** Nice mustache, Barry. Did the gut come with the truck? Eyeliner beats eye black, redneck.  
> **Barry:** I'll Irish Whip that eyeliner off your face and bury you behind my Hoon dirt track.  
> **Peaux:** Kicked out at sixteen, still tougher than you. Swing so I can stomp you in your driveway.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **10** | 10: Only Barry and Peaux speak, and the draft names no other wrestlers or new characters. The speaker ids match the GDD names. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **10** | 10: There is no talk of pinfalls, titles, belts, submissions or other match types. The threats are only about beating the other man up. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: The only moves named are the bodyslam, the Irish Whip and a stomp. All three exist in the MVP. There are no invented finishers. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **8** | 8: Barry's cigarette-butt line and the Hoon dirt track come straight from his profile. The mustache and gut appear only as Peaux's jabs. Barry's own voice has no trucker or stubborn-old-man detail. |  |
| N-05 Peaux sounds like Peaux | 3 | **8** | 8: Peaux uses the eyeliner-over-eye-black joke, the sixteen expulsion and the 'redneck' dig, all from his profile. The older football-playing brothers, the source of his toughness, are missing. The tone is cutting but not especially sullen. |  |
| N-06 Rooted in Duckville | 2 | **8** | 8: Duckville gravel and driveways set the scene, and the two read as neighbours. Barry's 'Hoon dirt track' sits in a different town, which muddies a scene set in the Duckville trailer park. Nothing in the lines places them at the trailers. | 10: place names: ['duckville', 'hoon']; trailer-park details: ['cigarette butts', 'dirt track', 'driveway', 'gravel'] |
| N-07 Dark Southern humor | 3 | **7** | 7: There are blunt insults and a 'bury you' threat. But 'Did the gut come with the truck?' and 'Swing so I can stomp you' are close to standard insult banter. They lack the dark, absurd Squidbillies or Eastbound & Down edge the GDD calls for. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: There are exactly four lines with speakers alternating. The longest line is 89 characters, so none goes over 90. | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* The exchange is solid and on-canon, but the humor is only moderately dark and some character beats (Barry's trucker identity, Peaux's football brothers) are thin or missing.

