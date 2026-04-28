Act as a Senior Fullstack Architect (.NET 9, Angular 20).

CONTEXT:
We need to implement bidirectional state tracking between AI-generated Product Ideas (inside an Opportunity) and the actual created Products. 
Currently, users can click "⚙️ Generate MVP Product" multiple times on the same idea, creating duplicates. Also, there is no way to delete a product. We need to track exactly WHICH idea spawned WHICH product, disable the button if it's already converted, and re-enable it if the product is deleted.

Please implement the following updates step-by-step:

PHASE 1: DB SCHEMA & AI PROMPT UPDATES (Backend)
1. Entity Update: Add `public string? SourceIdeaId { get; set; }` to the `ProductSuggestion` entity. Create an EF Core migration named `AddSourceIdeaIdToProduct` and apply it.
2. AI Prompt Update (`OpportunityAiService` / `AiPromptRegistry`): Update the Semantic Kernel prompt that generates the `productIdeas` array. Instruct the LLM to include a unique string `id` (e.g., a URL-friendly slug based on the name) for EACH idea in the JSON array. 
   - Example expected JSON: `[ { "id": "cloud-audit-sprint", "name": "...", "businessJustification": "..." } ]`
3. DTO Update: Ensure the `ProductIdea` record/class in C# includes this `Id` string property.

PHASE 2: API ENDPOINTS (Backend)
1. Update `POST /api/products/from-opportunity`: This endpoint must now accept the `SourceIdeaId` from the frontend payload and save it into the new `ProductSuggestion.SourceIdeaId` column.
2. Create `DELETE /api/products/{id}`: Add a new endpoint in `ProductController` to permanently delete a `ProductSuggestion` by its ID.
3. Update `GET /api/opportunities/{id}`: The DTO returned for the Opportunity detail MUST now include a list of strings: `List<string> ConvertedIdeaIds`. This list should contain the `SourceIdeaId` of all `ProductSuggestions` linked to this Opportunity.

PHASE 3: FRONTEND STATE & UI (Angular)
1. Models: Update the `ProductIdea` interface to include `id: string`. Update the Opportunity detail model to include `convertedIdeaIds: string[]`.
2. Opportunity Detail UI (`opportunity-detail.ts` & `.html`):
   - When rendering the list of AI ideas, check if `idea.id` exists in `opportunity.convertedIdeaIds`.
   - IF TRUE: Change the button text to "✅ Added to Product", apply a disabled state (`[disabled]="true"`), and change its visual style (e.g., grey/outline).
   - IF FALSE: Keep the "⚙️ Generate MVP Product" button enabled.
   - When calling the create product service, pass the `idea.id` as the `sourceIdeaId`.
   - After successfully creating the product, push the `idea.id` into the local `convertedIdeaIds` array so the UI updates instantly without refreshing.
3. Products List UI (`products.ts` & `.html`):
   - Add a "🗑️ Delete" (or Trash icon) button to each product card.
   - Implement the `deleteProduct(id)` method. It should call the new DELETE endpoint, show a loading state during the call, and remove the product from the local array upon success.

CONSTRAINTS:
- Keep the UI clean using Tailwind CSS.
- Ensure the delete button has a simple JS `confirm()` prompt to prevent accidental deletions.
- Ensure the `SourceIdeaId` logic elegantly prevents generating duplicate products from the exact same AI idea

##NEW_MODULE

Act as a Senior Fullstack Architect (.NET 9, Angular 20).

CONTEXT:
The client wants to build an "Idea Vault". Currently, when the LLM generates product ideas for an Opportunity, those ideas are transient or strictly bound to the Opportunity. 
We need to persist these ideas in a new standalone table. If an Opportunity is deleted, the ideas MUST NOT be deleted (to save LLM tokens and preserve business value). Instead, they become "orphaned" ideas that can be edited and manually re-linked to any Opportunity via a dropdown.

Execute the following implementation step-by-step:

PHASE 1: DATABASE & BACKEND (EF Core & API)
1. New Entity: Create an `OpportunityIdea` entity with the following properties:
   - `Id` (Guid or string URL-friendly slug)
   - `Name` (string)
   - `BusinessJustification` (string)
   - `OpportunityId` (Nullable foreign key: `int?` or `Guid?` depending on your PK type).
   - `CreatedAt` (DateTime)
2. EF Core Configuration: In `ApplicationDbContext`, configure the relationship so that deleting an `Opportunity` does NOT cascade delete the `OpportunityIdea`. Use `DeleteBehavior.SetNull` for the foreign key. Generate and apply the migration (`AddOpportunityIdeasTable`).
3. Update Synthesis Endpoint: Modify `POST /api/opportunities/{id}/synthesize-ideas`. When the LLM (CEO perspective) returns the array of ideas, the backend MUST instantly save these ideas into the `OpportunityIdeas` table (linked to the current `OpportunityId`) BEFORE returning them to the frontend.
4. New Controller (`OpportunityIdeaController`): Create endpoints to manage the vault:
   - `GET /api/opportunity-ideas` (List all ideas, including their linked Opportunity).
   - `PUT /api/opportunity-ideas/{id}` (Update Name, BusinessJustification, and OpportunityId).

PHASE 2: FRONTEND ROUTING & SIDEBAR (Angular)
1. Navigation: Add a new link to the main sidebar/sidenav: "💡 Opportunity Ideas".
2. Routing: Create a new route `/opportunity-ideas` pointing to a new component `OpportunityIdeaListComponent`.

