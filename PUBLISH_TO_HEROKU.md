# Heuristic Search Methods Simulation

App URL @ https://heuristic-search-methods-sim.herokuapp.com/

## To get started you will need:

- A heroku account
- The heroku cli https://devcenter.heroku.com/articles/heroku-cli
- Docker
- dotnet core sdk

## Steps

- Create your .Net app with docker support
- Ensure your app can be built and runs using the default docker setup
- Copy your DOCKER file to the root directory of the solution
- Modify the copied DOCKER file
  - comment out the ENTRYPOINT line
  - add CMD ASPNETCORE_URLS=http://\*:$PORT dotnet HeuristicSearchMethodsSimulation.dll after the commented out ENTRYPOINT line
- Create an app on heroku
- Add any variables the app requires in the heroku panel
- Push the docker image
- Enjoy!

## Dockerization and Pushing to Heroku

Using Powershell build the image and test it.

If your app requires enviromental variables, ensure they are in place.

```powershell
PS > docker build -t heuristicsearchmethodssimulation .
PS > docker run -d -p 8080:80 --name heuristicsearchmethodssimulation heuristicsearchmethodssimulation
PS > explorer http://localhost:8080
PS > docker rm --force heuristicsearchmethodssimulation
```

Assuming your are signed in to Heroku, publish away!

```powershell
PS > heroku container:push -a heuristic-search-methods-sim web
PS > heroku container:release -a heuristic-search-methods-sim web
```

## Resources

- https://dev.to/alrobilliard/deploying-net-core-to-heroku-1lfe