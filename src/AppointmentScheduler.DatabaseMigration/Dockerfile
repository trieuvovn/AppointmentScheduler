FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/AppointmentScheduler.DatabaseMigration/AppointmentScheduler.DatabaseMigration.csproj src/AppointmentScheduler.DatabaseMigration/
RUN dotnet restore src/AppointmentScheduler.DatabaseMigration/AppointmentScheduler.DatabaseMigration.csproj

COPY src/AppointmentScheduler.DatabaseMigration/ src/AppointmentScheduler.DatabaseMigration/

RUN dotnet publish src/AppointmentScheduler.DatabaseMigration/AppointmentScheduler.DatabaseMigration.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "AppointmentScheduler.DatabaseMigration.dll"]
