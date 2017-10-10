#! /bin/bash
dotnet publish -c Release
docker-compose build --no-cache
docker-compose up
