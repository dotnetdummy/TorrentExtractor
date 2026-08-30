# TorrentExtractor

A .NET worker that watches a torrent **source** directory, waits until downloads **settle**, then extracts and copies video or music into Plex library folders. Movie vs TV, and video resolution, are inferred from the **release name**. Music is inferred from audio files (or from audio markers in the name). Source files stay put.

Config env vars: [README.md](README.md). Binding and validation: `TorrentExtractor/Settings/`.

## Pipeline

`Worker` is the whole runtime. Order is load-bearing.

1. `FileSystemWatcher.Created` and `Renamed` on `Paths.Source` (that directory only, not recursive). Files already there at startup are ignored. After an event, wait 1s, then confirm the path still exists. Incomplete suffixes (`.!qB`, `.part`, `.!ut`) are skipped. Processing is serialized. `watcher.Error` is logged.
2. Continue if the path contains a hardcoded whitelist token (`Worker._whitelistedWords`: resolutions, codecs, `S0`/`Season`, video extensions, audio markers including `FLAC`/`WAV`/`OPUS`/`16BIT`/`24BIT`/`HI-RES`, audio extensions), **or** the path is a directory, **or** the path is an audio file (`PathBuilder.IsAudioFile`). Other files are skipped.
3. Stop if the path contains a configured blacklist token (`Paths.BlacklistedWords`, comma-separated and trimmed).
4. **Settle**: poll file or directory size every `Core.FileCompareInterval` seconds until two readings match, or until `Core.MaxSettleHours` (default 12). Directory size is a recursive sum (`Extensions.Length`).
5. Enumerate files. **Music** when any file has an audio extension (`PathBuilder.AudioExtensions`), or when the name matches the audio-marker fallback (`PathBuilder.IsMusic`). Stop if music and `Paths.Music` is unset. Stop if not music and the path was not name-whitelisted.
6. **Route**: video with `PathBuilder.GenerateDestinationPath`. Music destination is per file (`PathBuilder.GenerateMusicFileDestination`).
7. Recurse the source: copy video (`.mkv`/`.avi`/`.mp4`) and audio (`PathBuilder.IsAudioFile`); extract `.rar`/`.zip` into the destination. Other extensions are skipped. Copies open the source with `FileShare.ReadWrite` so a seeding client can keep the file open.

Copy mismatch: retry once, then delete the destination and keep the source. Archive directory entries log a warning and are skipped; nested archives inside an extract are not re-processed.

Startup waits until `Paths.Source` exists (TrueNAS mount race). Critical startup failure calls `Environment.Exit(1)`.

## Route

Music is classified first. A release is music when a recursive scan finds an audio file (`.flac`/`.mp3`/`.m4a`/`.aac`/`.wav`/`.ogg`/`.opus`/`.wma`/`.ape`/`.aiff`/`.aif`/`.wv`), including when video files are also present. If no audio file is found, the name-based fallback still treats it as music when the name contains an audio marker (`FLAC`, `ALAC`, `APE`, `MP3`, `M4A`, `WAV`, `OGG`, `OPUS`, `16BIT`, `24BIT`, `HI-RES`, `HIRES`) and does not contain a video marker (resolutions, video codecs, `BluRay`/`Webrip`, video extensions, `S0`/`Season`). That fallback covers scene folders that only contain `.rar`/`.zip`. `PATHS__MUSIC` is optional; if unset, music is skipped and never routed to movies.

Music destination is `{musicRoot}/{artist}/{album}`. Directory segments are taken from `Paths.Source` down to the file's parent:

- **One folder** (files sit in the watched release dir): the folder name is parsed as-is (spaces are not turned into dots).
  - **Spaced dash**: split on the first ` - `. Strip `[...]` groups, `(yyyy)`, a trailing scene group, and leftover format tokens from the album.
  - **Scene hyphen**: split on `-`. First token is artist. Later tokens are album until a metadata token (year, `PROPER`/`WEB`/`CD`/`FLAC`, bit-depth, and similar). `_` and `.` become spaces; casing is unchanged.
  - **Fallback**: `{musicRoot}/{folderName}` when artist or album cannot be parsed.
- **Two or more folders** (boxset): last folder is the album, the one before it is the artist. Destination is `{musicRoot}/{artist}/{album}`. Each audio file is copied into that album folder (`GenerateMusicFileDestination`).

Video routing turns spaces into dots, then splits the release name on `.`. Tokens are scanned in a first pass for TV vs movie and resolution (`[]()` stripped so `[1080p]` matches). Resolution before `Sxx` still routes as TV.

- **TV** when a token is `Sxx` / `SxxEyy` (`S01`, `S10`, `S60`; unpadded `S6` does not match), or `Season`/`Seasons` whose **next** token is a season number or range (`1`, `01`, `1-8`). A bare `Season` in a movie title does not match. Show name is the tokens before the season token, excluding resolution tokens. Destination: `{tvRoot}/{show}/{season}`.
- Season packs (`Seasons.1-8`) produce an empty season, so the destination is `{tvRoot}/{show}` (`GenerateSeasonPackTvPath`).
- **Movie** otherwise. Destination is the movies library for that resolution; no title subfolder.
- Resolution tokens `UHD`/`2160P`/`4K`, `1080P`, `720P` pick the matching path under `Paths.Movies` or `Paths.Tv`. Missing resolution paths fall back to `Default`.

A routing change is done when every new release-name pattern has a `PathBuilderShould` fact.

## Layout

- `TorrentExtractor/Worker.cs` — watch, settle, extract/copy.
- `TorrentExtractor/PathBuilder.cs` — release name / contained files → destination.
- `TorrentExtractor/Settings/` — `Core` and `Paths` Options; `Validate()` on startup.
- `TorrentExtractor.Tests/` — xunit; routing only today.

## Conventions

- Target framework is `net10.0`.
- Format with the local csharpier tool: `dotnet tool restore`, then `dotnet csharpier .`.
- `dotnet test` from the repo root.
- Whitelist is hardcoded in `Worker`. Blacklist is `Paths.BlacklistedWords`.
- `PATHS__MUSIC` is optional. Movies and TV defaults are required.
- Critical startup failure calls `Environment.Exit(1)`.
