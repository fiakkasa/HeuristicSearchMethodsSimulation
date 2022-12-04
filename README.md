# Heuristic Search Methods Simulation

## Prerequisites

- Install .NET 6 SDK from https://dotnet.microsoft.com/en-us/download/dotnet/6.0
- Set your user secrets
  ```json
  {
    "ConnectionStrings": {
      "MongoConnection": "connection_string"
    },
    "AuthMessageSenderOptions": {
      "SendGridKey": "key"
    }
  }
  ```

## Running the project

```powershell
 dotnet run -p ./HeuristicSearchMethodsSimulation/HeuristicSearchMethodsSimulation.csproj
```

## Running the project in watch mode

```powershell
dotnet watch run -p ./HeuristicSearchMethodsSimulation/HeuristicSearchMethodsSimulation.csproj
```

## Docker

### Images

- .NET SDK: https://hub.docker.com/_/microsoft-dotnet-sdk
- ASP.NET Core Runtime: https://hub.docker.com/_/microsoft-dotnet-aspnet
