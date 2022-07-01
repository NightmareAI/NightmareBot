FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["NightmareBot.Service/NightmareBot.Service.csproj", "NightmareBot.Service/"]
COPY ["NightmareBot.Common/NightmareBot.Common.csproj", "NightmareBot.Common/"]
RUN dotnet restore "NightmareBot.Service/NightmareBot.Service.csproj"
COPY . .
WORKDIR /src/NightmareBot.Service
RUN dotnet build "NightmareBot.Service.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NightmareBot.Service.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NightmareBot.Service.dll"]
