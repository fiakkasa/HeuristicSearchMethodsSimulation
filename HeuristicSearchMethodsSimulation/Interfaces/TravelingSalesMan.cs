using AutoMapper;
using GeoCoordinatePortable;
using HeuristicSearchMethodsSimulation.Interfaces;
using HeuristicSearchMethodsSimulation.Models;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbGenericRepository.Attributes;
using Plotly.Blazor;
using Plotly.Blazor.Traces;
using Plotly.Blazor.Traces.ScatterGeoLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HeuristicSearchMethodsSimulation.Services
{
    public class TravelingSalesMan : ITravelingSalesMan
    {
        private readonly Func<IMongoClient> _mongoClientFactory;
        private readonly IMapper _mapper;
        private readonly ILogger<TravelingSalesMan> _logger;
        private readonly IMongoClient? _client;
        private IMongoClient Client => _client ?? _mongoClientFactory();

        public TravelingSalesMan(Func<IMongoClient> mongoClientFactory, IMapper mapper, ILogger<TravelingSalesMan> logger)
        {
            _mongoClientFactory = mongoClientFactory;
            _mapper = mapper;
            _logger = logger;
        }

        private IMongoCollection<Location> Locations() =>
           Client.GetDatabase(Consts.MongoDatabase).GetCollection<Location>(
               Attribute.GetCustomAttributes(typeof(Location))
                   .OfType<CollectionNameAttribute>()
                   .Take(1)
                   .Select(x => x.Name)
                   .FirstOrDefault()
           );

        public async Task<List<LocationGeo>> Fetch(int limit = 1000, CancellationToken cancellationToken = default)
        {
            try
            {
                var collection =
                    await Locations()
                        .Find(Builders<Location>.Filter.Empty)
                        .SortBy(x => x.Ordinal)
                        .ThenBy(x => x.Label)
                        .ThenBy(x => x.ShortCode)
                        .ThenBy(x => x.Id)
                        .Limit(limit)
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(true);

                return collection.ConvertAll(x =>
                    _mapper.Map<LocationGeo>(x) with
                    {
                        Geo = new GeoCoordinate(x.Latitude, x.Longitude)
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return new List<LocationGeo>();
            }
        }

        public Task<List<ITrace>> CalculateMapMarkers(List<LocationGeo> locations, CancellationToken cancellationToken = default)
        {
            try
            {
                return Task.Run(() =>
                    locations.Count == 0
                        ? new List<ITrace> { new ScatterGeo { LocationMode = LocationModeEnum.ISO3 } }
                        : locations.ConvertAll<ITrace>(x =>
                            new ScatterGeo
                            {
                                LocationMode = LocationModeEnum.ISO3,
                                Lon = new List<object> { x.Longitude },
                                Lat = new List<object> { x.Latitude },
                                Mode = ModeFlag.Markers,
                                Text = x.Label,
                                Name = x.ShortCode,
                                HoverInfo = HoverInfoFlag.Lat | HoverInfoFlag.Lon | HoverInfoFlag.Text | HoverInfoFlag.Name
                            }
                        ),
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return Task.FromResult(new List<ITrace>());
            }
        }

        public Task<List<LocationRow>> CalculateMatrix(List<LocationGeo> locations, CancellationToken cancellationToken = default)
        {
            try
            {
                return Task.Run(() =>
                    locations.ConvertAll(
                        location =>
                        {
                            var rowCollection =
                                locations.Select(
                                    (otherLocation, index) =>
                                        new LocationToLocation(
                                            A: location,
                                            B: otherLocation,
                                            DirectionalKey: $"{location.ShortCode}-{otherLocation.ShortCode}",
                                            ReverseDirectionalKey: $"{otherLocation.ShortCode}-{location.ShortCode}",
                                            Key: location.ShortCode.CompareTo(otherLocation.ShortCode) <= 0
                                                ? $"{location.ShortCode}-{otherLocation.ShortCode}"
                                                : $"{otherLocation.ShortCode}-{location.ShortCode}",
                                            DistanceInMeters: (location, otherLocation) switch
                                            {
                                                { location: { Geo: { } lcGeo } lc, otherLocation: { Geo: { } olcGeo } } => lcGeo.GetDistanceTo(olcGeo),
                                                _ => 0
                                            },
                                            Index: index,
                                            OrdinalFromOrigin: 0
                                        )
                                    )
                                    .OrderBy(x => x.DistanceInMeters)
                                    .Select((x, ordinalFromOrigin) => x with { OrdinalFromOrigin = ordinalFromOrigin })
                                    .OrderBy(x => x.Index)
                                    .ToList();

                            var selfKey = $"{location.ShortCode}-{location.ShortCode}";

                            return new LocationRow(
                                Collection: rowCollection,
                                Ylabel: location.Label,
                                Xlabels: locations.ConvertAll(x => x.Label),
                                Min: rowCollection.Where(x => x.Key != selfKey).Min(),
                                Max: rowCollection.Where(x => x.Key != selfKey).Max()
                            );
                        }
                    ),
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                return Task.FromResult(new List<LocationRow>());
            }
        }
    }
}
