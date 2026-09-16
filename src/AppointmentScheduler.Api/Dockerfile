FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/AppointmentScheduler.Domain/AppointmentScheduler.Domain.csproj src/AppointmentScheduler.Domain/
COPY src/AppointmentScheduler.Application/AppointmentScheduler.Application.csproj src/AppointmentScheduler.Application/
COPY src/AppointmentScheduler.Infrastructure/AppointmentScheduler.Infrastructure.csproj src/AppointmentScheduler.Infrastructure/
COPY src/AppointmentScheduler.Api/AppointmentScheduler.Api.csproj src/AppointmentScheduler.Api/
RUN dotnet restore src/AppointmentScheduler.Api/AppointmentScheduler.Api.csproj

COPY src/AppointmentScheduler.Domain/ src/AppointmentScheduler.Domain/
COPY src/AppointmentScheduler.Application/ src/AppointmentScheduler.Application/
COPY src/AppointmentScheduler.Infrastructure/ src/AppointmentScheduler.Infrastructure/
COPY src/AppointmentScheduler.Api/ src/AppointmentScheduler.Api/

RUN dotnet publish src/AppointmentScheduler.Api/AppointmentScheduler.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AppointmentScheduler.Api.dll"]
