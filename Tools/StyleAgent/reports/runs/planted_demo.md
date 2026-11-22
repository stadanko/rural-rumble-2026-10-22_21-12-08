# Style Guide Agent run `2026-11-22T204039Z`

Model `claude-sonnet-5-5` · threshold 85/100 · every hard rule >= 7/10 · every other rule >= 6/10 · max 3 refinements · no human input during the run.

**Planted-violation demo.** Iteration 0 of each task is a hand-written, deliberately off-style draft from `fixtures/`, not Generator output. Everything after it is the agent working alone.

| Task | Result | Score per iteration |
|---|---|---|
| `trailer_backdrop` | **approved** | 1.1 → 83.9 → 87.8 |
| `trash_talk_prematch` | **approved** | 9.5 → 78.9 → 85.3 → 92.6 |

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
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **0** | 0: The prompt asks for a photorealistic 3D render with smooth gradients and cinematic 8k lighting, which is the opposite of early-90s arcade pixel art. The negative prompt also bans 'pixel art'. | 0: 0/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game); off-style terms: "3D" (3D render), "Photorealistic" (photorealism), "8k" (photorealism), "smooth gradients" (painterly/smooth) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **0** | 0: The palette is four neon and white colours, none from the Style Guide's 14 hex values, and there is no justification for them. | 0: 0/4 hex colours are on the Style Guide palette; off-palette: #00FFFF, #FF00FF, #FFFFFF, #1A1A1A; only 0 palette colours used (need at least 4 for shading ramps) |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **0** | 0: The lighting is night with neon, which directly contradicts the late-afternoon golden-hour look. | 0: 0/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows); conflicting light: "night", "Night" |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **0** | 0: The scene includes snowy mountains and a city skyline, which the guide explicitly bans, and has no pines or electric poles. | 0: Banned scenery in the prompt: "mountains" (mountains), "city" (city), "urban" (city), "city" (city), "skyline" (city), "neon" (city fixtures), "neon" (city fixtures), "snowy" (wrong biome/genre) |
| A-05 (hard) Deep woods, not a city | 2 | **0** | 0: The setting is an urban lot with neon signs and a skyline, the opposite of a trailer park in the middle of the woods. | 0: Banned scenery in the prompt: "mountains" (mountains), "city" (city), "urban" (city), "city" (city), "skyline" (city), "neon" (city fixtures), "neon" (city fixtures), "snowy" (wrong biome/genre) |
| A-06 Disrepair: nothing has been kept up | 3 | **0** | 0: The brief asks for a new, spotless, manicured trailer, which is the opposite of disrepair. | 0: 0 distinct neglect details (none); contradicting kept-up wording: "brand-new", "freshly painted", "spotless", "manicured" |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **0** | 1: The roof carries modern solar panels. The guide calls for an old TV antenna or DirecTV-style dish. | 0: No old TV antenna or satellite dish on the trailer roof. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **1** | 1: The size is 1200×260 px, far wider than it is tall. A single-wide trailer should be about 480 px tall, twice Barry's 240 px. The scale note gives no reference to Barry. | 1: height 260px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height); scale_note does not tie the size to the 240-px wrestlers |

*Evaluator summary:* The draft contradicts the docs on almost every rule: it is a photoreal neon night city render of a new trailer with mountains and solar panels, not a pixel-art, golden-hour, run-down woodland trailer at about 480 px tall.

**Refiner** (revising iteration 0):
- A-01: Prompt now asks for crisp hand-placed early-90s arcade pixel art citing WrestleFest, Final Fight, Streets of Rage at 1 native px per pixel; 3D/photoreal/smooth terms moved to negative_prompt and 'pixel art' removed from it.
- A-02: Palette field and prompt use the 14 Style Guide hex colours; off-palette neon/white removed.
- A-03: Lighting changed to late-afternoon golden hour with warm orange sky and long shadows.
- A-04: Removed mountains, skyline and snow; background is only pines, sky, an electric pole, dirt and gravel; banned items placed in negative_prompt.
- A-05: Setting is a woodland trailer park on dirt and gravel with thick pines; no city or neon.
- A-06: Replaced clean/new wording with rust streaks, peeling siding, sagging patched roof, cracked window, missing skirting, weeds, sagging porch.
- A-07: Replaced solar panels with a bent old TV antenna and a rusty DirecTV-style dish on the roof.
- A-08: Size set to 960x480 (480 px tall); scale_note ties it to Barry's 240-px sprite at 1 native px per art pixel.

