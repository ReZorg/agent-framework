# Workflow Human-in-the-Loop (HITL) Sample

This sample demonstrates a **Human-in-the-Loop** pattern in durable workflows using `RequestPort`. The workflow pauses execution to wait for external input (e.g., manager approval) and resumes when the response is provided.

## Key Concepts Demonstrated

- Using `RequestPort` to define external input points in a workflow
- Streaming workflow events with `IStreamingWorkflowRun`
- Handling `DurableRequestInfoEvent` to detect HITL pauses
- Using `SendResponseAsync` to provide responses and resume the workflow
- **Durability**: The workflow survives process restarts while waiting for human input

## Workflow

```
CreateApprovalRequest -> ManagerApproval (RequestPort) -> ExpenseReimburse
```

| Step | Description |
|------|-------------|
| CreateApprovalRequest | Retrieves expense details and creates an approval request |
| ManagerApproval (RequestPort) | **PAUSES** the workflow and waits for external approval |
| ExpenseReimburse | Processes the reimbursement based on approval response |

## How It Works

A `RequestPort` defines a typed external input point in the workflow:

```csharp
RequestPort<ApprovalRequest, ApprovalResponse> managerApproval =
    RequestPort.Create<ApprovalRequest, ApprovalResponse>("ManagerApproval");
```

Use `WatchStreamAsync` to observe events. When the workflow reaches a `RequestPort`, a `DurableRequestInfoEvent` is emitted. Call `SendResponseAsync` to provide the response and resume the workflow:

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    case DurableRequestInfoEvent requestEvent:
        ApprovalRequest? request = requestEvent.GetInputAs<ApprovalRequest>();
        await run.SendResponseAsync(requestEvent, new ApprovalResponse(Approved: true, Comments: "Approved."));
        break;
}
```

## Environment Setup

See the [README.md](../README.md) file in the parent directory for information on configuring the environment, including how to install and run the Durable Task Scheduler.

## Running the Sample

```bash
cd dotnet/samples/Durable/Workflow/ConsoleApps/08_WorkflowHITL
dotnet run --framework net10.0
```

### Sample Output

```text
Starting expense reimbursement workflow for expense: EXP-2025-001
Workflow started with instance ID: abc123...

Workflow paused at RequestPort: ManagerApproval
  Input: {"ExpenseId":"EXP-2025-001","Amount":1500.00,"EmployeeName":"Jerry"}
  Approval for: Jerry, Amount: $1,500.00
  Response sent: Approved=True

Workflow completed: Expense reimbursed at 2025-01-23T17:30:00.0000000Z
```
