# Style Guide Agent run `2026-11-22T203746Z`

Model `claude-sonnet-5-5` · threshold 85/100 · every hard rule >= 7/10 · every other rule >= 6/10 · max 3 refinements · no human input during the run.

**Planted-violation demo.** Iteration 0 of each task is a hand-written, deliberately off-style draft from `fixtures/`, not Generator output. Everything after it is the agent working alone.

| Task | Result | Score per iteration |
|---|---|---|
| `trailer_backdrop` | **approved** | 1.1 → 86.7 |
| `trash_talk_prematch` | **rejected** | 11.1 → 77.9 → 77.9 → 78.9 |

## `trailer_backdrop` (art_brief)

*Brief:* An image-generation brief for one single-wide trailer for the back row of the Duckville arena backdrop (the 'trailer along the back' in the chosen arena look). The Art Wrangler would feed the prompt to an image model to make a placeholder sprite.

### Iteration 0 (from seed): 1.1/100, below bar; hard rules under 7: A-01, A-02, A-04, A-05, A-08; rules under 6: A-03, A-06, A-07

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
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **0** | 0: The prompt asks for a photorealistic 3D render with smooth gradients and 8k cinematic lighting, which is the opposite of early-90s arcade pixel art. The negative prompt also bans pixel art. | 0: 0/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game); off-style terms: "3D" (3D render), "Photorealistic" (photorealism), "8k" (photorealism), "smooth gradients" (painterly/smooth) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **0** | 0: The palette is four neon and white colours, and none of them is from the Style Guide's 14 hex colours. No off-palette colour is justified. | 0: 0/4 hex colours are on the Style Guide palette; off-palette: #00FFFF, #FF00FF, #FFFFFF, #1A1A1A; only 0 palette colours used (need at least 4 for shading ramps) |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **0** | 0: The lighting is night with moonlight and neon glow. The guide calls for late-afternoon golden hour, a warm orange sky and long shadows. | 0: 0/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows); conflicting light: "night", "Night" |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **0** | 0: The scene has snowy mountains, a city skyline and neon signs, all of which the guide excludes. Mountains are named outright as banned. | 0: Banned scenery in the prompt: "mountains" (mountains), "city" (city), "urban" (city), "city" (city), "skyline" (city), "neon" (city fixtures), "neon" (city fixtures), "snowy" (wrong biome/genre) |
| A-05 (hard) Deep woods, not a city | 2 | **0** | 0: The setting is an urban lot with a skyline and neon signs, so it reads as a city. It does not read as a trailer park in the woods. | 0: Banned scenery in the prompt: "mountains" (mountains), "city" (city), "urban" (city), "city" (city), "skyline" (city), "neon" (city fixtures), "neon" (city fixtures), "snowy" (wrong biome/genre) |
| A-06 Disrepair: nothing has been kept up | 3 | **0** | 0: The brief asks for the opposite of disrepair: it is brand-new, spotless, with a manicured lawn. | 0: 0 distinct neglect details (none); contradicting kept-up wording: "brand-new", "freshly painted", "spotless", "manicured" |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **0** | 1: The roof has solar panels, which are modern. It has no old TV antenna or DirecTV-style dish as the guide requires. | 0: No old TV antenna or satellite dish on the trailer roof. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **1** | 1: The size is 1200×260 px, which is wide and short and is not tied to Barry's 240 px. A single-wide should be about 480 px tall. The scale note gives no pixel comparison. | 1: height 260px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height); scale_note does not tie the size to the 240-px wrestlers |

*Evaluator summary:* The draft contradicts the Style Guide on every rule: it asks for a photorealistic neon city night scene with a brand-new trailer, and the guide wants golden-hour pixel art of a decaying trailer in the woods.

**Refiner** (revising iteration 0):
- A-01: Rewrote the prompt as crisp early-90s arcade pixel art at 1 native px per art pixel, naming WrestleFest, Final Fight and Streets of Rage. Moved 3D, photoreal, vector and gradients to the negative prompt and removed pixel art from it.
- A-02: Palette is now the 14 Style Guide hex colours, named in the prompt.
- A-03: Lighting is now late-afternoon golden hour with a warm orange sky and long shadows.
- A-04: Removed mountains, skyline and neon. The scene is limited to the trailer, pines, sky, an electric pole and dirt, gravel and weeds.
- A-05: Placed the trailer in a clearing among thick pines. Urban elements are gone from the prompt and banned in the negative prompt.
- A-06: Added specific neglect details: rust, a tarp-patched sagging roof, a missing skirting panel, a cracked window, a crooked door and weeds.
- A-07: Replaced the solar panels with a bent TV antenna and a rusty DirecTV-style dish on the roof.
- A-08: Set the size to 960x480 px and wrote a scale note tying it to Barry's 240-px sprite (about 2x).

