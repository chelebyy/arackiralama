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
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RentACar.Worker.dll"]
