# Use the official .NET 8 SDK image for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app

# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Copy published output
COPY --from=build /app .

# Railway provides PORT environment variable
ENV ASPNETCORE_URLS=http://+:${PORT:-80}
EXPOSE ${PORT:-80}

# Start the application
ENTRYPOINT ["dotnet", "WebsiteBuilderAPI.dll"]