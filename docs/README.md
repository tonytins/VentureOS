# VentureOS Documentation

Named after the Siemens' [passenger cars](https://en.wikipedia.org/wiki/Siemens_Venture), VentureOS is my hobby operating
system written in C# and based on the [Cosmos](https://github.com/CosmosOS/Cosmos) framework. It is the spiritual successor to my earlier project, [TOMAS](https://github.com/tonytins/tomas/).


## Table of Contents

- [Architecture](./architecture.md): goals and principles, layout, layering, boot, the failure model, and how each part of the system works.
- [Roadmap](./ROADMAP.md): how VentureOS releases, the release in development, the ones considered, ideas, and risks.
- [Releases](./releases/): notes for each release.
- [History](./history/): the roadmap VentureOS was planned with until 0.4.
- [Scorecard](./scorecard.md): verified status of every area, with the evidence for each row, and known issues.
- [Tutorials](./tutorial/): write an app, step by step: [a command-line app](./tutorial/command-line.md), [a desktop app](./tutorial/desktop.md), and [troubleshooting](./tutorial/troubleshooting.md).
- [VentureOS HIG](./hig/): how VentureOS looks, behaves, and talks, and how apps outside it can share that design.
- [UIScript](./uiscript/): the spec for UIScript, the format of `.uix` interfaces, which VentureOS keeps and Hudini reads a subset of.