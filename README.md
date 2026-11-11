# Rural Rumble

A single-player 2D arcade wrestling game set in a fictional Louisiana trailer park (Unity 6, C#). Design docs live in `docs/` (start with `docs/GDD.md`).

## Third-party audio (not included in this repository)

Some sound effects come from paid Unity Asset Store packs. Their license doesn't allow redistribution, so they are **git-ignored** and not in this public repository (DECISION_LOG D-034, amendment). The game runs without them; those sounds are simply silent.

To get the full audio, buy and import these packs in Unity, then copy the files listed in `docs/ASSET_REGISTRY.csv` (license `Unity Asset Store EULA`) into `Assets/Audio/ThirdParty/`:

| Pack | Publisher | Store page |
|---|---|---|
| Pro Sound Collection | Gamemaster Audio | https://assetstore.unity.com/packages/audio/sound-fx/pro-sound-collection-50235 |
