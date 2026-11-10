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

Video and audio copies are written to `filename.ext.partial` in the library directory, flushed to disk, and renamed to the real name only after SHA-256 checks out. The hash is taken while reading the source, then the source is read again and the partial file is hashed. All three must match, so a large file is read twice more after the copy. If the source changes during the copy, the partial is deleted and the file has to settle again, up to `CORE__MAXSETTLEHOURS`. A matching hash means the copy matched the source. It does not mean the torrent was finished: a preallocated file that nobody is writing will hash the same on both sides and still be unplayable. Keep incomplete downloads out of `PATHS__SOURCE` (a separate incomplete directory, or `.!qB` until the client renames the finished file).

See [docker-compose.example.yml](docker-compose.example.yml) for a typical volume layout.

The intake whitelist is hardcoded (resolutions, codecs, season tokens, video/audio extensions). Directories without those tokens are still processed if they contain audio files after the download settles.

## Environment variables

- `LOGGING__TIMESTAMPFORMAT`: Timestamp format for logs. Default is `[yyyy-MM-dd HH:mm:ss.ffffffzzzz]`.
- `CORE__FILECOMPAREINTERVAL`: To determine if the download has settled, the length and last-write time are compared across this interval. Copying starts when both are unchanged. Default is 15 seconds. Must be 1 or greater.
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
