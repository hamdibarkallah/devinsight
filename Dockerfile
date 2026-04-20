FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY DevInsight.sln .
COPY src/DevInsight.Domain/DevInsight.Domain.csproj src/DevInsight.Domain/
COPY src/DevInsight.Application/DevInsight.Application.csproj src/DevInsight.Application/
COPY src/DevInsight.Infrastructure/DevInsight.Infrastructure.csproj src/DevInsight.Infrastructure/
COPY src/DevInsight.API/DevInsight.API.csproj src/DevInsight.API/
COPY tests/DevInsight.Tests.Unit/DevInsight.Tests.Unit.csproj tests/DevInsight.Tests.Unit/
RUN dotnet restore DevInsight.sln
COPY . .
RUN dotnet test tests/DevInsight.Tests.Unit/DevInsight.Tests.Unit.csproj -c Release --no-restore
RUN dotnet publish src/DevInsight.API/DevInsight.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "DevInsight.API.dll"]
