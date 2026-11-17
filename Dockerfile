# Stage 1: Compile Tailwind CSS v4 assets
FROM node:22-alpine AS tailwind-build
WORKDIR /src

# Copy package manifests and install dependencies
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts

# Copy web source files required by Tailwind scanner
COPY JASIntWeb ./JASIntWeb
COPY JASIntWeb.Client ./JASIntWeb.Client

# Generate minified output.css
RUN npx @tailwindcss/cli -i ./JASIntWeb/wwwroot/css/input.css -o ./JASIntWeb/wwwroot/css/output.css --minify

# Stage 2: .NET SDK build & publish
FROM mcr.microsoft.com/dotnet/sdk:11.0 AS build
WORKDIR /src

# Restore project dependencies
COPY global.json Directory.Build.props ./
COPY ["JASIntWeb/JASIntWeb.csproj", "JASIntWeb/"]
COPY ["JASIntWeb.Client/JASIntWeb.Client.csproj", "JASIntWeb.Client/"]
RUN dotnet restore "JASIntWeb/JASIntWeb.csproj"

# Copy all source files
COPY . .

# Copy compiled Tailwind stylesheet from Stage 1
COPY --from=tailwind-build /src/JASIntWeb/wwwroot/css/output.css ./JASIntWeb/wwwroot/css/output.css

# Publish Release build
RUN dotnet publish "JASIntWeb/JASIntWeb.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS final
WORKDIR /app

# Configure default container port
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Database__ApplyMigrations=false
ENV Seed__Enabled=false

COPY --from=build /app/publish .

RUN mkdir -p /app/wwwroot/uploads && chown -R $APP_UID:$APP_UID /app/wwwroot/uploads
USER $APP_UID

ENTRYPOINT ["dotnet", "JASIntWeb.dll"]
