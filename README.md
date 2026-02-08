# FinOps - Azure Budget & Cost Management

A Blazor Server application for managing Azure FinOps across multiple tenants. Built with .NET 10 and MudBlazor, it provides a unified dashboard for budget management, resource tagging, and policy assignments across Azure subscriptions -- including those accessible via Azure Lighthouse.

## Features

- **Multi-tenant support** -- connect to additional Azure AD tenants at runtime alongside your home/Lighthouse subscriptions
- **Budget management** -- view, create, and delete Azure Consumption budgets across multiple subscriptions in one operation
- **Resource tagging** -- apply or remove tags on resource groups and their child resources with real-time progress reporting
- **Policy management** -- assign and remove Azure's built-in tag inheritance policy on subscriptions, with automatic managed identity and Contributor role setup
- **Subscription search** -- filter subscriptions by name, ID, or tenant across all connected tenants

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az`) installed and on your PATH
- An Azure account with access to at least one subscription
- Logged in via Azure CLI:

```bash
az login
```

## Getting Started

```bash
# Clone the repository
git clone <repository-url>
cd finops

# Build
dotnet build

# Run
dotnet run
```

The app starts at `https://localhost:<port>` (the port is shown in the terminal output).

If the process is locked from a previous run:

```bash
# Windows
taskkill /F /IM FinOps.exe
```

## Authentication

The app uses **`AzureCliCredential`** exclusively. It picks up whatever identity you authenticated with via `az login`. `DefaultAzureCredential` is intentionally not used as it causes timeouts on some Windows configurations.

When connecting additional tenants at runtime, a separate `AzureCliCredential` is created with the target tenant ID, so your `az login` session must have access to that tenant.

## Architecture

### Tech Stack

