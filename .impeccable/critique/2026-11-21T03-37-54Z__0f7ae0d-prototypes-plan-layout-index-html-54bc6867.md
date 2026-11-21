---
target: the plan-layout prototype
total_score: 17
max_score: 32
na_heuristics: 5,9
p0_count: 0
p1_count: 3
target_identity: "file:/Users/glowacki/dev/cartographer/.claude/worktrees/agent-a627130c150f7ae0d/prototypes/plan-layout/index.html"
target_fingerprint: "sha256:b75094d12be2dd80e815b1a7eca46bfa5a590fd00e21b88cea71c1e707c3a9c7"
target_path: /Users/glowacki/dev/cartographer/.claude/worktrees/agent-a627130c150f7ae0d/prototypes/plan-layout/index.html
timestamp: 2026-11-21T03-37-54Z
slug: 0f7ae0d-prototypes-plan-layout-index-html-54bc6867
---
Method: dual-agent (A: isolated design-review agent · B: isolated detector-and-measurement agent)

# Critique: plan-layout prototype

## Design health score

| # | Heuristic | Score | Key issue |
|---|---|---|---|
| 1 | Visibility of system status | 3 | No busy state during the roughly 2-second first layout of the large sheet |
| 2 | Match with the real world | 2 | "ELK seeded" and "container outlines" are engineer terms; Basis column does not use extracted / inferred / stated |
| 3 | User control and freedom | 2 | Deselect only by re-tapping the same box; three fixed zoom steps, no pan; sheet switch resets size and selection |
| 4 | Consistency and standards | 2 | Magenta also used for the comparison table's current row; selected box looks identical to what it reaches |
| 5 | Error prevention | n/a | Nothing destructive or enterable |
| 6 | Recognition over recall | 2 | No legend for box tints; index lines carry bare numbers; grid letters scroll away at 100% |
| 7 | Flexibility and efficiency | 1 | No keyboard path to any box or schedule row, no search, no hover |
| 8 | Aesthetic and minimalist design | 2 | Boxes are calm; the large sheet is a wire mat of about 710 crossings |
| 9 | Error recovery | n/a | Only error state is a layout fallback |
| 10 | Help and documentation | 3 | Honest "what this shows / what is rough" note, but eight bullets precede the first drawing |
| **Total** | | **17/32 (53%)** | **Acceptable** |

## Design specificity verdict

Design review: the plan is specific to the survey-plan brief (ruled boxes with a number tab, flat tints by kind, lettered grid, hatched externals, dashed "not yet surveyed", a schedule with grid references). The surrounding page is generic lab scaffolding, fine for a throwaway. As evidence it proves the box vocabulary and falls short on lines and selection.

Deterministic scan: 10 findings, all `low-contrast`, all false positives (dark-theme token paired with a light-theme colour; never rendered; wrapped copy scans clean).

Visual overlays: skipped, no human-visible browser attached.

## Overall impression

Keeping positions works and the box design holds up. The survey plan as briefed does not yet answer "what does this change touch?": lines are unreadable at scale and selection floods the sheet. Biggest opportunity: stop drawing every table-level link on a system plan.

## What's working

- Box anatomy: number tab, name, kind line, dashed for unsurveyed, hatch for external; name contrast 11:1 or better in both themes.
- Kept positions: 0 of 19 and 0 of 79 boxes moved between scans, against all under a fresh layout; new boxes marked with a heavier border.
- Honesty apparatus: Synthetic / Real entries badges, "(invented)" in the schedule, "total not surveyed" on databases.

## Priority issues

- [P1] Lines fail the brief at scale. About 710 crossings on the large sheet, long parallel runs, labels far from either end, stacked arrowheads. Fix: at system scale draw only links between plots; move table-level links to the single-part view and the schedule; a real right-angled router with port spreading; a crossings budget as pass/fail. Command: /impeccable shape
- [P1] Selection does not answer the question. Downstream only; tapping a table lights nothing else; tapping a hub marks 19 of 22 rows; origin indistinguishable from reach. Fix: mark the origin, separate direct from indirect reach, add upstream, state the result in words above the plan. Command: /impeccable shape
- [P1] Text too small at the default size. 4.8 to 5.9px on the real sheet at Fit on desktop, about 3px on the large sheet, under 2px on a phone; 9px kind text and labels at 100%. Fix: a legible floor, default to 100% with pan and a locator inset, base sizes about 12px and 11px. Command: /impeccable typeset
- [P2] Schedule under-delivers. Lists boxes only; no links, no counts, nothing marks "stated by a person". Fix: a links schedule (from, verb, to, basis, evidence) and per-row counts. Command: /impeccable layout
- [P2] No keyboard or assistive path. 0 of 22 boxes and 0 of 22 schedule rows focusable; click-only; SVG has no accessible name. Fix: schedule rows as real buttons; the schedule as the accessible equivalent of the plan. Command: /impeccable harden

## Persona red flags

- Product person weighing a change: table tap lights nothing else; component tap lights everything; "ELK seeded" and the stability table are meaningless; no plain-language summary of a selection.
- Alex (power user): no search or jump-to-number; schedule row tap does not scroll the plan; selection lost on sheet change.
- Sam (accessibility): plan unreachable by keyboard; new-in-scan is border weight only; orange "Synthetic" badge 2.8:1 in light theme.
- Casey (mobile): Fit unreadable on a phone; at 100% one box fills the screen; scan buttons 35 to 37px wide.

## Minor observations

- "1 tables" grammar. "uses" lines end as a stub at the database edge. Disabled scan button about 2:1 contrast. First tap on the large sheet blocks about 2 seconds. No page-level sideways scroll at phone width; no console or network errors.

## Questions to consider

- Should one hop be the default reach, with the rest on request?
- Should a system plan draw table-level links at all, or is the schedule their home?
- After 50 scans, is a kept-positions sheet a stable map or a sprawl, and who decides when to re-survey?

Not inspected: scan 3 and ELK seeded drawings judged from numbers only; no screen reader run.
