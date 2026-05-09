FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY CryptoSignalBot.sln ./
COPY CryptoSignalBot.Domain/CryptoSignalBot.Domain.csproj CryptoSignalBot.Domain/
COPY CryptoSignalBot.Application/CryptoSignalBot.Application.csproj CryptoSignalBot.Application/
COPY CryptoSignalBot.Infrastructure/CryptoSignalBot.Infrastructure.csproj CryptoSignalBot.Infrastructure/
COPY CryptoSignalBot.Worker/CryptoSignalBot.Worker.csproj CryptoSignalBot.Worker/
COPY CryptoSignalBot.Dashboard/CryptoSignalBot.Dashboard.csproj CryptoSignalBot.Dashboard/
COPY tests/CryptoSignalBot.Application.Tests/CryptoSignalBot.Application.Tests.csproj tests/CryptoSignalBot.Application.Tests/
RUN dotnet restore CryptoSignalBot.sln

COPY . .
RUN dotnet publish CryptoSignalBot.Worker/CryptoSignalBot.Worker.csproj -c Release -o /out/worker --no-restore
RUN dotnet publish CryptoSignalBot.Dashboard/CryptoSignalBot.Dashboard.csproj -c Release -o /out/dashboard --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=5055
ENV CRYPTO_SIGNAL_BOT_WORKER_DLL=/app/worker/CryptoSignalBot.Worker.dll
ENV CRYPTO_SIGNAL_BOT_WORKER_SETTINGS=/app/worker/appsettings.json

WORKDIR /app
COPY --from=build /out/worker ./worker
COPY --from=build /out/dashboard ./dashboard

EXPOSE 5055
WORKDIR /app/dashboard
ENTRYPOINT ["dotnet", "CryptoSignalBot.Dashboard.dll"]
