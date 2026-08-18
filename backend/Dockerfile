FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY BlogApi/BlogApi.csproj BlogApi/
RUN dotnet restore BlogApi/BlogApi.csproj
COPY BlogApi/ BlogApi/
RUN dotnet publish BlogApi/BlogApi.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN chown -R 1000:1000 /app
USER 1000
ENTRYPOINT ["dotnet", "BlogApi.dll"]
