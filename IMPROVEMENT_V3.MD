Act as a Senior Fullstack Architect (.NET 9, Angular 20). 
The previous implementation missed crucial state management, allowed duplicates, and left legacy "Cluster" UI elements. We need to strictly enforce the manual 3-phase funnel (Jobs -> Opportunities -> Products) and completely remove the old automated concepts.

Execute the following fixes step-by-step:

PHASE 1: JOBS MODULE (State Persistence & Toggle)
1. Backend: Update the `JobOffer` or `JobInsight` DTO returned to the frontend to include a boolean `HasOpportunity` (and optionally the `OpportunityId`).
2. Backend: Update `POST /api/jobs/{id}/create-opportunity`. It MUST check if an Opportunity already exists for this JobId. If it exists, do not create a duplicate; return the existing one.
3. Backend: Create `DELETE /api/jobs/{id}/remove-opportunity` to delete the associated opportunity.
4. Frontend (`dashboard.html` / `dashboard.ts`): The UI must read `HasOpportunity` on load (no state lost on F5). 
   - If false: Show "+ Opportunity" (Primary button).
   - If true: Show "- Remove Opportunity" (Danger/Outline button).

PHASE 2: OPPORTUNITIES MODULE (Nuke Clusters & CEO Prompt)
1. Frontend Cleanup (`opportunities.html`): 
   - DELETE the entire "Automated Clusters" tab.
   - DELETE the buttons: "Backfill insights", "Cleanup (<5 jobs)", "Rebuild clusters".
   - REMOVE any text mentioning "Cluster" or "Clustered". The view must ONLY display the flat table of manually added "Sales Opportunities".
2. Fix Duplicates: Ensure the table only shows unique opportunities (fix the backend query if it's returning a Cartesian product or duplicates).
3. Update Semantic Kernel Prompt (Backend): Change the prompt for `POST /api/opportunities/{id}/synthesize-ideas`. It must act as a CEO, not a dev:
   "Act as a CEO and B2B Deal Maker. Analyze this job opportunity. Focus on the MONEY and the BUSINESS GAP. What are 3 high-ticket MVP products or services we can build to solve their problem and generate revenue? Return a JSON array 'productIdeas' with 'name' and 'businessJustification'."
4. Frontend Flow: Change the button next to the AI ideas from "Create Product" to "Generate MVP Product".
5. Backend State: When a Product is generated from an Opportunity, update the Opportunity's status in the database to "In Product" (or "Converted") so the user knows it moved to the next phase.

PHASE 3: PRODUCTS MODULE (Technical MVP Prompt & Closed State)
1. Data Source: The `/products` grid must ONLY load products that have a valid `OpportunityId` (manually created).
2. Frontend (`product-detail.html`): 
   - Keep the existing "💼 Develop Commercial Strategy (IA)" button and its 6-card dashboard.
   - ADD A NEW BUTTON: "⚙️ Develop MVP Technical Requirements (IA)".
3. Backend (New SK Prompt): 
   - Create `POST /api/products/{id}/synthesize-technical-mvp`.
   - Prompt: "Act as a Lead Software Architect. Based on this product idea, generate the MVP Technical Requirements. Return a JSON with: 'architectureStrategy', 'requiredTechStack', 'estimatedTimelines' (sprints), and 'coreFeatures'."
4. Frontend Dashboard: Display the technical JSON response in a new clean section below the commercial strategy.
5. Finalize State: Add a "🔒 Mark as Closed / Finalized" button on the Product detail page. When clicked, update a `Status` field on the `ProductSuggestion` entity to `Closed` (meaning the analysis is done and it's ready for the development team later).

Ensure all EF Core migrations are updated for the new `Status` fields, and clean up any unused Angular components related to "Clusters".