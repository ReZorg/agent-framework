// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.Workflows;

namespace Microsoft.Agents.AI.DurableTask.Workflows;

/// <summary>
/// Live status payload written to the orchestration via <c>SetCustomStatus</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the only orchestration state readable by external clients while the workflow
/// is still running. It is written after each superstep so that
/// <see cref="DurableStreamingWorkflowRun"/> can poll for new events.
/// On completion the framework clears it, so events are also
/// embedded in the output via <see cref="DurableWorkflowResult"/>.
/// </para>
/// <para>
/// When the workflow is paused at a <see cref="RequestPort"/>, <see cref="PendingEvent"/>
/// contains the request data needed by external actors to provide a response.
/// </para>
/// </remarks>
internal sealed class DurableWorkflowLiveStatus
{
    /// <summary>
    /// Gets or sets the pending request port state, or <see langword="null"/> if the workflow is not waiting for input.
    /// </summary>
    public PendingExternalEventStatus? PendingEvent { get; set; }

    /// <summary>
    /// Gets or sets the serialized workflow events emitted so far.
    /// </summary>
    public List<string> Events { get; set; } = [];
}
