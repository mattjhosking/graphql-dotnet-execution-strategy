using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.DataLoader;
using GraphQL.Execution;
using GraphQL.Types;

namespace GraphQL.ExecutionStrategy
{
    /// <inheritdoc cref="ExecuteNodeTreeAsync(ExecutionContext, ObjectExecutionNode)"/>
    public class PrioritisedParallelExecutionStrategy : Execution.ExecutionStrategy
    {
        // frequently reused objects
        private Queue<ExecutionNode>? _reusablePendingNodes;
        private List<ExecutionNode>? _reusablePendingDataLoaders;
        private List<Task>? _reusableCurrentTasks;
        private List<ExecutionNode>? _reusableCurrentNodes;

        /// <summary>
        /// Gets a static instance of <see cref="ParallelExecutionStrategy"/> strategy.
        /// </summary>
        public static PrioritisedParallelExecutionStrategy Instance { get; } = new PrioritisedParallelExecutionStrategy();

        /// <summary>
        /// Executes document nodes in parallel. Field resolvers must be designed for multi-threaded use.
        /// Nodes that return a <see cref="IDataLoaderResult"/> will execute once all other pending nodes
        /// have been completed.
        /// </summary>
        protected override async Task ExecuteNodeTreeAsync(ExecutionContext context, ObjectExecutionNode rootNode)
        {
            var pendingNodes = System.Threading.Interlocked.Exchange(ref _reusablePendingNodes, null) ?? new Queue<ExecutionNode>();
            pendingNodes.Enqueue(rootNode);
            var pendingDataLoaders = System.Threading.Interlocked.Exchange(ref _reusablePendingDataLoaders, null) ?? new List<ExecutionNode>();

            var currentTasks = System.Threading.Interlocked.Exchange(ref _reusableCurrentTasks, null) ?? new List<Task>();
            var currentNodes = System.Threading.Interlocked.Exchange(ref _reusableCurrentNodes, null) ?? new List<ExecutionNode>();

            try
            {
                while (pendingNodes.Count > 0 || pendingDataLoaders.Count > 0 || currentTasks.Count > 0)
                {
                    while (pendingNodes.Count > 0 || currentTasks.Count > 0)
                    {
                        // Start executing pending nodes, while limiting the maximum number of parallel executed nodes to the set limit
                        while ((context.MaxParallelExecutionCount == null || currentTasks.Count < context.MaxParallelExecutionCount)
                            && pendingNodes.Count > 0)
                        {
                            context.CancellationToken.ThrowIfCancellationRequested();
                            var pendingNode = pendingNodes.Dequeue();
                            var pendingNodeTask = ExecuteNodeAsync(context, pendingNode);
                            if (pendingNodeTask.IsCompleted)
                            {
                                // Throw any caught exceptions
                                await pendingNodeTask;

                                // Node completed synchronously, so no need to add it to the list of currently executing nodes
                                // instead add any child nodes to the pendingNodes queue directly here
                                if (pendingNode.Result is IDataLoaderResult)
                                {
                                    pendingDataLoaders.Add(pendingNode);
                                }
                                else if (pendingNode is IParentExecutionNode parentExecutionNode)
                                {
                                    parentExecutionNode.ApplyToChildren((node, state) => state.Enqueue(node), pendingNodes);
                                }
                            }
                            else
                            {
                                // Node is actually asynchronous, so add it to the list of current tasks being executed in parallel
                                currentTasks.Add(pendingNodeTask);
                                currentNodes.Add(pendingNode);
                            }

                        }

#pragma warning disable CS0612 // Type or member is obsolete
                        await OnBeforeExecutionStepAwaitedAsync(context)
#pragma warning restore CS0612 // Type or member is obsolete
                        .ConfigureAwait(false);

                        // Await tasks for this execution step
                        await Task.WhenAll(currentTasks)
                            .ConfigureAwait(false);

                        // Add child nodes to pending nodes to execute the next level in parallel
                        foreach (var node in currentNodes)
                        {
                            if (node.Result is IDataLoaderResult)
                            {
                                pendingDataLoaders.Add(node);
                            }
                            else if (node is IParentExecutionNode p)
                            {
                                p.ApplyToChildren((x, state) => state.Enqueue(x), pendingNodes);
                            }
                        }

                        currentTasks.Clear();
                        currentNodes.Clear();
                    }

                    var pendingLoaderGraphTypes = pendingDataLoaders
                        .SelectMany(x => GetGraphTypes(x, context).Select(t => t.Name))
                        .ToHashSet();

                    // always process priority data loaders first as they potentially block others that can be batched in
                    var priorityDataLoaders = new Queue<ExecutionNode>(pendingDataLoaders
                        .Where(x => HasChildOfGraphType(x, context, pendingLoaderGraphTypes))
                    );

                    var remainingDataLoaders = new Queue<ExecutionNode>(pendingDataLoaders
                        .Except(priorityDataLoaders)
                    );

                    if (priorityDataLoaders.Count > 0)
                    {
                        ProcessDataLoaders(priorityDataLoaders, context, currentTasks, currentNodes, pendingDataLoaders);
                        continue;
                    }

                    //run pending data loaders
                    ProcessDataLoaders(remainingDataLoaders, context, currentTasks, currentNodes, pendingDataLoaders);
                }
            }
            finally
            {
                pendingNodes.Clear();
                pendingDataLoaders.Clear();
                currentTasks.Clear();
                currentNodes.Clear();

                System.Threading.Interlocked.CompareExchange(ref _reusablePendingNodes, pendingNodes, null);
                System.Threading.Interlocked.CompareExchange(ref _reusablePendingDataLoaders, pendingDataLoaders, null);
                System.Threading.Interlocked.CompareExchange(ref _reusableCurrentTasks, currentTasks, null);
                System.Threading.Interlocked.CompareExchange(ref _reusableCurrentNodes, currentNodes, null);
            }
        }

