# Starting the solution

##

This example needs a backing model. Sugested: Azure foundry based model. Endpoint uri and key are needed in main/program.cs.

``` csharp
 string endpoint =
    Environment.GetEnvironmentVariable("talks-autonomous-agents-foundry-uri")
    ?? throw new InvalidOperationException("Missing Azure OpenAI endpoint.");

string apiKey =
    Environment.GetEnvironmentVariable("talks-autonomous-agents-foundry-key")
    ?? throw new InvalidOperationException("Missing Azure OpenAI key.");
```

Model can be set in the Agents.cs file, recommended is gpt-4o.

## Running the projects from cmd

`dotnet run --project .\src\main\main.csproj`

`dotnet run --project .\src\tool1\tool1.csproj`

`dotnet run --project .\src\tool2\tool2.csproj`

`dotnet run --project .\src\tool3\tool3.csproj`

## Running from VS

Open the solution, make sure all projects are set to startup and set main to Startup Project.

### Demo rest requests

Use the requests in demo.rest to test the system.

### Example result


Invoking tool 'generate_report' via HTTP callback to 'http://localhost:5001/task', payload: Sanitized and analyzed logs for the system. Observations include:
Date: 2025-11-12
Severity counts: INFO: 11, WARN: 6, ERROR: 6, DEBUG: 5
Outstanding exceptions detected:
- [ERROR] RequestId=9f52aa component=Billing description="Charge failed for user due to timeout"
- [ERROR] TraceId=fe12be userName="[REDACTED]" message="Unhandled exception from task processing"
- [ERROR] tenantId=alpha reason="Exceeded limit for user backup"
- [ERROR] userHandle="[REDACTED]" failure="Unable to contact upstream"
- [ERROR] pipeline stage=extract operator="[REDACTED]" details="Timeout while pulling dataset"
- [ERROR] microservice=catalog payload="[REDACTED] missing SKU" retry=false.
Recommendation for resolution and further investigation is required for these issues and anomalies.
Agent: ### Project Intelligence Brief Summary
**Date:** 2025-11-12
**Log Analysis Results:**

| Date         | Severity | Count | Outstanding Exceptions                                                                                  |
| ------------ | -------- | ----- | ------------------------------------------------------------------------------------------------------- |
| 2025-11-12   | ERROR    | 6     | - [ERROR] RequestId=9f52aa component=Billing description="Charge failed for user due to timeout"<br>- [ERROR] TraceId=fe12be userName="[REDACTED]" message="Unhandled exception from task processing"<br>- [ERROR] tenantId=alpha reason="Exceeded limit for user backup"<br>- [ERROR] userHandle="[REDACTED]" failure="Unable to contact upstream"<br>- [ERROR] pipeline stage=extract operator="[REDACTED]" details="Timeout while pulling dataset"<br>- [ERROR] microservice=catalog payload="[REDACTED] missing SKU" retry=false. |

---

Sanitized sensitive identifiers are ensured in the report. Key observation includes an actionable list of exceptions that require immediate resolution and further investigation.

**Recommendations:**
- Review and mitigate billing and timeout errors from components.
- Address upstream communication failures to enhance system stability.
- Restructure extract operation reliability within ETL pipelines.

---

This intelligence summary is ready for your perusal.
