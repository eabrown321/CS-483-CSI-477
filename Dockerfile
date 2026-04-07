FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CS-483-CSI-477.csproj", "."]
RUN dotnet restore "CS-483-CSI-477.csproj"
COPY . .
RUN dotnet publish "CS-483-CSI-477.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "CS-483-CSI-477.dll"]