| Component | Technology |
|---|---|
| Framework | .NET 10 / Blazor Server |
| UI library | [MudBlazor](https://mudblazor.com/) 8.x |
| Azure SDK | `Azure.ResourceManager` (ARM) |
| Auth | `Azure.Identity` (`AzureCliCredential`) |
| Render mode | Global Interactive Server (SignalR) |

### Service Architecture

All services are registered as **scoped** (per-circuit in Blazor Server), meaning each user session gets its own instance:

```
Program.cs
  +-- TenantClientManager (scoped)    -- manages ArmClient instances per tenant
  +-- AzureSubscriptionService        -- enumerates subscriptions across tenants
  +-- BudgetService                   -- budget CRUD via Azure Consumption API
  +-- ResourceTaggingService          -- tag apply/remove on resource groups & resources
  +-- PolicyService                   -- tag inheritance policy assignment/removal
```

**`TenantClientManager`** is the core service. It holds:
- A default `ArmClient` for the home tenant (created once at session start)
- A `ConcurrentDictionary<string, ArmClient>` of additional tenant clients that users connect at runtime

All other services receive `TenantClientManager` via constructor injection and call `GetClientForTenant(tenantId)` to obtain the correct `ArmClient` for each operation.

### Models

| Model | Purpose |
|---|---|
| `TenantSubscription` | DTO combining tenant ID, subscription ID, display name, and state. Decouples UI from Azure SDK types. |
| `BudgetInfo` | Read model for an existing Azure Consumption budget (name, amount, spend, dates). |
| `BudgetFormModel` | Form binding model for budget creation (name, amount, time grain, start/end dates). |
| `BudgetCreationResult` | Per-subscription result of a budget creation operation (success/failure + error). |
| `ResourceGroupInfo` | Resource group with its name, location, provisioning state, and existing tags. |
| `TagEntry` / `TagFormModel` | Form binding models for key/value tag pairs in the Apply Tags dialog. |
| `TagOperationResult` | Per-resource result of a tag apply/remove operation. |
| `PolicyAssignmentInfo` | Read model for a tag inheritance policy assignment. |
| `PolicyOperationResult` | Result of a policy assign/remove operation. |

## Pages & UI

### Home (`/`)

Simple landing page with a link to the Budgets page.

### Budgets (`/budgets`)

The main budget management page. Workflow:

1. **Tenant bar** -- shows connected tenants as chips. Click "Connect Tenant" to add a new Azure AD tenant by GUID. Click the X on a chip to disconnect.
2. **Subscription table** -- lists all subscriptions across connected tenants with multi-select. Supports search filtering by name/ID/tenant.
3. **Create Budget** -- select one or more subscriptions, click "Create Budget", fill in the dialog (name, amount, time grain, dates), and the budget is created on all selected subscriptions. Results are shown per-subscription.
4. **Existing Budgets** -- when subscriptions are selected, their existing budgets are loaded into a table showing amount, current spend (with colour-coded percentage), time grain, and dates. Budgets can be deleted individually.

### Tagging (`/tagging`)

Resource tagging page. Workflow:

1. **Select a subscription** from the table (single-select).
2. **Select resource groups** -- multi-select table showing name, location, state, and existing tags (with tooltips).
3. **Apply Tags** -- opens a dialog where you enter key/value pairs. Tags are merged (existing tags are not removed). Progress is reported per-resource in real time.

### Policy (`/policy`)

Policy management page with two tabs:

**Tab 1: Tag Inheritance Policy**
- Enter a tag name (and optional value) to create a tag inheritance policy assignment on the selected subscription.
- Uses Azure's built-in policy definition (`b27a0cbd-a167-4dfa-ae64-4337be671140`) which inherits tags from the subscription to child resources.
- Automatically creates a system-assigned managed identity and grants it the Contributor role so the policy can remediate resources.
- Existing policy assignments are listed in a table with delete support.

**Tab 2: Remove Tags**
- Select resource groups, then pick which tag keys to remove from a chip selector (auto-populated from existing tags across the selection).
- Tags are removed from both the resource group and all resources within it. Progress is streamed in real time.

## Project Structure

```
finops/
+-- Program.cs                                  # DI registration and middleware
+-- FinOps.csproj                               # Project file (.NET 10)
+-- Components/
|   +-- App.razor                               # HTML shell, global render mode, scripts
|   +-- Routes.razor                            # Router component
|   +-- _Imports.razor                          # Global Razor usings
|   +-- Layout/
|   |   +-- MainLayout.razor                    # MudBlazor layout (app bar, drawer, providers)
|   |   +-- NavMenu.razor                       # Side navigation links
|   |   +-- ReconnectModal.razor                # SignalR reconnection UI
|   +-- Pages/
|   |   +-- Home.razor                          # Landing page
|   |   +-- Budgets.razor                       # Budget management
|   |   +-- Tagging.razor                       # Resource tagging
|   |   +-- Policy.razor                        # Policy management
|   |   +-- Error.razor                         # Error page
|   |   +-- NotFound.razor                      # 404 page
|   +-- Dialogs/
|       +-- ConnectTenantDialog.razor           # Tenant GUID input + validation
|       +-- CreateBudgetDialog.razor            # Budget creation form
|       +-- AddTagsDialog.razor                 # Tag key/value entry + validation
+-- Services/
|   +-- TenantClientManager.cs                  # Per-session ArmClient management
|   +-- IAzureSubscriptionService.cs            # Subscription enumeration interface
|   +-- AzureSubscriptionService.cs             # Subscription enumeration implementation
|   +-- IBudgetService.cs                       # Budget CRUD interface
|   +-- BudgetService.cs                        # Budget CRUD implementation
|   +-- IResourceTaggingService.cs              # Resource tagging interface
|   +-- ResourceTaggingService.cs               # Resource tagging implementation
|   +-- IPolicyService.cs                       # Policy management interface
|   +-- PolicyService.cs                        # Policy management implementation
+-- Models/
    +-- TenantSubscription.cs                   # Tenant + subscription DTO
    +-- BudgetInfo.cs                           # Budget read model
    +-- BudgetFormModel.cs                      # Budget form binding model
    +-- BudgetCreationResult.cs                 # Budget creation result DTO
    +-- ResourceGroupInfo.cs                    # Resource group with tags
    +-- TagEntry.cs / TagFormModel.cs           # Tag form binding models
    +-- TagOperationResult.cs                   # Tag operation result DTO
    +-- PolicyAssignmentInfo.cs                 # Policy assignment read model
    +-- PolicyOperationResult.cs                # Policy operation result DTO
```

## Azure SDK Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Azure.Identity` | 1.17.1 | Authentication via `AzureCliCredential` |
| `Azure.ResourceManager` | 1.13.2 | Core ARM client, subscriptions, resource groups, tags |
| `Azure.ResourceManager.Authorization` | 1.1.6 | Role assignments for policy managed identities |
| `Azure.ResourceManager.Consumption` | 1.0.1 | Budget CRUD operations |

## Key Design Decisions

### Multi-tenant approach (Lighthouse + Connect)

The app supports two ways to access subscriptions across tenants:

1. **Azure Lighthouse** -- subscriptions delegated to your home tenant appear automatically via the default `ArmClient`. No additional configuration needed.
2. **Connect Tenant** -- users can connect to additional tenants at runtime by entering a tenant GUID. This creates a separate `ArmClient` with a tenant-specific `AzureCliCredential`.

If a subscription appears in both (e.g., via Lighthouse and a direct connection), the explicitly connected tenant client is preferred for write operations.

### Scoped services (per-circuit)

All services are registered as `Scoped` in Blazor Server, which means each SignalR circuit (browser tab) gets its own instance. This ensures that tenant connections in one tab don't affect another.

### Real-time progress

Tag operations (apply/remove) iterate over every resource in selected resource groups. The `onProgress` callback uses `InvokeAsync` + `StateHasChanged` to stream results to the UI row-by-row as each resource is processed, rather than waiting for all operations to complete.

### Policy managed identity

When assigning a tag inheritance policy, the service automatically:
1. Creates the policy assignment with a system-assigned managed identity
2. Grants that identity the Contributor role on the subscription

This is required because the Modify effect used by the tag inheritance policy needs write access to apply tags during remediation.

## Troubleshooting

| Issue | Solution |
|---|---|
| `az login` required | Run `az login` before starting the app. The app uses `AzureCliCredential` and has no other auth fallback. |
| App hangs on startup | Ensure you're not using `DefaultAzureCredential`. The app is configured to use `AzureCliCredential` only. |
| Process locked | Kill the previous instance: `taskkill /F /IM FinOps.exe` (Windows) |
| Dialogs/snackbars not working | Verify `App.razor` has `@rendermode="InteractiveServer"` on both `<Routes>` and `<HeadOutlet>`, and that `blazor.web.js` loads before `MudBlazor.min.js`. |
| Tag operation returns 409 | The resource has a background operation in progress. The app catches this and reports it as a skipped resource. Retry later. |
| Policy assignment fails | Ensure your account has `Microsoft.Authorization/policyAssignments/write` permission on the target subscription. |
| Connected tenant shows no subscriptions | Your `az login` session must have access to the target tenant. Try `az login --tenant <tenant-id>` first. |

## License

This project is for internal use.