### Iteration 1 (from refiner): 83.9/100, below bar

- **subject:** A rundown single-wide trailer for the back row of the Duckville trailer park arena, in the woods, with a bent TV antenna and rusty DirecTV-style dish on the roof
- **prompt:** Crisp hand-placed early-90s arcade pixel art sprite in the style of WWF WrestleFest, Final Fight and Streets of Rage (Technos Japan, Capcom, SNK), drawn at 1 native pixel per art pixel with hard pixel edges and limited-colour dithered shading. A single-wide trailer in disrepair, seen straight on, standing about 480 px tall, twice the height of a 240 px wrestler. Faded, peeling siding in #795037 and #E6DFCC with rust streaks in #A74727, sagging patched roof panels, a cracked window patched with cardboard, a missing skirting panel showing dark shadow in #10151A, weeds growing along the base, a sagging wooden porch with a broken step. A bent old rooftop TV antenna and a rusty DirecTV-style dish on the roof. Standing on dirt and gravel, thick North Louisiana pines in #25372C and #35523A behind it, an electric pole with sagging wires beside it, warm orange sky. Late-afternoon golden-hour light in #D26938 #E19A36 #FFD65A with long shadows falling across the dirt, deep shadows in #17243A. Palette: #10151A #17243A #25372C #35523A #657A3B #795037 #A74727 #D26938 #E19A36 #FFD65A #2872B8 #7435A8 #C7352C #E6DFCC
- **negative_prompt:** 3D render, photorealistic, photo, vector art, anime, painterly, smooth gradients, anti-aliasing, blur, 8k, mountains, hills, cliffs, city, skyline, urban, neon, highway, crowds, snow, night, new, clean, freshly painted, manicured, solar panels, text, watermark
- **palette:** `["#10151A", "#17243A", "#25372C", "#35523A", "#657A3B", "#795037", "#A74727", "#D26938", "#E19A36", "#FFD65A", "#2872B8", "#7435A8", "#C7352C", "#E6DFCC"]`
- **lighting:** Late-afternoon golden hour: warm orange sky, warm low sun on the trailer's side, long shadows falling across the dirt
- **details:** Rust streaks down faded, peeling siding; sagging patched roof; cracked window patched with cardboard; missing skirting panel; weeds along the base; sagging porch with broken step; bent rooftop TV antenna and rusty DirecTV-style dish; surrounded by dirt, gravel, pines and an electric pole.
- **size_px:** `{"width": 960, "height": 480}`
- **scale_note:** 480 px tall, roughly twice Barry's 240-px standing sprite (Peaux is 215 px), at 1 native px per art pixel in the 960×540 native view; a single-wide trailer is about 960 px wide at this height.
- **layer:** background

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **8** | 8: Prompt names the right studios/games, 1 native px per art pixel, hard edges, and negatives block 3D/vector/painterly. Small gap: 'dithered shading' is fine, but nothing says outlined sprite style or a Duckville-specific look; 960 px wide sprite is large for a single prop. | 10: 3/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **8** | 8: The prompt lists all 14 hexes and assigns many to specific elements. Minor gap: it doesn't say the 14 colours are the only ones used, and blue #2872B8, purple #7435A8 and red #C7352C are listed with no role. | 10: 14/14 hex colours are on the Style Guide palette |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **9** | 9: Golden-hour light, warm orange sky, low sun and long shadows across the dirt match D-016, with palette hexes assigned to the light. | 10: 4/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows) |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **8** | 8: Content stays within trailer, pines, pole, sky, dirt and gravel, and the negatives exclude mountains, hills and cliffs. Small gap: a porch and cardboard are extra props, and a trailer 'sprite' surrounded by pines and a pole blurs one asset with a scene. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-05 (hard) Deep woods, not a city | 2 | **8** | 8: Pines, dirt and gravel and a negative list that excludes city, neon, highway and crowds give a deep-woods feel with no city cues. 'In the woods' is shown only by pines behind the trailer, with no sense of isolation or encroaching forest. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-06 Disrepair: nothing has been kept up | 3 | **9** | 9: There are many concrete neglect details (peeling siding, rust streaks, patched sagging roof, cardboard-patched window, missing skirting, weeds, broken step), and the negatives block 'new' and 'clean'. | 10: 11 distinct neglect details (bent, broken, cracked, faded, missing, patched, peeling, rust, rusty, sagging, weeds) |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **10** | 10: The prompt, subject and details all put a bent old TV antenna and a rusty DirecTV-style dish on the roof. | 10: Roof carries a TV antenna or satellite dish. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **7** | 7: The brief gives 480 px tall, twice Barry's 240 px, and the native-pixel scale. However, it says the trailer is about 960 px wide, which fills the whole 960 px native view width and is an invented dimension. The doc doesn't give a width, and a single-wide at 480 px tall would be much narrower than 960 px. | 10: height 480px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height) |