        private void ProcessDataLoaders(Queue<ExecutionNode> dataLoaderQueue, ExecutionContext context, List<Task> currentTasks, List<ExecutionNode> currentNodes, List<ExecutionNode> pendingDataLoaders)
        {
            while (dataLoaderQueue.Count > 0)
            {
                var dataLoaderNode = dataLoaderQueue.Dequeue();
                pendingDataLoaders.Remove(dataLoaderNode);
                currentTasks.Add(CompleteDataLoaderNodeAsync(context, dataLoaderNode));
                currentNodes.Add(dataLoaderNode);
            }
        }

        private static IObjectGraphType[] GetGraphTypes(ExecutionNode executionNode, ExecutionContext context)
        {
            IGraphType? graphType = null;
            switch (executionNode)
            {
                case ValueExecutionNode:
                    return Array.Empty<IObjectGraphType>();
                case ObjectExecutionNode objectNode:
                    graphType = objectNode.GraphType;
                    break;
                case ArrayExecutionNode arrayNode:
                    {
                        graphType = ((ListGraphType)arrayNode.GraphType).ResolvedType;
                        break;
                    }
            }

            if (graphType is NonNullGraphType nonNullGraphType)
                graphType = nonNullGraphType.ResolvedType;

            return graphType switch
            {
                IInterfaceGraphType interfaceGraphType => interfaceGraphType.PossibleTypes.ToArray(),
                IObjectGraphType objectGraphType => new[] { objectGraphType },
                _ => Array.Empty<IObjectGraphType>()
            };
        }

        private bool HasChildOfGraphType(ExecutionNode executionNode, ExecutionContext context, ISet<string> searchGraphTypes)
        {
            var graphTypes = GetGraphTypes(executionNode, context);
            if (graphTypes.Length == 0)
                return false;

            return graphTypes
                .Any(graphType => CollectFieldsFrom(context, graphType, executionNode.Field?.SelectionSet, null)
                    .Select(x => new
                    {
                        Field = x.Value,
                        FieldDefinition = GetFieldDefinition(context.Schema, graphType, x.Value)
                    })
                    .Select(x => BuildExecutionNode(executionNode, x.FieldDefinition.ResolvedType, x.Field, x.FieldDefinition))
                    .Any(x => GetGraphTypes(x, context).Any(t => searchGraphTypes.Contains(t.Name)) || HasChildOfGraphType(x, context, searchGraphTypes))
                );
        }
    }
}