### Iteration 1 (from refiner): 86.7/100, PASS

- **subject:** A single-wide rundown trailer for the back row of the Duckville arena backdrop, in a clearing among thick North Louisiana pines
- **prompt:** Crisp hand-placed early-90s arcade pixel art, 16-bit style like WWF WrestleFest, Final Fight and Streets of Rage, drawn at 1 native pixel per art pixel with hard pixel edges and limited shading ramps. Side view of one single-wide trailer, about 480 px tall, parked in a dirt and gravel clearing with weeds, thick North Louisiana pine trees, a warm orange sky and an electric pole behind it. Late-afternoon golden hour, warm orange light from the west, long shadows falling across the dirt. The trailer is in long-neglected disrepair: rusted, streaked, faded siding, a sagging roof patched with a blue tarp, a missing skirting panel showing dark space underneath, a cracked window with a torn curtain, a dented door hanging crooked, weeds growing around stacked cinder blocks. A bent old rooftop TV antenna and a rusty DirecTV-style satellite dish are attached to the roof. Palette: #10151A #17243A #25372C #35523A #657A3B #795037 #A74727 #D26938 #E19A36 #FFD65A #2872B8 #7435A8 #C7352C #E6DFCC
- **negative_prompt:** 3D render, photorealistic, photograph, vector art, anime, painterly, smooth gradients, blur, anti-aliasing, mountains, hills, cliffs, city, skyline, neon, highway, crowds, snow, night, overcast, new, clean, freshly painted, solar panels, manicured lawn, text, watermark
- **palette:** `["#10151A", "#17243A", "#25372C", "#35523A", "#657A3B", "#795037", "#A74727", "#D26938", "#E19A36", "#FFD65A", "#2872B8", "#7435A8", "#C7352C", "#E6DFCC"]`
- **lighting:** Late-afternoon golden hour: warm orange sky, warm orange and yellow highlights on the trailer's west side, long shadows falling across the dirt
- **details:** Rusted, streaked, faded siding; sagging roof with a blue tarp patch; missing skirting panel; cracked window with a torn curtain; crooked dented door; weeds around cinder blocks; bent rooftop TV antenna and a rusty DirecTV-style dish on the roof; pines and an electric pole behind.
- **size_px:** `{"width": 960, "height": 480}`
- **scale_note:** Single-wide trailer is 480 px tall, about 2x Barry's 240-px standing sprite (Peaux is 215 px), at 1 native px per art pixel in the 960x540 native view.
- **layer:** background

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **9** | 9: Prompt names the right arcade references, 1 native px per art pixel, hard edges, and the negative prompt blocks 3D, photo, vector, anime, painterly and gradients. Small gap: it does not name Technos Japan, Capcom or SNK, and it sets no pixel-density or outline convention. | 10: 3/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **9** | 9: All 14 Style Guide hexes are in the prompt and in the palette field, so the palette is fully on-guide with no off-palette colours. Small gap: the prompt lists the hexes without saying which colour goes where, and the blue tarp only loosely maps to #2872B8. | 10: 14/14 hex colours are on the Style Guide palette |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **9** | 9: Golden hour is stated in the prompt and in the lighting field, with a warm orange sky, western light and long shadows across the dirt, which matches D-016. Small gap: the trailer is a side view, so it is unclear how shadows would fall in a flat sprite. | 10: 4/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows) |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **8** | 8: The scene holds only trailer, pines, sky, an electric pole and dirt, gravel and weeds. Mountains, hills and cliffs are named in the negative prompt. Small gap: the blue tarp, cinder blocks and cracked-window details are fine, but the prompt never limits the scene to the whitelist, and the pole is placed behind a single trailer. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-05 (hard) Deep woods, not a city | 2 | **8** | 8: Thick pines and a clearing give a woods setting, and the negative prompt blocks city, skyline, neon, highway and crowds. Small gap: nothing says the place is remote or lacks suburban polish. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-06 Disrepair: nothing has been kept up | 3 | **9** | 9: The prompt lists many concrete neglect details: rust, streaks, faded siding, a sagging patched roof, a missing skirting panel, a cracked window, a crooked door and weeds. The negative prompt blocks new and clean. The detail is strong but fairly generic, since it could describe any run-down trailer. | 10: 12 distinct neglect details (bent, cracked, dented, faded, missing, patch, patched, rusted, rusty, sagging, tarp, weeds) |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **10** | 10: The prompt and details both put a bent old rooftop TV antenna and a rusty DirecTV-style dish on the roof, as the guide asks. | 10: Roof carries a TV antenna or satellite dish. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **7** | 7: The brief gives 480 px tall, twice Barry's 240 px, and the Peaux comparison, so the scale is correct. The size_px field says 960 wide by 480 tall. The prompt adds an electric pole, pines and sky behind the trailer, so a 480 px canvas cannot hold both the trailer and the backdrop, and the trailer will not read as 480 px. | 10: height 480px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height) |

