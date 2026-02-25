# Admin Dashboard Translation Guide (AR/EN)

All admin views linked from the sidebar (excluding Help Center) must use **no untranslated text**. Every user-visible string must use the `Html.T("Arabic", "English")` helper so the UI switches correctly between Arabic and English based on culture.

## Pattern

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

## Views fully translated (no word left untranslated)

- **Dashboard:** Index
- **Users:** Index, Create, Details
- **Students:** Index
- **UserSessions:** Index (including modal)
- **Security:** Roles, Permissions
- **Courses:** Index, Details, Create, Edit, **Pending**, **Featured**
- **Categories:** Index, **Subcategories**
- **Bundles:** Index (full)
- **LearningPaths:** Index (full)
- **Books:** Index, **PendingReview** (full)
- **ContentDrip:** Index (full)
- **Instructors:** Index, **Statistics** (full)
- **LiveClasses:** **Active** (full)
- **Certificates:** Index, **Verify** (full), **CertificateTemplates:** Index, **Details** (full)
- **Payments:** Index (+ ر.س), **ManualPayments:** Index (full), **PaymentAnalytics:** Index (full)
- **Subscriptions:** Plans (already using Html.T for main strings)
- **Sidebar:** All menu items + SEO label use Html.T
- **FinancialReports:** **GatewayPerformance** (full), **CouponUsage** (full)
- **Tags:** **Details** (full)
- **Lessons:** **Details** (full)
- **Instructors:** **Reviews** (full), **Earnings** (full)
- **CourseAnalytics:** **Analytics** (full)
- **Disputes:** **Details** (full)
- **Currencies:** **Edit** (full), **Details** (full)
- **ManualPayments:** **Details** (full)
- **Courses:** **Edit** (full – confirm, labels, options, free course)
- **Payments:** **Details** (full – status, table, buttons, timeline, JS prompts/alerts)
- **Modules:** **Edit**, **Create**, **Details** (full)
- **Lessons:** **Edit** (full – card titles, options, placeholders, buttons)
- **Faq:** **Details** (confirm/alert via __FaqT)
- **Documents:** **Details** (confirm/alerts via __DocT)
- **LandingPages:** **Create** (alert)
- **Settings:** **Email** (alert), **Index** (.NET Version label)
- **Achievements:** **Details** (confirm, labels)
- **PaymentGatewaySettings:** **Index** (full – alerts, labels, region cards)
- **Categories:** **Create** (full)
- **ContentDrip:** **Create** (full)
- **EmailTemplates:** **Create**, **Test** (full)
- **Coupons:** **Create** (full)
- **Notifications:** **CreateTemplate** (full)
- **SecuritySettings:** **Index** (placeholder)
- **Media:** **Statistics** (full)

### Translation pass (full admin coverage – excluding Help Center)

The following views were updated for full translation (placeholders, labels, JS alerts/confirms, options, tips):

- **SmsTemplates:** Edit (placeholders).
- **AdvancedReports:** CreateTemplate (labels, placeholders, report type cards, report settings title).
- **PlatformSettings:** CreateSeoSetting (full – headings, labels, placeholders, tips, buttons, options).
- **LiveClasses:** Details, Index (alert strings with proper JS escaping).
- **SmsSettings:** CreateSmsSetting (placeholders, notes; provider cards and form labels).
- **VideoSettings:** CreateVideoSetting (notes placeholder).
- **CountryRestrictions:** Create (reason placeholder).
- **Reviews:** Details (rejection reason placeholder, student label).
- **LearningPaths:** Create (placeholders, level/category options, preview labels, empty message).
- **Security:** AddBlockedIp (placeholders, labels, block types, tips, stats labels).
- **Currencies:** Create (placeholders).
- **TaxSettings:** CreateTaxSetting (placeholders, labels, tax type options, small text).
- **Faq:** Create (category options, tips, placeholders).
- **PushNotificationSettings:** Index (enable label, JS: window.__PushIndexT for generating/sending/success/error, button loading text).
- **WithdrawalMethods:** Edit, Create (placeholders).
- **Categories:** Create (preview text, no-parent option).
- **UserSessions:** AllSessions (confirm/alert with proper JS escaping).

See **ADMIN_TRANSLATION_PLAN.md** for the full execution plan and remaining views list.

## Sidebar-linked areas – remaining views to translate

Apply the same pattern to every user-visible string in:

