using GraphQL.Execution;
using GraphQL.Language.AST;

namespace GraphQL.ExecutionStrategy
{
    public class OptimisedDocumentExecutor : DocumentExecuter
    {
        protected override IExecutionStrategy SelectExecutionStrategy(ExecutionContext context)
        {
            return context.Operation.OperationType == OperationType.Query
                ? new PrioritisedParallelExecutionStrategy()
                : base.SelectExecutionStrategy(context);
        }
    }
}