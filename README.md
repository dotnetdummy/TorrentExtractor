# TorrentExtractor
Intended to be run as a service, watching for downloaded files and extracting/moving them to a desired destination.

## Pre-requirements 
- .Net Core 3.1

## Build for linux
Run the following to build it as self-contained (without need to install .Net on the target machine)
```
dotnet publish -c Release -r linux-x64 /p:PublishSingleFile=true /p:PublishTrimmed=true
```
Copy the `appsettings.json` into the same dir afterwards in order to adjust the settings.
