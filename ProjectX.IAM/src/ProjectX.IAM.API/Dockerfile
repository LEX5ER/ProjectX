FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY src/ProjectX.IAM.API/ProjectX.IAM.API.csproj src/ProjectX.IAM.API/
COPY src/ProjectX.IAM.Application/ProjectX.IAM.Application.csproj src/ProjectX.IAM.Application/
COPY src/ProjectX.IAM.Domain/ProjectX.IAM.Domain.csproj src/ProjectX.IAM.Domain/
COPY src/ProjectX.IAM.Infrastructure/ProjectX.IAM.Infrastructure.csproj src/ProjectX.IAM.Infrastructure/

RUN dotnet restore src/ProjectX.IAM.API/ProjectX.IAM.API.csproj

COPY . .

RUN dotnet publish src/ProjectX.IAM.API/ProjectX.IAM.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "ProjectX.IAM.API.dll"]