| Section | Controllers | Views to check |
|--------|-------------|----------------|
| User Management | Users | Edit, ManageRoles, ResetPassword, Statistics |
| | Students | Details |
| | UserActivity | Index, Statistics |
| | UserSessions | Details |
| Instructors | Instructors | Applications, ApplicationDetails, Details, Edit, Statistics |
| Security | Security | Index, Dashboard, LoginLogs, ActiveSessions, BlockedIps, AddBlockedIp, AddCountryRestriction, CountryRestrictions, Permissions (done), Roles (done) |
| Content | Courses | Pending, Featured, Duplicate, Reviews, ManageModules |
| | Categories | Create, Edit, Details, Subcategories, Statistics |
| | Bundles | Create, Edit, Details, Analytics |
| | LearningPaths | Create, Edit, Details, Statistics |
| | Books | Details, PendingReview, Statistics |
| | LiveClasses | Index, Active, Schedules, ScheduleDetails, Revenue, Details, Edit, Statistics |
| | ContentDrip | Create, Edit, Details, Statistics |
| Assessments | QuestionBank | Index, Categories, Create, AddQuestion |
| | Lessons | Index, Create, Edit, Details, ManageLessons, Reorder, Preview, Unpublish |
| | Certificates | Index, Details, Verify, Revoke, Statistics |
| | CertificateTemplates | Index, Create, Edit, Details |
| Finance | Payments | Index, Details, Invoice, Withdrawals, FraudDetection, Statistics, Refunds |
| | ManualPayments | Index |
| | PaymentAnalytics | Index |
| | Subscriptions | Plans, Index, Details, EditPlan, CreatePlan |
| | Refunds | Index, Details, Process, CreateRefund |
| | WithdrawalMethods | Index, Requests, RequestDetails, Details, Create, Edit, Statistics |
| | TaxSettings | Index, CreateTaxSetting, EditTaxSetting |
| | FinancialReports | Index, DailyRevenue, InstructorEarnings, Subscriptions, Refunds, CouponUsage, GatewayPerformance |
| Marketing | FlashSales | Index, Create, Edit, Details |
| | Affiliates | Index, Commissions |
| | CommissionSettings | Index, Create, Edit, Details, Preview |
| | Badges | Index, Create, Edit, Details |
| | Achievements | Index, Create, Edit, Details |
| Reports & Analytics | Reports | Index, Financial, Students, Enrollments, CoursePerformance, Sales, Revenue, Courses, Users, Instructors |
| | AdvancedReports | Index, Exports, Templates, ScheduledReports, CreateTemplate, EditTemplate, CreateScheduledReport, EditScheduledReport |
| | Analytics | Index, UserActivity, CoursePerformance, ComparativeReport, InstructorsReport, RefundsReport, StudentsReport, SalesReport, EnrollmentsReport, CategoriesReport |
| | CourseAnalytics | Index, Details, Compare |
| Support | Support | Index, Details, Categories |
| | Reviews | Index, Pending, Details, Reply, SpamDetection, Statistics |
| | Comments | Index, Details, Edit |
| | Announcements | Index, Create, Edit, Details |
| | EmailTemplates | Index, Create, Edit, Details, DuplicateConfirm, Preview, Test |
| | EmailSettings | Index, Logs |
| | PushNotificationSettings | Index, Details |
| | NotificationPreferences | Index, Details |
| Logs | Security | LoginLogDetails, AllSessions, TwoFactorSettings, TwoFactorDetails |
| Settings | Settings | General (title), Localization, Security, SEO, Appearance, Integrations, Index, Video, Sms, Tax, Commissions, PaymentGateways, Payment, Email |
| | PlatformSettings | Index, CreateSmsSetting, SmsSettings, CreateVideoSetting, CreateTaxSetting, CreateSeoSetting, etc. |
| | PaymentSettings | Index, Currencies |
| | PaymentGatewaySettings | Index, Create, Edit |
| | SmsSettings | Index, CreateSmsSetting |
| | MediaSettings | Index |
| | VideoSettings | Index, CreateVideoSetting |
| | StorageSettings | Index, Analysis |
| | SecuritySettings | Index |
| | Proctoring | Index, ActiveExams, Reports, Violations |

## Quick checklist per view

1. `ViewData["Title"]` → use `Html.T("ar", "en")`.
2. Breadcrumbs and `HeaderActions` → every `Name`/`Text` uses `Html.T`.
3. All `<h5>`, `<h6>`, `<label>`, `<th>`, card titles → wrap in `Html.T`.
4. All buttons, links, dropdown items, badges → wrap in `Html.T`.
5. All `placeholder="..."`, `title="..."`, `aria-label="..."` → use `Html.T`.
6. All `confirm('...')` and alert/empty messages → use `Html.T`.
7. Options in `<select>` (when static) → use `Html.T`.
8. DataTables: filter/search text that is rendered from server → use variables from `Html.T` in script.

Excluding **Help** area and **Help** controller views as requested.
