FROM microsoft/aspnetcore:2.0
ARG source
WORKDIR /app
EXPOSE 80
COPY ${source:-bin/Release/netcoreapp2.0/publish} .
ENTRYPOINT ["dotnet", "NginxDocker.dll"]