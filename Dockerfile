FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY UrlRedirect/*.csproj ./UrlRedirect/
WORKDIR /app/UrlRedirect
RUN dotnet restore

# copy and publish app and libraries
WORKDIR /app/
COPY UrlRedirect/. ./UrlRedirect/
WORKDIR /app/UrlRedirect
RUN dotnet publish -c Release -o out

FROM mcr.microsoft.com/dotnet/core/runtime:2.2 AS runtime
WORKDIR /app
COPY --from=build /app/UrlRedirect/out ./
ENTRYPOINT ["dotnet", "UrlRedirect.dll"]
