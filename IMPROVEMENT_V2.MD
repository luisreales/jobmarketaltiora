Act as a Senior Software Architect (.NET 9, Angular 20, EF Core) and B2B SaaS Strategist.

CONTEXT:
The client has completely redefined the business flow of the system. The current automated pipeline generates too much noise and useless data. We are pivoting to a manual, human-in-the-loop B2B sales funnel divided into 3 distinct phases: Jobs -> Opportunities -> Products.
We will utilize Semantic Kernel with two specialized AI Prompts: one for Technical Ideation, and one for Commercial Strategy.

Execute this restructuring step-by-step:

PHASE 1: JOBS MODULE (From Raw Job to Opportunity)
1. Frontend (Jobs UI): In the raw Jobs table/list, add an action button to each row: "Convert to Opportunity".
2. Backend: Create the endpoint `POST /api/jobs/{id}/create-opportunity`. This endpoint takes the `JobInsight` data, creates a new record in the Opportunities table (repurposing `MarketClusters` or creating a new entity), links the base data of that job, and returns the ID of the newly created Opportunity.

PHASE 2: OPPORTUNITIES MODULE (Technical Ideation)
1. Frontend (Grid to Detail): 
   - In the `/opportunities` grid, change the action button to "View Opportunity" which must navigate to a completely new dedicated page: `/opportunities/:id`.
   - On the `/opportunities/:id` page, display the raw job data. Add a primary action button: "🧠 Analyze Opportunities (IA)".
2. Backend (Technical Prompt):
   - Create the endpoint `POST /api/opportunities/{id}/synthesize-ideas`.
   - Call Semantic Kernel with this System Prompt: 
     "Act as an Elite Software Architect. Analyze this tech job posting. What 3 specific technical products or services (e.g., Architecture Audits, Cloud Migrations, MVPs) could our 'Engineering Strike Team' sell to this company to solve their urgent technical bottlenecks? 
     Return strictly a JSON object with an array 'productIdeas'. Each object in the array must have 'name' and 'shortTechnicalDescription'."
3. Frontend (Flow towards Products): 
   - Display the technical ideas returned by the AI on the screen. 
   - Next to each idea, place a "Create Product" button. 
   - When clicked, call a new endpoint `POST /api/products/from-opportunity` that saves this specific product into the `ProductSuggestions` table, linked to that opportunity ID.

PHASE 3: PRODUCTS MODULE (Commercial Strategy & MVP)
1. Frontend (Grid to Detail):
   - In the `/products` grid, the action button must navigate to a completely new dedicated page: `/products/:id`.
   - On the `/products/:id` page, add a primary action button: "💼 Develop Commercial Strategy (IA)".
2. Backend (Business Prompt):
   - Create the endpoint `POST /api/products/{id}/synthesize-strategy`.
   - Call Semantic Kernel with this NEW System Prompt:
     "Act as an elite B2B Technical Sales Strategist. We are going to sell the product/service '{ProductName}' to the company that posted this job. Focus strictly on MONEY and BUSINESS, not code. 
     Return strictly a JSON object with this exact structure:
     {
       \"realBusinessProblem\": \"What is the actual business pain behind this job? (1 sentence, business terms)\",
       \"financialImpact\": \"Why this saves or makes them money (2 lines).\",
       \"mvpDefinition\": \"Scope of the Minimum Viable Product we will sell them first to enter quickly.\",
       \"targetBuyer\": \"Exact job title to contact (e.g., CTO) and Go-To-Market hook strategy.\",
       \"pricingStrategy\": \"Rough pricing model and estimate (e.g., Fixed $5k-$10k sprint).\",
       \"outreachMessage\": \"Short, aggressive, highly relevant cold email to the Target Buyer to close a discovery call.\"
     }"
3. Frontend (Dashboard): 
   - Parse this resulting JSON and display it in a clean, professional dashboard inside `/products/:id` using Tailwind CSS cards (e.g., a card for MVP Scope, a card for Pricing, and a copy-pasteable box for the Outreach Message).

CONSTRAINTS:
- Write production-ready C# code for the Controllers and Services using Clean Architecture principles.
- Ensure Angular components use proper routing, state management for loading spinners, and typed models for the JSON responses.
- Implement error handling for the Semantic Kernel calls in case the LLM returns invalid JSON.