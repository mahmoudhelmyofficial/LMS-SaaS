# Instructor Dashboard Translation Guide (AR/EN)

All instructor dashboard views linked from the sidebar **excluding Help Center** must use **no untranslated text**. Every user-visible string must use the `Html.T("Arabic", "English")` helper so the UI switches correctly between Arabic and English based on culture.

## Pattern (same as Admin/Student)

- **Page title:** `ViewData["Title"] = Html.T("النص العربي", "English Text");`
- **Breadcrumbs:** `(Html.T("العربي", "English"), url)`
- **Headers/labels:** `<h5>@Html.T("العربي", "English")</h5>` or `<label>@Html.T("...", "...")</label>`
- **Buttons/links:** `<span>@Html.T("العربي", "English")</span>`
- **Placeholders:** `placeholder="@Html.T("العربي", "English")"`
- **Tooltips/titles:** `title="@Html.T("العربي", "English")"`
- **Confirm messages:** `confirm('@(Html.T("العربي", "English"))')`
- **Badges/status:** `<span class="badge">@Html.T("العربي", "English")</span>`
- **Table headers:** `<th>@Html.T("العربي", "English")</th>`
- **Dropdown options:** `<option>@Html.T("العربي", "English")</option>`
- **Empty/helper text:** `<p class="text-muted">@Html.T("العربي", "English")</p>`
- **JS strings:** Inject view-specific keys into `window.__T` and use `(window.__T && window.__T.Key) || 'Fallback'` in alerts/confirms.

## Excluded

- **Help Center:** All views under `Areas/Instructor/Views/Help/` are excluded from this translation requirement.

## Scope

- **Sidebar:** Already uses `Html.T` for all menu items.
- **Layout:** Title and meta use `Html.T`.
- **Shared:** _PageHeader, _Sidebar use `Html.T`; breadcrumb items must be passed with translated text.

## Views to be translated (all sidebar-linked areas except Help)

- Dashboard, Courses (Index, Create, Edit, Details, Draft, Published, Wizard steps), Modules, Lessons, LessonResources
- LiveClasses, LiveSessionSchedules, Recordings, LiveClassAttendance
- QuestionBank, Quizzes, QuizAttempts, Assignments, AssignmentSubmissions
- Books, ContentDrip, LearningPaths, Bundles, Resources, Documents, MediaLibrary
- Students, Progress, Submissions, Messages, Discussions, Comments, Reviews, Announcements, Faq
- CourseInstructors, Proctoring, Analytics, Earnings, Commissions, WithdrawalRequests, Coupons, FlashSales, Affiliates
- Profile (Index, Edit, Settings, PaymentSettings, Security, Activity, Notifications, TwoFactorSetup, SocialLinks), Settings
- Application (Index, Details, Status, NoApplication, Documents, History)

## Views fully translated (this pass; excluding Help Center)

- **WithdrawalRequests:** Details (titles, labels, status, timeline, request info, buttons).
- **Courses:** Index, Details (stats, tabs, content, lessons, modules), Edit (completion, missing items).
- **Profile:** Edit (completion checklist, quick actions, tips, cancel).
- **Application:** Details (status labels and descriptions).
- **Books:** Details (alerts, stats, this month, description, chapters, reviews, price), Edit (chapters, add chapter modal, submit/status messages).
- **Earnings:** Transactions (export section).
- **Bundles:** Edit (current statistics).
- **Coupons:** Details (inactive/expired/exhausted alerts), Edit (quick info).
- **Modules:** Edit (placeholder).
- **Progress:** Course (search placeholder).
- **Students:** Details (progress overview).
- **CourseInstructors:** Edit, Add (permissions heading), SelectCourse (manage co-instructors).
- **Comments:** Index (reply modal title), Details (comment text, reply, edit/delete reply, edit modal title).
- **Reviews:** Report (report received, review, action, appropriate action).
- **MediaLibrary:** Upload (drag/drop heading).
- **Documents:** Upload (access and sharing heading).
- **Quizzes:** Create (additional options).
- **Messages:** BulkMessage (heading).
- **Announcements:** Create (preview title).
- **LearningPaths:** Statistics (monthly revenue heading).
- **Wizard:** _Step3_ContentBuilder (add new rule heading).

## Quick checklist per view

1. `ViewData["Title"]` and `ViewData["Subtitle"]` → use `Html.T("ar", "en")`.
2. Breadcrumbs and header action buttons → every visible text uses `Html.T`.
3. All `<h1>`–`<h6>`, `<label>`, `<th>`, card titles → wrap in `Html.T`.
4. All buttons, links, dropdown items, badges → wrap in `Html.T`.
5. All user-facing `placeholder="..."`, `title="..."`, `aria-label="..."` → use `Html.T` (technical placeholders like `https://...` or numeric examples can stay or use `Html.T` for hint text).
6. All `confirm('...')` and alert/empty messages → use `Html.T` or `window.__T`.
7. Static `<select>` options → use `Html.T`.
