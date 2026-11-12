# Review Guide

Use this checklist when reviewing a change to VentureOS. It is adapted from the
contributor and review practices in [NVIDIA FLARE's contribution guide](https://github.com/NVIDIA/NVFlare/blob/main/CONTRIBUTING.md),
by way of [Hudini](https://github.com/tonytins/Hudini), with checks tailored to
a Cosmos gen3 kernel. The rules it checks against are in [AGENTS.md](AGENTS.md).

## Before reviewing

- Read the change description and the relevant code, tests, and docs.
- Identify the user-visible behavior or problem the change is meant to address.
  If it refers to a roadmap milestone, an issue, or a design discussion, check
  that the change addresses that context.
- Check the available test and CI results. Run `dotnet test --project
  tests/Venture.OS.Common.Tests`, `cosmos check`, and `cosmos build -p
  src/Venture.OS` when the change or its verification warrants it. For changes
  to boot, storage, networking, or commands, boot it with `./run.sh` (or
  `./run.sh --headless` to read the serial log). Report checks that couldn't be
  run, such as an ARM64 build or a boot.

## Review checklist

- Does the behavior match the stated intent, including boundary cases such as
  empty input, unclosed quotes, missing devices, no DHCP server, and sizes at
  or past a limit?
- **Stability:** Can anything in the change stop the system after boot? Does
  every new failure path report what failed and return to the prompt?
- **Boot:** Is each new boot step classified as critical or optional, and is
  that the right call? Does the change alter boot order? Are hardware and
  network waits bounded?
- **Boundaries:** Is input from the shell, disks, the network, and devices
  validated? Are `ulong` sizes and offsets range-checked before narrowing or
  slicing?
- **Cosmos:** Do the APIs exist in the pinned Cosmos version? Does the code
  avoid reflection, `dynamic`, and BCL features the ahead-of-time compiler or
  Cosmos doesn't support? Does it keep ARM64 building?
- **Placement:** Does code that doesn't need hardware live in
  `Venture.OS.Common`, with tests? Does `Venture.OS.Common` still avoid any
  Cosmos reference?
- **Shell:** Is a new command registered, does it have a clear name, summary,
  usage, and argument range, and does it write through `IShellOutput`?
- **Dependencies:** Are package versions, new dependencies, or Cosmos bumps
  called out and kept in a change of their own?
- **Tests:** Do focused tests cover changed `Venture.OS.Common` behavior, and
  would a new test fail without the fix? Is a boot needed beyond the tests?
- **Docs:** Are the changelog (user-visible changes only), architecture,
  scorecard, roadmap, README, and AGENTS.md updated where affected? Do they
  describe only what has been shown to work?

## Writing review comments

- Report concrete correctness, stability, compatibility, or missing-test
  concerns; do not block changes on personal style preferences.
- Make each comment specific, constructive, and actionable. Point to the
  smallest relevant location and explain the impact and a useful next step.
- Prioritize findings by impact:
  - **Blocking**: prevents the tests or kernel from building, panics or hangs
    boot in normal use, or halts the system after boot.
  - **High**: breaks an existing command, boot step, or documented behavior, or
    leaves a boundary unvalidated where bad input can reach the kernel.
  - **Medium**: causes a limited but meaningful behavior or reliability issue,
    or leaves docs describing something that isn't true.
  - **Low**: a small edge case or non-blocking improvement.
- If no actionable issues remain, say so and note any important checks that
  were not run.
