FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /App

COPY TorrentExtractor/TorrentExtractor.csproj ./TorrentExtractor/
RUN dotnet restore TorrentExtractor/TorrentExtractor.csproj

COPY TorrentExtractor/ ./TorrentExtractor/
RUN dotnet publish TorrentExtractor/TorrentExtractor.csproj -c Release -o out --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /App
COPY --from=build-env /App/out .
ENTRYPOINT ["dotnet", "TorrentExtractor.dll"]
