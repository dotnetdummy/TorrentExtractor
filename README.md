# TorrentExtractor

[![Latest](https://github.com/dotnetdummy/TorrentExtractor/actions/workflows/auto-deploy-to-docker-hub.yml/badge.svg)](https://github.com/dotnetdummy/TorrentExtractor/actions/workflows/auto-deploy-to-docker-hub.yml)

Intended to be run as a service, watching for downloaded files and extracting/moving them to a desired destination. Can also be run as a [docker image](https://hub.docker.com/r/dotnetdummy/torrent-extractor).

## Pre-requirements

- .Net 8

## Build as linux service

Run the following to build it as self-contained (without need to install .Net on the target machine)

```
dotnet publish -c Release -r linux-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true
```

## Environment variables

- `LOGGING__TIMESTAMPFORMAT`: Timestamp format for logs. Default is `[yyyy-MM-dd HH:mm:ss.ffffffzzzz]`.
- `CORE__FILECOMPAREINTERVAL`: To determine if the file has been fully copied, the length of the file is compared between a given interval. If the lengths are equal, then the copy process starts. Default is 15 seconds. Must be 1 or greater.
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
