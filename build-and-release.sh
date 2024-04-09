#!/bin/bash

docker build -t torrent-extractor .

docker login

docker tag torrent-extractor dotnetdummy/torrent-extractor:latest

docker push dotnetdummy/torrent-extractor:latest