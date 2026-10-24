# Test Plan and Screenshot Checklist

For every test, take a Postman screenshot that shows: **method + URL**, the **Authorization** tab (which role's token is used), the **status code**, and the **response body**. Put the screenshots in `docs/screenshots/` using the file name given in the table.

Requests match `postman/StudentGradeManagement.postman_collection.json`. The base URL is `http://localhost:5080`.

## A. Postman / API tests

| # | Requirement | Method & endpoint | Role (token) | Body / change | Expected status | Expected body | Screenshot |
|---|---|---|---|---|---|---|---|
| 1 | Login | POST `/api/auth/login` | none | faculty email + password | 200 | `accessToken`, `role: "Faculty"` | `01-login-faculty.png` |
| 2 | Login | POST `/api/auth/login` | none | student email + password | 200 | `role: "Student"`, `studentNumber: "2026-0001"` | `02-login-student.png` |
| 3 | Login failure | POST `/api/auth/login` | none | wrong password | 401 | `detail: "Invalid email or password."` | `03-login-wrong.png` |
| 4 | **Faculty GET** | GET `/api/grades` | Faculty | – | 200 | array of grades with `remarks` | `04-faculty-get-all.png` |
| 5 | Faculty GET one | GET `/api/grades/{id}` | Faculty | – | 200 | one grade | `05-faculty-get-one.png` |
| 6 | Not found | GET `/api/grades/999999` | Faculty | – | 404 | `Grade record 999999 was not found.` | `06-not-found.png` |
| 7 | **Faculty POST** | POST `/api/grades` | Faculty | grade 88 | **201** | `remarks: "SAP"`, `Location` header | `07-faculty-post.png` |
| 8 | Duplicate | POST `/api/grades` | Faculty | same student/subject/term | 409 | duplicate message | `08-duplicate.png` |
| 9 | **Faculty PUT** (remark changes) | PUT `/api/grades/{newId}` | Faculty | grade 88 → 96 | 200 | `remarks: "CMS"` (was SAP), `dateModified` set | `09-faculty-put-cms.png` |
| 10 | PUT again | PUT `/api/grades/{newId}` | Faculty | grade 96 → 74 | 200 | `remarks: "BEP/FAILED"` | `10-faculty-put-failed.png` |
| 11 | **Validation 105** | POST `/api/grades` | Faculty | `"grade": 105` | 400 | `errors.Grade` | `11-validation-105.png` |
| 12 | **Validation -1** | POST `/api/grades` | Faculty | `"grade": -1` | 400 | `errors.Grade` | `12-validation-neg.png` |
| 13 | **Validation non-integer** | POST `/api/grades` | Faculty | `"grade": 85.5` | 400 | `errors.Grade: "…whole number…"` | `13-validation-decimal.png` |
| 14 | Validation text | POST `/api/grades` | Faculty | `"grade": "ninety"` | 400 | `errors.Grade` | `14-validation-text.png` |
| 15 | Validation missing | POST `/api/grades` | Faculty | no `grade` | 400 | `errors.Grade: "Grade is required."` | `15-validation-missing.png` |
| 16 | **Tampering (POST)** | POST `/api/grades` | Faculty | `grade: 60, remarks: "CMS", facultyId: 999` | 201 | `remarks: "BEP/FAILED"`, `facultyId` is the signed-in faculty | `16-tamper-post.png` |
| 17 | **Tampering (PUT)** | PUT `/api/grades/{tamperId}` | Faculty | `grade: 99, remarks: "BEP/FAILED"` | 200 | `remarks: "CMS"` | `17-tamper-put.png` |
| 18 | **Student read** | GET `/api/student/grades` | Student (Juan) | – | 200 | only `studentNumber: "2026-0001"` rows | `18-student-grades.png` |
| 19 | Student profile | GET `/api/student/me` | Student | – | 200 | name + student number | `19-student-me.png` |
| 20 | **Ownership – query** | GET `/api/student/grades?studentId=2` | Student (Juan) | changed query string | 200 | still only Juan's grades | `20-ownership-query.png` |
| 21 | **Ownership – route** | GET `/api/grades/{Maria's gradeId}` | Student (Juan) | another student's id | 404 | not found (existence hidden) | `21-ownership-id.png` |
| 22 | Ownership – URL | GET `/api/student/grades/2` | Student | invented URL | 404 | – | `22-ownership-url.png` |
| 23 | Ownership – list | GET `/api/grades` | Student | – | 403 | Forbidden | `23-student-list-403.png` |
| 24 | **Student POST** | POST `/api/grades` | Student | grade 100 | 403 | Forbidden | `24-student-post-403.png` |
| 25 | **Student PUT** | PUT `/api/grades/{own id}` | Student | grade 100 | 403 | Forbidden | `25-student-put-403.png` |
| 26 | **Student DELETE** | DELETE `/api/grades/{own id}` | Student | – | 403 | Forbidden | `26-student-delete-403.png` |
| 27 | **No token** | GET `/api/grades` | none | – | 401 | "Authentication is required…" | `27-no-token.png` |
| 28 | No token (student route) | GET `/api/student/grades` | none | – | 401 | – | `28-no-token-student.png` |
| 29 | No token (write) | POST `/api/grades` | none | – | 401 | – | `29-no-token-post.png` |
| 30 | Invalid token | GET `/api/grades` | forged token | – | 401 | "The access token is invalid." | `30-invalid-token.png` |
| 31 | **Faculty DELETE** | DELETE `/api/grades/{newId}` | Faculty | – | **204** | empty | `31-faculty-delete.png` |
| 32 | Deleted is gone | GET `/api/grades/{newId}` | Faculty | – | 404 | – | `32-deleted-404.png` |

To capture the whole run at once: Collection Runner → *Run* → screenshot the summary (all tests passed).

## B. Web application checks (live demo)

| # | Check | How | Expected |
|---|---|---|---|
| W1 | Faculty dashboard loads real data | sign in as andrea.garcia@example.com | table filled from `GET /api/grades` (DevTools → Network shows the fetch) |
| W2 | Add grade | **+ Add grade**, grade 91 | success message "91 → SP (remark computed by the server)", new row |
| W3 | Edit grade | **Edit**, change 91 → 70 | row now shows BEP/FAILED |
| W4 | Delete after confirmation | **Delete** → confirm dialog → OK | row removed; Cancel keeps it |
| W5 | Validation in UI | enter 105 or 85.5 | red field message from the API, nothing saved |
| W6 | Student dashboard | sign in as juan.delacruz@example.com | name + student number + only Juan's grades; **no** add/edit/delete controls |
| W7 | Empty state | `INSERT` a new student (e.g. 2026-0004, new email) in SQL, restart API, sign in as that student with `Student#2026` | "No grades have been posted for you yet." |
| W8 | Request failure | stop the API, click **Refresh** on the student page | "Cannot reach the Grade API…" message, no crash |
| W9 | Not authorized | as a student, open https://localhost:7090/Faculty | Access denied page (and the API would return 403 anyway) |
| W10 | Expired session | restart the API (random dev key), click **Refresh** | "Your session has expired… Sign in again" link |

SQL for W7:

```sql
USE StudentGradeManagementDB;
IF NOT EXISTS (SELECT 1 FROM dbo.Students WHERE StudentNumber = N'2026-0004')
    INSERT INTO dbo.Students (StudentNumber, FirstName, LastName, Email)
    VALUES (N'2026-0004', N'Ana', N'Lopez', N'ana.lopez@example.com');
```
