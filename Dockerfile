# Etapa base: imagen del runtime de ASP.NET Core
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Etapa de build: imagen del SDK de .NET
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# Copiar el archivo de proyecto y restaurar dependencias
COPY ["MyBackend/MyBackend.csproj", "MyBackend/"]
RUN dotnet restore "MyBackend/MyBackend.csproj"
# Copiar todo el código fuente
COPY . .
WORKDIR "/src/MyBackend"
# Publicar la aplicación en modo Release
RUN dotnet publish "MyBackend.csproj" -c Release -o /app/publish

# Etapa final: copiar los archivos publicados en la imagen base
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "MyBackend.dll"]
