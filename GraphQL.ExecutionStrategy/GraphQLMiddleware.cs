using GraphQL.Types;
using GraphQL.Validation;
using Microsoft.AspNetCore.Http;
using StarWars;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace GraphQL.ExecutionStrategy
{
    public class GraphQLMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly GraphQLSettings _settings;
        private readonly IDocumentExecuter _executer;
        private readonly IDocumentWriter _writer;

        public GraphQLMiddleware(
            RequestDelegate next,
            GraphQLSettings settings,
            IDocumentExecuter executer,
            IDocumentWriter writer)
        {
            _next = next;
            _settings = settings;
            _executer = executer;
            _writer = writer;
        }

        public async Task Invoke(HttpContext context, ISchema schema)
        {
            if (!IsGraphQLRequest(context))
            {
                await _next(context);
                return;
            }

            await ExecuteAsync(context, schema);
        }

        private bool IsGraphQLRequest(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments(_settings.Path)
                && string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ExecuteAsync(HttpContext context, ISchema schema)
        {
            var request = await Deserialize<GraphQLRequest>(context.Request.Body);
            if (request == null)
            {
                context.Response.StatusCode = 400;
            }
            else
            {
                var result = await _executer.ExecuteAsync(_ =>
                {
                    _.Schema = schema;
                    _.Query = request.Query;
                    _.OperationName = request.OperationName;
                    _.Inputs = request.Variables.ToInputs();
                    _.UserContext = _settings.BuildUserContext?.Invoke(context);
                    _.ValidationRules = DocumentValidator.CoreRules.Concat(new[] { new InputValidationRule() });
                    _.EnableMetrics = _settings.EnableMetrics;
                });

                await WriteResponseAsync(context, result);
            }
        }

        private async Task WriteResponseAsync(HttpContext context, ExecutionResult result)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = result.Errors?.Any() == true ? (int)HttpStatusCode.BadRequest : (int)HttpStatusCode.OK;

            await _writer.WriteAsync(context.Response.Body, result);
        }

        public static async Task<T?> Deserialize<T>(Stream s)
        {
            using (var reader = new StreamReader(s))
                return await JsonSerializer.DeserializeAsync<T>(s, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }
    }
}