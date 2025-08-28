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

# Railway provides PORT environment variable at runtime
# We use a shell script to handle the dynamic PORT
EXPOSE 80

# Create a startup script
RUN echo '#!/bin/sh\nexport ASPNETCORE_URLS="http://+:${PORT:-80}"\nexec dotnet WebsiteBuilderAPI.dll' > /app/start.sh && \
    chmod +x /app/start.sh

# Start the application using the script
ENTRYPOINT ["/app/start.sh"]