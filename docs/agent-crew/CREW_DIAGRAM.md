# Rural Rumble Agent Crew: Diagrams

Two views of the same crew. The first shows **roles, connections, and data flow**.
The second shows **the order agents work in** on one real feature (the lock-up).

## 1. Roles, connections, and data flow

```mermaid
flowchart TB
    designer(["Designer (human)<br/>assigns tasks · approves art · owns design docs"])
    main{{"Main Claude Code session<br/>ORCHESTRATOR<br/>follows CLAUDE.md delegation rules"}}

    subgraph DOCS["Source of truth: docs/"]
        direction LR
        styleGuide["STYLE_GUIDE.md<br/>+ concept art"]
        combatSpec["COMBAT_SPEC.md<br/>CONTROLS.md · GDD FSM"]
        arch["ARCHITECTURE.md<br/>interfaces · folder ownership"]
        decisions["DECISION_LOG.md"]
    end

    subgraph CREW["Agent crew: .claude/agents/"]
        direction LR
        art["Art Wrangler<br/>art-wrangler.md"]
        anim["Unity Animation & Tooling<br/>unity-animation-tooling.md"]
        combat["Combat State & AI<br/>combat-state-ai.md"]
    end

    subgraph TOOLS["Tools the agents call"]
        direction LR
        unityAI["Unity AI generation<br/>(MCP)"]
        artPipe["Tools/ArtPipeline/*.py<br/>+ ffmpeg"]
        unityCLI["Unity Editor via unity CLI<br/>compile · console · tests"]
    end

    %% Hand-off artifacts (the agents never call each other directly)
    staging[("Staging/Art/ · _Generated/<br/>candidate sprites")]
    registry[("docs/ASSET_REGISTRY.csv<br/>provenance + sheet cell sizes")]
    sheets[("Assets/Art/Wrestlers/<br/>approved sprite sheets")]
    combatCode[("Assets/Scripts/Combat + AI<br/>CombatMatch · LockUpController<br/>OpponentBrain · CombatTuning")]
    events[["CombatEvents contract<br/>StateChanged · MoveStarted + MoveTiming<br/>HitLanded · PairedMoveStarted"]]
    animOut[("Assets/Animation + Prefabs<br/>Animator controller · clips<br/>MoveAnimationTable · WrestlerView")]
    builder["WrestlerAnimationBuilder.cs<br/>Editor menu tool"]
    game(["Playable match in Unity 6"])

    %% Orchestration (hub and spoke)
    designer -->|task| main
    main -->|brief| art
    main -->|brief| anim
    main -->|brief| combat
    art -->|report| main
    anim -->|report| main
    combat -->|report| main
    main -->|review request| designer
    designer -.->|approve / reject art| main

    %% Inputs
    decisions -.->|read every task| CREW
    styleGuide --> art
    styleGuide --> anim
    combatSpec --> combat
    combatSpec -->|state definitions| anim
    arch --> combat
    arch --> anim

    %% Art Wrangler
    art --> unityAI
    art --> artPipe
    art --> staging
    art -->|row per asset| registry
    staging -->|promoted only after approval| sheets

    %% Combat State & AI
    combat --> combatCode
    combatCode -->|raises| events

    %% Unity Animation & Tooling
    sheets --> anim
    registry -->|cell size per sheet| anim
    events -->|WrestlerView subscribes| anim
    anim --> builder
    builder -->|slices sheets, builds clips + prefabs| animOut

    %% Feedback loops (routed through the orchestrator)
    anim -.->|"frame faces wrong way: send back to Art"| main
    anim -.->|"timing mismatch: report, never change combat"| main

    %% Verification and result
    anim --> unityCLI
    combat --> unityCLI
    combatCode --> game
    animOut --> game

    classDef human fill:#f9e2af,stroke:#b8860b,color:#000
    classDef orch fill:#cba6f7,stroke:#6a3fb5,color:#000
    classDef agent fill:#89b4fa,stroke:#1e5bb8,color:#000
    classDef data fill:#e6e6e6,stroke:#666,color:#000
    classDef contract fill:#a6e3a1,stroke:#2e7d32,color:#000
    class designer,game human
    class main orch
    class art,anim,combat agent
    class staging,registry,sheets,combatCode,animOut,builder data
    class events contract
```

**Legend**

| Shape / color | Meaning |
|---|---|
| Yellow rounded | Human designer, and the final playable result |
| Purple hexagon | Orchestrator (main Claude Code session) |
| Blue box | Crew agent (one `.claude/agents/*.md` file each) |
| Gray cylinder | File or asset one agent writes and another reads |
| Green box | Code contract between Combat and Animation |
| Solid arrow | Data or delegation |
| Dotted arrow | Feedback, approval, or "read but don't write" |

## 2. Order of work on one feature: the lock-up

The lock-up is in progress on the `lock-ups` branch. Its combat rules and animation states exist, and
the clips are marked placeholders until the art steps (6–11) are approved.

```mermaid
sequenceDiagram
    autonumber
    actor D as Designer
    participant M as Orchestrator<br/>(main session)
    participant C as Combat State & AI
    participant A as Art Wrangler
    participant U as Unity Animation & Tooling
    participant E as Unity Editor

    D->>M: "Add the standing lock-up"
    M->>C: Brief: COMBAT_SPEC lock-up rules
    C->>E: LockUpController, tuning values,<br/>PairedMoveStarted event
    E-->>C: compiles, EditMode tests pass
    C-->>M: Report: rules done, tuning gaps for review
    M->>A: Brief: lock-up poses for Barry + Peaux
    A->>E: Generate from base sprite (Unity AI),<br/>clean up with ArtPipeline
    A-->>M: Report: staged sheets + registry rows
    M->>D: Show staged sheets
    D-->>M: Approve
    M->>A: Promote to Assets/Art/Wrestlers/
    M->>U: Brief: paired lock-up clips
    U->>E: Run WrestlerAnimationBuilder:<br/>slice, clips, Animator states, MoveAnimationTable
    U->>E: WrestlerView listens for PairedMoveStarted
    E-->>U: compiles, no console errors
    U-->>M: Report: placeholder or final per state
    M-->>D: Ready for playtest
```
