// Copyright (c) Microsoft. All rights reserved.

// This sample demonstrates a Human-in-the-Loop (HITL) workflow hosted in Azure Functions.
// Workflow: CreateApprovalRequest -> ManagerApproval (RequestPort/HITL pause) -> ExpenseReimburse
//
// The workflow pauses at a RequestPort and waits for an external approval response via HTTP.
// Two custom HTTP endpoints allow clients to check pending approvals and send responses.

using System.Net;
using System.Text.Json;
using Microsoft.Agents.AI.Hosting.AzureFunctions;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Hosting;
using WorkflowHITLFunctions;

// Define executors and a RequestPort for the HITL pause point
CreateApprovalRequest createRequest = new();
RequestPort<ApprovalRequest, ApprovalResponse> managerApproval = RequestPort.Create<ApprovalRequest, ApprovalResponse>("ManagerApproval");
ExpenseReimburse reimburse = new();

// Build the workflow: CreateApprovalRequest -> ManagerApproval (HITL) -> ExpenseReimburse
Workflow expenseApproval = new WorkflowBuilder(createRequest)
    .WithName("ExpenseReimbursement")
    .WithDescription("Expense reimbursement with manager approval")
    .AddEdge(createRequest, managerApproval)
    .AddEdge(managerApproval, reimburse)
    .Build();

using IHost app = FunctionsApplication
    .CreateBuilder(args)
    .ConfigureFunctionsWebApplication()
    .ConfigureDurableWorkflows(workflows => workflows.AddWorkflow(expenseApproval))
    .Build();
app.Run();

/// <summary>
/// HTTP endpoints for checking workflow status and sending HITL responses.
/// </summary>
internal static class WorkflowHITLEndpoints
{
    /// <summary>
    /// Returns the workflow status. When the workflow is paused at a RequestPort,
    /// the response includes the pending approval details (event name, input, types).
    /// </summary>
    [Function("workflows-status")]
    public static async Task<HttpResponseData> GetStatusAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "workflows/status/{runId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string runId)
    {
        OrchestrationMetadata? metadata = await client.GetInstanceAsync(runId, getInputsAndOutputs: true);
        if (metadata is null)
        {
            HttpResponseData notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "Workflow run not found.", runId });
            return notFound;
        }

        // The orchestration sets a "PendingEvent" in custom status when waiting for HITL input
        object? pendingApproval = null;
        if (metadata.SerializedCustomStatus is not null)
        {
            using JsonDocument statusDoc = JsonDocument.Parse(metadata.SerializedCustomStatus);
            if (statusDoc.RootElement.TryGetProperty("PendingEvent", out JsonElement pending)
                && pending.ValueKind != JsonValueKind.Null)
            {
                pendingApproval = new
                {
                    eventName = pending.GetProperty("EventName").GetString(),
                    input = pending.GetProperty("Input").GetString(),
                };
            }
        }

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            runId,
            status = metadata.RuntimeStatus.ToString(),
            pendingApproval,
            output = metadata.RuntimeStatus == OrchestrationRuntimeStatus.Completed ? metadata.SerializedOutput : null,
        });
        return response;
    }

    /// <summary>
    /// Sends a response to a pending RequestPort, resuming the workflow.
    /// Body: <c>{ "eventName": "ManagerApproval", "response": { "Approved": true, "Comments": "..." } }</c>
    /// </summary>
    [Function("workflows-respond")]
    public static async Task<HttpResponseData> RespondAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "workflows/respond/{runId}")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string runId)
    {
        string? body = await req.ReadAsStringAsync();
        if (string.IsNullOrEmpty(body))
        {
            HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Request body is required." });
            return badRequest;
        }

        // Expects { "eventName": "...", "response": { ... } }
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("eventName", out JsonElement eventNameEl)
            || !root.TryGetProperty("response", out JsonElement responseEl))
        {
            HttpResponseData badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "Body must contain 'eventName' and 'response' properties." });
            return badRequest;
        }

        string eventName = eventNameEl.GetString()!;

        // Raise the external event to unblock the orchestration's WaitForExternalEvent call
        await client.RaiseEventAsync(runId, eventName, responseEl.GetRawText());

        HttpResponseData response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { message = "Response sent to workflow.", runId, eventName });
        return response;
    }
}