*Evaluator summary:* The brief is strong on palette, light, disrepair and the antenna, but the unsupported 960 px width implies the trailer is as wide as the whole native screen. The lack of a pixel-outline and Arena 4 style anchor, and the pines and pole bundled into a single trailer sprite, also hold it back.

**Refiner** (revising iteration 1):
- A-08: Removed the 960 px width claim; trailer is now about 700 px wide in a 720×480 sprite, not spanning the full view.
- A-01: Added Arena 4 pixel style, bold dark pixel outlines and flat cel-style clusters; sprite is smaller.
- A-02: Stated that only the 14 colours are used and gave blue (trim), purple (tarp), red (appliance) roles.
- A-04: Made it a trailer-only sprite on a plain background; pines and pole moved to separate backdrop layers; extra scenery added to negatives.
- A-05: Details note pine woods with no other buildings or lights visible.
- A-06: Added a sagging tarp patch and a junked appliance by the porch.
- A-03: Specified sun low from the right with shadows falling left.

### Iteration 2 (from refiner): 87.8/100, PASS

- **subject:** A rundown single-wide trailer sprite for the back row of the Duckville trailer park arena, with a bent TV antenna and rusty DirecTV-style dish on the roof
- **prompt:** Crisp hand-placed early-90s arcade pixel art sprite in the style of WWF WrestleFest, Final Fight and Streets of Rage (Technos Japan, Capcom, SNK), matching Arena 4's pixel style, drawn at 1 native pixel per art pixel with hard pixel edges, bold dark #10151A pixel outlines, flat cel-style colour clusters and limited-colour dithered shading. One single-wide trailer only, as a standalone backdrop prop on a plain flat background, seen straight on, standing about 480 px tall, twice the height of a 240 px wrestler, about 700 px wide. Faded, peeling siding in #795037 and #E6DFCC with faded blue trim in #2872B8 and rust streaks in #A74727, sagging patched roof panels with a sagging purple #7435A8 tarp patch, a cracked window patched with cardboard, a missing skirting panel showing dark shadow in #10151A, weeds in #657A3B growing along the base, a sagging wooden porch with a broken step and a junked red #C7352C appliance beside it. A bent old rooftop TV antenna and a rusty DirecTV-style dish on the roof. Late-afternoon golden-hour light with the sun low from the right, warm orange light in #D26938 #E19A36 #FFD65A on the right side of the trailer, long shadows falling left, deep shadows in #17243A, with dark green #25372C and #35523A in the shaded siding edges. Use only these 14 colours, no others: #10151A #17243A #25372C #35523A #657A3B #795037 #A74727 #D26938 #E19A36 #FFD65A #2872B8 #7435A8 #C7352C #E6DFCC
- **negative_prompt:** 3D render, photorealistic, photo, vector art, anime, painterly, smooth gradients, anti-aliasing, blur, 8k, mountains, hills, cliffs, city, skyline, urban, neon, highway, crowds, snow, night, new, clean, freshly painted, manicured, solar panels, text, watermark, scene background, extra buildings
- **palette:** `["#10151A", "#17243A", "#25372C", "#35523A", "#657A3B", "#795037", "#A74727", "#D26938", "#E19A36", "#FFD65A", "#2872B8", "#7435A8", "#C7352C", "#E6DFCC"]`
- **lighting:** Late-afternoon golden hour: sun low from the right, warm orange light on the trailer's right side, long shadows falling left across the dirt
- **details:** Trailer-only sprite; the pines and electric pole are separate backdrop layers. Rust streaks down faded, peeling siding; sagging patched roof with tarp patch; cracked window patched with cardboard; missing skirting panel; weeds along the base; sagging porch with broken step; junked appliance by the porch; bent rooftop TV antenna and rusty DirecTV-style dish. Sits in thick North Louisiana pine woods with no other buildings or lights visible.
- **size_px:** `{"width": 720, "height": 480}`
- **scale_note:** 480 px tall, roughly twice Barry's 240-px standing sprite (Peaux is 215 px), at 1 native px per art pixel in the 960×540 native view; about 700 px wide, so it does not span the whole view.
- **layer:** background

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| A-01 (hard) Early-90s arcade pixel art at native scale | 3 | **8** | 8: Prompt names the right reference games and studios, 1 native px per art pixel, hard edges, flat cel clusters, and negatives for 3D/photo/vector/anime/painterly. Small gap: 'limited-colour dithered shading' and 'bold outlines' are generic, and a 700 px wide detailed sprite is not tied to Arena 4's actual look beyond a name drop. | 10: 3/3 arcade pixel-art markers in the prompt (pixel art / 16-bit or arcade / a named reference game) |
| A-02 (hard) Rural Rumble 14-colour palette | 2 | **9** | 9: The prompt lists all 14 hex values, assigns them to specific parts, and says 'only these 14 colours'. Small gap: image models can't hold to an exact palette, so a post-process palette-snap step is not mentioned. | 10: 14/14 hex colours are on the Style Guide palette |
| A-03 Late-afternoon golden-hour light (D-016) | 2 | **9** | 9: Golden hour is specified with a low sun, warm orange palette colours on the lit side and long shadows. Minor gap: the 'warm orange sky' is absent because the sprite is on a plain background, and the shadows have no dirt to fall on. | 10: 4/4 golden-hour cues (golden hour, late afternoon, warm orange light, long shadows) |
| A-04 (hard) Background whitelist: trees, skies, electric poles, trailers | 2 | **9** | 9: The scene is trailer-only, and the details say pines and the pole are separate layers. The negatives exclude mountains, hills, cliffs and extra buildings. The porch and appliance are plausible trailer-adjacent items, and nothing outside the whitelist appears. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-05 (hard) Deep woods, not a city | 2 | **7** | 7: Negatives block city, skyline, neon and highway, and the details put the trailer in thick pine woods. However the prompt asks for a plain flat background, and the pine woods line sits only in 'details', so the image gets no woods cue. | 10: No banned scenery (mountains, large hills, city, neon, other biomes) in the prompt. |
| A-06 Disrepair: nothing has been kept up | 3 | **10** | 10: Lists many concrete neglect details: peeling faded siding, rust streaks, sagging patched roof, a cracked window patched with cardboard, a missing skirting panel, weeds, a broken porch step and a junked appliance. It reads as years of neglect. | 10: 13 distinct neglect details (bent, broken, cracked, faded, missing, patch, patched, peeling, rust, rusty, sagging, tarp, weeds) |
| A-07 Trailer roofs carry old TV antennas or satellite dishes | 2 | **10** | 10: A bent old rooftop TV antenna and a rusty DirecTV-style dish are named in the subject, the prompt and the details. | 10: Roof carries a TV antenna or satellite dish. |
| A-08 (hard) Drawn to scale with the 240-px wrestlers | 2 | **8** | 8: The brief states 480 px tall as twice the 240 px wrestler, in native pixels, and mentions Peaux. Small gap: it says about 700 px wide in the prompt but size_px says 720, and a fixed-size image can't be guaranteed from the model. | 10: height 480px vs target 430-530px (a single-wide trailer stands roughly twice Barry's 240-px height) |

