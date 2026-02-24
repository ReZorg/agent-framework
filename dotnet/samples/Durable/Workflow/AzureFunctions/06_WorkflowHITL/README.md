# Human-in-the-Loop (HITL) Workflow — Azure Functions

This sample demonstrates a durable workflow with Human-in-the-Loop support hosted in Azure Functions. The workflow pauses at a `RequestPort` and waits for an external approval response sent via an HTTP endpoint.

## Key Concepts Demonstrated

- Using `RequestPort` for human-in-the-loop interaction in a durable workflow
- Custom HTTP endpoints for querying pending approvals and sending responses
- Pausing orchestrations via `WaitForExternalEvent` and resuming via `RaiseEventAsync`

## Workflow

`CreateApprovalRequest` → `ManagerApproval` (HITL pause) → `ExpenseReimburse`

## HTTP Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/workflows/ExpenseReimbursement/run` | Start the workflow |
| GET | `/api/workflows/status/{runId}` | Check status and pending approvals |
| POST | `/api/workflows/respond/{runId}` | Send approval response to resume |

## Environment Setup

See the [README.md](../../README.md) file in the parent directory for information on how to configure the environment, including how to install and run the Durable Task Scheduler.

## Running the Sample

Use the `demo.http` file or the steps below:

### Step 1: Start the Workflow

```bash
curl -X POST "http://localhost:7071/api/workflows/ExpenseReimbursement/run?runId=expense-001" \
    -H "Content-Type: text/plain" -d "EXP-2025-001"
```

### Step 2: Check Workflow Status

The workflow pauses at the `ManagerApproval` RequestPort:

```bash
curl http://localhost:7071/api/workflows/status/expense-001
```

```json
{
  "runId": "expense-001",
  "status": "Running",
  "pendingApproval": {
    "eventName": "ManagerApproval",
    "input": "{\"ExpenseId\":\"EXP-2025-001\",\"Amount\":1500.00,\"EmployeeName\":\"Jerry\"}"
  },
  "output": null
}
```

### Step 3: Send Approval Response

```bash
curl -X POST http://localhost:7071/api/workflows/respond/expense-001 \
    -H "Content-Type: application/json" \
    -d '{"eventName": "ManagerApproval", "response": {"Approved": true, "Comments": "Looks good!"}}'
```

### Step 4: Verify Completion

```bash
curl http://localhost:7071/api/workflows/status/expense-001
```

```json
{
  "runId": "expense-001",
  "status": "Completed",
  "pendingApproval": null,
  "output": "..."
}
```

### DTS Dashboard

If using the DTS emulator, the dashboard is available at `http://localhost:8082` to visualize the orchestration and inspect the external event interaction.
