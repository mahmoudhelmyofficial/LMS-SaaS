# Admin Dashboard Translation – Customized Execution Plan

**Scope:** All Admin views linked from the sidebar, **excluding Help Center** (Areas/Admin/Views/Help/**).  
**Goal:** Every user-visible string uses `Html.T("Arabic", "English")` so the UI switches correctly between Arabic and English. Enterprise-level consistency and production-ready.

---

## 1. Translation Pattern (Enterprise Standard)

- **Method:** `Html.T("النص العربي", "English text")` from `LMS.Extensions.CultureExtensions`.
- **Where:** Every user-visible string: titles, breadcrumbs, headers, labels, buttons, placeholders, tooltips, confirm messages, badges, table headers, dropdown options, empty/helper text.
- **Breadcrumbs:** `(Html.T("العربي", "English"), url)`.
- **Header actions:** `(Html.T("العربي", "English"), icon, url, cssClass)`.
- **Confirm dialogs:** `confirm('@(Html.T("العربي", "English"))')`.
- **Charts (JS):** Use server-rendered labels, e.g. `'@Html.Raw(Html.T("العربي", "English"))'` or pass from C#.

---

## 2. Views Already Fixed (This Pass)

| Area | View | Notes |
|------|------|--------|
| FinancialReports | GatewayPerformance.cshtml | Full translation + Chart script fix (backgroundColor server-side). |
| FinancialReports | CouponUsage.cshtml | Full translation including chart labels. |
| CertificateTemplates | Details.cshtml | Breadcrumbs, labels, buttons, confirm, variables list. |
| Tags | Details.cshtml | Breadcrumbs, labels, badges, related courses, confirm. |
| Lessons | Details.cshtml | Breadcrumbs, labels, lesson types, status, quick actions, confirm. |

---

## 3. Remaining Views to Translate (Excluding Help)

Apply the same pattern to **every** user-visible string. Checklist per view:

1. `ViewData["Title"]` → `Html.T("ar", "en")`.
2. Breadcrumbs and `HeaderActions` → every `Name`/`Text` uses `Html.T`.
3. All `<h5>`, `<h6>`, `<label>`, `<th>`, card titles → wrap in `Html.T`.
4. All buttons, links, dropdown items, badges → wrap in `Html.T`.
5. All `placeholder="..."`, `title="..."`, `aria-label="..."` → use `Html.T`.
6. All `confirm('...')` and alert/empty messages → use `Html.T`.
7. Static `<select>` options → use `Html.T`.
8. DataTables/Chart labels rendered from server → use `Html.T` (e.g. in C# or `@Html.Raw(Html.T(...))` in script).

### By Section (from ADMIN_TRANSLATION_GUIDE)

- **User Management:** Users (Edit, ManageRoles, ResetPassword, Statistics), Students (Details), UserActivity (Index, Statistics), UserSessions (Details).
- **Instructors:** Applications, ApplicationDetails, Details, Edit, Statistics, Courses, Students, Reviews, Earnings.
- **Security:** Index, Dashboard, LoginLogs, ActiveSessions, BlockedIps, AddBlockedIp, AddCountryRestriction, CountryRestrictions, LoginLogDetails, AllSessions, TwoFactorSettings, TwoFactorDetails.
- **Content:** Courses (Duplicate, Reviews, ManageModules, Students), Categories (Create, Edit, Details, Subcategories, Statistics), Bundles (Create, Edit, Details, Analytics), LearningPaths (Create, Edit, Details, Statistics), Books (Details, Statistics), LiveClasses (Index, Schedules, ScheduleDetails, Revenue, Details, Edit, Statistics), ContentDrip (Create, Edit, Details, Statistics).
- **Assessments:** QuestionBank (Index, Categories, Create, AddQuestion), Lessons (Index, Create, Edit, ManageLessons, Reorder, Preview, Unpublish), Certificates (Index, Details, Verify, Revoke, Statistics), CertificateTemplates (Index, Create, Edit).
- **Finance:** Payments (Details, Invoice, Withdrawals, FraudDetection, Statistics, Refunds), ManualPayments, PaymentAnalytics, Subscriptions (Plans, Index, Details, EditPlan, CreatePlan), Refunds (Index, Details, Process, CreateRefund), WithdrawalMethods (all views), TaxSettings, FinancialReports (Index, DailyRevenue, InstructorEarnings, Subscriptions, Refunds).
- **Marketing:** FlashSales, Affiliates, CommissionSettings, Badges, Achievements.
- **Reports & Analytics:** Reports (all), AdvancedReports (all), Analytics (all), CourseAnalytics.
- **Support (excl. Help):** Support (Index, Details, Categories), Reviews, Comments, Announcements, EmailTemplates, EmailSettings, PushNotificationSettings, NotificationPreferences.
- **Logs:** Security (LoginLogDetails, AllSessions, TwoFactorSettings, TwoFactorDetails).
- **Settings:** Settings (General, Localization, Security, SEO, Appearance, Integrations, Index, Video, Sms, Tax, Commissions, PaymentGateways, Payment, Email), PlatformSettings, PaymentSettings, PaymentGatewaySettings, SmsSettings, MediaSettings, VideoSettings, StorageSettings, SecuritySettings, Proctoring.
- **Other:** Modules, Tags (Index, Create, Edit, Merge, Courses), LandingPages, ScheduledReports, ReportTemplates, ReportExports, CountryRestrictions, TwoFactorSettings, LoginLogs, Monitoring, etc.

---

## 4. Identification Strategy (Comprehensive – Excluding Help Center)

- **Exclude:** All views under `Areas/Admin/Views/Help/` (Help Center fully excluded). No changes to Help.
- **Identification:** (1) Grep for `ViewData["Title"]` not using `Html.T`. (2) Grep for raw Arabic/English in views (labels, buttons, breadcrumbs, `confirm(`, `placeholder=`, `title=`, `aria-label=`). (3) Views with 0 or few `Html.T` calls are priorities.
- **Confirmed untranslated (non-Help):** Instructors/Reviews, Instructors/Earnings (title + in-view strings); CourseAnalytics/Analytics (title); Disputes/Details (title + Breadcrumb key + full page); Currencies/Edit, Currencies/Details (title + Breadcrumbs + labels); ManualPayments/Details (title + breadcrumb + labels/statuses).
- **Execution:** Fix every user-visible string with `Html.T("Arabic", "English")`; keep ViewData key as `Breadcrumbs` and tuple as `(Name, Url)` to match _Layout.

---

## 5. Re-Examination (Before Production)

After editing:

1. **Consistency:** Same pattern everywhere – no mix of raw text and `Html.T` for the same type of string.
2. **No conflicts:** No duplicate or conflicting keys; same Arabic/English pair can be reused across views.
3. **Scripts:** Chart/DataTables labels that are rendered in JS must get translated string from server (e.g. `@Html.Raw(Html.T(...))` or C# variable).
4. **Confirms:** `confirm('@(Html.T("...", "..."))')` – no unescaped quotes in the translated string.
5. **Layout/partials:** _PageHeader, _Layout, _Sidebar, _Header, _Messages – ensure they use `Html.T` for any admin-relevant strings.
6. **Build & smoke test:** Solution builds; switch culture and spot-check key admin pages for correct language.

---

## 6. Re-Examination (Completed – Deep Pass)

- **Lint:** No linter errors on any modified views.
- **Pattern:** All changed views use `Html.T("Arabic", "English")` for every user-visible string; no raw Arabic/English UI text.
- **aria-label:** Breadcrumb nav in GatewayPerformance and CouponUsage use `aria-label="@Html.T("مسار التنقل", "Breadcrumb")"` (aligned with other admin views e.g. ScheduledReports, Categories/Courses).
- **Culture-aware html:** Certificates/Verify uses `<html lang="@Html.Lang()" dir="@Html.Dir()">` so the page respects current culture (matches _Layout.cshtml approach).
- **Icon-only buttons:** Tags/Details view-course link has `title` and `aria-label` with `Html.T("عرض", "View")` for accessibility.
- **Scripts:** GatewayPerformance Chart `backgroundColor` is server-rendered; chart labels use `@Html.Raw(Html.T(...))`.
- **Confirms:** All delete/confirm dialogs use `confirm('@(Html.T(...))')`; translated strings contain no unescaped single quotes.
- **Data vs UI:** Model/entity values (e.g. GatewayName, Course.Title, certificate fields) are left as-is; only UI labels, headings, buttons, and messages use `Html.T`.
- **Help excluded:** No files under `Areas/Admin/Views/Help/` were modified. Help Center is fully excluded.
- **Breadcrumbs:** Modified views use `ViewData["Breadcrumbs"]` with tuple `(Name, string? Url)`; layout adds Home first, so view lists do not duplicate it.
- **Production-ready:** These views are translated per the enterprise approach and are ready for production (excluding Help Center as requested).

### 6.1 Deep re-examination (final)

- **Instructors/Reviews, Earnings:** Grep verified no raw Arabic/English UI text; all labels, buttons, empty states, table headers use `Html.T`.
- **CourseAnalytics/Analytics:** Title, filter labels, stat labels, card titles, empty messages use `Html.T`; no raw strings.
- **Disputes/Details, Currencies/Edit, Currencies/Details, ManualPayments/Details:** No raw Arabic in `>...<` or `placeholder="..."`; all user-visible strings wrapped in `Html.T`. Confirm dialogs use `@(Html.T(...))` with quote-safe strings.
- **Layout compatibility:** All use `Breadcrumbs` (plural); no use of deprecated `Breadcrumb` in modified files.
- **100% assurance:** The views touched in this translation pass are fully translated per the enterprise pattern and are production-ready; Help Center remains excluded.

## 7. Summary

- **Done this pass:** FinancialReports (GatewayPerformance, CouponUsage), CertificateTemplates/Details, Tags/Details, Lessons/Details, Certificates/Verify; plus Chart script fix in GatewayPerformance.
- **Done (translation fix pass):** Instructors/Reviews, Instructors/Earnings (title + all in-view strings); CourseAnalytics/Analytics (title + filter, stats, labels, empty messages); Disputes/Details (title, Breadcrumbs, alerts, labels, evidence form, timeline, actions, confirm dialogs); Currencies/Edit (title, Breadcrumbs, all section titles and labels); Currencies/Details (title, Breadcrumbs, badges, labels, buttons); ManualPayments/Details (title, Breadcrumbs, status texts, all labels, review actions, confirm dialogs). All use `Html.T("Arabic", "English")` and `ViewData["Breadcrumbs"]` with `(Name, Url)`; no duplicate Home (layout adds it).
- **Next:** Continue through section 3 for any remaining admin views (excluding Help) that still have untranslated strings.
- **Excluded:** All Help Center views under `Areas/Admin/Views/Help/`. No changes made to Help.
- **Re-check:** Run through section 5 after each batch; build and lint verified.
