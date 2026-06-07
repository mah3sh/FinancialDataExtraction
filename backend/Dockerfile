FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DocPipeline.API/DocPipeline.API.csproj", "src/DocPipeline.API/"]
COPY ["src/DocPipeline.Application/DocPipeline.Application.csproj", "src/DocPipeline.Application/"]
COPY ["src/DocPipeline.Domain/DocPipeline.Domain.csproj", "src/DocPipeline.Domain/"]
COPY ["src/DocPipeline.Infrastructure/DocPipeline.Infrastructure.csproj", "src/DocPipeline.Infrastructure/"]
RUN dotnet restore "src/DocPipeline.API/DocPipeline.API.csproj"
COPY . .
RUN dotnet publish "src/DocPipeline.API/DocPipeline.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /tmp/docpipeline-uploads
ENTRYPOINT ["dotnet", "DocPipeline.API.dll"]
