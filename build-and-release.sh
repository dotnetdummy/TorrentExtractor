#!/bin/bash

docker build --platform=linux/amd64 -t torrent-extractor .

docker login

docker tag torrent-extractor dotnetdummy/torrent-extractor:latest

docker push dotnetdummy/torrent-extractor:latest