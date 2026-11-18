# Rural Rumble

A single-player 2D arcade wrestling game set in a fictional Louisiana trailer park (Unity 6, C#). Design docs live in `docs/` (start with `docs/GDD.md`).

## Third-party audio (not included in this repository)

Some sound effects come from paid Unity Asset Store packs. Their license doesn't allow redistribution, so they are **git-ignored** and not in this public repository (DECISION_LOG D-034, amendment). The game runs without them; those sounds are simply silent.

To get the full audio, buy and import these packs in Unity, then copy the files listed in `docs/ASSET_REGISTRY.csv` (license `Unity Asset Store EULA`) into `Assets/Audio/ThirdParty/`, at the exact path and name in each row's `file_path`. Some files (the footsteps) are trimmed copies; the row's `modifications` column says how to recreate them:

| Pack | Publisher | Store page |
|---|---|---|
| Pro Sound Collection | Gamemaster Audio | https://assetstore.unity.com/packages/audio/sound-fx/pro-sound-collection-50235 |
