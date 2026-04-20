FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/ProjectX.PM.API/ProjectX.PM.API.csproj src/ProjectX.PM.API/
COPY src/ProjectX.PM.Application/ProjectX.PM.Application.csproj src/ProjectX.PM.Application/
COPY src/ProjectX.PM.Domain/ProjectX.PM.Domain.csproj src/ProjectX.PM.Domain/
COPY src/ProjectX.PM.Infrastructure/ProjectX.PM.Infrastructure.csproj src/ProjectX.PM.Infrastructure/

RUN dotnet restore src/ProjectX.PM.API/ProjectX.PM.API.csproj

COPY . .

RUN dotnet publish src/ProjectX.PM.API/ProjectX.PM.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ProjectX.PM.API.dll"]

