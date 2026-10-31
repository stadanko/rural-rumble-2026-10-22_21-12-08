# Rural Rumble Style Guide

## Overall Game Style

- The look/style of the game should be patterned after Early 90s arcade games from Technos Japan, Capcom, and SNK.
- Game examples are WWF Wrestlefest, WWF Superstars, Final Fight, Street Fighter II, Streets of Rage Series, Fatal Fury, 3 Count Bout
- Movie/Show examples Squidbillies, Beavis and Butthead, Trailer Park Boys, True Detective

## Characters

![](images/style-guide-01.png)

### Peaux Strange

**Characteristics**

- Late teens
- Goth styling
- Black, shaggy hair
- Black lipstick & eyeliner
- Black T-Shirt
- Skull & Crossbones Logo
- Wide Leg Jeans
- Black chunky boots
- Cruiserweight Frame

**Similar Wrestling Characters:** Darby Allen, Early Hardy Boys, Raven, Kidman

![](images/style-guide-02.png)

### Barry “Hot Potato” Lankford

**Characteristics**

- Early Thirties
- Country Strong
- Trucker/Dirt Racer
- Hat with Semi Truck
- Wild Bill Mustache
- Middle chest hair patch
- No tattoos
- Beer gut/spare tire
- Cut off jean shorts with fray
- Carhartt style workboots

**Similar Wrestling Characters:** Hacksaw Jim Duggan, Bobby Eaton, Dennis Condry

## Sprite Scale

- The game renders at a native resolution of 960×540, scaled ×2 to fill a 1080p screen (×3 at 1440p, ×4 at 4K).
- Barry is 240 pixels tall in his standing sprite. Peaux, a cruiserweight, is 215 pixels tall.

## Arena

![](images/style-guide-03.png)

*Concept Art*

- The arena is an open grass/dirt/gravel area in a trailer park where players can duke it out in the Rural Rumble combat style.
- The area for movement needs to be around the size of a wrestling ring in Wrestlefest or WWF Superstars
- The area should be shaped roughly in a rectangle.
- Some of the items in the concept art(old truck, trailer, pool) could be used to act as the edge of the ring. This will keep the player from moving beyond that edge. It will also be an obstacle that will injure the player if they run into it during the fight.
- There should be thick North Louisiana Pine Trees as part of the background.
- The background should not include mountains or large hills. Just trees, skies, electric poles, and trailers.
- The general feeling of the trailer park should be disrepair. It has not been kept up with over the years.
- Trailers should have old TV antennas or direct TV style dishes attached to some of the roofs.
- Feels like in the middle of the woods, not a busy city.
- **Chosen look (D-016):** Arena 4's layout and pixel style in late-afternoon golden-hour light: warm orange sky behind the pines, long shadows across the dirt, trailer along the back, truck on the left, pool on the right.
- **Arena size and camera (D-017):** the arena is wider than the screen. About 60% of its width is on screen at once, and the camera scrolls to follow the wrestlers. When they split toward opposite screen edges, the camera zooms out smoothly to show more of the arena (up to all of it), and zooms back in as they close the distance. These values are adjustable in the Unity Inspector.
- Props must be drawn to scale with the 240-px wrestlers: a single-wide trailer stands roughly twice Barry's height.

![](images/style-guide-04.jpg) ![](images/style-guide-05.jpg) ![](images/style-guide-06.png) ![](images/style-guide-07.jpg)

## Sprite Sheet & Animation Examples

Sprite Examples are from the Hulk Hogan character in WWF Wrestlefest. These are meant to be examples and not for actual use in the game. These are meant to represent the animations that will coincide with the states in the Combat Spec. Most of the animation examples are at half speed of the actual game animation, so these will need to be doubled to match the actual game speed. Any that is not half speed will be appended to say so and should not be doubled.

- Standing Front and Back
    - When user stops walking, they will stand to match the most recent animation.
    - ![](images/style-guide-08.png)
