# TorrentExtractor

A .NET worker that watches a torrent **source** directory, waits until downloads **settle**, then extracts and copies video into Plex library folders. Movie vs TV and resolution are inferred from the **release name**. Source files stay put.

Config env vars: [README.md](README.md). Binding and validation: `TorrentExtractor/Settings/`.

## Pipeline

`Worker` is the whole runtime. Order is load-bearing.

1. `FileSystemWatcher.Created` on `Paths.Source` (that directory only, not recursive). Files already there at startup are ignored. After Created, wait 1s, then confirm the path still exists.
2. Continue only if the path contains a hardcoded whitelist token (`Worker._whitelistedWords`: resolutions, codecs, `S0`/`Season`, video extensions).
3. Stop if the path contains a configured blacklist token (`Paths.BlacklistedWords`).
4. **Settle**: poll file or directory size every `Core.FileCompareInterval` seconds until two readings match. Directory size is a recursive sum (`Extensions.Length`).
5. **Route** with `PathBuilder.GenerateDestinationPath`.
6. Recurse the source: copy `.mkv`/`.avi`/`.mp4`; extract `.rar`/`.zip` into the destination. Other extensions are skipped.

Copy mismatch: retry once, then delete the destination and keep the source. Archive directory entries log a warning and are skipped; nested archives inside an extract are not re-processed.

## Route

`PathBuilder` turns spaces into dots, then splits the release name on `.`.

- **TV** when a token starts with `Season` or `S0`–`S5` (`S01` and `S10` match; unpadded `S6` does not). Show name is the tokens before that token. Destination: `{tvRoot}/{show}/{season}`.
- Season packs (`Seasons.1-8`) produce an empty season, so the destination is `{tvRoot}/{show}` (`GenerateSeasonPackTvPath`).
- **Movie** otherwise. Destination is the movies library for that resolution; no title subfolder.
- Resolution tokens `UHD`/`2160P`/`4K`, `1080P`, `720P` pick the matching path under `Paths.Movies` or `Paths.Tv`. Missing resolution paths fall back to `Default`.

A routing change is done when every new release-name pattern has a `PathBuilderShould` fact.

## Layout

- `TorrentExtractor/Worker.cs` — watch, settle, extract/copy.
- `TorrentExtractor/PathBuilder.cs` — release name → destination.
- `TorrentExtractor/Settings/` — `Core` and `Paths` Options; `Validate()` on startup.
- `TorrentExtractor.Tests/` — xunit; routing only today.

## Conventions

- Target framework is `net10.0`.
- Format with the local csharpier tool: `dotnet tool restore`, then `dotnet csharpier .`.
- `dotnet test` from the repo root.
- Whitelist is hardcoded in `Worker`. `PATHS__WHITELISTEDWORDS` in launchSettings is unused. Blacklist is `Paths.BlacklistedWords`.
- Critical startup failure calls `Environment.Exit(0)`.
