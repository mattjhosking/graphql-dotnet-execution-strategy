using FluentAssertions;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
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
            var client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IDocumentExecuter, DocumentExecuter>());
                    });
                })
                .CreateClient();

            await RetrieveDroids(client);
        }

        [Fact]
        public async Task RetrieveDroidsWithOptimizedDocumentExecuter()
        {
            var client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        services.Replace(ServiceDescriptor.Singleton<IDocumentExecuter, OptimisedDocumentExecutor>());
                    });
                })
                .CreateClient();

            await RetrieveDroids(client);
        }

        private async Task RetrieveDroids(HttpClient httpClient)
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
    }
}
