# Few-Shot Prompting for Data Model Navigation

Our strategy to help the AI (Gemini) accurately translate natural language into SQL is to use few-shot prompting. This involves including a small set of well-crafted example questions and their correct SQL translations directly within the prompt we send to the model.

This approach addresses two key challenges:

## 1. Improving Data Model Understanding

Simply giving the LLM the raw database schema (table names, column names, and data types) only provides a static map of the data. The few-shot examples provide context and demonstrate relationships that aren't explicit in the schema alone:

*   **Demonstrating Joins:** An example like "Which players were in the 'A Grade' in the 2024 season?" and its corresponding SQL (which likely involves joining the Players table, the Grades table, and possibly a Seasons table) clearly shows the LLM how to traverse the database relationships.

*   **Illustrating Filter Conditions:** Examples show the LLM the common values it should look for in specific columns (e.g., how to reference a specific grade like 'B Grade' or a season year like '2024').

*   **Handling Aggregations:** Examples that require `COUNT()`, `SUM()`, or `AVG()` demonstrate how to combine data from multiple rows and use `GROUP BY` clauses, which is often difficult for LLMs without explicit instruction.

## 2. Refining the Output Format and Intent

The examples serve as a template, guiding the LLM to output the SQL in the exact format we need. They reinforce the primary goal:

*   **Explicit SQL Intent:** The model learns that the desired output is only a valid SQL statement, not conversational text or explanations.

*   **Preferred Query Style:** You can subtly guide the model towards using specific SQL syntax or best practices (e.g., using table aliases or preferring `INNER JOIN` over older join syntax).

## Practical Implementation Steps

In the prompt sent to the Gemini API, you will combine your elements in a structured way:

*   **System Instruction / Goal:** Clearly instruct the model that its role is to act as a SQL translator and only output SQL.

*   **Data Model (Schema):** Include the `CREATE TABLE` statements or a condensed, clear representation of your tables, columns, and primary/foreign keys.

*   **Few-Shot Examples (Input/Output Pairs):** This is where you add your curated questions and SQL:

    ```
    User Question: "List all teams in the 2023 season's B Grade."

    SQL Output: SELECT T.team_name FROM Teams T JOIN Season_Grades SG ON T.team_id = SG.team_id WHERE SG.season_year = 2023 AND SG.grade_name = 'B Grade';
    ```

    Repeat this pattern with 3-5 high-quality examples.

*   **New User Query:** The actual question you want the LLM to answer (e.g., "Which player scored the most goals in 2024?").

By structuring the prompt this way, you give the LLM the data context, the task instruction, and working examples—significantly improving its ability to generate accurate and complex SQL.