PHASE 3: FRONTEND IDEA VAULT UI (Angular)
1. Idea Vault View (`opportunity-idea-list.html` & `.ts`):
   - Build a data grid/table displaying all generated ideas.
   - Columns: Idea Name, Business Justification, Linked Opportunity, Actions.
   - If `opportunityId` is null, display a badge saying "Unlinked".
2. Edit Functionality:
   - Add an "Edit" button to each row. 
   - Clicking Edit should open a Modal or Inline Form.
   - The form must allow editing the `Name` and `BusinessJustification` (Textareas).
   - The form MUST include a `<select>` dropdown populated with all active `Opportunities` (fetch via `GET /api/opportunities`). This allows the user to re-link an orphaned idea to an existing Opportunity.
   - Provide a "Save" button that triggers `PUT /api/opportunity-ideas/{id}` and updates the grid.

CONSTRAINTS:
- Keep the UI consistent using Tailwind CSS.
- Ensure that the "⚙️ Generate MVP Product" flow in the Opportunity detail page still works flawlessly using this new persisted `OpportunityIdea` entity.
- The `OpportunityIdea` ID must be used as the `SourceIdeaId` when converting an idea into an actual `ProductSuggestion`.
This is a brilliant architectural decision. You are essentially creating an "Idea Vault" or "Idea Bank". By decoupling the generated ideas from the lifecycle of the Opportunity, you ensure that you never lose valuable AI-generated business concepts, and more importantly, you save money on LLM tokens by never having to re-analyze the same data twice.

Here is the definitive, highly structured English prompt to copy and paste to your AI assistant (Claude/Roo Code) so it implements this exact workflow, including the database relationships, the new UI module, and the dropdown linking logic.

Plaintext
Act as a Senior Fullstack Architect (.NET 9, Angular 20).

CONTEXT:
The client wants to build an "Idea Vault". Currently, when the LLM generates product ideas for an Opportunity, those ideas are transient or strictly bound to the Opportunity. 
We need to persist these ideas in a new standalone table. If an Opportunity is deleted, the ideas MUST NOT be deleted (to save LLM tokens and preserve business value). Instead, they become "orphaned" ideas that can be edited and manually re-linked to any Opportunity via a dropdown.

Execute the following implementation step-by-step:

PHASE 1: DATABASE & BACKEND (EF Core & API)
1. New Entity: Create an `OpportunityIdea` entity with the following properties:
   - `Id` (Guid or string URL-friendly slug)
   - `Name` (string)
   - `BusinessJustification` (string)
   - `OpportunityId` (Nullable foreign key: `int?` or `Guid?` depending on your PK type).
   - `CreatedAt` (DateTime)
2. EF Core Configuration: In `ApplicationDbContext`, configure the relationship so that deleting an `Opportunity` does NOT cascade delete the `OpportunityIdea`. Use `DeleteBehavior.SetNull` for the foreign key. Generate and apply the migration (`AddOpportunityIdeasTable`).
3. Update Synthesis Endpoint: Modify `POST /api/opportunities/{id}/synthesize-ideas`. When the LLM (CEO perspective) returns the array of ideas, the backend MUST instantly save these ideas into the `OpportunityIdeas` table (linked to the current `OpportunityId`) BEFORE returning them to the frontend.
4. New Controller (`OpportunityIdeaController`): Create endpoints to manage the vault:
   - `GET /api/opportunity-ideas` (List all ideas, including their linked Opportunity).
   - `PUT /api/opportunity-ideas/{id}` (Update Name, BusinessJustification, and OpportunityId).

PHASE 2: FRONTEND ROUTING & SIDEBAR (Angular)
1. Navigation: Add a new link to the main sidebar/sidenav: "💡 Opportunity Ideas".
2. Routing: Create a new route `/opportunity-ideas` pointing to a new component `OpportunityIdeaListComponent`.

PHASE 3: FRONTEND IDEA VAULT UI (Angular)
1. Idea Vault View (`opportunity-idea-list.html` & `.ts`):
   - Build a data grid/table displaying all generated ideas.
   - Columns: Idea Name, Business Justification, Linked Opportunity, Actions.
   - If `opportunityId` is null, display a badge saying "Unlinked".
2. Edit Functionality:
   - Add an "Edit" button to each row. 
   - Clicking Edit should open a Modal or Inline Form.
   - The form must allow editing the `Name` and `BusinessJustification` (Textareas).
   - The form MUST include a `<select>` dropdown populated with all active `Opportunities` (fetch via `GET /api/opportunities`). This allows the user to re-link an orphaned idea to an existing Opportunity.
   - Provide a "Save" button that triggers `PUT /api/opportunity-ideas/{id}` and updates the grid.

CONSTRAINTS:
- Keep the UI consistent using Tailwind CSS.
- Ensure that the "⚙️ Generate MVP Product" flow in the Opportunity detail page still works flawlessly using this new persisted `OpportunityIdea` entity.
- The `OpportunityIdea` ID must be used as the `SourceIdeaId` when converting an idea into an actual `ProductSuggestion`.
Why this specific prompt is bulletproof:
DeleteBehavior.SetNull: This is the secret sauce in C# Entity Framework. It explicitly tells the database: "If I delete the Opportunity, just turn the OpportunityId on the Idea to NULL. Do NOT delete the Idea."

Instant Persistence: Previously, the LLM returned JSON to the frontend, and it only saved when you clicked "Create Product". Now, the backend intercepts the LLM response, saves the 3 ideas to the database instantly, and then sends them to the frontend.

The "Orphan" Workflow: By adding the dropdown in the Edit modal, you can now resurrect old, unused ideas and attach them to new clients that have the exact same technical problem.

Pass this to your agent. This is a massive upgrade to the intellectual property (IP) retention of your software!