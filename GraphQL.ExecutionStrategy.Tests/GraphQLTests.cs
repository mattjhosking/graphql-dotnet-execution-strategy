using FluentAssertions;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using GraphQL.Instrumentation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using StarWars;
using StarWars.Types;
using Xunit;

namespace GraphQL.ExecutionStrategy.Tests
{
    public class GraphQLTests : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public GraphQLTests(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RetrieveDroidsWithDefaultDocumentExecuter()
        {
            await RetrieveDroids(CreateClient<OptimisedDocumentExecutor>().HttpClient);
        }

        [Fact]
        public async Task RetrieveDroidsWithOptimizedDocumentExecuter()
        {
            await RetrieveDroids(CreateClient<OptimisedDocumentExecutor>().HttpClient);
        }

        private (HttpClient HttpClient, ITestDocumentExecuter DocumentExecutor, Mock<StarWarsData> Data) CreateClient<T>()
            where T : class, IDocumentExecuter, new()
        {
            var trackedStarWarsData = new Mock<StarWarsData> {CallBase = true};
            trackedStarWarsData.Setup(x => x.GetDroidByIdAsync(It.IsAny<string>()));
            trackedStarWarsData.Setup(x => x.GetDroidOwnersByIdsAsync(It.IsAny<string[]>()));
            trackedStarWarsData.Setup(x => x.GetDroidsByIdsAsync(It.IsAny<string[]>()));
            trackedStarWarsData.Setup(x => x.GetFriends(It.IsAny<StarWarsCharacter>()));
            trackedStarWarsData.Setup(x => x.GetFriendsForIdsAsync(It.IsAny<string[]>()));
            trackedStarWarsData.Setup(x => x.GetHumanByIdAsync(It.IsAny<string>()));

            var factory = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IDocumentExecuter, TestDocumentExecuter<T>>());
                        services.Replace(ServiceDescriptor.Singleton(trackedStarWarsData.Object));
                    });
                });
            return (factory.CreateClient(), factory.Services.GetRequiredService<IDocumentExecuter>() as ITestDocumentExecuter, trackedStarWarsData);
        }

        private static async Task RetrieveDroids(HttpClient httpClient)
        {
            var graphClient = new GraphQLHttpClient(new GraphQLHttpClientOptions { EndPoint = new Uri("http://localhost/api/graphql") }, new SystemTextJsonSerializer(), httpClient);

            var response = await graphClient
                .SendQueryAsync(@"query TestQuery($id: String!) {
    droid(id: $id) {
        id
        name
        primaryFunction
        owner {
            id
            name
            friends {
                id
                name
            }
        }
        friends {
            id
            name
        }
        appearsIn
    }
}",
                    new
                    {
                        id = "3"
                    }, "TestQuery", () => new { droid = new DroidResponse() });

            response.Errors.Should().BeNullOrEmpty();
            response.Data.droid.Name.Should().Be("R2-D2");
        }

        [Fact]
        public async Task ComparePerformance()
        {
            var invocations = new List<int>();
            var timings = new List<double>();
            foreach (var createClient in new Func<(HttpClient HttpClient, ITestDocumentExecuter DocumentExecuter, Mock<StarWarsData> Data)>[] { CreateClient<DocumentExecuter>, CreateClient<OptimisedDocumentExecutor> })
            {
                var executionRecords = new List<PerfRecord>();
                var client = createClient();
                // Warm up
                await RetrieveDroids(client.HttpClient);
                client.Data.Invocations.Clear();
                client.DocumentExecuter.OnExecute += perf => executionRecords.Add(perf.FirstOrDefault(x => x.Category == "execution"));
                for (int i = 0; i < 100; i++)
                    await RetrieveDroids(client.HttpClient);
                invocations.Add(client.Data.Invocations.Count);
                timings.Add(executionRecords.Sum(x => x.Duration));
            }

            invocations.Should().HaveCount(2);
            // Ensure the optimised strategy performs less invocations
            invocations[1].Should().BeLessThan(invocations[0]);

            timings.Should().HaveCount(2);
            // Ensure it's no more than 150% of the time to execute the default strategy
            // In an I/O bound scenario this will be the CPU time difference (here there is no I/O)
            // In most cases less I/O is much more valuable than less CPU usage (to a point)
            (timings[1] / timings[0]).Should().BeLessThan(1.5);
        }
    }
}