*Evaluator summary:* The brief is strong overall; its biggest remaining gap is that the woods setting reaches the image model only through 'details', while the prompt asks for a plain background, plus a 700/720 px width inconsistency.


## `trash_talk_prematch` (trash_talk)

*Brief:* A four-line pre-match trash-talk exchange between Barry and Peaux, spoken in the trailer park just before the bell. Candidate flavour text for the moment before the versus text; it is not wired into the game.

### Iteration 0 (from seed): 9.5/100, below bar; hard rules under 7: N-01, N-02, N-03; rules under 6: N-04, N-05, N-06, N-07, N-08

> **Barry:** Tonight at the arena I'm gonna pin you one-two-three and take the world championship belt, kid!  
> **Peaux:** Not before my piledriver and a steel chair put you through the announce table, old man.  
> **Barry:** The crowd came to see "The Hammer" win, and the Hammer always wins!  
> **Barry:** Hulk Hogan taught me everything. Let's give the fans a great show and shake hands after.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **0** | 2: Barry and Peaux are the speakers, but Barry is given an invented alias, "The Hammer", and Hulk Hogan, a real wrestler, is named. Both break the no-other-wrestlers and no-new-names rule. Barry's nickname is never given as "Hot Potato". | 0: other wrestlers named: ['Hogan', 'Hulk']; nicknames not in the GDD: ['The Hammer'] |
| N-02 (hard) Knockout is the only way to win | 3 | **0** | 0: The draft talks about pinfalls, a three-count and a championship belt. The GDD makes KO the only win condition and lists pinfall as a stretch goal. | 0: Non-MVP mechanics mentioned: "championship" (title/belt), "belt" (title/belt), "pin" (pinfall), "one-two-three" (pinfall) |
| N-03 (hard) Only moves that exist in the MVP | 2 | **0** | 0: Peaux threatens a piledriver, which is not an MVP move. The steel chair and announce table are also not in the game. | 0: Moves the game doesn't have: "piledriver", "steel chair", "table" |
| N-04 Barry sounds like Barry | 3 | **2** | 2: Barry sounds like a generic wrestling promo. He mentions none of the trucker life, the dirt track in Hoon, the cigarette butts, the mustache or the beer gut. "Old man" is aimed at him by Peaux, but Barry himself is not given a stubborn, country voice. | 10: Barry states no numbers the docs don't (ages, years, counts). |
| N-05 Peaux sounds like Peaux | 3 | **2** | 2: Peaux's line is a stock heel promo. It has no goth look, eyeliner, expulsion, football brothers or contempt for rednecks. The only link to the GDD is a slight "old man" jab at the age gap. | 10: Peaux states no numbers the docs don't (ages, years, counts). |
| N-06 Rooted in Duckville | 2 | **0** | 1: The exchange is set at an arena with a crowd, fans, an announce table and a championship. The brief calls for the Duckville trailer park, and the draft never mentions it. The two are not treated as neighbors. | 0: place names: none; trailer-park details: none |
| N-07 Dark Southern humor | 3 | **1** | 1: This is generic wrestling-promo bravado. The last line is wholesome, with a handshake and a "great show". There is no dark Southern, irreverent humor in the style of Squidbillies or Eastbound & Down. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **3** | 3: There are four lines, but Barry speaks the last two, so the speakers do not alternate. Lines 1, 2 and 4 are over 90 characters. Line 3 is about 68 characters. | 5: speakers don't alternate; 1 line(s) over 90 characters ([95]) |

