FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Копіюємо файли проекту
COPY *.csproj ./
RUN dotnet restore

# Копіюємо весь код та збираємо
COPY . ./
RUN dotnet publish -c Release -o out

# Створюємо фінальний образ
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/out .

# Запускаємо сервер
ENTRYPOINT ["dotnet", "UdpChatServer.dll", "server"]
