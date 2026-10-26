FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY TaskApi/TaskApi.csproj TaskApi/
COPY TaskApi.Tests/TaskApi.Tests.csproj TaskApi.Tests/
RUN dotnet restore TaskApi/TaskApi.csproj

COPY . .
RUN dotnet publish TaskApi/TaskApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskApi.dll"]