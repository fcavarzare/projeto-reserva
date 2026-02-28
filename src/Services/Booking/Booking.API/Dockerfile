# Estágio 1: Base de execução (Runtime - Leve)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# Estágio 2: Compilação (Build - Pesado)
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar arquivos de projeto e restaurar dependências (otimiza o cache do Docker)
COPY ["src/Services/Booking/Booking.API/Booking.API.csproj", "src/Services/Booking/Booking.API/"]
COPY ["src/Services/Booking/Booking.Application/Booking.Application.csproj", "src/Services/Booking/Booking.Application/"]
COPY ["src/Services/Booking/Booking.Domain/Booking.Domain.csproj", "src/Services/Booking/Booking.Domain/"]
COPY ["src/Services/Booking/Booking.Infrastructure/Booking.Infrastructure.csproj", "src/Services/Booking/Booking.Infrastructure/"]

RUN dotnet restore "src/Services/Booking/Booking.API/Booking.API.csproj"

# Copiar todo o código e compilar
COPY . .
WORKDIR "/src/src/Services/Booking/Booking.API"
RUN dotnet build "Booking.API.csproj" -c Release -o /app/build

# Estágio 3: Publicação (Publish - Apenas os binários finais)
FROM build AS publish
RUN dotnet publish "Booking.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Estágio 4: Imagem Final (Produção)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Booking.API.dll"]
