# TorrentExtractor

[![Latest](https://github.com/dotnetdummy/TorrentExtractor/actions/workflows/auto-deploy-to-docker-hub.yml/badge.svg)](https://github.com/dotnetdummy/TorrentExtractor/actions/workflows/auto-deploy-to-docker-hub.yml)

Intended to be run as a service, watching for downloaded files and extracting/moving them to a desired destination. Can also be run as a [docker image](https://hub.docker.com/r/dotnetdummy/torrent-extractor).

## Pre-requirements

- .NET 10

## Build as linux service

Run the following to build it as self-contained (without need to install .Net on the target machine)

```
dotnet publish -c Release -r linux-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true
```

## Docker / TrueNAS

Bind-mount the torrent complete folder to `PATHS__SOURCE` and each Plex library to the matching destination. Use `restart: unless-stopped` (or the TrueNAS equivalent) so a crash can recover.

qBittorrent and similar clients often finish a download by renaming `*.!qB` / `*.part` to the real filename. The worker watches both create and rename, and ignores incomplete suffixes until that rename happens. Files already in the source folder at startup are not processed.

See [docker-compose.example.yml](docker-compose.example.yml) for a typical volume layout.

The intake whitelist is hardcoded (resolutions, codecs, season tokens, video/audio extensions). `PATHS__WHITELISTEDWORDS` is not used.

## Environment variables

- `LOGGING__TIMESTAMPFORMAT`: Timestamp format for logs. Default is `[yyyy-MM-dd HH:mm:ss.ffffffzzzz]`.
- `CORE__FILECOMPAREINTERVAL`: To determine if the file has been fully copied, the length of the file is compared between a given interval. If the lengths are equal, then the copy process starts. Default is 15 seconds. Must be 1 or greater.
- `CORE__MAXSETTLEHOURS`: Maximum time to wait for a download to settle before skipping it. Default is 12 hours. Must be 1 or greater.
- `PATHS__SOURCE`: **(required)** Source directory to watch for new files.
- `PATHS__BLACKLISTEDWORDS`: Comma separated list of words to blacklist files in source directory. If not set, all files will be processed.
- `PATHS__MOVIES__DEFAULT`: **(required)** Default destination directory for movies.
- `PATHS__MOVIES__2160P`: 2160P destination directory for movies.
- `PATHS__MOVIES__1080P`: 1080P destination directory for movies.
- `PATHS__MOVIES__720P`: 720P destination directory for movies.
- `PATHS__TV__DEFAULT`: **(required)** Default destination directory for tv shows.
- `PATHS__TV__2160P`: 2160P destination directory for tv shows.
- `PATHS__TV__1080P`: 1080P destination directory for tv shows.
- `PATHS__TV__720P`: 720P destination directory for tv shows.
- `PATHS__MUSIC`: Destination directory for music. If not set, music releases are skipped.
