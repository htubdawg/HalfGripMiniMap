# Car marker regression checks

From the repository root, with the .NET 8 SDK:

```bash
dotnet run -c Release --project tools/racecheck
```

Links the actual `MiniMap/src/Race.cs` into a small executable with managed stand-ins for Unity and the game. Checks initial
registration, a replacement remote car absent from a nonempty leaderboard, repeated respawns, stale wrecks, deduplication,
blue remote/red bot markers, inactive/local cars, missing lists, and scene changes. A failed assertion exits nonzero.

To verify the regression against another revision without changing the checkout:

```bash
git show main:MiniMap/src/Race.cs > /tmp/minimap-race-before.cs
dotnet run -c Release --project tools/racecheck -p:RaceSource=/tmp/minimap-race-before.cs
```

Use the appropriate ref (`origin/main` in a fresh cloud checkout). The unfixed code passes initial registration, then fails
`respawn absent from nonempty leaderboard appears blue` with zero markers instead of one.

These checks exercise collection logic; they do not simulate Unity's native destroyed-object equality or the network. Also run
`tools/check.sh` against the game references. Before merging, test two players in Open World: wreck and respawn one player twice,
check that the other player sees one blue marker return on both the screen minimap and GPS, and confirm ordinary race markers.
