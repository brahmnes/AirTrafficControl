FROM microsoft/aspnetcore:2.0.5 AS base
WORKDIR /app
EXPOSE 5023

FROM microsoft/aspnetcore-build:2.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore
WORKDIR /src/atcsvc
RUN dotnet build --no-restore -o /app

FROM build AS publish
RUN dotnet publish --no-restore -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "atcsvc.dll"]