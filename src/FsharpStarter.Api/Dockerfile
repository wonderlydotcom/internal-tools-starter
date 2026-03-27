# Build frontend
FROM node:22-alpine AS frontend-build
WORKDIR /frontend
COPY ./www/package*.json ./
RUN npm ci
COPY ./www .
RUN npm run build

# Build .NET App
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy project files and restore as distinct layers
COPY ./FsharpStarter.Production.sln ./
COPY ./src/FsharpStarter.Domain/FsharpStarter.Domain.fsproj ./src/FsharpStarter.Domain/
COPY ./src/FsharpStarter.Application/FsharpStarter.Application.fsproj ./src/FsharpStarter.Application/
COPY ./src/FsharpStarter.Infrastructure/FsharpStarter.Infrastructure.fsproj ./src/FsharpStarter.Infrastructure/
COPY ./src/FsharpStarter.Api/FsharpStarter.Api.fsproj ./src/FsharpStarter.Api/
COPY ./src/FsharpStarter.McpServer/FsharpStarter.McpServer.fsproj ./src/FsharpStarter.McpServer/
COPY ./src/FsharpStarter.Domain/test/FsharpStarter.Domain.Tests.fsproj ./src/FsharpStarter.Domain/test/
COPY ./src/FsharpStarter.Application/test/FsharpStarter.Application.Tests.fsproj ./src/FsharpStarter.Application/test/
RUN dotnet restore

# Copy and publish app
COPY . .
RUN dotnet publish src/FsharpStarter.Api/FsharpStarter.Api.fsproj -c release -o /app

# Final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY --from=frontend-build /frontend/dist ./wwwroot

EXPOSE 8080
ENV HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "FsharpStarter.Api.dll"]
