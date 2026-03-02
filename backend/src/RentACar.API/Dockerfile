FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["src/RentACar.API/RentACar.API.csproj", "src/RentACar.API/"]
COPY ["src/RentACar.Infrastructure/RentACar.Infrastructure.csproj", "src/RentACar.Infrastructure/"]
COPY ["src/RentACar.Core/RentACar.Core.csproj", "src/RentACar.Core/"]
RUN dotnet restore "src/RentACar.API/RentACar.API.csproj"

COPY . .
RUN dotnet publish "src/RentACar.API/RentACar.API.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RentACar.API.dll"]
