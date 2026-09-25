FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["OpenIec104MqttBridge.csproj", "./"]
COPY ["OpenIec104MqttBridge.Tests/OpenIec104MqttBridge.Tests.csproj", "OpenIec104MqttBridge.Tests/"]
RUN dotnet restore "OpenIec104MqttBridge.csproj"
RUN dotnet restore "OpenIec104MqttBridge.Tests/OpenIec104MqttBridge.Tests.csproj"

# Copy everything else and build
COPY . .

# Run tests. The docker build will instantly fail if tests fail!
RUN dotnet test "OpenIec104MqttBridge.Tests/OpenIec104MqttBridge.Tests.csproj" -c Release

# Publish the final binaries
RUN dotnet publish "OpenIec104MqttBridge.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build minimal runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-bookworm-slim AS final
WORKDIR /app
COPY --from=build /app/publish .

# Run as a non-root user for security in production
RUN useradd -m bridgeuser
USER bridgeuser

CMD ["dotnet", "OpenIec104MqttBridge.dll"]