- Walking Front Facing
    - Joy stick directions Left, Down/Left, Down, Down/Right, Right
    - ![](images/style-guide-09.png)
    - ![](images/style-guide-10.gif)
- Walking Back Facing
    - Joy stick directions Up/Left, Up, Up/Right
    - ![](images/style-guide-11.png)
    - ![](images/style-guide-12.gif)
        - Not half speed
- Running
    - ![](images/style-guide-13.png)
    - ![](images/style-guide-14.gif)
- Strike 1(First strike in a combo)
    - ![](images/style-guide-15.png)
    - ![](images/style-guide-16.gif)
- Strike 2(Second strike in combo)
    - ![](images/style-guide-17.png)
    - ![](images/style-guide-18.gif)
- Strike 3(Third strike in combo)
    - ![](images/style-guide-19.png)
    - ![](images/style-guide-20.gif)
- Struck and Being Knocked Down
    - ![](images/style-guide-21.png)
    - ![](images/style-guide-22.gif)
        - Not half speed
- Knocked Down
    - ![](images/style-guide-23.png)
- Lock Up(Triggering a Lock Up Animation)
    - ![](images/style-guide-24.png)
- Locked Up(Lock Up Animation Triggered by Opponent)
    - ![](images/style-guide-25.png)
- Irish Whip(Receiver of Irish Whip Begins Running Animation)
    - ![](images/style-guide-26.png)
    - ![](images/style-guide-27.gif)
- Bodyslam(Triggering grapple on Opponent)
    - ![](images/style-guide-28.png)
    - ![](images/style-guide-29.gif)
- Bodyslammed(Grapple is triggered by Opponent)
    - ![](images/style-guide-30.png)
    - ![](images/style-guide-31.gif)
- Recovering(Clutching ribs while standing and walking slowly, D-023)
    - Example image to come from the designer.
- Knocked Out
    - Reuses the Knocked Down animation; the wrestler does not get up (D-023).

## Music & Sounds
- Music Style
    - Trailer park hair bands like Skid Row, Poison, and Warrant
    - Outlaw Country like Hank Williams Jr, Waylon Jennings, and David Allen Coe
- Sound Effects
    - Heavy hit sounds for strikes, similar to a kung fu movie. A lot of bass
    - Painful grunts when hit or slammed to the ground
    - Hitting the ground should sound more like a thud than a slam to a wrestling match
    - Footsteps should make sounds based on the material they are walking/running on. 
        - Gravel
        - Grass
        - Wood
        - Concrete
    - Footstep sounds should have a different cadence based on if the wrestler is running or walking

## UI

The UI will include the following screens

- Start Screen
    - Background: An image of the trailer park
    - Title Logo: Arcade style “Rural Rumble” is the focus of the screen
    - “Press Any Button to Start” sits below the title logo
- Character Select Screen
    - A screen that shows profile images of the 2 characters with their names underneath
    - The user can select from either character
- Arena Screen
    - The main game screen
    - The top left should be the user player’s health bar, a profile image of the wrestler, and the character name.
    - The top right should be the AI player’s health bar, a profile image of the wrestler, and the character name.
    - The top middle should be a timer that keeps track of how long the match has lasted.
    - At the beginning of the match, there should be versus text in the middle of the screen that says {Player Character} vs {AI Character}
    - HUD style (D-016) is "Clean Retro": diamond-framed portraits, chunky health bars with a white damage trail that briefly shows how much health the last hit took, names above the bars, and a large plain timer.
- Victory Screen
    - When the knockout condition has been met, the victory screen should say “Victory! {Winner} knocked out {Loser} in {timer}”
        - If the User Player wins, it should say “Congratulations!”
        - If the AI Character wins, it should say “Game Over!”

## Color Palette

`#10151A` `#17243A` `#25372C` `#35523A` `#657A3B` `#795037` `#A74727` `#D26938` `#E19A36` `#FFD65A` `#2872B8` `#7435A8` `#C7352C` `#E6DFCC`

The palette guides the game's overall look, but not every color has to come from it. Skin tones are the main exception.

![](images/style-guide-32.png)
