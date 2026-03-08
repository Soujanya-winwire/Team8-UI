1️⃣ Standard Expectations of Data-Driven Testing (Framework Side)

These are the technical capabilities a good DDT framework should provide.

1. External Test Data Source

Test data should be stored outside the test scripts so that tests are reusable.

Common sources:

Excel

CSV

JSON

YAML

Database

API

Google Sheets

Example:

username,password,expected_result
user1,pass1,Success
user2,pass2,Failure

2. Single Test Logic, Multiple Data Sets

The same test should run multiple times using different input data.

Example:

Login Test
Run 1 → user1/pass1
Run 2 → user2/pass2
Run 3 → user3/pass3


This avoids writing multiple duplicate tests.

3. Parameterization

Test steps should accept parameters instead of hardcoded values.

Example:

Enter Username → ${username}
Enter Password → ${password}

4. Automatic Iteration

The framework should automatically loop through rows of data.

Example:

Row 1 → Run Test
Row 2 → Run Test
Row 3 → Run Test

5. Data Mapping

Columns from the data file should map clearly to test step parameters.

Example:

Column	Test Step
username	Enter Username
password	Enter Password
expected_result	Validate Login
6. Result Tracking per Data Row

Each dataset should produce a separate result.

Example Report:

Login Test
✔ user1/pass1 → Passed
❌ user2/pass2 → Failed
✔ user3/pass3 → Passed

7. Error Handling

Framework should:

Skip invalid rows

Report missing data

Handle null values

8. Data Isolation

Each iteration should run independently.

Failure in one dataset should not stop other datasets.

9. Scalability

Framework should support:

Large datasets

Parallel execution

Multiple test environments

10. Reusability

Same dataset should be reusable across multiple tests.

Example:

UserData.xlsx
Login Test
Profile Test
Checkout Test