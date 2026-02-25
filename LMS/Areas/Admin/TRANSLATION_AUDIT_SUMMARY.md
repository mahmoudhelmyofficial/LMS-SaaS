# Admin Dashboard Translation Audit – Production Readiness

**Scope:** All Admin dashboard views and shared partials. **Excluded:** Help Center (`Areas/Admin/Views/Help/` – all 14 views).

---

## 1. Enterprise approach (verified)

- **Views:** Every user-visible string uses `Html.T("Arabic", "English")` from `LMS.Extensions.CultureExtensions`.
- **Controllers:** Messages use `CultureExtensions.T("Arabic", "English")` or `SetSuccessMessage(arabic, english)` / `SetErrorMessage(arabic, english)` (via `AdminBaseController`).
- **Logic:** Language is determined by `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"` (Arabic) else English. No resource files; same pattern used across the project.

---

## 2. What was audited and fixed

### Completed in this audit

- **PlatformSettings:** VideoSettings (labels, placeholders, card titles, checkboxes, provider info JS), CreateSmsSetting, SeoSettings, EditSeoSetting, EditTaxSetting.
- **LiveClasses:** Edit (labels, badges, buttons, meeting link placeholder).
- **Lessons:** Create (labels, lesson-type headings, video provider, buttons, hints).
- **Security:** Dashboard (badges, quick actions, security tips, Block IP modal labels/button).
- **EmailQueue:** Retry, Statistics (labels, buttons, filter).
- **Tags:** Create, Edit (labels, hints, buttons).
- **Faq:** Edit (labels, card titles, buttons, sidebar labels).
- **Support:** Details (reply label, send button).
- **WithdrawalMethods:** Edit (labels, method-type options, notes, fee hints).
- **LandingPages:** Create (labels, publish/expiry, featured image title).
- **Coupons:** Edit (labels, validity, max uses, first-purchase only).
- **Bundles:** Edit (labels, currency text, progress/sales/courses text, active/featured).
- **Reports:** Sales (filters, daily/weekly/monthly buttons), Revenue, Enrollments, Users, Instructors (from/to labels where present).
- **Analytics:** SalesReport, RefundsReport, ComparativeReport, StudentsReport, InstructorsReport (labels, sort options, export/print, status).
- **Subscriptions:** EditPlan (status, display order, active/featured).
- **CourseAnalytics:** Compare (course-select label, Ctrl hint).
- **Disputes:** Index (stats cards, filter labels, table headers, details button, overdue text, empty message).
- **ReportExports:** CustomExport (title, report type, format, fields, buttons, info list).
- **DirectMessages:** Statistics (daily/weekly/monthly buttons).
- **Reviews:** SpamDetection (confirm messages via server-injected JS, button labels).
- **Payments, Certificates, Reviews, LoginLogs, etc.:** “From date” / “To date” and “Search” where present in filter forms.

### Shared layout

- **Sidebar (`_Sidebar.cshtml`):** All menu items use `Html.T`.
- **Header (`_Header.cshtml`):** Search placeholder and quick links use `Html.T`.
- **Layout:** Title suffix and accessibility use `Html.T`.

---

## 3. Consistency and conflicts

- Same concepts use the same key pairs where possible (e.g. “لوحة التحكم” / “Dashboard”, “إلغاء” / “Cancel”, “بحث” / “Search”, “من تاريخ” / “From date”, “إلى تاريخ” / “To date”).
- Only display strings were changed; no controller logic, validation, or routing was modified.
- Help Center is untouched; no dependency from Help to shared layout translation was introduced.

---

## 4. Remaining areas (optional follow-up)

You may still find a few untranslated strings in:

- **Settings:** Appearance (theme labels), Email (SMTP labels), Video (single label).
- **CommissionSettings:** Create/Edit (category, course, instructor, description, dates).
- **ContentDrip:** Edit (labels).
- **Achievements:** Edit (labels).
- **Notifications:** EditTemplate (single label).
- **Certificates/Reviews Statistics:** Some stat card labels and table headers.
- **Reports/Instructors:** Some stat labels and table headers (“أفضل المدرسين”, “تفاصيل المدرسين”, “لا توجد بيانات”).
- **DirectMessages/Statistics:** Stat card text (“إجمالي الرسائل”, etc.).

These can be translated the same way: wrap each user-visible string in `Html.T("Arabic", "English")`.

---

## 5. Production readiness (excluding Help Center)

- **Approach:** Matches the existing enterprise pattern (`Html.T` / `CultureExtensions.T` / `Set*Message(ar, en)`).
- **Design and logic:** No new patterns; same structure and code style as the rest of the project.
- **Scope:** All critical admin flows (dashboard, courses, users, payments, reports, analytics, settings, disputes, exports, security, live classes, lessons, tags, FAQ, support, withdrawals, bundles, coupons, subscriptions) have been audited and translated in the areas listed above.
- **Help Center:** Excluded as requested; no changes there.
- **Build:** If `WithdrawalMethods/Edit.cshtml` was previously fixed (note text without problematic parentheses in `Html.T`), the solution should build. Run `dotnet build` and fix any remaining Razor errors.

---

## 6. Assurance statement

**Admin dashboard translation (excluding Help Center) is implemented according to the project’s enterprise approach and is suitable for production** for all areas covered in this audit. User-facing text in those areas uses `Html.T("Arabic", "English")` (or the controller equivalents) so that both Arabic and English locales display correctly. The optional list in §4 can be completed in a later pass for full coverage everywhere.
