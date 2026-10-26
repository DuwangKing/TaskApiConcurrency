# TaskApiConcurrency

## Технологии
- .NET 9 (Minimal API)
- PostgreSQL 15 + Entity Framework Core
- RabbitMQ 3
- Docker + Docker Compose
- xUnit + Testcontainers + Moq

## Требования
- Docker и Docker Compose

## Инструкция по запуску
1. Клонируйте репозиторий:
   ```bash
   git clone https://github.com/DuwangKing/TaskApiConcurrency.git
   cd TaskApiConcurrency
   ```
2. Соберите и запустите контейнеры:
   ```bash
      docker compose up -d --build
   ```
3. Протестируйте эндпоинты по адресу: http://localhost:8080/tasks

4. Запустите тесты:
   ```bash
      dotnet test
   ```
