Act as a Senior .NET Data Engineer and Software Architect. I need to build a highly resilient web scraper to extract product complaints and market gaps from AppSumo. 

The goal is to navigate through categories, enter each product page, filter the reviews to extract only the 1, 2, and 3 "Taco" ratings, save the data, and systematically proceed to the next product and category.

### 1. The Workflow Rules:
1. Parse local files named `APPSUMO_*.html` to understand the initial DOM structure if needed.
2. Target URL: https://appsumo.com/software/
3. Extract categories from the sidebar menu. Example HTML structure provided below.
4. Iterate through every category URL.
5. In each category, iterate through every product listed.
6. Enter the product page (e.g., https://appsumo.com/products/tidycal/).
7. Locate the review section and interact with the "Tacos" dropdown. Example HTML structure for the dropdown is provided below.
8. Filter and extract ONLY the reviews with 3, 2, and 1 Tacos (these contain the user complaints and feature requests).
9. Save the extracted review details (Product Name, Rating, Review Text, User, Date).
10. Come back, proceed to the next product. When finished with the page/category, proceed to the next category.

### 2. HTML Reference Data:
**Category Menu Structure:**
<ul><li class="my-1"><a href="/software" target="_self" class="block font-bold underline hidden">Software</a><ul class=""><li class="my-1"><a href="/software/operations" target="_self" class="block font-bold underline">Operations</a><ul class="pl-4"><li><a href="/software/operations/calendar-scheduling" target="_self" class="block ">Calendar &amp; scheduling</a></li>...</ul></li><li class="my-1"><a href="/software/marketing-sales" target="_self" class="block ">Marketing &amp; sales</a></li>...</ul>

**Taco Filter Dropdown Structure:**
<div class="flex items-center justify-between"><span class=""><span class="flex items-center">All tacos</span></span><svg aria-hidden="true" focusable="false" data-prefix="fas" data-icon="angle-down" class="svg-inline--fa fa-angle-down ml-1 transition duration-150" role="img" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512"><path fill="currentColor" d="M201.4 374.6c12.5 12.5 32.8 12.5 45.3 0l160-160c12.5-12.5 12.5-32.8 0-45.3s-32.8-12.5-45.3 0L224 306.7 86.6 169.4c-12.5-12.5-32.8-12.5-45.3 0s-12.5 32.8 0 45.3l160 160z"></path></svg></div>

### 3. Required Outputs:

**OUTPUT A: The Master Plan (Markdown File)**
Create a comprehensive `.md` file detailing the scraping architecture using C# and Playwright (since AppSumo is heavily dynamic). Include:
- Project structure.
- Strategy for handling rate limits, bot detection, and dynamic JS loading.
- Logic for the nested loops (Category -> Product -> Pagination/Reviews).

**OUTPUT B: SQL Server Database Schema (T-SQL)**
Create the T-SQL script to store this data robustly.
- Check if tables exist before creating them to avoid affecting existing databases