*Evaluator summary:* The size and scale are ambiguous: the canvas is 480 px tall yet the prompt adds a pole, pines and sky around a trailer that must itself be 480 px tall.


## `trash_talk_prematch` (trash_talk)

*Brief:* A four-line pre-match trash-talk exchange between Barry and Peaux, spoken in the trailer park just before the bell. Candidate flavour text for the moment before the versus text; it is not wired into the game.

### Iteration 0 (from seed): 11.1/100, below bar; hard rules under 7: N-01, N-02, N-03; rules under 6: N-04, N-05, N-06, N-07, N-08

> **Barry:** Tonight at the arena I'm gonna pin you one-two-three and take the world championship belt, kid!  
> **Peaux:** Not before my piledriver and a steel chair put you through the announce table, old man.  
> **Barry:** The crowd came to see "The Hammer" win, and the Hammer always wins!  
> **Barry:** Hulk Hogan taught me everything. Let's give the fans a great show and shake hands after.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **0** | 2: Speakers are Barry and Peaux, but Barry names himself "The Hammer", an invented nickname replacing "Hot Potato", and Hulk Hogan, a real wrestler, is referenced. The rule bans real wrestlers and new names. | 0: other wrestlers named: ['Hogan', 'Hulk']; nicknames not in the GDD: ['The Hammer'] |
| N-02 (hard) Knockout is the only way to win | 3 | **0** | 0: The draft is about pinfalls and a championship belt, both explicitly excluded. The only way to win in the docs is a KO. | 0: Non-MVP mechanics mentioned: "championship" (title/belt), "belt" (title/belt), "pin" (pinfall), "one-two-three" (pinfall) |
| N-03 (hard) Only moves that exist in the MVP | 2 | **0** | 0: Peaux threatens a piledriver, which is not in the MVP. The steel chair and announce table are also outside the move set. | 0: Moves the game doesn't have: "piledriver", "steel chair", "table" |
| N-04 Barry sounds like Barry | 3 | **2** | 2: Barry sounds like a generic wrestling promo. He has no trucker, Hoon dirt track, cigarette butt or mustache details, and the final line is soft and friendly, which contradicts his stubborn, punk-beating profile. |  |
| N-05 Peaux sounds like Peaux | 3 | **2** | 2: Peaux's line is generic wrestling bravado. It has no goth, eyeliner, brothers or rednecks angle. "Old man" is the only nod to his youth. |  |
| N-06 Rooted in Duckville | 2 | **0** | 1: The setting is an arena with a crowd, fans and an announce table, which the rule forbids. There is no Duckville, trailer or gravel, and the two do not read as neighbours. | 0: place names: none; trailer-park details: none |
| N-07 Dark Southern humor | 3 | **2** | 2: This is stock wrestling-promo bravado. The closing sportsmanship line is wholesome. There is no dark, blunt Southern humor in the Squidbillies or Eastbound & Down vein. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **3** | 3: There are four lines, but the speakers don't alternate (Barry speaks twice in a row at the end). Lines 1 and 2 exceed 90 characters, at about 97 and 88. Check: line 2 is about 88 characters, so only line 1 is clearly over, and line 4 is about 87. | 5: speakers don't alternate; 1 line(s) over 90 characters ([95]) |

*Evaluator summary:* The draft is a generic arena wrestling promo that violates the KO-only, MVP-moves, no-real-wrestlers and Duckville rules, and it doesn't sound like either character.

