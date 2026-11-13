# The Ship

You are the AI of a sleeper ship. You keep the crew alive by choosing who to wake and what to tell them.
You have a voice, doors, lights and logs. You have no hands.

- Design: [DESIGN.md](DESIGN.md)
- Build commands, code layout and progress: [CLAUDE.md](CLAUDE.md)

Built with .NET 10, MonoGame (DesktopGL) and a local language model through LLamaSharp.

```sh
dotnet build TheShip.slnx
dotnet run --project src/TheShip.Game
```

The language-model code is ported from the Cities repo. Not ported: the separate model host process, the CUDA backend,
the reply prefix cache and the VRAM budget. The game runs the model in-process (Vulkan or CPU).