*Evaluator summary:* The draft is a generic arena wrestling promo that breaks the hard rules (pinfall and belt talk, a piledriver, Hulk Hogan, an invented alias). It has no Duckville trailer-park setting and no character-specific voices for Barry or Peaux.

**Refiner** (revising iteration 0):
- N-02: Removed pin, three-count and belt talk; all threats now about knocking out.
- N-01: Removed 'The Hammer' and Hulk Hogan; only Barry and Peaux speak, no other wrestlers named.
- N-03: Removed piledriver, steel chair, table; only bodyslam, stomping a downed opponent and a knockout remain.
- N-06: Set in Duckville trailer park: gravel, trailer, driveway, neighbors feuding.
- N-07: Rewrote with dark, blunt Southern jokes; removed handshake and sportsmanship.
- N-04: Barry now gruff and stubborn, cigarette butts, gut reference.
- N-05: Peaux sullen and cutting: football brothers, contempt for rednecks.
- N-08: Alternating Barry/Peaux/Barry/Peaux, all lines under 90 characters.

### Iteration 1 (from refiner): 78.9/100, below bar; rules under 6: N-06

> **Barry:** Pick up them cigarette butts, boy, or I'll knock you cold in the gravel.  
> **Peaux:** My brothers played football on me for years. Your gut's gonna bruise worse than I do.  
> **Barry:** I'll bodyslam you into your mama's trailer. Y'all can share the drywall.  
> **Peaux:** Go ahead, redneck. I'll stomp you flat in your driveway and leave you for the dogs.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **10** | 10: Only Barry and Peaux speak. No other wrestlers or named characters are introduced. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **9** | 9: The threats are about knocking out or beating down, with no pinfalls, titles or other match types. 'Leave you for the dogs' is vague about how the match is won, though it doesn't contradict KO-only. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: Only MVP moves appear: bodyslam, stomping a downed opponent, and a knockout strike. No suplexes or finishers. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **6** | 6: Barry's cigarette-butt line and gravel threat come straight from his profile, and the blunt country voice fits. Nothing refers to the trucker background, the Hoon dirt track, the mustache or his stubbornness. Line 3 also invents Peaux's 'mama'. | 10: Barry states no numbers the docs don't (ages, years, counts). |
| N-05 Peaux sounds like Peaux | 3 | **7** | 7: The football brothers and the 'redneck' jab match the profile, and the tone is cutting. Nothing mentions the goth styling, eyeliner, black lipstick, or being expelled, and he doesn't sound especially young or sullen. | 10: Peaux states no numbers the docs don't (ages, years, counts). |
| N-06 Rooted in Duckville | 2 | **5** | 8: Gravel, trailer, driveway and drywall put the exchange in the trailer park, and the two read as neighbours. Duckville and the dirt-lot setting are never named, and no arena or crowd appears. | 5: place names: none; trailer-park details: ['cigarette butts', 'driveway', 'gravel', 'trailer'] |
| N-07 Dark Southern humor | 3 | **8** | 8: The humor is blunt and dark, especially 'share the drywall' and 'leave you for the dogs', and it avoids sports-movie bravado. It's still somewhat generic insult-trading and could sit in many Southern comedies. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: There are exactly four lines, the speakers alternate, and every line is under 90 characters (the longest is about 85). | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* Barry's lines are the weakest: they lean on the cigarette-butt joke and never use his trucker, dirt-track or mustache details, and neither character's lines feel distinct enough from generic trash talk.