**Refiner** (revising iteration 0):
- N-02: Removed pin, belt and championship talk; the stakes are now getting knocked out cold.
- N-01: Removed Hulk Hogan and 'The Hammer'; Barry uses 'Hot Potato' exactly.
- N-03: Removed piledriver, steel chair and table; only bodyslam, Irish Whip and stomping appear.
- N-06: Moved the exchange to the Duckville trailer park (gravel, trailer, driveway) and made them neighbours. Removed arena, crowd and fans.
- N-04: Barry is now blunt and country, with cigarette butts, diesel and his trailer. Cut the handshake.
- N-05: Peaux is now sullen and cutting, with his brothers, eyeliner and 'old man'.
- N-07: Rewrote with dark, irreverent trailer-park jabs, not a stock promo.
- N-08: Four lines alternating Barry/Peaux/Barry/Peaux, each under 90 characters.

### Iteration 1 (from refiner): 77.9/100, below bar; rules under 6: N-06

> **Barry:** Pick up your cigarette butts, boy, or I'll bodyslam you into the gravel by my trailer.  
> **Peaux:** My brothers hit harder than you, and they didn't smell like diesel and regret.  
> **Barry:** Hot Potato's gonna Irish Whip that eyeliner clean off your face, kid.  
> **Peaux:** Swing, old man. I'll stomp you out cold and cut through your driveway.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **9** | 9: Only Barry and Peaux speak, and Barry's 'Hot Potato' nickname is used correctly. The brothers are mentioned but not named, so no new character is introduced. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **9** | 9: The only win talk is 'stomp you out cold', which is a knockout. There are no pinfalls, belts or other match types. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: Every move named (bodyslam, Irish Whip, stomp) exists in the MVP. No invented finishers. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **7** | 7: The cigarette butts, trailer and Hot Potato nickname are right, and the tone is blunt. The long-haul trucker, Hoon dirt track, Wild Bill mustache and beer gut never appear, and line 3 could be any tough guy. |  |
| N-05 Peaux sounds like Peaux | 3 | **7** | 7: The older football-playing brothers and the cutting, sullen tone fit Peaux. Nothing hits the goth look, the school expulsion or his contempt for loudmouthed rednecks, and 'cut through your driveway' is muddled. |  |
| N-06 Rooted in Duckville | 2 | **5** | 8: Gravel, trailer and driveway place it in the trailer park, and the two read as hostile neighbours. Duckville is never named, and nothing ties it to this specific park. | 5: place names: none; trailer-park details: ['cigarette butts', 'driveway', 'gravel', 'trailer'] |
| N-07 Dark Southern humor | 3 | **7** | 7: 'diesel and regret' and the cigarette-butt feud have a dark, blunt Southern bite. Lines 3 and 4 fall back on standard wrestling bravado ('Swing, old man'). |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: Exactly four lines, speakers alternate, and the longest line is about 86 characters, under the 90 limit. | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* The exchange is clean on rules and length but thin on Rural Rumble specifics: Barry's trucker and dirt-track side and Peaux's goth and redneck-hating side barely show, Duckville is never named, and lines 3–4 drift toward generic banter.

**Refiner** (revising iteration 1):
- N-06: Named Duckville, kept gravel and dirt lot, and made the neighbour grievance explicit.
- N-04: Replaced the generic threat with a long-haul trucker line, a Hoon reference and Irish Whip. Kept the cigarette-butt opener.
- N-05: Dropped the muddled driveway clause. Peaux now mocks Barry as a loudmouthed redneck in cutoffs and refers to his football-playing brothers.
- N-07: Made lines 3 and 4 darker and more irreverent (mudflap whipping, burying him in the dirt lot) in place of stock bravado.
- N-02: Made the KO explicit: 'knock you out cold'.
- N-01: Kept only the two speakers. Barry's nickname is dropped from the text, and the brothers stay unnamed.

### Iteration 2 (from refiner): 77.9/100, below bar; rules under 6: N-04

