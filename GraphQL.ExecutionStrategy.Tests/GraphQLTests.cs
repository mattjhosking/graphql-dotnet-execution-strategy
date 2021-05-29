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

        private (HttpClient HttpClient, ITestDocumentExecuter DocumentExecutor) CreateClient<T>()
            where T : class, IDocumentExecuter, new()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IDocumentExecuter, TestDocumentExecuter<T>>());
                    });
                });
            return (factory.CreateClient(), factory.Services.GetRequiredService<IDocumentExecuter>() as ITestDocumentExecuter);
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
            var timings = new List<double>();
            foreach (var createClient in new Func<(HttpClient HttpClient, ITestDocumentExecuter DocumentExecuter)>[] { CreateClient<DocumentExecuter>, CreateClient<OptimisedDocumentExecutor> })
            {
                var executionRecords = new List<PerfRecord>();
                var client = createClient();
                // Warm up
                await RetrieveDroids(client.HttpClient);
                client.DocumentExecuter.OnExecute += perf => executionRecords.Add(perf.FirstOrDefault(x => x.Category == "execution"));
                for (int i = 0; i < 100; i++)
                    await RetrieveDroids(client.HttpClient);
                timings.Add(executionRecords.Sum(x => x.Duration));
            }

            timings.Should().HaveCount(2);
            // Ensure it's no more than 150% of the time to execute the default strategy
            (timings[1] / timings[0]).Should().BeLessThan(1.5);
        }
    }
}
