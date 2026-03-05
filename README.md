
# Notes

This app is just a playground for me to try out things, yes the users.db file is checked in to github. This is an experimental app, please treat it as so.

# FinOps - Azure Cost Management & Governance Platform

A comprehensive Blazor Server application for managing Azure FinOps, budgets, cost analysis, security, and governance across multiple tenants. Built with .NET 10 and MudBlazor, it provides a unified dashboard for complete Azure financial and operational management across subscriptions accessible directly or via Azure Lighthouse.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Authentication](#authentication)
  - [Application Login](#application-login)
  - [Azure Authentication](#azure-authentication)
  - [Multi-Tenant Authentication](#multi-tenant-authentication)
- [Complete Feature Guide](#complete-feature-guide)
  - [Home Dashboard](#1-home-dashboard)
  - [Budget Management](#2-budget-management)
  - [Cost Management](#3-cost-management)
  - [Resource Tagging](#4-resource-tagging)
  - [Policy Management](#5-policy-management)
  - [Orphaned Resources](#6-orphaned-resources)
  - [Azure Advisor](#7-azure-advisor)
  - [Security Recommendations](#8-security-recommendations)
  - [Service Retirements](#9-service-retirements)
  - [Logging Usage](#10-logging-usage)
  - [Rightsizing](#11-rightsizing)
  - [Maturity Dashboard](#12-maturity-dashboard)
  - [Carbon Optimisation](#13-carbon-optimisation)
  - [Private Endpoint Recommendations](#14-private-endpoint-recommendations)
  - [Theme Management](#15-theme-management)
  - [Admin Panel](#16-admin-panel)
  - [Cost Questions](#17-cost-questions)
  - [Upsell Opportunities](#18-upsell-opportunities)
- [Multi-Tenant Support](#multi-tenant-support)
- [Feature Flags](#feature-flags)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Troubleshooting](#troubleshooting)
- [Rate Limiting & Best Practices](#rate-limiting--best-practices)

---

## Prerequisites

### Required Software

- **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)** - Latest .NET 10 runtime and SDK
- **[Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli)** - Must be installed and available in your PATH
- **Azure Account** - Access to at least one Azure subscription with appropriate permissions

### Required Azure Permissions

To use all features, your account needs the following permissions on target subscriptions:

| Feature | Required Permission |
|---------|-------------------|
| View subscriptions | `Reader` |
| Budget management | `Microsoft.Consumption/budgets/*` |
| Cost analysis | `Microsoft.CostManagement/query/action` |
| Resource tagging | `Microsoft.Resources/tags/write` |
| Policy assignment | `Microsoft.Authorization/policyAssignments/write` |
| Advisor recommendations | `Microsoft.Advisor/recommendations/read` |
| Security Center | `Microsoft.Security/assessments/read` |
| Resource Graph queries | `Microsoft.ResourceGraph/resources/read` |

### Initial Setup

1. **Login to Azure CLI:**

   ```bash
   az login
   ```

   This command will open your browser for authentication. The app exclusively uses `AzureCliCredential`, so you must be logged in via `az` before running.

2. **(Optional) Login to specific tenant:**

   If you need to access multiple tenants, login with tenant ID:

   ```bash
   az login --tenant <tenant-id>
   ```

---

## Getting Started

### Clone and Build

```bash
# Clone the repository
git clone <repository-url>
cd finops

# Build the project
dotnet build

# Run the application
dotnet run
```

The application will start and display output like:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

**Access the app:** Open your browser and navigate to `https://localhost:5001` (or the port shown in your terminal). You will be redirected to the login page.

### Stopping the Application

- **In terminal:** Press `Ctrl+C`
- **If process is locked:**

  ```bash
  # Windows
  taskkill /F /IM FinOps.exe

  # Linux/Mac
  pkill -f FinOps
  ```

---

## Authentication

The application has two separate authentication layers: application login (who can use the app) and Azure authentication (what Azure resources the app can access).

### Application Login

The app uses ASP.NET Core Identity backed by a local SQLite database (`users.db`) to control who can access it. All pages require a logged-in account.

**Logging in:**
- Navigate to `https://localhost:5001` — you will be redirected to `/Account/Login`
- Enter your email and password
- On success you are redirected to the home dashboard
- Sessions expire after **8 hours of inactivity**

**Account requirements:**
- Password must be at least **12 characters** and contain at least one digit and one special character
- Accounts are locked for **15 minutes** after 5 consecutive failed login attempts

**User provisioning:**
- New accounts are created through `/Account/Register`
- Contact your administrator to have an account created

### Azure Authentication

Once logged into the app, all Azure API calls use **`AzureCliCredential` exclusively**. This means:

- You must run `az login` in a terminal before starting the app
- The app inherits your Azure CLI identity and permissions
- No service principals, certificates, or additional configuration needed

`DefaultAzureCredential` is intentionally not used — it causes timeouts on Windows due to credential chain probing.

### Multi-Tenant Authentication

When connecting to additional tenants at runtime:
1. Your `az login` session must have access to that tenant
2. The app creates a tenant-specific `AzureCliCredential` instance
3. If you cannot see subscriptions from a connected tenant, run: `az login --tenant <tenant-id>`

---

## Complete Feature Guide

### 1. Home Dashboard

**Location:** `/`

**Purpose:** Landing page with an overview of all available features and quick navigation links.

**Features:**
- Feature cards for all modules with descriptions
- Navigation links to each feature
- Tech stack display
- Dark mode toggle in header

---

### 2. Budget Management

The application provides two types of budget management:

#### 2.1 Subscription Budgets

**Location:** `/budgets/subscriptions`

**Purpose:** Create and manage Azure Consumption budgets at the subscription level.

**Step-by-Step Workflow:**

1. **Connect Tenants (Optional)**
   - Click "Connect Tenant" in the tenant bar
   - Enter the target Azure AD Tenant GUID
   - Click "Connect"
   - Subscriptions from the new tenant will appear in the table

2. **Select Target Subscriptions**
   - Browse the subscription table showing:
     - Tenant name and ID
     - Subscription name (clickable links to Azure Portal)
     - Subscription ID
     - State (Enabled/Disabled/Warning)
   - Use the search box to filter by name, ID, or tenant
   - Select one or multiple subscriptions using checkboxes

3. **Create Budget**
   - Click "Create Budget" button in the toolbar
   - Fill in the budget creation dialog:
     - **Budget Name:** Unique identifier (e.g., "Monthly-Production-Budget")
     - **Amount:** Budget limit in USD (e.g., 5000.00)
     - **Time Grain:** Choose from:
       - `Monthly` - Resets on the 1st of each month
       - `Quarterly` - Resets every 3 months
       - `Annually` - Resets on January 1st
     - **Start Date:** When budget tracking begins (defaults to today)
     - **End Date:** When budget expires (defaults to 1 year from start)
     - **Email Alerts (optional):** Add email addresses to receive alerts at configured thresholds
   - Click "Create"
   - Progress dialog shows results per subscription:
     - ✅ Green checkmark = Budget created successfully
     - ❌ Red X = Failed

4. **View Existing Budgets**
   - After selecting subscriptions, existing budgets load automatically in the bottom table
   - Table shows:
     - **Budget Name:** Name of the budget
     - **Amount:** Total budget allocation
     - **Current Spend:** Actual spend so far
     - **Percentage:** Color-coded progress bar:
       - 🟢 Green (0-75%) - Under budget
       - 🟡 Yellow (75-90%) - Approaching limit
       - 🔴 Red (90%+) - Over or near budget
     - **Time Grain:** Monthly/Quarterly/Annually
     - **Period:** Start and end dates
     - **Actions:** Edit and delete buttons

5. **Edit Budget**
   - Click the edit icon on any budget row
   - Modify amount, end date, or email alerts
   - Click "Update" to save changes

6. **Delete Budget**
   - Click the delete icon on any budget row
   - Budget is immediately removed from Azure

**Use Cases:**
- Set monthly spending limits for development subscriptions
- Create quarterly budgets for production environments
- Track annual cloud costs across departments
- Bulk-create budgets on multiple subscriptions simultaneously

---

#### 2.2 Resource Group Budgets

**Location:** `/budgets/resource-groups`

**Purpose:** Create and manage budgets at the resource group level for granular cost control.

**Step-by-Step Workflow:**

1. **Select a Subscription**
   - Choose ONE subscription from the table
   - Only single subscription selection supported (resource groups are subscription-specific)

2. **Select Resource Groups**
   - After selecting a subscription, resource groups load automatically
   - Table displays:
     - Resource group name
     - Location (Azure region)
     - Provisioning state
     - Existing tags (with tooltip showing all tags)
   - Select one or multiple resource groups

3. **Create Resource Group Budget**
   - Click "Create Budget" button
   - Fill in the same budget form as subscription budgets
   - Budget is created for each selected resource group
   - Results shown per resource group

4. **View and Manage**
   - View existing resource group budgets
   - Edit or delete budgets individually
   - Monitor spend at resource group level

**Use Cases:**
- Set budgets for specific project resource groups
- Isolate costs for different teams or applications
- Track spending for ephemeral environments (dev/test)
- Create cost guardrails for individual workloads

---

### 3. Cost Management

The application provides four comprehensive cost management views:

#### 3.1 Overview Dashboard

**Location:** `/dashboard/overview`

**Purpose:** Executive-level dashboard showing aggregated cost metrics, trends, and alerts across selected subscriptions.

**Features:**

1. **KPI Cards (Top Row)**
   - **Current Month Spend:** Total spend so far this month
   - **Month Forecast:** Predicted end-of-month total
   - **Budget Status:** Remaining budget and percentage
   - **Potential Savings:** Total savings from Advisor recommendations

2. **Per-Subscription Cards**
   - Visual cards for each subscription showing:
     - Subscription name and colored indicator
     - Current month spend (large display)
     - Month-to-date change (vs. previous period)
     - Budget information and progress bar
     - Cost trend chart (embedded mini-chart)
     - Alerts for budget overruns
   - Color coding:
     - 🟢 Green: Under 75% of budget
     - 🟡 Yellow: 75-90% of budget
     - 🔴 Red: Over 90% of budget

3. **Cost Breakdown Charts**
   - Top services by cost
   - Top resource groups by cost
   - Top Azure regions by cost

**Use Cases:**
- Daily financial standup dashboard
- Executive reporting
- Budget variance analysis
- Multi-subscription cost comparison

---

#### 3.2 Cost Overview with Interactive Charts

**Location:** `/costs/overview`

**Purpose:** Detailed cost analysis with interactive charts showing trends, forecasts, and dimensional breakdowns.

**Features:**

1. **Subscription Selection Table**
   - Lists all subscriptions across connected tenants
   - Multi-select enabled
   - Search/filter by any column
   - Cost data only loads after you select subscriptions (to avoid API rate limiting)

2. **Interactive Charts** (Powered by ApexCharts)

   **Chart 1: Daily Cost Trend with Forecast**
   - Blue area: Historical costs (last 30 days)
   - Orange line: Forecasted costs (rest of month)
   - Hover for exact daily cost, zoom and pan enabled

   **Chart 2: Cost Breakdown by Service**
   - Donut chart showing top 8 Azure services by cost
   - Click legend to toggle services

   **Chart 3: Cost Breakdown by Resource Group**
   - Horizontal bar chart showing top 10 resource groups by cost

   **Chart 4: Multi-Subscription Comparison** (2+ subscriptions)
   - Stacked bar chart showing daily costs per subscription

**Use Cases:**
- Identify cost trends and anomalies
- Forecast end-of-month spend
- Identify top cost drivers (services, resource groups)
- Compare costs across multiple subscriptions

**Rate Limiting Note:**
- Charts load data in batches with 800ms delays between batches
- If you see errors, wait 60 seconds and refresh

---

#### 3.3 Cost Dashboard

**Location:** `/costs/dashboard`

**Purpose:** Single-subscription detailed cost dashboard with comprehensive metrics.

**Features:**
- Current month spend, forecast, budget comparison
- Cost trends over time
- Service-level and resource group breakdowns
- Location-based costs

**Workflow:**
1. Select a single subscription
2. Wait for dashboard data to load
3. Review detailed cost metrics

---

#### 3.4 Costs by Tag

**Location:** `/tagging/costs`

**Purpose:** Analyse and break down Azure costs by tag key and value to understand spending by business dimension (cost center, team, environment, etc.).

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Choose one or more subscriptions to analyse

2. **Select a Tag Key**
   - After subscription selection, available tag keys are loaded
   - Pick the tag dimension to analyse (e.g., "CostCenter", "Environment")

3. **Analyse Costs**
   - Click "Analyse" to fetch cost data grouped by the selected tag value
   - Results show spend per tag value for the current month

4. **Review Breakdown**
   - Table and chart show cost contribution per tag value
   - Untagged resources appear as a separate "untagged" entry
   - Use results to identify which teams or environments are driving spend

**Use Cases:**
- Chargeback and showback reporting
- Identify which cost center is over budget
- Compare environment spend (Dev vs. Prod)
- Validate tagging coverage

---

### 4. Resource Tagging

**Location:** `/tagging`

**Purpose:** Apply or remove tags on Azure resource groups and their child resources with real-time progress reporting.

**Key Concepts:**

Tags are key-value pairs (e.g., `Environment: Production`, `CostCenter: IT`) used for cost allocation, governance, and resource organization.

**Step-by-Step Workflow:**

1. **Select a Subscription**
   - Choose ONE subscription from the table

2. **Select Resource Groups**
   - Table shows name, location, provisioning state, and existing tags
   - Select one or multiple resource groups

3. **Apply Tags**
   - Click "Apply Tags" button
   - Dialog opens with tag entry form
   - Add key/value pairs (duplicates and empty keys not allowed)
   - Click "Apply"

4. **Real-Time Progress**
   - Progress dialog shows per-resource results as they complete:
     - ✅ Green: Tag applied successfully
     - ⚠️ Yellow: Skipped (resource busy, 409 conflict)
     - ❌ Red: Failed
   - Tags are applied to the resource group **and all resources within it**
   - Existing tags are NOT removed — new tags are merged

5. **Remove Tags (Tab 2)**
   - Select resource groups to scan
   - Existing tags appear as selectable chips
   - Select tags to remove and click "Remove Tags"
   - Tags are removed from the resource group and all child resources

**Use Cases:**
- Apply cost center tags to all resources in a subscription
- Tag environments (Dev, Test, Prod) for filtering
- Add owner tags for accountability
- Bulk tagging for compliance

**Best Practices:**
- Use consistent naming conventions (e.g., PascalCase)
- Apply tags at resource group level first, then use inheritance policies
- Common tags: Environment, CostCenter, Owner, Project, Application

---

### 5. Policy Management

**Location:** `/policy`

**Purpose:** Assign Azure Policy definitions to enforce governance rules, particularly tag inheritance policies. The page has two tabs.

#### 5.1 Tab 1: Tag Inheritance Policy

**Purpose:** Automatically inherit tags from subscriptions to child resources using Azure Policy.

Azure's built-in policy `b27a0cbd-a167-4dfa-ae64-4337be671140` ("Inherit a tag from the subscription if missing") ensures resources automatically get tagged with subscription-level tags. This policy:
- Only adds tags if the resource doesn't already have that tag
- Uses the "Modify" effect with automatic remediation
- Requires a managed identity with Contributor role

**Workflow:**
1. Select ONE subscription
2. Enter the tag name to inherit (and optionally a specific value)
3. Click "Assign Policy" — the service automatically:
   - Creates the policy assignment with a system-assigned managed identity
   - Grants the managed identity **Contributor** role on the subscription
   - Enables automatic remediation
4. View and delete existing policy assignments in the table below

**Use Cases:**
- Ensure all resources inherit "Environment" tag from subscription
- Enforce cost center tagging across all resources
- Prevent missing tags on new resources

#### 5.2 Tab 2: Remove Tags

**Purpose:** Bulk remove specific tags from resource groups and their child resources.

**Workflow:**
1. Select resource groups
2. Select tag chips to remove
3. Confirm removal and monitor real-time progress

---

### 6. Orphaned Resources

**Location:** `/orphaned-resources`

**Purpose:** Identify Azure resources that are no longer in use but still incurring costs.

**Common Orphaned Resource Types:**

| Resource Type | Why It's Orphaned |
|--------------|------------------|
| **Unattached Disks** | VM deleted but disk remains |
| **Unused NICs** | Network interface not attached to VM |
| **Orphaned Public IPs** | IP address not associated with any resource |
| **Empty Resource Groups** | No resources inside |
| **Stopped/Deallocated VMs** | VM powered off for extended period |

**Workflow:**
1. Select one or more subscriptions
2. Click "Scan"
3. Review results table showing resource name, type, resource group, location, reason, estimated monthly cost, and a link to Azure Portal
4. Navigate to Azure Portal links to review and delete confirmed orphans

**Scan Logic (Resource Graph queries):**
- Disks: `properties.diskState == 'Unattached'`
- NICs: `properties.virtualMachine == null`
- Public IPs: `properties.ipConfiguration == null`
- Empty Resource Groups: `resourceCount == 0`
- Deallocated VMs: `powerState == 'VM deallocated'`

**Use Cases:**
- Monthly cost optimization reviews
- Post-project cleanup
- Reduce cloud waste

**Best Practices:**
- Review before deleting — confirm the resource is truly unused
- Delete disks carefully (check for backups/snapshots first)
- Typical savings: 5-15% of total subscription costs

---

### 7. Azure Advisor

**Location:** `/advisor`

**Purpose:** View Azure Advisor recommendations across Cost, Performance, Security, Reliability, and Operational Excellence categories.

**Workflow:**
1. Select one or more subscriptions
2. Click "Get Recommendations"
3. Review **Advisor Scores** (out of 100) per category:
   - 🟢 80-100: Good
   - 🟡 60-79: Needs Attention
   - 🔴 0-59: Critical
4. Review the recommendations table showing category, impact (High/Medium/Low), title, impacted resource, and potential savings
5. Filter by category and sort by column headers
6. Follow the "Action" guidance for each recommendation

**Categories:**
- 💰 **Cost:** Right-size VMs, delete unused resources, reserve instances
- ⚡ **Performance:** Upgrade storage tiers, scale resources
- 🛡️ **Security:** Enable MFA, restrict network access, enable encryption
- 🔄 **Reliability:** Use availability zones, enable backup, geo-redundancy
- ⚙️ **Operational Excellence:** Tagging, naming conventions, diagnostics

**Use Cases:**
- Monthly optimization reviews
- Security posture assessment
- Cost reduction initiatives

---

### 8. Security Recommendations

**Location:** `/security-recommendations`

**Purpose:** View Azure Security Center (Defender for Cloud) security recommendations and compliance posture.

**Workflow:**
1. Select one or more subscriptions
2. Click "Scan"
3. Review security score and breakdown by category (Identity, Networking, Compute, Data)
4. Review the recommendations table:
   - **Severity:** Critical / High / Medium / Low
   - **Title, Category, Affected Resources, Description, Remediation Steps**
5. Filter by severity chip to focus on critical items
6. Click "View in Portal" links to remediate

**Common Critical/High Recommendations:**
- Enable MFA for subscription owners
- Apply system updates to VMs
- Restrict management ports (RDP/SSH)
- Enable disk encryption
- Enable network security groups

**Compliance Frameworks Covered:**
- CIS Microsoft Azure Foundations Benchmark
- PCI-DSS v3.2.1
- ISO 27001:2013
- NIST SP 800-53 R4
- Azure Security Benchmark

---

### 9. Service Retirements

**Location:** `/service-retirements`

**Purpose:** Identify Azure services and features scheduled for retirement that are currently in use in your subscriptions.

**Workflow:**
1. Select one or more subscriptions
2. Click "Scan"
3. Review results grouped by retirement timeline:
   - 🔴 Retiring within 30 days
   - 🟡 Retiring within 90 days
   - 🟢 Retiring within 1 year
4. Each row shows: service name, retirement date, description, affected resources, and migration path
5. Click the expand icon to see a full list of affected resources with Portal links

**Use Cases:**
- Quarterly retirement reviews
- Avoid surprise downtime
- Technical debt reduction

**Best Practices:**
- Create migration plans immediately for <90 day retirements
- Test migrations in dev/test environments first
- Subscribe to Azure Service Health alerts

---

### 10. Logging Usage

**Location:** `/logging-usage`

**Purpose:** Analyse Log Analytics workspace usage and costs to optimise logging spend.

**Why Monitor Logging Usage?**

Log Analytics costs are based on data ingestion (GB) and retention:
- First 5 GB/month free
- Pay-as-you-go: ~$2.76/GB ingested
- High-volume applications can generate 100s of GB/day

**Workflow:**
1. Select one or more subscriptions
2. Click "Get Workspaces" to enumerate all workspaces
3. Review the workspace list (name, resource group, location, SKU, retention days)
4. Select one or more workspaces and click "Query Usage"
5. Review per-workspace metrics:
   - Daily ingestion trend (30-day chart)
   - Top data tables consuming most ingestion
   - Estimated monthly cost
   - Optimisation recommendations

**Common Optimisation Strategies:**
- Reduce retention from 90 to 30 days (free tier) if compliance allows
- Filter verbose/debug logs at source via data collection rules
- Switch to Capacity Reservation pricing at >100 GB/day (saves ~25-30%)
- Disable diagnostics for non-critical resources

---

### 11. Rightsizing

**Location:** `/rightsizing`

**Purpose:** Identify Virtual Machines and compute resources that are oversized or underutilised, with recommendations to right-size or shut down to reduce costs.

**Workflow:**
1. Select one or more subscriptions
2. Click "Scan for Recommendations"
3. Review the recommendations table:
   - **Resource Name and Type**
   - **Current SKU/Size**
   - **Recommended SKU/Size**
   - **Estimated Monthly Savings**
   - **Impact:** High/Medium/Low
   - **Justification:** Why the recommendation was made

4. Click resource names to open in Azure Portal and action the recommendation

**Data Source:** Azure Advisor Cost recommendations filtered for rightsizing and shutdown suggestions.

**Use Cases:**
- Monthly cost optimisation reviews
- Post-deployment right-sizing
- Identify idle or consistently underutilised VMs

**Best Practices:**
- Check utilisation metrics before acting (CPU, memory, disk)
- Right-size in dev/test first
- Schedule resizing during maintenance windows
- Consider Reserved Instances after right-sizing

> **Note:** This feature is controlled by a feature flag and may need to be enabled in the Admin panel.

---

### 12. Maturity Dashboard

**Location:** `/maturity`

**Purpose:** Assess the cloud governance maturity of your Azure subscriptions across multiple dimensions, producing a scored maturity report.

**Maturity Dimensions Assessed:**
- **Tagging** — Percentage of resources with required tags
- **Budgets** — Whether budgets are configured
- **Cost Waste** — Orphaned/idle resources present
- **Reservations** — Use of reserved instances for predictable workloads

**Workflow:**
1. Select one or more subscriptions
2. Click "Score Selected"
3. Progress indicator shows scoring in real time per subscription
4. Review the maturity scorecard:
   - Overall score per subscription
   - Breakdown by category (colour-coded)
   - Specific findings that reduce the score
5. Use findings as a prioritised action list

**Use Cases:**
- FinOps capability assessment
- Identifying governance gaps across a subscription estate
- Reporting to leadership on cloud governance maturity
- Tracking improvements over time

> **Note:** This feature is controlled by a feature flag and may need to be enabled in the Admin panel.

---

### 13. Carbon Optimisation

**Location:** `/carbon`

**Purpose:** Estimate the carbon footprint of your Azure Virtual Machine estate by region, helping identify opportunities to reduce environmental impact by migrating to lower-carbon regions or right-sizing VMs.

**How Carbon Estimates Work:**
- Carbon intensity (gCO₂/kWh) varies by Azure region based on the energy mix used
- Estimates are based on VM CPU core count and regional intensity data
- All figures are estimates — Azure does not publish per-VM carbon data

**Workflow:**
1. Select one or more subscriptions
2. Click "Scan for Carbon Data"
3. Review results:
   - **Per-VM estimates:** VM name, region, CPU cores, estimated kg CO₂/month
   - **Region summary:** Total VMs, total cores, total CO₂ estimate, intensity per core
   - **Carbon intensity map:** Relative intensity by region (green = low carbon, red = high carbon)
4. Identify high-carbon regions and consider migration to greener alternatives

**Use Cases:**
- Environmental impact reporting
- ESG/sustainability metrics
- Identifying regions with high carbon intensity for migration planning
- Comparing carbon cost of scaling up vs. right-sizing

> **Note:** This feature is controlled by a feature flag and may need to be enabled in the Admin panel.

---

### 14. Private Endpoint Recommendations

**Location:** `/private-endpoints` (via navigation)

**Purpose:** Identify Azure services that are currently exposed via public endpoints and would benefit from Private Endpoint configuration to improve security posture and potentially reduce egress costs.

**Workflow:**
1. Select one or more subscriptions
2. Click "Scan for Recommendations"
3. Review the recommendations table:
   - **Resource Name and Type** (e.g., Storage Account, Key Vault, SQL Server)
   - **Service Type**
   - **Access Pattern**
   - **Benefit:** Why a private endpoint is recommended
   - **Portal Link:** Direct link to configure in Azure Portal

**Use Cases:**
- Security hardening (remove public exposure)
- Compliance requirements (e.g., PCI-DSS, HIPAA)
- Reduce data egress costs for high-traffic services
- Network architecture reviews

> **Note:** This feature is controlled by a feature flag and may need to be enabled in the Admin panel.

---

### 15. Theme Management

**Location:** `/themes`

**Purpose:** Customise the application's visual appearance by selecting from three built-in themes.

**Available Themes:**

| Theme | Description |
|-------|-------------|
| **Azure Blue** | Clean Microsoft Azure-inspired colour palette with blue accents |
| **MissionControl** | High-contrast dark theme suited for NOC/operations dashboards |
| **DarkFinance** | Dark mode with finance-inspired green accents |

**Workflow:**
1. Navigate to Themes via the side navigation
2. Click a theme card to apply it immediately
3. The theme is persisted across sessions (saved to `theme.json`)
4. The dark mode toggle in the top bar continues to work within any theme

> **Note:** This feature is controlled by a feature flag and may need to be enabled in the Admin panel.

---

### 16. Admin Panel

**Location:** `/admin`

**Purpose:** Control which features are visible in the navigation and throughout the application using feature flags.

**Available Feature Flags:**

| Flag | Controls |
|------|---------|
| **Budgets** | Subscription and resource group budget pages |
| **Cost Management** | Cost Overview, Cost Dashboard, Overview Dashboard |
| **Tagging** | Resource tagging and Costs by Tag pages |
| **Policy** | Policy management page |
| **Orphaned Resources** | Orphaned resource scanner |
| **Advisor** | Azure Advisor recommendations |
| **Security** | Security recommendations page |
| **Private Endpoints** | Private endpoint recommendations |
| **Service Retirements** | Service retirement scanner |
| **Logging Usage** | Log Analytics usage analysis |
| **Rightsizing** | VM rightsizing recommendations |
| **Maturity** | Maturity dashboard |
| **Carbon** | Carbon optimisation page |
| **Themes** | Theme selection page |
| **Cost Questions** | AI-powered FinOps chat (GitHub Copilot) |
| **Upsell** | Upsell opportunities scanner |

**Workflow:**
1. Navigate to Admin in the side navigation
2. Toggle feature flags on or off using the switches
3. Click "Save" to persist changes
4. Changes take effect immediately for all connected users — the navigation updates in real time

> **Access:** The Admin panel requires an admin-role account. Contact your administrator if you need access.

---

### 17. Cost Questions

**Location:** `/cost-questions`

**Purpose:** An AI-powered chat interface that lets you ask natural-language questions about Azure costs and FinOps best practices, powered by GitHub Copilot.

**Features:**
- Conversational chat UI with message history
- Real-time streaming responses from GitHub Copilot
- Persistent session for follow-up questions within a browser session
- Enter to send, Shift+Enter for multi-line input

**Workflow:**
1. Navigate to Cost Questions in the side navigation
2. Wait for the Copilot session to initialise (spinner shown)
3. Type a question in the input box and press Enter or click Send
4. Copilot streams a response in real time
5. Continue the conversation with follow-up questions

**Example questions:**
- "What are the top strategies for reducing Azure VM costs?"
- "How do I set up Azure budgets with alerts?"
- "What is a FinOps maturity model?"
- "How does Azure Reserved Instances pricing compare to pay-as-you-go?"

> **Note:** This feature is controlled by a feature flag and requires a valid GitHub Copilot token. Enable it from the Admin panel. If the session fails to initialise, an error message is shown — check that Copilot credentials are available.

---

### 18. Upsell Opportunities

**Location:** `/upsell`

**Purpose:** Scan your Azure subscriptions for actionable improvement opportunities — cost savings, security enhancements, reliability improvements, and performance optimisations — presented in a prioritised, filterable table.

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Connect tenants (optional) using the tenant bar
   - Select one or more subscriptions to scan

2. **Scan**
   - Click "Scan" to analyse the selected subscriptions
   - A progress bar is shown while scanning across all Azure services

3. **Review Opportunities**
   - Results appear in a table with the following columns:
     - **Impact:** High / Medium / Low (colour-coded)
     - **Category:** Cost, Security, Reliability, Performance, etc.
     - **Opportunity:** Title, business value, and technical detail
     - **Resource:** Affected resource with Azure Portal link
     - **Subscription:** Link to subscription in Portal
     - **Source:** Data source (e.g., Advisor, Security Center)
     - **Est. Savings/mo:** Estimated monthly saving where applicable

4. **Filter Results**
   - Click Impact or Category chips in the summary bar to filter
   - Use the search box to search by keyword
   - Click "Clear Filters" to reset

**Use Cases:**
- Consolidated view of all optimisation opportunities in one place
- Prioritise work by impact and potential savings
- MSP/partner reporting on customer subscription improvements
- Pre-sales upsell identification for Azure services

> **Note:** This feature is controlled by a feature flag and may need to be enabled in the Admin panel.

---

## Multi-Tenant Support

The application supports accessing Azure resources across multiple tenants using two methods:

### Method 1: Azure Lighthouse

**What is Lighthouse?**
- Microsoft's multi-tenant management solution
- Allows MSPs to manage customer subscriptions from their own tenant
- Subscriptions delegated to your home tenant appear automatically

**How It Works in FinOps:**
1. On app startup, your home tenant is detected automatically
2. All Lighthouse-delegated subscriptions load in subscription tables
3. No configuration needed — works out of the box

### Method 2: Connect Tenant (Runtime Connection)

**Purpose:** Access subscriptions in tenants where you have guest access.

**How to Connect:**

1. **Get Tenant ID:**
   - Login to Azure Portal → Azure Active Directory → copy the "Tenant ID"

2. **Connect in FinOps:**
   - Click "Connect Tenant" button (appears on most pages)
   - Enter tenant GUID
   - Click "Connect"
   - If your `az login` session doesn't yet have credentials for this tenant, a browser window opens automatically for you to complete login

3. **Verify Access:**
   - Subscriptions from connected tenant appear in tables
   - If no subscriptions appear: `az login --tenant <tenant-id>`

4. **Disconnect:**
   - Click the X on the tenant chip in the tenant bar

**Tenant Bar (Top of Most Pages):**
- 🟦 Blue chip: Home tenant (default)
- 🟪 Purple chips: Connected tenants
- Shows tenant display name if available, otherwise tenant GUID

**Persistence:**
- Tenant connections are **per-session** (scoped to your browser tab)
- Connections are lost when you close the browser tab
- Reconnect each session as needed

**Permissions Required:**
- Guest user account in target tenant
- Appropriate RBAC roles on subscriptions in that tenant
- `az login --tenant <tenant-id>` must succeed

---

## Feature Flags

Feature flags control which pages and navigation items are visible. They are managed by an admin in the [Admin Panel](#16-admin-panel) and stored in `featureflags.json`.

**Behaviour:**
- Disabled features are hidden from the navigation menu
- All flags default to **enabled** on first run
- Changes apply immediately to all connected users
- Flag state persists across application restarts

If a page appears missing from the navigation, check the Admin panel to ensure its flag is enabled.

---

## Architecture

### Technology Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Framework** | .NET | 10.0 |
| **App Model** | Blazor Server | .NET 10 |
| **UI Library** | MudBlazor | 8.15.0 |
| **Charts** | Blazor-ApexCharts | 4.0.0 |
| **Azure SDK** | Azure.ResourceManager | 1.13.2 |
| **Authentication (Azure)** | Azure.Identity | 1.17.1 |
| **Authentication (App)** | ASP.NET Core Identity | .NET 10 |
| **Database** | SQLite via EF Core | 9.0.0 |
| **Cost Management** | Azure.ResourceManager.CostManagement | 1.0.2 |
| **Budgets** | Azure.ResourceManager.Consumption | 1.0.1 |
| **Advisor** | Azure.ResourceManager.Advisor | 1.0.0-beta.5 |
| **Log Analytics** | Azure.ResourceManager.OperationalInsights | 1.3.1 |
| **Resource Graph** | Azure.ResourceManager.ResourceGraph | 1.1.0 |
| **Monitor Query** | Azure.Monitor.Query | 1.5.0 |

### Service Architecture

All services are registered as **Scoped** in the dependency injection container, meaning each Blazor SignalR circuit (user session) gets its own service instances. `IFeatureFlagService` and `IThemeService` are **Singleton** (shared across all sessions).

```
TenantClientManager (scoped)
├── Manages ArmClient instances per tenant
├── Home tenant client (created once at startup)
└── Connected tenant clients (created on-demand)

ITenantConnectionService (scoped)
└── Handles az login fallback for new tenant connections

IAzureSubscriptionService (scoped)
├── Enumerates subscriptions across all tenants
└── Filters by state, tenant, name

IBudgetService (scoped)
├── Create/edit/delete budgets
├── Get budgets for subscriptions/resource groups
└── Uses Azure Consumption API

ICostAnalysisService (scoped)
├── Cost queries (historical data)
├── Cost forecasts
├── Dimensional breakdowns (service, resource group, location, tag)
└── Exponential backoff retry for 429 rate limiting

IResourceTaggingService (scoped)
├── Apply tags to resource groups and resources
├── Remove tags with progress callbacks
└── Real-time operation results via Action<TagOperationResult>

IPolicyService (scoped)
├── Assign tag inheritance policies
├── Create managed identities
├── Grant RBAC roles
└── List and delete policy assignments

IAdvisorService (scoped)
├── Get Advisor recommendations
├── Calculate Advisor scores per category
└── Potential cost savings calculation

IOrphanedResourceService (scoped)
├── Identify unattached disks, NICs, IPs
├── Find empty resource groups
├── Locate deallocated VMs
└── Uses Azure Resource Graph queries

ISecurityRecommendationService (scoped)
├── Get Security Center recommendations
└── Calculate secure scores

IServiceRetirementService (scoped)
├── Query Azure Service Health / Advisor for retirement notices
└── Match against deployed resources

ILogAnalyticsService (scoped)
├── Enumerate Log Analytics workspaces
├── Query usage metrics via Azure Monitor Query
└── Calculate costs and ingestion trends

IRightsizingService (scoped)
└── Advisor-based VM rightsizing and shutdown recommendations

IMaturityService (scoped)
└── Multi-dimension maturity scoring with progress callbacks

ICarbonService (scoped)
├── VM carbon estimates by region
└── Regional carbon intensity data

IPrivateEndpointService (scoped)
└── Identify services that should use private endpoints

ICopilotService (scoped)
├── Create Copilot chat sessions
└── Stream responses via GitHub Copilot API

IUpsellService (scoped)
└── Scan subscriptions for upsell/optimisation opportunities

IFeatureFlagService (singleton)
├── Feature flag state (which pages are enabled)
├── Persists to featureflags.json
└── OnFlagsChanged event for real-time nav updates

IThemeService (singleton)
├── Theme selection (AzureBlue, MissionControl, DarkFinance)
├── Persists to theme.json
└── OnThemeChanged event for real-time theme updates
```

### TenantClientManager (Core Service)

**Purpose:** Centralized management of `ArmClient` instances for multi-tenant scenarios.

```csharp
public class TenantClientManager
{
    private ArmClient _homeClient;
    private ConcurrentDictionary<string, ArmClient> _tenantClients;

    public ArmClient GetClientForTenant(string tenantId)
    {
        // If home tenant, return home client
        // If connected tenant, return from cache or create new
        // New clients use AzureCliCredential with TenantId option
    }
}
```

### Render Mode: Interactive Server

The application uses **global Interactive Server** render mode:

```razor
<Routes @rendermode="InteractiveServer" />
<HeadOutlet @rendermode="InteractiveServer" />
```

This is required for MudBlazor dialogs, snackbars, and real-time progress updates to function. The global mode is set in `App.razor` — removing it silently breaks all dialogs and snackbars.

---

## Project Structure

```
finops/
│
├── Program.cs                          # DI registration, middleware pipeline
├── FinOps.csproj                       # Project file, package references
├── appsettings.json                    # Logging, connection strings, AllowedHosts
├── .claude/
│   └── CLAUDE.md                       # Developer notes and conventions
│
├── Components/
│   ├── App.razor                       # HTML shell, global render mode, script order
│   ├── Routes.razor                    # Router configuration
│   ├── _Imports.razor                  # Global Razor usings
│   │
│   ├── Layout/
│   │   ├── MainLayout.razor            # MudBlazor layout, app bar, drawer, providers
│   │   ├── NavMenu.razor               # Side navigation (feature-flag controlled)
│   │   └── ReconnectModal.razor        # SignalR reconnection UI
│   │
│   ├── Pages/
│   │   ├── Home.razor                  # Landing page / feature overview
│   │   ├── Budgets.razor               # Subscription budget management
│   │   ├── ResourceGroupBudgets.razor  # Resource group budget management
│   │   ├── OverviewDashboard.razor     # Executive cost dashboard
│   │   ├── CostOverview.razor          # Cost analysis with ApexCharts
│   │   ├── CostDashboard.razor         # Single subscription cost dashboard
│   │   ├── CostsByTagging.razor        # Cost breakdown by tag
│   │   ├── Tagging.razor               # Resource tagging (apply + remove tabs)
│   │   ├── Policy.razor                # Policy management (2 tabs)
│   │   ├── Advisor.razor               # Azure Advisor recommendations
│   │   ├── OrphanedResources.razor     # Orphaned resource scanner
│   │   ├── SecurityRecommendations.razor # Security Center recommendations
│   │   ├── ServiceRetirements.razor    # Service retirement scanner
│   │   ├── LoggingUsage.razor          # Log Analytics usage analysis
│   │   ├── Rightsizing.razor           # VM rightsizing recommendations
│   │   ├── MaturityDashboard.razor     # Subscription maturity scoring
│   │   ├── CarbonOptimisation.razor    # Carbon footprint estimation
│   │   ├── PrivateEndpoints.razor      # Private endpoint recommendations
│   │   ├── Themes.razor                # Theme selection
│   │   ├── CostQuestions.razor         # AI-powered FinOps chat (GitHub Copilot)
│   │   ├── Upsell.razor                # Upsell / optimisation opportunities
│   │   ├── Admin.razor                 # Feature flag management
│   │   ├── Error.razor                 # Error boundary page
│   │   └── NotFound.razor              # 404 page
│   │
│   └── Dialogs/
│       ├── ConnectTenantDialog.razor   # Tenant GUID input + GUID validation
│       ├── CreateBudgetDialog.razor    # Budget creation/edit form
│       ├── AddTagsDialog.razor         # Tag key/value entry form
│       └── RetirementDetailsDialog.razor # Service retirement details + resources
│
├── Pages/
│   └── Account/
│       ├── Login.cshtml / .cs          # Application login page
│       ├── Register.cshtml / .cs       # New user registration
│       └── Logout.cshtml / .cs         # POST logout with antiforgery
│
├── Data/
│   ├── AppDbContext.cs                 # EF Core DbContext (Identity tables)
│   └── ApplicationUser.cs             # Extended IdentityUser
│
├── Services/
│   ├── TenantClientManager.cs          # Core multi-tenant ArmClient manager
│   ├── ITenantConnectionService.cs     # Tenant connection interface
│   ├── TenantConnectionService.cs      # az login fallback connection
│   ├── IAzureSubscriptionService.cs    # Subscription enumeration interface
│   ├── AzureSubscriptionService.cs     # Subscription enumeration (all tenants)
│   ├── IBudgetService.cs               # Budget CRUD interface
│   ├── BudgetService.cs                # Budget CRUD (Consumption API)
│   ├── ICostAnalysisService.cs         # Cost analysis interface
│   ├── CostAnalysisService.cs          # Cost queries + retry logic
│   ├── IResourceTaggingService.cs      # Tagging interface
│   ├── ResourceTaggingService.cs       # Tagging with progress callbacks
│   ├── IPolicyService.cs               # Policy management interface
│   ├── PolicyService.cs                # Policy assignment + managed identities
│   ├── IAdvisorService.cs              # Advisor interface
│   ├── AdvisorService.cs               # Advisor recommendations + scores
│   ├── IOrphanedResourceService.cs     # Orphaned resource scanner interface
│   ├── OrphanedResourceService.cs      # Resource Graph queries for orphans
│   ├── ISecurityRecommendationService.cs
│   ├── SecurityRecommendationService.cs # Defender for Cloud integration
│   ├── IServiceRetirementService.cs
│   ├── ServiceRetirementService.cs     # Service Health + retirement matching
│   ├── ILogAnalyticsService.cs
│   ├── LogAnalyticsService.cs          # Workspace usage via Monitor Query API
│   ├── IRightsizingService.cs
│   ├── RightsizingService.cs           # Advisor-based rightsizing
│   ├── IMaturityService.cs
│   ├── MaturityService.cs              # Maturity scoring with progress callbacks
│   ├── ICarbonService.cs
│   ├── CarbonService.cs                # Carbon estimates by region
│   ├── IPrivateEndpointService.cs
│   ├── PrivateEndpointService.cs       # Private endpoint recommendations
│   ├── ICopilotService.cs
│   ├── CopilotService.cs               # GitHub Copilot chat session management
│   ├── ICopilotChatSession.cs
│   ├── CopilotChatSession.cs           # Per-conversation Copilot session
│   ├── IUpsellService.cs
│   ├── UpsellService.cs                # Upsell/optimisation opportunity scanner
│   ├── IFeatureFlagService.cs
│   ├── FeatureFlagService.cs           # Feature flag state + persistence
│   ├── IThemeService.cs
│   └── ThemeService.cs                 # Theme selection + persistence
│
└── Models/
    ├── TenantSubscription.cs           # Tenant + subscription DTO
    ├── BudgetInfo.cs                   # Budget read model
    ├── BudgetFormModel.cs              # Budget form binding + validation
    ├── BudgetAlertThreshold.cs         # Alert threshold config
    ├── BudgetCreationResult.cs         # Budget operation result
    ├── ResourceGroupInfo.cs            # Resource group with tags
    ├── TagEntry.cs                     # Tag key/value pair
    ├── TagFormModel.cs                 # Tag form binding
    ├── TagOperationResult.cs           # Per-resource tag result
    ├── PolicyAssignmentInfo.cs         # Policy assignment read model
    ├── PolicyOperationResult.cs        # Policy operation result
    ├── CostDataPoint.cs                # Historical cost data point
    ├── ForecastDataPoint.cs            # Forecast data point
    ├── CostBreakdownItem.cs            # Dimensional cost breakdown
    ├── CostBreakdownBySubscriptionItem.cs
    ├── CostQueryResult.cs              # Cost query result
    ├── ForecastResult.cs               # Forecast result
    ├── CostDashboardData.cs            # Aggregated dashboard data
    ├── AdvisorRecommendation.cs        # Advisor recommendation
    ├── AdvisorScore.cs                 # Advisor score per category
    ├── OrphanedResourceInfo.cs         # Orphaned resource details
    ├── SecurityRecommendation.cs       # Security recommendation
    ├── ServiceRetirement.cs            # Retirement notice
    ├── RetirementResource.cs           # Affected resource within a retirement
    ├── RightsizingRecommendation.cs    # Rightsizing recommendation
    ├── SubscriptionMaturityScore.cs    # Maturity score per subscription
    ├── CarbonEstimate.cs               # Per-VM carbon estimate
    ├── RegionCarbonSummary.cs          # Region-level carbon summary
    ├── PrivateEndpointRecommendation.cs
    ├── WorkspaceInfo.cs                # Log Analytics workspace
    ├── WorkspaceUsageData.cs           # Workspace usage metrics
    ├── UpsellOpportunity.cs            # Upsell/optimisation opportunity
    └── FeatureFlags.cs                 # Feature flag state model
```

---

## Troubleshooting

### Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| **Redirected to login on start** | No account yet created | Register an account at `/Account/Register` |
| **Login fails** | Incorrect credentials | Check email/password; account may be locked after 5 failures (wait 15 min) |
| **App won't start** | Not logged in to Azure CLI | Run `az login` before starting the app |
| **App hangs on startup** | Wrong credential type | Verify `AzureCliCredential` is used, not `DefaultAzureCredential` |
| **Process locked** | Previous instance still running | Run: `taskkill /F /IM FinOps.exe` (Windows) |
| **Dialogs don't open** | Missing InteractiveServer render mode | Check `App.razor` has `@rendermode="InteractiveServer"` on `<Routes>` and `<HeadOutlet>` |
| **Snackbars not showing** | Missing provider in layout | Verify `MainLayout.razor` has `<MudSnackbarProvider />` |
| **Charts not loading** | Missing ApexCharts scripts | Verify `App.razor` loads `blazor.web.js` before `MudBlazor.min.js` |
| **Feature missing from nav** | Feature flag disabled | Check Admin panel and enable the relevant feature flag |
| **No subscriptions shown** | No access in current tenant | Verify `az account list` shows subscriptions |
| **Connected tenant shows nothing** | Not logged into that tenant | Run: `az login --tenant <tenant-id>` |
| **Tag operation returns 409** | Resource locked by another operation | Wait 5 minutes and retry |
| **Policy assignment fails** | Missing permissions | Ensure you have `Microsoft.Authorization/policyAssignments/write` |
| **Budget creation fails** | Missing Consumption permissions | Ensure you have `Microsoft.Consumption/budgets/write` |
| **Cost queries fail with 429** | Too many API requests | Wait 60 seconds; the app retries automatically |
| **Charts show "No data"** | No cost data in selected period | Verify subscription has costs for the selected period |
| **SignalR disconnected** | Network issue or server restart | Wait for reconnection modal, or refresh page |

### Debugging Tips

**Enable detailed logging** — add to `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "FinOps.Services": "Debug",
      "Azure": "Warning"
    }
  }
}
```

**Check Azure CLI status:**
```bash
# Verify logged in
az account show

# List all subscriptions
az account list --output table

# Check tenant access
az account list --query "[?tenantId=='<tenant-id>']" --output table
```

**Common error messages:**

| Error Message | Meaning | Fix |
|--------------|---------|-----|
| `AzureCliCredential authentication failed` | Not logged into Azure CLI | Run `az login` |
| `429 Too Many Requests` | Rate limit exceeded | Wait 60 seconds |
| `403 Forbidden` | Insufficient permissions | Grant appropriate RBAC role |
| `404 Not Found` | Resource or subscription doesn't exist | Verify resource ID |
| `409 Conflict` | Resource is locked | Wait for conflicting operation to complete |
| `The circuit failed to initialize` | Blazor SignalR connection issue | Refresh page, check network connectivity |

---

## Rate Limiting & Best Practices

### Azure API Rate Limits

| API | Limit | Scope |
|-----|-------|-------|
| **ARM Reads** | 12,000 requests/hour | Per subscription |
| **ARM Writes** | 1,200 requests/hour | Per subscription |
| **Cost Management** | 30 requests/minute | Per subscription |
| **Resource Graph** | 15 requests/second | Per tenant |
| **Advisor** | 100 requests/minute | Per subscription |

### Rate Limiting Strategies in FinOps

**Exponential Backoff with Retry** — all cost queries retry automatically on 429:
```
Attempt 1 → 1 second delay → Attempt 2 → 2 second delay → Attempt 3
```

**Batched API Calls** — Cost Overview loads data in batches with 800ms delays:
- Batch 1: Historical costs → 800ms → Batch 2: Forecasts → 800ms → Batch 3: Breakdowns

**On-Demand Loading** — data only fetches when you select subscriptions or click scan/load buttons.

### Best Practices

1. Analyse 1-5 subscriptions at a time rather than selecting all at once
2. Wait for one scan to complete before starting another
3. If you see `429 Too Many Requests`, wait 60 seconds — do not manually retry
4. Run intensive scans (Security, Orphaned Resources, Maturity) during off-peak hours
5. Close browser tabs when not in use to free server memory

### Performance Reference

| Operation | Typical Time |
|-----------|-------------|
| View subscription list | < 1 second |
| Create a single budget | < 2 seconds |
| Connect a tenant | < 1 second |
| Cost charts (1-3 subscriptions) | 3-8 seconds |
| Advisor recommendations | 10-20 seconds |
| Security scan | 20-40 seconds |
| Maturity scoring (5 subscriptions) | 30-60 seconds |

---

## FAQ

**Q: Can I use this without Azure CLI?**
A: No. The app uses `AzureCliCredential` exclusively. Run `az login` before starting the app.

**Q: How do I create the first user account?**
A: Register at `/Account/Register` before the first login. Subsequent accounts can also be created there, or provisioned by an administrator directly in the database.

**Q: Does this work with Azure Government or Azure China?**
A: Not currently, but it could be extended by configuring `ArmEnvironment` in `ArmClient` initialization.

**Q: Can I export cost data to Excel/CSV?**
A: Not currently implemented, but could be added using MudBlazor table export features.

**Q: How do I add more subscriptions?**
A: Lighthouse-delegated subscriptions appear automatically. For other tenants, use "Connect Tenant".

**Q: Is there a dark mode?**
A: Yes — toggle with the moon icon in the top bar. The MissionControl and DarkFinance themes also provide full dark experiences.

**Q: Can multiple users use this simultaneously?**
A: Yes. Each user has their own session with independent tenant connections, subscription selections, and Azure credentials.

**Q: A feature is missing from the navigation — where is it?**
A: Check the Admin panel (`/admin`). The feature may be disabled via a feature flag.

**Q: How much does running this app cost?**
A: The app itself is free (.NET hosting). Azure API calls for read operations are free. You only pay for Azure resources you create (budgets, policy assignments, etc.).

---

## License

This project is for internal use. See LICENSE file for details.

---

## Support

For issues, questions, or feature requests:
- Check the [Troubleshooting](#troubleshooting) section first
- Review existing GitHub issues
- Create a new issue with a detailed description and the error message from the browser console or server logs

---

*Built with .NET 10, Blazor Server, and MudBlazor*
