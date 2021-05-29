using System;
using GraphQL.Instrumentation;

namespace GraphQL.ExecutionStrategy.Tests
{
    public interface ITestDocumentExecuter
    {
        event Action<PerfRecord[]> OnExecute;
    }
}