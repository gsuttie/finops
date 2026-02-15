# FinOps - Azure Cost Management & Governance Platform

A comprehensive Blazor Server application for managing Azure FinOps, budgets, cost analysis, security, and governance across multiple tenants. Built with .NET 10 and MudBlazor, it provides a unified dashboard for complete Azure financial and operational management across subscriptions accessible directly or via Azure Lighthouse.

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Authentication](#authentication)
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
- [Multi-Tenant Support](#multi-tenant-support)
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

**Access the app:** Open your browser and navigate to `https://localhost:5001` (or the port shown in your terminal).

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

### Authentication Method

The application uses **`AzureCliCredential` exclusively** for authentication. This means:

✅ **What this means:**
- You must run `az login` before starting the app
- The app inherits your Azure CLI identity and permissions
- No additional configuration needed (no service principals, certificates, etc.)
- Works with both personal accounts and managed identities

❌ **Not used:**
- `DefaultAzureCredential` - Intentionally disabled as it causes timeouts on some Windows configurations
- Service Principal authentication
- Managed Identity (unless you're running the app on Azure and logged in via `az`)

### Multi-Tenant Authentication

When connecting to additional tenants at runtime:
1. Your `az login` session must have access to that tenant
2. The app creates a tenant-specific `AzureCliCredential` instance
3. If you can't see subscriptions from a connected tenant, run: `az login --tenant <tenant-id>`

---

## Complete Feature Guide

### 1. Home Dashboard

**Location:** `/` (root page)

**Purpose:** Landing page with quick navigation to main features.

**Features:**
- Welcome screen with application overview
- Quick links to Budget Management
- Application status indicators
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
   - Click "Create"
   - Progress dialog shows results per subscription:
     - ✅ Green checkmark = Budget created successfully
     - ❌ Red X = Failed (with error message)

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
     - **Actions:** Delete button

5. **Delete Budget**
   - Click the delete icon on any budget row
   - Budget is immediately removed from Azure
   - Success/error notification appears

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
   - Delete budgets individually
   - Monitor spend at resource group level

**Use Cases:**
- Set budgets for specific project resource groups
- Isolate costs for different teams or applications
- Track spending for ephemeral environments (dev/test)
- Create cost guardrails for individual workloads

---

### 3. Cost Management

The application provides three comprehensive cost management views:

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

**Workflow:**
1. Navigate to Overview Dashboard
2. Select one or more subscriptions from the table
3. Wait for dashboard to load (shows progress indicator)
4. View high-level metrics and per-subscription breakdowns
5. Click on subscription names to drill into Azure Portal

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
   - Shows: Tenant, Name, Subscription ID
   - Multi-select enabled
   - Search/filter by any column
   - **Important:** Cost data only loads AFTER you select subscriptions (to avoid API rate limiting)

2. **Cost Analysis Summary**
   - Displays after subscription selection
   - Shows total cost for last 30 days
   - Number of subscriptions selected

3. **Interactive Charts** (Powered by ApexCharts)

   **Chart 1: Daily Cost Trend with Forecast**
   - **Type:** Area chart with line overlay
   - **Data:**
     - Blue area: Historical costs (last 30 days)
     - Orange line: Forecasted costs (rest of month)
   - **Features:**
     - Hover to see exact daily cost
     - Zoom and pan enabled (toolbar)
     - Smooth curves for better visualization
     - Datetime X-axis with automatic formatting
     - Currency-formatted Y-axis ($X.XX)
   - **Location:** Right side, 2/3 width on desktop

   **Chart 2: Cost Breakdown by Service**
   - **Type:** Donut chart
   - **Data:** Top 8 Azure services by cost
   - **Features:**
     - Percentage labels on segments
     - Center label showing total cost
     - Legend at bottom
     - Click legend to toggle services
   - **Location:** Left side, 1/3 width on desktop

   **Chart 3: Cost Breakdown by Resource Group**
   - **Type:** Horizontal bar chart
   - **Data:** Top 10 resource groups by cost
   - **Features:**
     - Sorted by cost descending
     - Currency values displayed
     - Green color scheme
   - **Location:** Full width row below trend chart

   **Chart 4: Multi-Subscription Comparison** (Conditional)
   - **Type:** Stacked bar chart
   - **Data:** Daily costs per subscription, stacked
   - **Features:**
     - Different color per subscription
     - Legend to identify subscriptions
     - Shows total daily spend across all selected
   - **When shown:** Only appears if 2+ subscriptions selected
   - **Location:** Full width row at bottom

4. **Responsive Design**
   - Desktop: Charts arranged in optimal grid layout
   - Tablet: Charts stack vertically, full width
   - Mobile: Single column, scrollable

**Workflow:**
1. Navigate to Cost Overview
2. (Optional) Connect additional tenants
3. Select one or more subscriptions from table
4. Wait for loading indicator (chart data fetches in batches)
5. Interact with charts:
   - Hover for tooltips
   - Zoom/pan on trend chart
   - Click legend to filter
   - Resize window to test responsive layout

**Use Cases:**
- Identify cost trends and anomalies
- Forecast end-of-month spend
- Identify top cost drivers (services, resource groups)
- Compare costs across multiple subscriptions
- Generate reports for stakeholders

**Rate Limiting Note:**
- Charts load data in batches with 800ms delays
- Total load time for 3 subscriptions: ~3-4 seconds
- If you see "Too Many Requests" errors, wait 60 seconds and refresh

---

#### 3.3 Cost Dashboard

**Location:** `/costs/dashboard`

**Purpose:** Single-subscription detailed cost dashboard with comprehensive metrics.

**Features:**

1. **Single Subscription Selection**
   - Select ONE subscription to analyze
   - Full dashboard loads with detailed metrics

2. **Dashboard Panels:**
   - Current month spend
   - Forecasted month-end total
   - Budget comparison
   - Cost trends over time
   - Service-level breakdowns
   - Resource group costs
   - Location-based costs

3. **Placeholder for Charts** (Future Enhancement)
   - Message: "Charts will be added in a future update"

**Workflow:**
1. Navigate to Cost Dashboard
2. Select a single subscription
3. Wait for dashboard data to load
4. Review detailed cost metrics

**Use Cases:**
- Deep-dive analysis for a specific subscription
- Budget variance investigation
- Detailed cost allocation review

---

### 4. Resource Tagging

**Location:** `/tagging`

**Purpose:** Apply or remove tags on Azure resource groups and their child resources with real-time progress reporting.

**Key Concepts:**

**Tags in Azure:**
- Tags are key-value pairs (e.g., `Environment: Production`, `CostCenter: IT`)
- Used for cost allocation, governance, and resource organization
- Applied to resources and resource groups
- Maximum 50 tags per resource

**Step-by-Step Workflow:**

1. **Select a Subscription**
   - Choose ONE subscription from the table
   - Only single-select supported

2. **Select Resource Groups**
   - After subscription selection, resource groups table loads
   - Table shows:
     - Name
     - Location
     - Provisioning state
     - **Existing tags** (hover to see all tags in tooltip)
   - Select one or multiple resource groups

3. **Apply Tags**
   - Click "Apply Tags" button
   - Dialog opens with tag entry form
   - Add tags:
     - Click "Add Tag" button
     - Enter **Key** (e.g., "Environment")
     - Enter **Value** (e.g., "Production")
     - Repeat for multiple tags
   - Validation:
     - Keys and values cannot be empty
     - Duplicate keys not allowed
   - Click "Apply"

4. **Real-Time Progress**
   - Progress dialog opens showing:
     - Total resources being tagged
     - Progress bar
     - Per-resource results:
       - ✅ Green: Tag applied successfully
       - ⚠️ Yellow: Skipped (resource busy, 409 conflict)
       - ❌ Red: Failed with error message
   - Tags are applied to:
     - The resource group itself
     - **All resources within the resource group**
   - **Note:** Existing tags are NOT removed, new tags are merged

5. **Progress Streaming**
   - Results appear row-by-row as each resource is processed
   - No need to wait for all resources to complete
   - Can monitor progress in real-time

**Use Cases:**
- Apply cost center tags to all resources in a subscription
- Tag environments (Dev, Test, Prod) for filtering
- Add owner tags for accountability
- Compliance tagging (e.g., "DataClassification: Confidential")
- Bulk tagging for resource organization

**Best Practices:**
- Use consistent tag naming conventions (e.g., PascalCase)
- Create a tag taxonomy before bulk tagging
- Apply tags at resource group level first, then use inheritance policies
- Common tags: Environment, CostCenter, Owner, Project, Application

---

### 5. Policy Management

**Location:** `/policy`

**Purpose:** Assign Azure Policy definitions to enforce governance rules, particularly tag inheritance policies.

The page has **two tabs:**

---

#### 5.1 Tab 1: Tag Inheritance Policy

**Purpose:** Automatically inherit tags from subscriptions to child resources using Azure Policy.

**What is Tag Inheritance Policy?**

Azure's built-in policy `b27a0cbd-a167-4dfa-ae64-4337be671140` (called "Inherit a tag from the subscription if missing") ensures that resources automatically get tagged with subscription-level tags. This policy:
- Runs during resource creation and updates
- Only adds tags if the resource doesn't already have that tag
- Uses the "Modify" effect with automatic remediation
- Requires a managed identity with Contributor role

**Step-by-Step Workflow:**

1. **Select a Subscription**
   - Choose ONE subscription from the table
   - Policy assignments are subscription-scoped

2. **Configure Tag Inheritance**
   - Enter **Tag Name** to inherit (e.g., "Environment")
   - (Optional) Enter **Tag Value** (e.g., "Production")
     - If blank, policy inherits whatever value the subscription has

3. **Assign Policy**
   - Click "Assign Policy"
   - The service automatically:
     1. Creates policy assignment with a system-assigned managed identity
     2. Waits for managed identity to be created
     3. Grants the managed identity **Contributor** role on the subscription
     4. Enables automatic remediation
   - Success notification appears

4. **View Existing Assignments**
   - Table shows all tag inheritance policy assignments on selected subscription:
     - Assignment name
     - Tag name being inherited
     - Tag value (if specified)
     - Assignment ID
   - Click delete to remove policy assignment

**Important Notes:**
- **Managed Identity is Required:** The policy needs write access to apply tags
- **Contributor Role:** Automatically granted to policy's managed identity
- **Remediation:** Runs automatically on existing resources (not just new ones)
- **Multiple Tags:** Create separate policy assignments for each tag you want to inherit

**Use Cases:**
- Ensure all resources inherit "Environment" tag from subscription
- Enforce cost center tagging across all resources
- Automatic compliance tagging
- Prevent missing tags on new resources

---

#### 5.2 Tab 2: Remove Tags

**Purpose:** Bulk remove specific tags from resource groups and their child resources.

**Step-by-Step Workflow:**

1. **Select Resource Groups**
   - After selecting a subscription, choose resource groups to clean up

2. **View Available Tags**
   - After selecting resource groups, app scans all tags across selected groups
   - Tags appear as chips in the UI

3. **Select Tags to Remove**
   - Click on tag chips to select tags for removal
   - Multiple tags can be selected
   - Only tags that exist on selected resource groups are shown

4. **Remove Tags**
   - Click "Remove Tags" button
   - Confirmation dialog appears
   - Progress dialog shows real-time removal:
     - Resource groups processed
     - Resources within each group
     - Success/failure per resource
   - Tags are removed from:
     - Resource group itself
     - All resources within the resource group

**Use Cases:**
- Clean up obsolete tags after project completion
- Remove incorrect tags applied by mistake
- Standardize tag taxonomy (remove old naming conventions)
- Compliance cleanup (remove sensitive tags)

**Best Practices:**
- Review selected tags carefully before removing
- Consider backing up tag data before bulk removal
- Remove tags during maintenance windows (some resources may be locked)

---

### 6. Orphaned Resources

**Location:** `/orphaned-resources`

**Purpose:** Identify Azure resources that are no longer in use but still incurring costs.

**What are Orphaned Resources?**

Orphaned resources are Azure resources that:
- Have no active connections or dependencies
- Are not being actively used
- Continue to incur costs
- Can typically be safely deleted

**Common Orphaned Resource Types:**

| Resource Type | Why It's Orphaned |
|--------------|------------------|
| **Unattached Disks** | VM deleted but disk remains |
| **Unused NICs** | Network interface not attached to VM |
| **Orphaned Public IPs** | IP address not associated with any resource |
| **Empty Resource Groups** | No resources inside, just overhead |
| **Stopped/Deallocated VMs** | VM powered off for extended period |
| **Unused Load Balancers** | No backend pools configured |
| **Expired Snapshots** | Old backups no longer needed |

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Choose one or more subscriptions to scan
   - Use search to filter subscriptions

2. **Scan for Orphaned Resources**
   - Click "Scan" button
   - Progress indicator shows scanning status
   - Wait for scan to complete (may take 30-60 seconds per subscription)

3. **Review Results**
   - Summary chips show:
     - Total orphaned resources found
     - Breakdown by resource type
     - Estimated monthly cost savings (if available)

4. **Results Table**
   - Detailed table showing:
     - **Resource Name:** Name of the orphaned resource
     - **Resource Type:** Type (disk, NIC, IP, etc.)
     - **Resource Group:** Parent resource group
     - **Location:** Azure region
     - **Reason:** Why it's considered orphaned
     - **Monthly Cost:** Estimated monthly cost (if available)
     - **Azure Portal Link:** Click to view in Azure Portal

5. **Take Action**
   - Links open Azure Portal for each resource
   - Manually review each resource before deleting
   - Delete resources you confirm are no longer needed
   - Track cost savings

**Scan Logic:**

The scan uses Azure Resource Graph queries to identify:
- Disks: `properties.diskState == 'Unattached'`
- NICs: `properties.virtualMachine == null`
- Public IPs: `properties.ipConfiguration == null`
- Empty Resource Groups: `resourceCount == 0`
- Deallocated VMs: `powerState == 'VM deallocated'` for 30+ days

**Use Cases:**
- Monthly cost optimization reviews
- Post-project cleanup
- Reduce cloud waste
- Compliance cleanup (remove unused resources)
- Subscription cost reduction initiatives

**Best Practices:**
- Schedule monthly orphaned resource scans
- Review before deleting (confirm resource is truly unused)
- Delete disks carefully (check for backups/snapshots first)
- Document deletion reasons for audit trail
- Keep orphaned resources for 7-30 days before deletion (safety buffer)

**Estimated Savings:**
- Typical findings: 5-15% of total subscription costs
- Common savings: $500-$5000/month per subscription for large estates

---

### 7. Azure Advisor

**Location:** `/advisor`

**Purpose:** View Azure Advisor recommendations across Cost, Performance, Security, Reliability, and Operational Excellence categories.

**What is Azure Advisor?**

Azure Advisor is Microsoft's built-in cloud optimization tool that analyzes your resource configuration and usage to provide recommendations across five categories:
- 💰 **Cost:** Reduce spending
- ⚡ **Performance:** Improve speed and responsiveness
- 🛡️ **Security:** Close security vulnerabilities
- 🔄 **Reliability:** Improve uptime and resilience
- ⚙️ **Operational Excellence:** Process and workflow improvements

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Choose one or more subscriptions to analyze
   - Multi-select supported

2. **Get Recommendations**
   - Click "Get Recommendations" button
   - Progress dialog appears while fetching
   - Wait for completion (10-30 seconds per subscription)

3. **Review Advisor Scores**
   - Summary section shows **Advisor Scores** for each category:
     - Score out of 100 (higher is better)
     - Color-coded indicators:
       - 🟢 80-100: Good
       - 🟡 60-79: Needs Attention
       - 🔴 0-59: Critical
   - Categories:
     - Cost Optimization
     - Performance
     - Security
     - Reliability
     - Operational Excellence

4. **Review Recommendations Table**
   - Detailed table showing all recommendations:
     - **Category:** Cost/Performance/Security/Reliability/Operational
     - **Impact:** High/Medium/Low
     - **Title:** Brief description
     - **Impacted Resource:** Resource name and type
     - **Resource Group:** Parent resource group
     - **Recommendation:** Detailed explanation of the issue
     - **Action:** What to do to resolve
     - **Potential Savings:** Monthly cost savings (for cost recommendations)

5. **Filter and Sort**
   - Use search box to filter recommendations
   - Click column headers to sort
   - Filter by category using chips

6. **Take Action**
   - Click on resource names to open in Azure Portal
   - Follow "Action" guidance to implement recommendation
   - Mark recommendations as dismissed in Azure Portal once resolved

**Recommendation Categories in Detail:**

**💰 Cost Recommendations:**
- Right-size or shutdown underutilized VMs
- Delete unattached disks
- Reserve instances for long-running workloads
- Use Azure Hybrid Benefit
- Remove unused ExpressRoute circuits
- **Potential Savings:** Shown per recommendation

**⚡ Performance Recommendations:**
- Upgrade to faster storage tiers
- Add more VM cores/memory
- Use availability zones
- Configure connection pooling
- Optimize database DTUs

**🛡️ Security Recommendations:**
- Enable multi-factor authentication
- Restrict network access (NSG rules)
- Enable disk encryption
- Update vulnerable software versions
- Enable Azure Security Center

**🔄 Reliability Recommendations:**
- Use availability sets/zones
- Enable backup
- Implement geo-redundancy
- Configure health probes
- Use managed disks

**⚙️ Operational Excellence:**
- Implement tagging strategy
- Use resource naming conventions
- Enable diagnostics/logging
- Implement CI/CD pipelines
- Use Azure Policy for governance

**Use Cases:**
- Monthly optimization reviews
- Pre-production deployment checklist
- Security posture assessment
- Cost reduction initiatives
- Compliance preparation

**Best Practices:**
- Review recommendations monthly
- Prioritize High impact recommendations
- Start with Cost recommendations for quick wins
- Address Security recommendations immediately
- Track savings achieved

---

### 8. Security Recommendations

**Location:** `/security-recommendations`

**Purpose:** View Azure Security Center (Defender for Cloud) security recommendations and compliance posture.

**What are Security Recommendations?**

Azure Security Center continuously assesses your Azure resources against security best practices and compliance standards (CIS, PCI-DSS, etc.). Recommendations are actionable security improvements.

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Choose one or more subscriptions to assess
   - Multi-select supported

2. **Scan for Recommendations**
   - Click "Scan" button
   - Progress dialog appears
   - Wait for scan to complete (20-40 seconds per subscription)

3. **Review Security Score**
   - Summary section shows:
     - **Overall Security Score:** Percentage (0-100%)
     - **Secure Score:** Points out of maximum possible
     - **Breakdown by Category:**
       - Identity and Access
       - Data and Storage
       - Networking
       - Compute and Apps
       - DevOps Security

4. **Review Recommendations Table**
   - Detailed table showing:
     - **Severity:** Critical/High/Medium/Low
     - **Title:** Recommendation name
     - **Category:** Security domain
     - **Affected Resources:** Count of resources impacted
     - **Description:** Detailed explanation (supports HTML with links)
     - **Remediation Steps:** How to fix
     - **Azure Portal Link:** Direct link to recommendation

5. **Severity Filtering**
   - Filter by severity using chips:
     - 🔴 Critical
     - 🟠 High
     - 🟡 Medium
     - 🟢 Low

6. **Take Action**
   - Click "View in Portal" to see full details
   - Follow remediation steps
   - Mark as resolved once fixed
   - Re-scan to verify remediation

**Common Security Recommendations:**

**Critical Severity:**
- Enable MFA for subscription owners
- Apply system updates to VMs
- Fix vulnerabilities in security configuration
- Enable disk encryption
- Remediate SQL vulnerabilities

**High Severity:**
- Restrict management ports (RDP/SSH)
- Enable network security groups
- Install endpoint protection
- Enable SQL auditing
- Configure web application firewall

**Medium Severity:**
- Enable diagnostic logging
- Configure security contact email
- Implement network segmentation
- Enable storage encryption
- Configure backup

**Low Severity:**
- Apply tags for governance
- Enable Azure Policy
- Configure alerts
- Implement role-based access control

**Use Cases:**
- Security compliance audits (SOC 2, ISO 27001, PCI-DSS)
- Pre-deployment security validation
- Incident response and remediation
- Security posture improvement
- Regulatory compliance reporting

**Best Practices:**
- Scan weekly for new recommendations
- Address Critical and High severity first
- Document remediation decisions
- Implement Azure Policy to prevent recurrence
- Use Security Center's automated remediation when available
- Track security score improvements over time

**Integration with Compliance Frameworks:**
- The recommendations align with:
  - CIS Microsoft Azure Foundations Benchmark
  - PCI-DSS v3.2.1
  - ISO 27001:2013
  - NIST SP 800-53 R4
  - Azure Security Benchmark

---

### 9. Service Retirements

**Location:** `/service-retirements`

**Purpose:** Identify Azure services and features scheduled for retirement that are currently in use in your subscriptions.

**What are Service Retirements?**

Microsoft regularly retires Azure services, features, and API versions. Service Retirements are:
- Azure services being deprecated
- Features being removed
- API versions reaching end-of-life
- Resources that need migration to newer services

**Why This Matters:**
- Retired services stop working after retirement date
- Can cause application downtime if not addressed
- Security vulnerabilities may not be patched
- Compliance issues if using unsupported services

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Choose one or more subscriptions to scan

2. **Scan for Retirements**
   - Click "Scan" button
   - Progress dialog appears
   - Scan checks Azure Service Health API for retirement notices

3. **Review Results**
   - Summary chips show:
     - Total services being retired
     - Breakdown by retirement timeline:
       - 🔴 Retiring within 30 days
       - 🟡 Retiring within 90 days
       - 🟢 Retiring within 1 year

4. **Results Table**
   - Detailed table showing:
     - **Service Name:** Name of retiring service/feature
     - **Current Version:** Version you're using
     - **Retirement Date:** When service stops working
     - **Impact:** Description of what will happen
     - **Affected Resources:** List of your resources using this service
     - **Migration Path:** Recommended replacement service
     - **Documentation Link:** Microsoft migration guide

5. **Priority Actions**
   - Focus on services retiring within 30 days first
   - Plan migration for 90-day retirements
   - Research replacements for 1-year retirements

**Common Service Retirements:**

| Retiring Service | Replacement | Typical Retirement Timeline |
|-----------------|-------------|---------------------------|
| Classic VMs | Azure Resource Manager VMs | 1-2 years |
| Cloud Services (classic) | Azure App Service / Container Apps | 1-2 years |
| Classic Storage Accounts | ARM Storage Accounts | 1-2 years |
| Old API versions | Latest API version | 6-12 months |
| Deprecated SKUs | New SKUs | 3-6 months |

**Use Cases:**
- Quarterly retirement reviews
- Pre-deployment compatibility checks
- Technical debt reduction
- Avoid surprise downtime
- Compliance and support requirements

**Best Practices:**
- Scan quarterly for new retirement notices
- Create migration plans immediately for <90 day retirements
- Test migrations in dev/test environments first
- Document migration steps for team knowledge
- Subscribe to Azure Service Health alerts

---

### 10. Logging Usage

**Location:** `/logging-usage`

**Purpose:** Analyze Log Analytics workspace usage and costs to optimize logging spend.

**What is Log Analytics?**

Log Analytics workspaces store logs and telemetry from:
- Azure Monitor
- Application Insights
- Virtual machine logs
- Container insights
- Security Center

**Why Monitor Logging Usage?**

Log Analytics costs are based on data ingestion (GB) and retention:
- First 5 GB/month free
- Pay-as-you-go: ~$2.76/GB ingested
- High-volume applications can generate 100s of GB/day
- Logs older than 30 days incur retention costs

**Step-by-Step Workflow:**

1. **Select Subscriptions**
   - Choose one or more subscriptions containing Log Analytics workspaces

2. **Get Workspaces**
   - Click "Get Workspaces" button
   - App enumerates all workspaces in selected subscriptions

3. **Review Workspace List**
   - Table shows all workspaces:
     - **Workspace Name**
     - **Resource Group**
     - **Location**
     - **SKU/Pricing Tier:** Free/PerGB2018/CapacityReservation
     - **Retention Days:** How long logs are kept

4. **Analyze Usage** (Per Workspace)
   - Click on a workspace to see detailed metrics:
     - **Daily Ingestion:** GB ingested per day
     - **Ingestion Trend:** 30-day chart
     - **Top Tables:** Tables consuming most data
     - **Estimated Monthly Cost:** Based on current usage
     - **Retention Cost:** Cost to store logs beyond 30 days

5. **Top Data Tables**
   - Table breakdown showing:
     - Table name (e.g., ContainerLog, Syslog, SecurityEvent)
     - Daily ingestion volume (GB/day)
     - Percentage of total ingestion
     - Estimated monthly cost

6. **Optimization Recommendations**
   - Based on usage patterns, recommendations may include:
     - "Reduce retention from 90 to 30 days to save $XXX/month"
     - "Consider Capacity Reservation pricing tier"
     - "Filter verbose logs in ContainerLog table"
     - "Disable diagnostic logs for non-production VMs"

**Common Cost Optimization Strategies:**

1. **Reduce Retention Period:**
   - Default: 30 days (free)
   - Only extend retention if required for compliance
   - Archive old logs to cheaper storage

2. **Filter Verbose Logs:**
   - Exclude debug/verbose logs in production
   - Use data collection rules to filter at source
   - Only collect Warning and Error severity

3. **Optimize Container Logging:**
   - Container logs are often the highest volume
   - Reduce stdout/stderr verbosity
   - Use structured logging (JSON)

4. **Right-Size Diagnostic Settings:**
   - Don't collect metrics you don't use
   - Disable diagnostics for non-critical resources
   - Sample metrics instead of full collection

5. **Use Capacity Reservations:**
   - If ingesting >100 GB/day, switch to Capacity Reservation
   - Can save 25-30% vs. pay-as-you-go
   - Available in 100/200/300/400/500 GB/day tiers

**Use Cases:**
- Monthly logging cost review
- Budget forecasting
- Identify noisy log sources
- Optimize container logging
- Compliance retention planning

**Best Practices:**
- Review top tables monthly
- Set alerts for unexpected usage spikes
- Archive logs to Azure Storage for long-term retention
- Use workspace caps to prevent runaway costs
- Document retention requirements before reducing

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
3. No configuration needed - works out of the box

### Method 2: Connect Tenant (Runtime Connection)

**Purpose:** Access subscriptions in tenants where you have guest access.

**How to Connect:**

1. **Get Tenant ID:**
   - Login to Azure Portal
   - Navigate to Azure Active Directory
   - Copy "Tenant ID" from Overview page

2. **Connect in FinOps:**
   - Click "Connect Tenant" button (appears on most pages)
   - Enter tenant GUID
   - Click "Connect"

3. **Verify Access:**
   - Subscriptions from connected tenant appear in tables
   - If no subscriptions appear, verify you have access via: `az login --tenant <tenant-id>`

4. **Disconnect:**
   - Click the X on the tenant chip in the tenant bar
   - Subscriptions from that tenant are removed from view

**Tenant Bar (Top of Most Pages):**
- 🟦 Blue chip: Home tenant (default)
- 🟪 Purple chips: Connected tenants
- Shows tenant display name if available, otherwise tenant GUID

**Use Cases:**
- MSPs managing multiple customer tenants
- Enterprises with multiple Azure AD tenants
- Consultants with guest access to client tenants
- Cross-organization projects

**Permissions Required:**
- Guest user account in target tenant
- Appropriate RBAC roles on subscriptions in that tenant
- `az login --tenant <tenant-id>` must succeed

**Persistence:**
- Tenant connections are **per-session** (scoped to your browser tab)
- Connections are lost when you close the browser tab
- Reconnect each session as needed

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
| **Authentication** | Azure.Identity | 1.17.1 |
| **Cost Management** | Azure.ResourceManager.CostManagement | 1.0.2 |
| **Budgets** | Azure.ResourceManager.Consumption | 1.0.1 |
| **Advisor** | Azure.ResourceManager.Advisor | 1.0.0-beta.5 |
| **Log Analytics** | Azure.ResourceManager.OperationalInsights | 1.3.1 |
| **Resource Graph** | Azure.ResourceManager.ResourceGraph | 1.1.0 |

### Service Architecture

All services are registered as **Scoped** in the dependency injection container, meaning each Blazor SignalR circuit (user session) gets its own service instances.

**Core Services:**

```
TenantClientManager (scoped)
├── Manages ArmClient instances per tenant
├── Home tenant client (created once at startup)
└── Connected tenant clients (created on-demand)

IAzureSubscriptionService (scoped)
├── Enumerates subscriptions across all tenants
└── Filters by state, tenant, name

IBudgetService (scoped)
├── Create/delete budgets
├── Get budgets for subscriptions/resource groups
└── Uses Azure Consumption API

ICostAnalysisService (scoped)
├── Cost queries (historical data)
├── Cost forecasts
├── Dimensional breakdowns (service, resource group, location)
└── Uses Azure Cost Management API

IResourceTaggingService (scoped)
├── Apply tags to resource groups and resources
├── Remove tags with progress callbacks
└── Real-time operation results

IPolicyService (scoped)
├── Assign tag inheritance policies
├── Create managed identities
├── Grant RBAC roles
└── List and delete policy assignments

IAdvisorService (scoped)
├── Get Advisor recommendations
├── Calculate Advisor scores
├── Potential cost savings calculation
└── Uses Azure Advisor API

IOrphanedResourceService (scoped)
├── Identify unattached disks, NICs, IPs
├── Find empty resource groups
├── Locate deallocated VMs
└── Uses Azure Resource Graph queries

ISecurityRecommendationService (scoped)
├── Get Security Center recommendations
├── Calculate secure scores
└── Uses Azure Security Center API

IServiceRetirementService (scoped)
├── Query Azure Service Health
├── Identify retiring services
└── Match against deployed resources

ILogAnalyticsService (scoped)
├── Enumerate Log Analytics workspaces
├── Query usage metrics
├── Calculate costs
└── Uses Azure Monitor Query API
```

### TenantClientManager (Core Service)

**Purpose:** Centralized management of `ArmClient` instances for multi-tenant scenarios.

**Key Responsibilities:**
1. Create and cache `ArmClient` for home tenant
2. Create and cache `ArmClient` for connected tenants
3. Provide `GetClientForTenant(tenantId)` method
4. Track connected tenant IDs
5. Dispose of clients when disconnected

**Implementation:**

```csharp
public class TenantClientManager
{
    private ArmClient _homeClient;
    private ConcurrentDictionary<string, ArmClient> _tenantClients;

    public ArmClient GetClientForTenant(string tenantId)
    {
        // If home tenant, return home client
        // If connected tenant, return from cache or create new
        // Create uses new AzureCliCredential(new AzureCliCredentialOptions { TenantId = tenantId })
    }
}
```

### Data Flow Example: Creating a Budget

1. **User Action:** User selects 3 subscriptions and clicks "Create Budget"
2. **UI Layer:** `Budgets.razor` calls `BudgetService.CreateBudgetAsync()`
3. **Service Layer:**
   - `BudgetService` receives subscription list
   - For each subscription:
     - Gets tenant ID from subscription
     - Calls `TenantClientManager.GetClientForTenant(tenantId)`
     - Uses returned `ArmClient` to create budget via Azure SDK
     - Returns `BudgetCreationResult` with success/failure
4. **UI Update:** Results displayed in dialog with color-coded status

### Render Mode: Interactive Server

The application uses **global Interactive Server** render mode:

**App.razor:**
```razor
<Routes @rendermode="InteractiveServer" />
<HeadOutlet @rendermode="InteractiveServer" />
```

**Why Interactive Server?**
- Required for MudBlazor dialogs and snackbars to work
- Enables real-time progress updates via SignalR
- Low latency for Azure API calls (server-side execution)
- No need to download .NET runtime to browser

**Trade-offs:**
- Requires active SignalR connection
- Server-side state (uses more server memory)
- Reconnection modal shown if connection lost

---

## Project Structure

```
finops/
│
├── Program.cs                          # DI registration, middleware pipeline
├── FinOps.csproj                       # Project file, package references
├── .claude/                            # Claude Code configuration
│   └── CLAUDE.md                       # Project documentation
│
├── Components/
│   ├── App.razor                       # HTML shell, global render mode
│   ├── Routes.razor                    # Router configuration
│   ├── _Imports.razor                  # Global Razor usings
│   │
│   ├── Layout/
│   │   ├── MainLayout.razor            # MudBlazor layout, app bar, drawer, providers
│   │   ├── NavMenu.razor               # Side navigation menu
│   │   └── ReconnectModal.razor        # SignalR reconnection UI
│   │
│   ├── Pages/
│   │   ├── Home.razor                  # Landing page
│   │   ├── Budgets.razor               # Subscription budget management
│   │   ├── ResourceGroupBudgets.razor  # Resource group budget management
│   │   ├── OverviewDashboard.razor     # Executive cost dashboard
│   │   ├── CostOverview.razor          # Cost analysis with charts
│   │   ├── CostDashboard.razor         # Single subscription cost dashboard
│   │   ├── Tagging.razor               # Resource tagging
│   │   ├── Policy.razor                # Policy management (2 tabs)
│   │   ├── Advisor.razor               # Azure Advisor recommendations
│   │   ├── OrphanedResources.razor     # Orphaned resource scanner
│   │   ├── SecurityRecommendations.razor # Security Center recommendations
│   │   ├── ServiceRetirements.razor    # Service retirement scanner
│   │   ├── LoggingUsage.razor          # Log Analytics usage analysis
│   │   ├── Error.razor                 # Error page
│   │   └── NotFound.razor              # 404 page
│   │
│   └── Dialogs/
│       ├── ConnectTenantDialog.razor   # Tenant GUID input + validation
│       ├── CreateBudgetDialog.razor    # Budget creation form
│       └── AddTagsDialog.razor         # Tag key/value entry form
│
├── Services/
│   ├── TenantClientManager.cs          # Core multi-tenant ArmClient manager
│   ├── IAzureSubscriptionService.cs    # Subscription enumeration interface
│   ├── AzureSubscriptionService.cs     # Subscription enumeration implementation
│   ├── IBudgetService.cs               # Budget CRUD interface
│   ├── BudgetService.cs                # Budget CRUD implementation
│   ├── ICostAnalysisService.cs         # Cost analysis interface
│   ├── CostAnalysisService.cs          # Cost analysis with retry logic
│   ├── IResourceTaggingService.cs      # Tagging interface
│   ├── ResourceTaggingService.cs       # Tagging with progress callbacks
│   ├── IPolicyService.cs               # Policy management interface
│   ├── PolicyService.cs                # Policy assignment with managed identities
│   ├── IAdvisorService.cs              # Advisor interface
│   ├── AdvisorService.cs               # Advisor recommendations
│   ├── IOrphanedResourceService.cs     # Orphaned resource scanner interface
│   ├── OrphanedResourceService.cs      # Resource Graph queries for orphans
│   ├── ISecurityRecommendationService.cs # Security recommendations interface
│   ├── SecurityRecommendationService.cs  # Security Center integration
│   ├── IServiceRetirementService.cs    # Service retirement interface
│   ├── ServiceRetirementService.cs     # Service Health API integration
│   ├── ILogAnalyticsService.cs         # Log Analytics interface
│   ├── LogAnalyticsService.cs          # Workspace usage queries
│   └── ITenantConnectionService.cs     # Tenant connection interface
│
└── Models/
    ├── TenantSubscription.cs           # Tenant + subscription DTO
    ├── BudgetInfo.cs                   # Budget read model
    ├── BudgetFormModel.cs              # Budget form binding model
    ├── BudgetCreationResult.cs         # Budget creation result
    ├── ResourceGroupInfo.cs            # Resource group with tags
    ├── TagEntry.cs                     # Tag key/value pair
    ├── TagFormModel.cs                 # Tag form binding model
    ├── TagOperationResult.cs           # Tag operation result
    ├── PolicyAssignmentInfo.cs         # Policy assignment read model
    ├── PolicyOperationResult.cs        # Policy operation result
    ├── CostDataPoint.cs                # Historical cost data point
    ├── ForecastDataPoint.cs            # Forecast data point
    ├── CostBreakdownItem.cs            # Dimensional cost breakdown
    ├── CostQueryResult.cs              # Cost query result
    ├── ForecastResult.cs               # Forecast query result
    ├── CostDashboardData.cs            # Dashboard aggregated data
    ├── AdvisorRecommendation.cs        # Advisor recommendation
    ├── AdvisorScore.cs                 # Advisor score per category
    ├── OrphanedResource.cs             # Orphaned resource details
    ├── SecurityRecommendation.cs       # Security recommendation
    ├── ServiceRetirement.cs            # Retirement notice
    └── WorkspaceUsage.cs               # Log Analytics usage data
```

---

## Troubleshooting

### Common Issues and Solutions

| Issue | Cause | Solution |
|-------|-------|----------|
| **App won't start** | Not logged in to Azure CLI | Run `az login` before starting the app |
| **App hangs on startup** | Wrong credential type being used | Verify `AzureCliCredential` is used, not `DefaultAzureCredential` |
| **Process locked** | Previous instance still running | Run: `taskkill /F /IM FinOps.exe` (Windows) |
| **Dialogs don't open** | Missing InteractiveServer render mode | Check `App.razor` has `@rendermode="InteractiveServer"` |
| **Snackbars not showing** | Missing provider in layout | Verify `MainLayout.razor` has `<MudSnackbarProvider />` |
| **Charts not loading** | Missing ApexCharts scripts | Verify `App.razor` includes ApexCharts JS before MudBlazor |
| **No subscriptions shown** | No access in current tenant | Verify `az account list` shows subscriptions |
| **Connected tenant shows nothing** | Not logged into that tenant | Run: `az login --tenant <tenant-id>` |
| **Tag operation returns 409** | Resource locked by another operation | Wait 5 minutes and retry |
| **Policy assignment fails** | Missing permissions | Ensure you have `Microsoft.Authorization/policyAssignments/write` |
| **Budget creation fails** | Missing Consumption permissions | Ensure you have `Microsoft.Consumption/budgets/write` |
| **Cost queries fail with 429** | Too many API requests | Wait 60 seconds, app will retry automatically |
| **Charts show "No data"** | No cost data in selected period | Select different date range or verify subscription has costs |
| **SignalR disconnected** | Network issue or server restart | Wait for reconnection modal, or refresh page |

### Debugging Tips

**Enable detailed logging:**

Add to `appsettings.json`:
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

**Browser developer tools:**
- Open F12 DevTools
- Check Console tab for JavaScript errors
- Check Network tab for failed API calls
- Check Application > Local Storage for session state

**Common error messages:**

| Error Message | Meaning | Fix |
|--------------|---------|-----|
| "AzureCliCredential authentication failed" | Not logged into Azure CLI | Run `az login` |
| "429 Too Many Requests" | Rate limit exceeded | Wait 60 seconds, reduce parallel operations |
| "403 Forbidden" | Insufficient permissions | Grant appropriate RBAC role |
| "404 Not Found" | Resource or subscription doesn't exist | Verify resource ID and subscription |
| "409 Conflict" | Resource is locked | Wait for conflicting operation to complete |
| "The circuit failed to initialize" | Blazor SignalR connection issue | Refresh page, check network connectivity |

---

## Rate Limiting & Best Practices

### Azure API Rate Limits

Azure APIs have rate limits to prevent abuse. Common limits:

| API | Limit | Scope |
|-----|-------|-------|
| **ARM Reads** | 12,000 requests/hour | Per subscription |
| **ARM Writes** | 1,200 requests/hour | Per subscription |
| **Cost Management** | 30 requests/minute | Per subscription |
| **Resource Graph** | 15 requests/second | Per tenant |
| **Advisor** | 100 requests/minute | Per subscription |

### Rate Limiting in FinOps

The application implements several rate limiting strategies:

**1. Exponential Backoff with Retry**

All cost management queries use automatic retry logic:
```csharp
private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
{
    for (int attempt = 0; attempt < 3; attempt++)
    {
        try { return await operation(); }
        catch (RequestFailedException ex) when (ex.Status == 429)
        {
            var delayMs = 1000 * (int)Math.Pow(2, attempt); // 1s, 2s, 4s
            await Task.Delay(delayMs);
        }
    }
}
```

**2. Batched API Calls**

Cost Overview page loads data in batches with delays:
- Batch 1: Historical costs (all subscriptions in parallel)
- **800ms delay**
- Batch 2: Forecasts
- **800ms delay**
- Batch 3: Service breakdown
- **800ms delay**
- Batch 4: Resource group breakdown

**3. On-Demand Loading**

Instead of loading all data on page load, data fetches only when:
- User selects subscriptions
- User clicks "Get Recommendations" / "Scan" buttons
- User expands sections

**4. Caching**

Future enhancement: Cache cost data for 15 minutes to reduce redundant queries.

### Best Practices to Avoid Rate Limits

1. **Select Fewer Subscriptions:**
   - Analyze 1-5 subscriptions at a time
   - Don't select all 50 subscriptions at once

2. **Sequential Operations:**
   - Don't click multiple "Scan" buttons simultaneously
   - Wait for one scan to complete before starting another

3. **Avoid Rapid Refreshes:**
   - Don't refresh pages repeatedly
   - Wait at least 60 seconds between refreshes

4. **Use Filters:**
   - Filter subscription lists before selecting
   - Only select subscriptions you need to analyze

5. **Monitor Rate Limit Errors:**
   - If you see "429 Too Many Requests", wait 60 seconds
   - The app will automatically retry, don't manually retry

6. **Schedule Scans:**
   - Run intensive scans (Orphaned Resources, Security) during off-peak hours
   - Don't run all scans simultaneously

### Performance Optimization Tips

**Fast Operations:**
- Viewing subscription lists (< 1 second)
- Creating single budget (< 2 seconds)
- Connecting tenants (< 1 second)

**Medium Operations (5-15 seconds):**
- Loading cost charts for 1-3 subscriptions
- Getting Advisor recommendations
- Scanning orphaned resources

**Slow Operations (30-60 seconds):**
- Loading cost data for 10+ subscriptions
- Scanning security recommendations across multiple subscriptions
- Log Analytics usage analysis

**Optimization Tips:**
- Use search/filter before selecting subscriptions
- Analyze subscriptions in batches (5 at a time)
- Close browser tabs when not in use (free up server memory)
- Clear selections when done to stop background polling

---

## Advanced Topics

### Custom Queries with Resource Graph

The Orphaned Resources feature uses Azure Resource Graph. You can extend this for custom queries.

**Example: Find VMs without backup:**

```csharp
// In OrphanedResourceService.cs
var query = @"
Resources
| where type == 'microsoft.compute/virtualmachines'
| where properties.storageProfile.osDisk.managedDisk.id !in (
    Resources
    | where type == 'microsoft.recoveryservices/vaults/backupfabrics/protectioncontainers/protecteditems'
    | project properties.sourceResourceId
)
| project name, resourceGroup, location";
```

### Extending Budget Alerts

Budgets support email/SMS alerts. To add alert configuration:

1. Extend `BudgetFormModel` with alert thresholds
2. Update `CreateBudgetDialog` with threshold inputs
3. Modify `BudgetService.CreateBudgetAsync` to add notifications

### Creating Custom Dashboards

To create new dashboard pages:

1. Create new Razor page in `Components/Pages/`
2. Inject required services
3. Add to `NavMenu.razor`
4. Follow existing patterns for subscription selection and data loading

---

## Contributing

### Code Style Guidelines

- Use explicit type declarations (`string` not `var` for readability)
- Services should be async by default
- All external API calls should have timeout and retry logic
- Use `StateHasChanged()` after async operations in Blazor components
- Follow existing naming conventions for consistency

### Testing Locally

Before committing changes:

1. **Build:** `dotnet build` (must succeed with 0 warnings)
2. **Run:** `dotnet run` and test manually
3. **Test multi-tenant:** Connect to a secondary tenant
4. **Test error handling:** Disconnect from network, verify graceful failures
5. **Check UI:** Test on mobile width (resize browser)

---

## Security Considerations

### Data Privacy

- No data is stored persistently (all in-memory, per-session)
- No logging of sensitive data (subscription IDs, tenant IDs are logged, but not credentials)
- All communications with Azure use HTTPS

### Authentication Security

- Uses Azure CLI credentials (no passwords stored)
- Leverages Azure RBAC for permissions
- No custom authentication/authorization logic

### Deployment Security

If deploying to production:

1. **Use HTTPS only** (configure certificates)
2. **Enable authentication** (Azure AD integration)
3. **Restrict network access** (firewall/NSG rules)
4. **Enable audit logging** (Azure Monitor)
5. **Use managed identities** (instead of Azure CLI credentials)

---

## FAQ

**Q: Can I use this without Azure CLI?**
A: No, the app exclusively uses `AzureCliCredential`. You must run `az login` first.

**Q: Does this work with Azure Government or Azure China?**
A: Not currently, but could be extended by configuring `ArmEnvironment` in `ArmClient` initialization.

**Q: Can I export cost data to Excel/CSV?**
A: Not currently implemented, but could be added using MudBlazor table export features.

**Q: How do I add more subscriptions?**
A: If you have access via your home tenant (Lighthouse), they appear automatically. Otherwise, use "Connect Tenant" to add additional tenants.

**Q: Is there a dark mode?**
A: Yes, toggle in the header (moon icon).

**Q: Can multiple users use this simultaneously?**
A: Yes, Blazor Server supports multiple concurrent users, each with their own session.

**Q: How much does running this app cost?**
A: The app itself is free (just .NET hosting). Azure API calls are free. You only pay for Azure resources you create (budgets, policies, etc.).

**Q: Can I customize the charts?**
A: Yes, edit `CostOverview.razor` and modify ApexCharts options.

---

## License

This project is for internal use. See LICENSE file for details.

---

## Support

For issues, questions, or feature requests:
- Check the [Troubleshooting](#troubleshooting) section
- Review existing GitHub issues
- Create a new issue with detailed description and error messages

---

**Built with ❤️ using .NET 10, Blazor Server, and MudBlazor**