**Refiner** (revising iteration 1):
- N-06: Named Duckville and kept gravel and trailer details.
- N-04: Added trucker and Hoon dirt-track references; removed the invented mama.
- N-05: Made Peaux sullen and young, with eyeliner and brothers references, and a deadpan 'old man' jab.
- N-07: Sharpened the humor with the diesel smell and the mustache-first stomp.
- N-02: Peaux's closing threat is now an explicit knockout: 'stomp you out cold'.

### Iteration 2 (from refiner): 85.3/100, below bar; rules under 6: N-08

> **Barry:** Pick up them cigarette butts, boy, or I'll knock you cold on this Duckville gravel.  
> **Peaux:** Cute, old man. My brothers hit harder than you, and they didn't smell like diesel.  
> **Barry:** Hauled freight for years, run a dirt track in Hoon. I'll bodyslam you through your trailer.  
> **Peaux:** Try it, redneck. I'll stomp you out cold, mustache first, eyeliner still perfect.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **10** | 10: Only Barry and Peaux speak. The only other person mentioned is Peaux's brothers, who are unnamed and come from the GDD profile. No new named characters or other wrestlers. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **10** | 10: All threats are about knocking the opponent out cold. There are no pinfalls, titles, submissions or other match types. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: The only moves named are the bodyslam and stomping a downed opponent, and both are in the MVP list. There are no invented finishers. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **8** | 8: Barry's lines use the cigarette-butt grudge, long-haul trucking, the Hoon dirt track and a blunt country voice. Line 3 reads like a résumé. His stubbornness and his beer gut or mustache are never voiced by Barry himself. | 10: Barry states no numbers the docs don't (ages, years, counts). |
| N-05 Peaux sounds like Peaux | 3 | **8** | 8: Peaux is cutting and young, with the eyeliner, the football-playing brothers and contempt for rednecks all drawn from his profile. It leans on 'old man' sass more than sullen goth, and there is no hint of his expulsion or troubled past. | 10: Peaux states no numbers the docs don't (ages, years, counts). |
| N-06 Rooted in Duckville | 2 | **9** | 9: The lines are set in Duckville gravel with trailers, and the two read as neighbours with a grudge. There are no arenas or crowds. The Duckville reference is a single name-drop. | 10: place names: ['duckville', 'hoon']; trailer-park details: ['cigarette butts', 'dirt track', 'gravel', 'trailer'] |
| N-07 Dark Southern humor | 3 | **7** | 7: The tone is blunt and irreverent, with a good diesel and eyeliner-perfect bite. The threats are fairly standard 'I'll knock you out' bravado, and it is not especially dark or Squidbillies-like. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **5** | 5: There are exactly four lines and the speakers alternate. Line 3 is 91 characters, over the 90 limit, which breaks the hard length constraint. | 8: 1 line(s) over 90 characters ([91]) |