> **Barry:** Pick up your cigarette butts, boy, or I'll bodyslam you into this Duckville gravel.  
> **Peaux:** Four brothers played football on me. You're just a loud redneck in cutoffs.  
> **Barry:** Twenty years hauling freight, and I'll whip that eyeliner off you like a Hoon mudflap.  
> **Peaux:** Cry louder, old man. I'll knock you out cold and bury you in the dirt lot.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **9** | 9: Only Barry and Peaux speak, and no other wrestlers or named characters appear. The speaker ids are lowercase and the nickname never appears in a line, so the GDD name form isn't shown. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **9** | 9: The only win talk is a knockout. There are no pins, belts or other match types. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **9** | 9: The bodyslam is an MVP move. The "whip" line reads as an Irish Whip, though it is mostly figurative. No suplexes or finishers appear. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **5** | 5: The cigarette butts, Hoon and the trucker background all fit Barry. But "Twenty years hauling freight" contradicts the Style Guide's "Early Thirties" and is an invented figure. The GDD only says "years". The lines also skip his mustache, beer gut and stubborn streak. |  |
| N-05 Peaux sounds like Peaux | 3 | **7** | 7: Peaux's lines use his football-playing brothers, his disdain for rednecks and his cutting tone. But "Four brothers" is an invented number, and the football line is garbled. Nothing refers to his expulsion at sixteen. "Old man" suits his youth. |  |
| N-06 Rooted in Duckville | 2 | **9** | 9: The setting is Duckville gravel and a dirt lot, and the two read as neighbours. There are no crowds or arenas. Hoon is Barry's track town, which is fine. | 10: place names: ['duckville', 'hoon']; trailer-park details: ['cigarette butts', 'dirt lot', 'gravel'] |
| N-07 Dark Southern humor | 3 | **7** | 7: "Bury you in the dirt lot" is dark and the insults are blunt and Southern. Much of the rest is standard promo-style taunting ("Cry louder, old man"). It lacks the absurd Squidbillies or Eastbound & Down bite. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: There are exactly four lines and the speakers alternate. All lines are well under 90 characters (the longest is about 86). | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* Barry's "Twenty years hauling freight" contradicts his early-thirties age in the Style Guide, and Peaux's "Four brothers" is an invented number.

**Refiner** (revising iteration 1):
- N-06: Named Duckville in line 1 and kept gravel, driveway and the neighbour feud over cigarette butts.
- N-04: Barry now cites long-haul trucking and Hoon dirt track, and the cigarette-butt grudge.
- N-05: Peaux references his football brothers, calls Barry a loudmouthed redneck, removed muddled driveway clause.
- N-07: Replaced stock bravado with blunter, darker jokes (mustache, running eyeliner, brothers beating him).
- N-02: Made the win explicit: 'knock you out cold'.
- N-01: Still only Barry and Peaux speak; brothers unnamed; no new characters.

### Iteration 3 (from refiner): 78.9/100, below bar; rules under 6: N-04

> **Barry:** Duckville's got one rule, boy: pick up your cigarette butts or I'll bodyslam you.  
> **Peaux:** Four football brothers beat me daily, old man. Your mustache wrestling won't scare me.  
> **Barry:** Thirty years hauling freight and running Hoon dirt. Your eyeliner's gonna run too.  
> **Peaux:** Keep yapping, redneck. I'll knock you out cold in your own gravel driveway.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **9** | 9: Only Barry and Peaux speak and no other wrestlers or named characters appear. The speaker tags are lowercase ids rather than the full GDD names, which is a small gap. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **10** | 10: The only win or loss talk is a knockout, and there are no pinfalls, titles or other match types. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: The only move named is the bodyslam, which is in the MVP. No invented moves. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **5** | 5: Barry's cigarette-butt grudge, Hoon dirt track and trucker background are all from his profile. But "Thirty years hauling freight" contradicts the Style Guide's early-thirties age, which caps this rule at 5. His voice is blunt and country, but the mustache and beer gut are not used. |  |
| N-05 Peaux sounds like Peaux | 3 | **7** | 7: The football brothers, eyeliner and "redneck" jab match Peaux's profile and give a young, cutting voice. "Four" brothers and "beat me daily" are invented details, and the expulsion and sullen goth tone are missing. |  |
| N-06 Rooted in Duckville | 2 | **9** | 9: The setting is Duckville, with the gravel driveway and a neighbours-who-hate-each-other feel. There are no arenas or crowds. The lines are a little thin on trailers or dirt-lot detail. | 10: place names: ['duckville', 'hoon']; trailer-park details: ['cigarette butts', 'dirt', 'driveway', 'gravel'] |
| N-07 Dark Southern humor | 3 | **6** | 6: The tone is blunt and irreverent, but the jabs ("mustache wrestling", "eyeliner's gonna run", "Keep yapping") are stock trash talk. There is little of the dark, absurd Squidbillies or Eastbound & Down edge. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: There are exactly four lines, the speakers alternate, and each line is under 90 characters (the longest is about 87). | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* Barry's "Thirty years hauling freight" contradicts his early-thirties age in the Style Guide, and the humor is more generic trash talk than dark Southern comedy.

