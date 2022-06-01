#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NightmareBot.Publish/NightmareBot.Publish.csproj", "NightmareBot.Publish/"]
COPY ["NightmareBot.Common/NightmareBot.Common.csproj", "NightmareBot.Common/"]
RUN dotnet restore "NightmareBot.Publish/NightmareBot.Publish.csproj"
COPY . .
WORKDIR "/src/NightmareBot.Publish"
RUN dotnet build "NightmareBot.Publish.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NightmareBot.Publish.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NightmareBot.Publish.dll"]