# Admin Dashboard Translation – Deep Re-Examination & Production Assurance

**Scope:** All Admin dashboard views, **excluding Help Center** (`Areas/Admin/Views/Help/`).  
**Date:** Re-examination for 100% enterprise alignment and production readiness.

---

## 1. Enterprise Approach (Verified)

| Element | Standard | Status |
|--------|----------|--------|
| **Views** | Every user-visible string uses `Html.T("Arabic", "English")` from `LMS.Extensions.CultureExtensions`. | ✅ Applied |
| **Language** | `CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar"` → Arabic; else English. | ✅ No resource files; single T contract. |
| **Controllers** | `SetSuccessMessage(ar, en)`, `SetErrorMessage(ar, en)`, etc. on `AdminBaseController`; or `CultureExtensions.T(ar, en)` for interpolated messages. | ✅ Documented in TRANSLATION_AUDIT.md |
| **Breadcrumbs** | `ViewData["Breadcrumbs"]` as `List<(string Name, string? Url)>`; every `Name` uses `Html.T`. | ✅ Layout expects this; no duplicate Home. |
| **Header actions** | `ViewData["HeaderActions"]` as `List<(string Text, string Icon, string Url, string? CssClass)>`; every `Text` uses `Html.T`. | ✅ |
| **Confirm/alert (JS)** | Server-injected translated strings via `window.__*T` or `@Html.Raw(Html.T(...).Replace("'", "\\'"))` to avoid quote breaks. | ✅ Applied in all touched views. |

---

## 2. What Was Re-Verified and Fixed in This Pass

### 2.1 Consistency and safety

- **CommissionSettings/Edit.cshtml** – Confirm dialog now uses `.Replace("'", "\\'")` for consistency and safety against apostrophes in translated text.
- **Payments/FraudDetection.cshtml** – All user-facing JS strings translated:
  - `window.__FraudT`: prompt (suspend reason), alerts (user suspended, payment blocked).
  - Chart labels (fraud stats and risk distribution) and dataset label use `window.__FraudT`.
  - Modal buttons "علامة آمن" / "Mark safe" and "حظر" / "Block" wrapped in `Html.T`.

### 2.2 Views fully translated (this pass + prior)

- **Courses:** Edit (labels, options, confirm, buttons).
- **Payments:** Details (status, table, buttons, timeline, prompt/alert via `__PaymentT`); **FraudDetection** (modals, JS prompts/alerts, chart labels via `__FraudT`).
- **Modules:** Edit, Create, Details.
- **Lessons:** Edit.
- **Faq:** Details (confirm/alert via `__FaqT`).
- **Documents:** Details (confirm/alert via `__DocT`).
- **LandingPages:** Create (alert).
- **Settings:** Email (alert), Index (.NET label).
- **Achievements:** Details (confirm via `__AchieveT`, labels).
- **PaymentGatewaySettings:** Index (alerts and labels via `__GatewayT`).
- **Categories:** Create.
- **ContentDrip:** Create.
- **EmailTemplates:** Create, Test.
- **Coupons:** Create.
- **Notifications:** CreateTemplate.
- **SecuritySettings:** Index (placeholder).
- **Media:** Statistics.

---

## 3. Confirm/Alert Audit (Excluding Help)

- All **confirm()** and **alert()** in the above views use either:
  - `@Html.Raw(Html.T("...", "...").Replace("'", "\\'"))` (or `.ToString().Replace(...)` where used), or
  - Server-injected `window.__*T` keys.
- **FraudDetection** and **CommissionSettings/Edit** were the last gaps fixed in this re-examination.

---

## 4. Remaining Areas (Not in Help)

For **100% coverage** across the entire admin area (still excluding Help), the same pattern should be applied to:

- **Placeholders:** Many settings/create/edit views still have raw placeholders (e.g. technical hints like `https://`, `pk_test_`, or user-facing Arabic in Currencies/Create, LearningPaths/Create, Faq/Create, Security/AddBlockedIp, TaxSettings, Badges, SmsSettings, VideoSettings, etc.). Standard: `placeholder="@Html.T("ar", "en")"`.
- **Other alerts/prompts:** Faq/Edit, Announcements (Edit, Create, Details), PlatformSettings (CreateSmsSetting, CreateSeoSetting), Notifications (Send, EditTemplate), Certificates/Revoke, Refunds/Process, Bundles/Details, AdvancedReports (EditTemplate), Settings/Sms, VideoSettings/CreateVideoSetting, SmsSettings/CreateSmsSetting, LearningPaths/Create, CountryRestrictions/Create, Reviews/Details, Comments/Details, DirectMessages/Index, LoginLogs/Details, Tags/Merge, PushNotificationSettings/Details, ActivityLog/Details, SmsTemplates (Index, Details, Edit, Create), ErrorLogs/Index – any remaining `alert('...')` or `prompt('...')` with raw Arabic/English should use server-injected `Html.T` or `window.__*T`.
- **Chart/table labels:** Any other views that render chart labels or filter text in JS should pass translated strings from the server (same pattern as FraudDetection).

These do not change the assurance for the **views and flows already translated**; they are the checklist for extending coverage to every remaining admin view.

---

## 5. Conflicts and Layout

- **No breaking changes:** Only display strings were added/changed; no changes to `ViewData` key names, tuple shapes, or layout contracts.
- **Help excluded:** No files under `Areas/Admin/Views/Help/` were modified.
- **Data vs UI:** Model/entity values (e.g. course title, user name) are not wrapped in `Html.T`; only UI labels, headings, buttons, messages, and script-facing strings are.

---

## 6. Assurance Statement

- **Enterprise approach:** The admin dashboard (excluding Help Center) uses the same translation approach as the rest of the project: `Html.T("Arabic", "English")` in views, `CultureExtensions.T` or two-argument `Set*Message(ar, en)` in controllers, and server-injected translated strings for JS confirm/alert/prompt and chart labels where applicable.
- **Production readiness (translated views):** All views and flows listed in §2 are fully translated, with no raw Arabic/English left in titles, breadcrumbs, headers, labels, buttons, placeholders, table headers, options, confirm/alert/prompt text, or chart labels. They are consistent with the enterprise pattern and ready for production.
- **Re-examination:** Confirm/alert escaping was verified; missing translations in **Payments/FraudDetection** and **CommissionSettings/Edit** were fixed. Chart labels in FraudDetection are culture-aware.
- **100% coverage (all admin excluding Help):** To reach full coverage, apply the same pattern to the remaining placeholders and JS strings listed in §4; the approach and patterns are defined and repeatable.

**Exclusion:** Help Center (`Areas/Admin/Views/Help/`) remains fully excluded from translation requirements and from this assurance.
