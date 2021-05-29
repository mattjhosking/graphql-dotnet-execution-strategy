using System;
using System.Threading.Tasks;
using GraphQL.Instrumentation;

namespace GraphQL.ExecutionStrategy.Tests
{
    public class TestDocumentExecuter<T> : IDocumentExecuter, ITestDocumentExecuter 
        where T : class, IDocumentExecuter, new()
    {
        private readonly IDocumentExecuter _inner;

        public TestDocumentExecuter()
        {
            _inner = new T();
        }

        public async Task<ExecutionResult> ExecuteAsync(ExecutionOptions options)
        {
            var result = await _inner.ExecuteAsync(options);
            OnExecute?.Invoke(result.Perf);
            return result;
        }

        public event Action<PerfRecord[]> OnExecute;
    }
}