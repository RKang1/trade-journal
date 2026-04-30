FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TradeJournal.slnx ./
COPY src/TradeJournal.Api/TradeJournal.Api.csproj src/TradeJournal.Api/
COPY src/TradeJournal.Services/TradeJournal.Services.csproj src/TradeJournal.Services/
COPY src/TradeJournal.Data/TradeJournal.Data.csproj src/TradeJournal.Data/

RUN dotnet restore src/TradeJournal.Api/TradeJournal.Api.csproj

COPY src ./src

RUN dotnet publish src/TradeJournal.Api/TradeJournal.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "TradeJournal.Api.dll"]
