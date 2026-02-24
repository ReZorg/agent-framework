// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.Agents.AI.DurableTask.Workflows;

/// <summary>
/// Describes a request port that the workflow is currently paused at, waiting for external input.
/// </summary>
/// <param name="EventName">The event name to raise when responding (matches the RequestPort ID).</param>
/// <param name="Input">The serialized request data passed to the RequestPort.</param>
internal sealed record PendingExternalEventStatus(
    string EventName,
    string Input);