*Evaluator summary:* Line 3 is 91 characters, over the 90-character cap, and the humor is blunt but only mildly dark, so it reads closer to a standard insult exchange than Squidbillies-style dark comedy.

**Refiner** (revising iteration 2):
- N-08: Rewrote line 3 and trimmed others so every line is under 90 characters.
- N-07: Added darker, pettier jokes: mustache crying, bodyslamming through mama's trailer, beer gut jab.
- N-04: Line 3 now a stubborn threat tied to the Hoon dirt track instead of a résumé; dropped 'years'.
- N-05: Added expulsion at sixteen (from the docs) and deadpan eyeliner jab; removed diesel line.
- N-06: Kept Duckville gravel and added the neighbour's trailer ('your mama's trailer').

### Iteration 3 (from refiner): 92.6/100, PASS

> **Barry:** Pick up them cigarette butts, boy, or I'll knock you cold in this Duckville gravel.  
> **Peaux:** Cute, old man. Got expelled at sixteen and still hit harder than your beer gut.  
> **Barry:** I run a dirt track in Hoon. I'll bodyslam you through your mama's trailer, boy.  
> **Peaux:** Try it, redneck. I'll stomp you till your mustache cries, eyeliner still perfect.  

| Rule | W | Score | Evaluator | Automated check |
|---|---|---|---|---|
| N-01 (hard) Only the two GDD wrestlers, named as the GDD names them | 2 | **10** | 10: Only Barry and Peaux speak. No other wrestlers or named characters appear. The speaker ids are barry and peaux, and the draft names no one else. | 10: Only Barry and Peaux speak; no other wrestlers or invented nicknames. |
| N-02 (hard) Knockout is the only way to win | 3 | **10** | 10: The only win or loss threat is "knock you cold", which is a knockout. There are no pinfalls, belts or other match types. | 10: No non-MVP win conditions (pinfall, submission, titles, tag team, rings/ropes). |
| N-03 (hard) Only moves that exist in the MVP | 2 | **10** | 10: The only moves named are the bodyslam and stomping, both in the MVP. No suplexes or finishers. | 10: Only MVP moves referenced (punch/kick, lock-up, bodyslam, Irish Whip, stomp) or none. |
| N-04 Barry sounds like Barry | 3 | **9** | 9: Barry is blunt, stubborn and country. He uses the cigarette-butt grievance, the Hoon dirt track and "boy", all from his profile. Small gap: it never uses his trucker background or hat. | 10: Barry states no numbers the docs don't (ages, years, counts). |
| N-05 Peaux sounds like Peaux | 3 | **9** | 9: Peaux is sullen and cutting. He uses his expulsion at sixteen, his eyeliner, "redneck" and a jab at Barry's age. Small gap: the football-playing brothers that toughened him are missing, and "hit harder than your beer gut" is a slightly muddled comparison. | 10: Peaux states no numbers the docs don't (ages, years, counts). |
| N-06 Rooted in Duckville | 2 | **9** | 9: The exchange is set in Duckville's gravel and trailers. The two read as neighbours who can't stand each other. There are no crowds or arenas. Mentioning Hoon is fine because it is Barry's track, but it slightly pulls focus from Duckville. | 10: place names: ['duckville', 'hoon']; trailer-park details: ['cigarette butts', 'dirt track', 'gravel', 'trailer'] |
| N-07 Dark Southern humor | 3 | **8** | 8: The tone is blunt, irreverent and Southern, with insults like "mustache cries" and the beer gut. It is closer to a playful promo than truly dark. The mama's-trailer line is a familiar trope. |  |
| N-08 Short enough not to interrupt the arcade action | 1 | **10** | 10: There are exactly four lines, alternating barry/peaux/barry/peaux. The longest is about 83 characters, so all are under 90. | 10: 4 alternating lines, all within 90 characters. |

*Evaluator summary:* The draft is solid on all rules. The humour is the weakest part: it is a bit mild and doesn't reach the dark, Squidbillies edge, and neither wrestler's lines use the football brothers or the trucker hat.

