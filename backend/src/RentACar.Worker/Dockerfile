FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["global.json", "."]
COPY ["src/RentACar.Worker/RentACar.Worker.csproj", "src/RentACar.Worker/"]
COPY ["src/RentACar.Infrastructure/RentACar.Infrastructure.csproj", "src/RentACar.Infrastructure/"]
COPY ["src/RentACar.Core/RentACar.Core.csproj", "src/RentACar.Core/"]
RUN dotnet restore "src/RentACar.Worker/RentACar.Worker.csproj"

COPY . .
RUN dotnet publish "src/RentACar.Worker/RentACar.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

# Create non-root user for security (Debian-based image)
RUN groupadd --system --gid 10000 appgroup && \
    useradd --system --uid 10000 --gid appgroup --shell /bin/false appuser

COPY --from=build /app/publish .

# Set ownership to non-root user
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

ENTRYPOINT ["dotnet", "RentACar.Worker.dll"]
