#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-bullseye-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-bullseye-slim AS build
WORKDIR /src
COPY ["HeuristicSearchMethodsSimulation/HeuristicSearchMethodsSimulation.csproj", "HeuristicSearchMethodsSimulation/"]
RUN dotnet restore "HeuristicSearchMethodsSimulation/HeuristicSearchMethodsSimulation.csproj"
COPY . .
WORKDIR "/src/HeuristicSearchMethodsSimulation"
RUN dotnet build "HeuristicSearchMethodsSimulation.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "HeuristicSearchMethodsSimulation.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "HeuristicSearchMethodsSimulation.dll"]
CMD ASPNETCORE_URLS=http://*:$PORT dotnet HeuristicSearchMethodsSimulation.dll
