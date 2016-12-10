dotnet restore
dotnet pack -c Release vtortola.Core.WebSockets\project.json
dotnet pack -c Release vtortola.Core.WebSockets.Deflate\project.json
dotnet pack -c Release vtortola.Core.WebSockets.Rfc6455\project.json
dotnet pack -c Release vtortola.Core.WebSocketListener\project.json


copy vtortola.Core.WebSockets\bin\Release\*.nupkg ..\nuget\dotnet-core
copy vtortola.Core.WebSockets.Deflate\bin\Release\*.nupkg ..\nuget\dotnet-core
copy vtortola.Core.WebSockets.Rfc6455\bin\Release\*.nupkg ..\nuget\dotnet-core
copy vtortola.Core.WebSocketListener\bin\Release\*.nupkg ..\nuget\dotnet-core
pause