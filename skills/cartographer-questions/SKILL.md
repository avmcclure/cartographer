---
name: cartographer-questions
description: Works through the open questions in a Cartographer atlas without a full scan. Retries questions whose blocker has changed, answers what code or the atlas can now answer, and folds answers that people typed into entries as stated facts. Use when the person asks to work, retry, answer or clear open questions, to fold in or apply answers people wrote, or says they have answered questions in the atlas.
compatibility: Needs the cartographer CLI on the path and git.
---

# Working open questions

A Cartographer **atlas** is a git repository of Markdown **entries** mapping an organisation's code. When a scan cannot determine something an entry needs, it records an **open question** on that entry, in a fixed layout under `## Open questions`. A question ends in one of two ways: an agent answers it from code once what it was waiting on turns up, or a person types an answer after its `Answer:` line. This skill does both jobs between scans.

The atlas is only useful if it can be trusted, and what a person tells it is the most trusted thing in it. Two rules follow, and they matter more than clearing the list:

- **What a person stated is never changed by an agent.** A fact with basis `stated`, and text between `<!-- person -->` markers, stays as it is. If code contradicts it, raise a new question that shows the conflict and waits on `person`.
- **A question a person raised is never dismissed by an agent.** If you cannot answer it, record the attempt and leave it for a person.

Run every command from the atlas root (the directory holding `cartographer.yml`). Do not ask the person anything while working; what you cannot settle stays a question.

## Steps

1. **Start a branch.** `cartographer session start`, so that nothing lands on the atlas's main branch directly.
2. **Find answers people wrote.** `grep -rn '^- Answer: *[^ ]' repositories databases systems channels external flows 2>/dev/null` lists questions with text after `Answer:`. Fold each one in first; someone took the trouble to write it.
3. **Find questions worth retrying.** `cartographer question retry` lists open questions whose `Waiting on` target has changed since they were last tried. A question whose blocker has not moved is not retried: the answer would be the same and the attempt would count against it.
4. **Take them in this order:** raised by a person, then by an agent, then by the CLI. For each, open only the entry it is on, and follow [references/working-questions.md](references/working-questions.md) for how to answer it, fold it in, record a failed attempt or dismiss it.
5. **Validate and index.** `cartographer check`, fix any `error:` line on an entry you touched, then `cartographer index`.
6. **Commit** on the scan branch with plain git: `git add -A`, then `git commit -m "Work open questions: <n> folded in, <n> answered, <n> attempted"`.
7. **End.** `cartographer session end`, or with `--pull-request` if the person asked for one.
8. **Report** briefly: how many answers were folded in, how many questions were answered from code, how many attempts failed, which questions now need a person (entry and question id), and any answer you could not use and why.

If there are many questions, work in batches of about twenty and stop when your context runs low. Whatever is left is still in the atlas for next time.

## Reference

- [references/working-questions.md](references/working-questions.md): how to answer a question from code or the atlas, how to turn a person's answer into links and text, and when a question may be dismissed.
